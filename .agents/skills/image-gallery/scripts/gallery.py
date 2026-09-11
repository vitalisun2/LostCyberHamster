"""Build an image gallery from a manifest, capture output, or image folders."""
import argparse
import copy
import functools
import hashlib
import http.server
import json
import os
from pathlib import Path
import re
import shutil
import sys
from urllib.parse import quote

SKILL = Path(__file__).resolve().parent.parent
IDENT = re.compile(r"^[a-z0-9]+(?:-+[a-z0-9]+)*$")
FORMATS = {".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".avif", ".bmp", ".ico"}
CAPTURE_MANIFEST = "capture-manifest.json"
GALLERY_MANIFEST = "manifest.json"


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def slug(value, fallback):
    value = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    return value or fallback


def unique_ident(value, used, fallback):
    base = slug(value, fallback)
    candidate = base
    suffix = 2
    while candidate in used:
        candidate = f"{base}-{suffix}"
        suffix += 1
    used.add(candidate)
    return candidate


def title_from_name(name):
    text = Path(name).stem if Path(name).suffix else str(name)
    text = re.sub(r"[_-]+", " ", text).strip()
    return text or str(name)


def iter_images(root):
    return sorted((path for path in root.rglob("*") if path.is_file() and path.suffix.lower() in FORMATS),
                  key=lambda path: path.as_posix().lower())


def validate(data):
    if not isinstance(data.get("title"), str) or not data["title"].strip() or not data.get("cases"):
        raise ValueError("title and nonempty cases required")
    case_ids = set()
    for case in data["cases"]:
        cid = case.get("id", "")
        if not IDENT.fullmatch(cid) or cid in case_ids or not case.get("title") or not case.get("states"):
            raise ValueError(f"Invalid/duplicate case: {cid}")
        case_ids.add(cid)
        state_ids = set()
        for state in case["states"]:
            sid = state.get("id", "")
            if not IDENT.fullmatch(sid) or sid in state_ids or not state.get("title"):
                raise ValueError(f"Invalid/duplicate state: {cid}/{sid}")
            state_ids.add(sid)
            if state.get("captured") is False:
                continue
            if not isinstance(state.get("file"), str) or not state["file"].strip():
                raise ValueError(f"Missing image path: {cid}/{sid}")
    return data


def resolve_state_file(root, state):
    value = Path(state.get("file", ""))
    return value.resolve() if value.is_absolute() else (root / value).resolve()


def sidecar_output(src):
    src = Path(src).resolve()
    if src.is_dir():
        parent, stem = src.parent, src.name
    elif src.name in {CAPTURE_MANIFEST, GALLERY_MANIFEST}:
        parent, stem = src.parent.parent, src.parent.name
    else:
        parent, stem = src.parent, src.stem
    return parent / f"{stem}_gallery"


def default_output(src):
    return sidecar_output(src)


def import_folder(src):
    src = Path(src).resolve()
    cases, case_ids = [], set()

    def add_case(title, source_label, files, fallback, relative_root):
        if not files:
            return
        state_ids, states = set(), []
        for index, path in enumerate(files, 1):
            relative = path.relative_to(src).as_posix()
            local = path.relative_to(relative_root).with_suffix("").as_posix()
            state_title = title_from_name(local)
            sid = unique_ident(local, state_ids, f"state-{index}")
            states.append({"id": sid, "title": state_title, "file": relative, "captured": True})
        cid = unique_ident(title, case_ids, fallback)
        cases.append({"id": cid, "title": title, "source": source_label, "states": states})

    root_files = sorted((path for path in src.iterdir() if path.is_file() and path.suffix.lower() in FORMATS),
                        key=lambda path: path.name.lower())
    add_case(src.name, src.name, root_files, "root", src)
    for folder in sorted((path for path in src.iterdir() if path.is_dir()), key=lambda path: path.name.lower()):
        add_case(title_from_name(folder.name), folder.relative_to(src).as_posix(), iter_images(folder), f"case-{len(cases) + 1}", folder)
    if not cases:
        raise ValueError(f"No supported images found: {src}")
    return {
        "title": title_from_name(src.name),
        "description": f"Gallery imported from {src.name}.",
        "status": "Gallery built from image folders.",
        "cases": cases,
    }


def load_source(src):
    src = Path(src).resolve()
    if src.is_file():
        return validate(read(src)), src.parent, "manifest"
    if not src.is_dir():
        raise ValueError(f"Missing source: {src}")
    capture = src / CAPTURE_MANIFEST
    manifest = src / GALLERY_MANIFEST
    if capture.is_file():
        return validate(read(capture)), src, "capture"
    if manifest.is_file():
        return validate(read(manifest)), src, "manifest-dir"
    return validate(import_folder(src)), src, "folder"


def serve_root(out):
    out = Path(out).resolve()
    manifest = read(out / GALLERY_MANIFEST)
    root = out.parent
    for case in manifest.get("cases", []):
        for state in case.get("states", []):
            if state.get("captured") is False or not state.get("file"):
                continue
            source = (out / state["file"]).resolve()
            if Path(os.path.commonpath([str(root), str(source)])) != root:
                raise ValueError("linked gallery serve requires viewer and images under a shared parent; rebuild with default output or use bundle mode")
    index = Path(os.path.relpath(out / "index.html", root)).as_posix()
    return root, index


def build(src, out, mode="bundle"):
    if mode not in {"linked", "bundle"}:
        raise ValueError(f"Unsupported mode: {mode}")
    src, out = Path(src).resolve(), Path(out).resolve()
    data, source_root, _ = load_source(src)
    data = copy.deepcopy(data)
    operations, captured = [], 0
    for case in data["cases"]:
        for state in case["states"]:
            if state.get("captured") is False:
                state["captured"] = False
                continue
            source = resolve_state_file(source_root, state)
            if not source.is_file() or source.suffix.lower() not in FORMATS:
                raise ValueError(f"Missing or unsupported image: {source}")
            digest = hashlib.sha256(source.read_bytes()).hexdigest()
            state.update(captured=True, sha256=digest)
            captured += 1
            if mode == "bundle":
                target = out / "images" / (str(len(operations) + 1).zfill(4) + source.suffix.lower())
                if target.exists() and target.resolve() != source and target.read_bytes() != source.read_bytes():
                    raise ValueError(f"Output contains a different image; use a new folder: {target}")
                operations.append((source, target))
                state["file"] = target.relative_to(out).as_posix()
            else:
                try:
                    state["file"] = Path(os.path.relpath(source, out)).as_posix()
                except ValueError as error:
                    raise ValueError("linked mode requires source and output on the same drive") from error
    out.mkdir(parents=True, exist_ok=True)
    for source, target in operations:
        target.parent.mkdir(parents=True, exist_ok=True)
        if source != target.resolve():
            shutil.copyfile(source, target)
    data["captured"] = captured
    data["expected"] = sum(len(case["states"]) for case in data["cases"])
    data["complete"] = data["captured"] == data["expected"] and data.get("complete", True)
    serialized = json.dumps(data, ensure_ascii=False, indent=2)
    (out / GALLERY_MANIFEST).write_text(serialized, encoding="utf-8")
    template = (SKILL / "assets/gallery.html").read_text(encoding="utf-8")
    (out / "index.html").write_text(template.replace("__GALLERY_DATA__", serialized.replace("<", "\\u003c")), encoding="utf-8")
    return data


def serve(out, port=8767):
    root, index = serve_root(out)
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(root))
    server = http.server.ThreadingHTTPServer(("127.0.0.1", port), handler)
    print(f"http://127.0.0.1:{server.server_port}/{quote(index, safe='/')}", flush=True)
    server.serve_forever()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("build")
    source = p.add_mutually_exclusive_group(required=True)
    source.add_argument("--src")
    source.add_argument("--manifest")
    p.add_argument("--out")
    p.add_argument("--mode", choices=("linked", "bundle"), default="linked")
    p = sub.add_parser("serve")
    p.add_argument("--out", required=True)
    p.add_argument("--port", type=int, default=8767)
    args = parser.parse_args()
    if args.command == "serve":
        serve(args.out, args.port)
    else:
        src = args.src or args.manifest
        out = Path(args.out).resolve() if args.out else default_output(src)
        data = build(src, out, mode=args.mode)
        print(json.dumps({"images": data["captured"], "complete": data["complete"], "gallery": str(out / "index.html")}))
        return 0 if data["complete"] else 1
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
