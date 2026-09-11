"""Unity UI capture; gallery rendering belongs to image-gallery. Python 3 standard library only."""
import argparse
from datetime import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import time
import uuid
import zlib

SKILL = Path(__file__).resolve().parent.parent
ADAPTERS = SKILL / "adapters"
IDENT = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def downloads_directory():
    """Return the Windows Downloads known folder, including redirected profiles."""
    if os.name == "nt":
        try:
            import winreg
            key_path = r"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"
            folder_id = "{374DE290-123F-4565-9164-39C4925E467B}"
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path) as key:
                value, _ = winreg.QueryValueEx(key, folder_id)
                return Path(os.path.expandvars(value)).expanduser()
        except (ImportError, OSError):
            pass
    return Path.home() / "Downloads"


def available_modules():
    modules = []
    if not ADAPTERS.is_dir():
        return modules
    for descriptor_path in sorted(ADAPTERS.glob("*/adapter.json")):
        descriptor = read(descriptor_path)
        if not IDENT.fullmatch(descriptor.get("id", "")):
            raise ValueError(f"Invalid module id: {descriptor_path}")
        descriptor["_root"] = descriptor_path.parent
        modules.append(descriptor)
    return modules


def resolve_module(project, module_id=None):
    modules = available_modules()
    if module_id:
        matches = [module for module in modules if module["id"] == module_id]
        if not matches:
            raise ValueError(f"Unknown capture module: {module_id}")
        return matches[0]
    matches = [module for module in modules
               if module.get("projectMarkers") and
               all((project / marker).exists() for marker in module["projectMarkers"])]
    if len(matches) != 1:
        found = ", ".join(module["id"] for module in matches) or "none"
        raise ValueError(f"Cannot auto-select one capture module (matched: {found}); use --module")
    return matches[0]


def default_output(project, module=None):
    prefix = (module or {}).get("outputName") or f"{project.name}_UI"
    if not re.fullmatch(r"[\w .-]+", prefix, re.UNICODE):
        raise ValueError(f"Invalid outputName: {prefix}")
    root = downloads_directory()
    root.mkdir(parents=True, exist_ok=True)
    stem = f"{prefix}_{datetime.now():%Y-%m-%d_%H-%M-%S}"
    candidate = root / stem
    suffix = 2
    while candidate.exists():
        candidate = root / f"{stem}_{suffix:02d}"
        suffix += 1
    return candidate


def case_folder(case):
    value = case.get("folder")
    if not value:
        return None
    folder = Path(value)
    if folder.is_absolute() or ".." in folder.parts or value in (".", ""):
        raise ValueError(f"Case {case.get('id')}: invalid folder: {value}")
    return folder


def shot_file(case, state):
    folder = case_folder(case)
    name = f"{case['id']}_{state['id']}.png" if folder else f"{case['id']}--{state['id']}.png"
    return (folder / name).as_posix() if folder else name


def runtime_plan(plan):
    default_attempts = plan.get("defaultAttempts", 1)
    return {
        "continueOnError": plan.get("continueOnError", False),
        "shots": [dict(id=case["id"] + "--" + state["id"], fixture=state["fixture"],
            file=shot_file(case, state), dataJson=json.dumps(state.get("data", {}), ensure_ascii=False),
            expected=state["expected"], expectedText=state.get("expectedText", []),
            settleMs=state.get("settleMs", 600), attempts=state.get("attempts", default_attempts))
            for case in plan["cases"] for state in case["states"]]
    }


def gallery_builder():
    path = SKILL.parent / "image-gallery/scripts/gallery.py"
    if not path.is_file():
        raise RuntimeError("Install sibling image-gallery skill alongside unity-ui-capture")
    spec = importlib.util.spec_from_file_location("image_gallery_builder", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def validate(plan):
    if not isinstance(plan.get("title"), str) or not plan["title"].strip():
        raise ValueError("Plan title required")
    if not plan.get("cases"):
        raise ValueError("At least one case required")
    case_ids, shot_ids = set(), set()
    default_attempts = plan.get("defaultAttempts", 1)
    if not isinstance(default_attempts, int) or not 1 <= default_attempts <= 3:
        raise ValueError("defaultAttempts must be 1..3")
    if not isinstance(plan.get("continueOnError", False), bool):
        raise ValueError("continueOnError must be boolean")
    for case in plan["cases"]:
        cid = case.get("id", "")
        if not IDENT.fullmatch(cid) or cid in case_ids:
            raise ValueError(f"Invalid/duplicate case id: {cid}")
        case_ids.add(cid)
        case_folder(case)
        if not case.get("title") or not case.get("source") or not case.get("states"):
            raise ValueError(f"Case {cid}: title, source, states required")
        for state in case["states"]:
            sid = state.get("id", "")
            key = cid + "--" + sid
            if not IDENT.fullmatch(sid) or key in shot_ids:
                raise ValueError(f"Invalid/duplicate state id: {key}")
            shot_ids.add(key)
            if not state.get("title") or not state.get("fixture"):
                raise ValueError(f"State {key}: title and fixture required")
            if not isinstance(state.get("data", {}), dict):
                raise ValueError(f"State {key}: data must be an object")
            for field in ("expected", "expectedText"):
                values = state.get(field, [])
                if not isinstance(values, list) or not all(isinstance(v, str) and v for v in values):
                    raise ValueError(f"State {key}: invalid {field}")
            if not state.get("expected"):
                raise ValueError(f"State {key}: expected visible element required")
            if not isinstance(state.get("settleMs", 600), int) or not 100 <= state.get("settleMs", 600) <= 10000:
                raise ValueError(f"State {key}: settleMs must be 100..10000")
            attempts = state.get("attempts", default_attempts)
            if not isinstance(attempts, int) or not 1 <= attempts <= 3:
                raise ValueError(f"State {key}: attempts must be 1..3")
    return len(shot_ids)


def png_info(path):
    """Validate PNG container, chunk CRCs and compressed image data, without dependencies."""
    data = Path(path).read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Not a PNG: {path}")
    pos, size, compressed, ended = 8, None, bytearray(), False
    while pos + 12 <= len(data):
        length = struct.unpack_from(">I", data, pos)[0]
        kind, payload = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + length]
        end = pos + 12 + length
        if end > len(data) or zlib.crc32(kind + payload) != struct.unpack_from(">I", data, pos + 8 + length)[0]:
            raise ValueError(f"Invalid PNG chunk: {path}")
        if kind == b"IHDR":
            size = struct.unpack_from(">II", payload)
        if kind == b"IDAT":
            compressed.extend(payload)
        if kind == b"IEND":
            ended = True
            break
        pos = end
    if not ended or not size or not all(size) or not compressed:
        raise ValueError(f"Incomplete PNG: {path}")
    if not zlib.decompress(compressed):
        raise ValueError(f"Empty PNG data: {path}")
    return {"width": size[0], "height": size[1], "sha256": hashlib.sha256(data).hexdigest()}


def audit(out):
    out = Path(out).resolve()
    plan = read(out / "plan.json")
    validate(plan)
    result_path = out / "capture-result.json"
    result = read(result_path) if result_path.exists() else {"frames": [], "error": "Capture has no result"}
    run = read(out / "run.json") if (out / "run.json").exists() else {}
    frames = {frame["id"]: frame for frame in result.get("frames", [])}
    cases, captured = [], 0
    for case in plan["cases"]:
        item = {"id": case["id"], "title": case["title"], "source": case["source"], "states": []}
        for state in case["states"]:
            key = case["id"] + "--" + state["id"]
            frame = frames.get(key, {})
            shot = dict(state, id=key, file=frame.get("file") or shot_file(case, state), captured=False)
            shot["method"] = frame.get("method", "")
            shot["error"] = frame.get("error") or "Состояние не снято"
            if frame.get("success"):
                try:
                    shot.update(png_info(out / shot["file"]))
                    if (shot["width"], shot["height"]) != (frame["width"], frame["height"]):
                        raise ValueError("PNG size differs from capture metadata")
                    shot.update(captured=True, error=None, elements=frame.get("elements"), visibleText=frame.get("visibleText"))
                    captured += 1
                except (OSError, ValueError, zlib.error) as error:
                    shot["error"] = str(error)
            item["states"].append(shot)
        cases.append(item)
    expected = sum(len(c["states"]) for c in cases)
    manifest = {"title": plan["title"], "cases": cases, "captured": captured, "expected": expected,
                "complete": captured == expected and run.get("success", False),
                "captureError": result.get("error"), "cleanup": run.get("cleanup"), "revision": run.get("revision")}
    manifest["description"] = "Реальный Unity UI с подготовленными состояниями. Способ подготовки указан у каждого кадра."
    manifest["status"] = ("Съёмка завершена. Исходная сессия восстановлена." if manifest["complete"] else
                          "Набор неполный или восстановление требует проверки. Подробности: run.json и capture-result.json.")
    write(out / "capture-manifest.json", manifest)
    return manifest


def build(out):
    out = Path(out).resolve()
    manifest = audit(out)
    write(out / "gallery-input.json", manifest)
    return gallery_builder().build(out / "gallery-input.json", out)



def cli(project, command, args=(), timeout=45):
    executable = shutil.which("unity")
    if not executable:
        raise RuntimeError("unity is not in PATH; see docs/rules/unity_cli.md")
    proc = subprocess.run([executable, "command", command, *args, "--project-path", str(project), "--format", "json"],
                          cwd=project, capture_output=True, text=True, encoding="utf-8", timeout=timeout)
    try:
        value = json.loads(proc.stdout)
    except ValueError as error:
        raise RuntimeError(f"Unity {command}: invalid JSON response (exit {proc.returncode})") from error
    nested = value.get("data") or {}
    result = nested.get("result")
    if proc.returncode or value.get("success") is False or nested.get("success") is False or isinstance(result, dict) and result.get("success") is False:
        # Avoid logging transport details or process command lines, which may include credentials.
        raise RuntimeError(f"Unity {command} failed: {json.dumps(result, ensure_ascii=False)}")
    if "result" not in nested:
        raise RuntimeError(f"Unity {command}: no command result; inspect Editor state before retry")
    return result


def eval_code(project, out, code):
    path = out / "probe.cs"
    path.write_text(code, encoding="utf-8")
    response = cli(project, "eval_file", ["--file", str(path)])
    return response.get("result")


def editor_status(project, out):
    return eval_code(project, out,
        'bool dirty=false; for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++) '
        'dirty|=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty; '
        'return new {playing=UnityEngine.Application.isPlaying, '
        'compiling=UnityEditor.EditorApplication.isCompiling, updating=UnityEditor.EditorApplication.isUpdating, '
        'dirty=dirty, version=UnityEngine.Application.unityVersion, '
        'scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path};')


def wait_editor(project, out, playing):
    error = None
    for _ in range(12):
        try:
            status = editor_status(project, out)
            if status["playing"] == playing and not status["compiling"] and not status["updating"]:
                return status
        except (RuntimeError, subprocess.TimeoutExpired) as exc:
            error = exc
        time.sleep(1)
    raise RuntimeError(f"Editor did not settle after mode change: {error}")


def capture_inputs(args):
    project = Path(args.project).resolve()
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        raise ValueError(f"Not a Unity project: {project}")
    module = None
    if args.adapter:
        if not args.plan:
            raise ValueError("--plan is required with --adapter")
        adapters = [Path(path).resolve() for path in args.adapter]
        plan_path = Path(args.plan).resolve()
    else:
        module = resolve_module(project, args.module)
        root = module["_root"]
        adapters = [(root / path).resolve() for path in module.get("adapter", [])]
        plan_path = Path(args.plan).resolve() if args.plan else (root / module["plan"]).resolve()
    missing = [str(path) for path in [plan_path, *adapters] if not path.is_file()]
    if missing:
        raise ValueError("Missing capture input: " + ", ".join(missing))
    out = Path(args.out).resolve() if args.out else default_output(project, module).resolve()
    lock_value = args.lock_file or (module or {}).get("lockFile") or ".ui-gallery.lock"
    lock = Path(lock_value)
    if not lock.is_absolute():
        lock = project / lock
    return project, module, plan_path, adapters, out, lock.resolve()


def inspect_capture(args):
    project, module, plan_path, adapters, out, lock = capture_inputs(args)
    plan = read(plan_path)
    count = validate(plan)
    return {
        "project": str(project),
        "module": module["id"] if module else None,
        "plan": str(plan_path),
        "adapters": [str(path) for path in adapters],
        "states": count,
        "folders": len({case.get("folder", case["id"]) for case in plan["cases"]}),
        "output": str(out),
        "lockFile": str(lock),
    }


def capture(args):
    if args.gallery:
        gallery_builder()  # Resolve optional sibling before any Editor mutation.
    project, module, plan_path, adapter_paths, out, lock = capture_inputs(args)
    plan = read(plan_path)
    count = validate(plan)
    if out == project / "Assets" or project / "Assets" in out.parents:
        raise ValueError("Output must be outside Assets")
    if out.exists() and any(out.iterdir()):
        raise ValueError("Output must be new or empty; keep previous evidence in its own directory")
    adapter = "\n".join(path.read_text(encoding="utf-8-sig") for path in adapter_paths)
    out.mkdir(parents=True, exist_ok=True)
    write(out / "plan.json", plan)
    write(out / "capture-plan.json", runtime_plan(plan))
    token = "ui-gallery-" + uuid.uuid4().hex
    lock.parent.mkdir(parents=True, exist_ok=True)
    owner = "owner=" + token + " phase=visual-capture\n"
    # Exclusive creation: never overwrite another task's ownership.
    try:
        with lock.open("x", encoding="utf-8") as handle:
            handle.write(owner)
    except OSError as error:
        if lock.exists():
            raise RuntimeError(f"Integration lock is busy: {lock}") from error
        raise
    run = {"success": False, "cleanup": {"playStopped": None, "profileRestored": None}, "token": token,
           "module": module["id"] if module else None}
    started_at = time.monotonic()
    started, safe_release = False, True
    try:
        status = editor_status(project, out)
        if status["playing"] or status["compiling"] or status["updating"] or status["dirty"]:
            raise RuntimeError("Editor must be stopped, settled and have a clean scene")
        revision = subprocess.run(["git", "rev-parse", "HEAD"], cwd=project, capture_output=True, text=True)
        run["revision"] = revision.stdout.strip() if revision.returncode == 0 else None
        run["editor"] = status
        write(out / "run.json", run)
        source = (SKILL / "scripts/Capture.cs").read_text(encoding="utf-8")
        for key, value in {"__PLAN_PATH__": str(out / "capture-plan.json"), "__OUTPUT_PATH__": str(out), "__TASK_KEY__": token}.items():
            source = source.replace(key, json.dumps(value))
        source += "\n" + adapter
        script = out / "capture.cs"
        script.write_text(source, encoding="utf-8")
        started, safe_release = True, False
        try:
            cli(project, "editor_play")
        except (RuntimeError, subprocess.TimeoutExpired) as error:
            # A domain reload can lose the reply after the mode change was applied.
            run["playRequestNote"] = str(error)
        wait_editor(project, out, True)
        cli(project, "run_script", ["--file", str(script), "--entry", "UiGallery.Capture.Start"])
        deadline = time.monotonic() + args.timeout
        last = None
        while time.monotonic() < deadline:
            result_path = out / "capture-result.json"
            if result_path.exists():
                try:
                    result = read(result_path)
                except ValueError:
                    time.sleep(.2)
                    continue
                if args.progress and result.get("current") != last:
                    last = result.get("current")
                    print(json.dumps({"phase": result["phase"], "current": last, "captured": len(result.get("frames", [])), "total": count}, ensure_ascii=False), flush=True)
                if result.get("done"):
                    run["cleanup"]["profileRestored"] = result.get("restored", False)
                    if result.get("error") or result.get("cleanupError"):
                        raise RuntimeError(result.get("error") or result["cleanupError"])
                    run["success"] = True
                    break
            time.sleep(.5)
        else:
            raise TimeoutError("Capture timed out; do not retry before cleanup is verified")
    except BaseException as error:
        run["error"] = str(error)
    finally:
        if started:
            try:
                # Request cooperative cancellation and await the task before any recovery mutation.
                eval_code(project, out, 'System.AppDomain.CurrentDomain.SetData(' + json.dumps(token + ":cancel") + ',true); return true;')
                settled = False
                for _ in range(5):
                    settled = eval_code(project, out, 'var t=System.AppDomain.CurrentDomain.GetData(' + json.dumps(token) + ') as System.Threading.Tasks.Task; return t==null || t.IsCompleted;')
                    if settled:
                        break
                    time.sleep(.5)
                if not settled:
                    # Do not restore while capture code can still resume and mutate the profile.
                    cli(project, "editor_stop")
                    raise RuntimeError("Capture task did not settle; Play stopped. Verify testing backup before releasing lock")
                # Project-specific restoration is owned by IProjectAdapter, including on Play exit.
                if run["cleanup"]["profileRestored"] is None:
                    result_path = out / "capture-result.json"
                    run["cleanup"]["profileRestored"] = read(result_path).get("restored", False) if result_path.exists() else True
                try:
                    cli(project, "editor_stop")
                except (RuntimeError, subprocess.TimeoutExpired):
                    pass
                stopped = not wait_editor(project, out, False)["playing"]
                run["cleanup"]["playStopped"] = stopped
                safe_release = stopped and bool(run["cleanup"]["profileRestored"])
            except Exception as error:
                run["cleanup"]["error"] = str(error)
            run["success"] = run["success"] and safe_release
        if safe_release and lock.exists() and lock.read_text(encoding="utf-8") == owner:
            lock.unlink()
        run["cleanup"]["lockReleased"] = safe_release
        run["durationSeconds"] = round(time.monotonic() - started_at, 2)
        write(out / "run.json", run)
    manifest = build(out) if args.gallery else audit(out)
    summary = {"success": manifest["complete"], "captured": manifest["captured"],
               "expected": manifest["expected"], "output": str(out),
               "error": run.get("error"), "cleanup": run["cleanup"],
               "durationSeconds": run["durationSeconds"]}
    if args.gallery:
        summary["gallery"] = str(out / "index.html")
    print(json.dumps(summary, ensure_ascii=False), flush=True)
    return 0 if manifest["complete"] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    def add_inputs(command):
        command.add_argument("--plan", help="Override the module plan")
        command.add_argument("--out", help="Defaults to Downloads/<project>_UI_<timestamp>")
        command.add_argument("--module", help="Project module id; auto-detected when omitted")
        command.add_argument("--adapter", nargs="+", help="Manual adapter files; requires --plan")
        command.add_argument("--lock-file", help="Override the project integration lock")

    p = sub.add_parser("capture")
    p.add_argument("--project", required=True)
    add_inputs(p)
    p.add_argument("--timeout", type=int, default=240)
    p.add_argument("--progress", action="store_true", help="Print every state transition")
    p.add_argument("--gallery", action="store_true", help="Also build the optional HTML gallery")
    p = sub.add_parser("inspect")
    p.add_argument("--project", required=True)
    add_inputs(p)
    p = sub.add_parser("validate")
    p.add_argument("--plan", required=True)
    p = sub.add_parser("audit")
    p.add_argument("--out", required=True)
    p = sub.add_parser("build")
    p.add_argument("--out", required=True)
    p = sub.add_parser("serve")
    p.add_argument("--out", required=True)
    p.add_argument("--port", type=int, default=8767)
    args = parser.parse_args()
    if args.command == "capture":
        return capture(args)
    if args.command == "inspect":
        print(json.dumps(inspect_capture(args), ensure_ascii=False))
        return 0
    if args.command == "validate":
        print(json.dumps({"valid": True, "states": validate(read(args.plan))}))
    elif args.command == "audit":
        value = audit(args.out)
        print(json.dumps({"captured": value["captured"], "expected": value["expected"], "complete": value["complete"]}))
    elif args.command == "build":
        value = build(args.out)
        print(json.dumps({"captured": value["captured"], "expected": value["expected"], "complete": value["complete"]}))
    else:
        gallery_builder().serve(args.out, args.port)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, RuntimeError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
