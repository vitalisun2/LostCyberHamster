"""Build a portable image gallery from a JSON manifest. Python standard library only."""
import argparse
import copy
import functools
import hashlib
import http.server
import json
from pathlib import Path
import re
import shutil
import sys

SKILL = Path(__file__).resolve().parent.parent
IDENT = re.compile(r"^[a-z0-9]+(?:-+[a-z0-9]+)*$")
FORMATS = {".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".avif", ".bmp", ".ico"}


def build(manifest_path, out):
    manifest_path, out = Path(manifest_path).resolve(), Path(out).resolve()
    data = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    if not isinstance(data.get("title"), str) or not data["title"].strip() or not data.get("cases"):
        raise ValueError("title and nonempty cases required")
    data = copy.deepcopy(data)
    case_ids, operations = set(), []
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
            source = (manifest_path.parent / state.get("file", "")).resolve()
            if not source.is_file() or source.suffix.lower() not in FORMATS:
                raise ValueError(f"Missing or unsupported image: {source}")
            # Numbered names avoid collisions between groups and duplicate source basenames.
            target = out / "images" / (str(len(operations) + 1).zfill(4) + source.suffix.lower())
            if target.exists() and target.resolve() != source:
                if target.read_bytes() != source.read_bytes():
                    raise ValueError(f"Output contains a different image; use a new folder: {target}")
            operations.append((source, target))
            state.update(file=target.relative_to(out).as_posix(), captured=True,
                         sha256=hashlib.sha256(source.read_bytes()).hexdigest())
    for source, target in operations:
        target.parent.mkdir(parents=True, exist_ok=True)
        if source != target.resolve():
            shutil.copyfile(source, target)
    out.mkdir(parents=True, exist_ok=True)
    data["captured"] = len(operations)
    data["expected"] = sum(len(c["states"]) for c in data["cases"])
    data["complete"] = data["captured"] == data["expected"] and data.get("complete", True)
    serialized = json.dumps(data, ensure_ascii=False, indent=2)
    (out / "manifest.json").write_text(serialized, encoding="utf-8")
    template = (SKILL / "assets/gallery.html").read_text(encoding="utf-8")
    (out / "index.html").write_text(template.replace("__GALLERY_DATA__", serialized.replace("<", "\\u003c")), encoding="utf-8")
    return data


def serve(out, port=8767):
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(Path(out).resolve()))
    server = http.server.ThreadingHTTPServer(("127.0.0.1", port), handler)
    print(f"http://127.0.0.1:{server.server_port}/index.html", flush=True)
    server.serve_forever()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("build")
    p.add_argument("--manifest", required=True)
    p.add_argument("--out", required=True)
    p = sub.add_parser("serve")
    p.add_argument("--out", required=True)
    p.add_argument("--port", type=int, default=8767)
    args = parser.parse_args()
    if args.command == "serve":
        serve(args.out, args.port)
    else:
        data = build(args.manifest, args.out)
        print(json.dumps({"images": data["captured"], "complete": data["complete"], "gallery": str(Path(args.out).resolve() / "index.html")}))
        return 0 if data["complete"] else 1
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
