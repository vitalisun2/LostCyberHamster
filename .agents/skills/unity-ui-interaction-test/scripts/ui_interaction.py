"""Real UI Toolkit interaction regression for LostCyberHamster."""
import argparse
from datetime import datetime
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid


SCRIPT_DIR = Path(__file__).resolve().parent
SKILL_DIR = SCRIPT_DIR.parent
CAPTURE_SKILL = SKILL_DIR.parent / "unity-ui-capture"


def load_capture():
    path = CAPTURE_SKILL / "scripts" / "capture.py"
    if not path.is_file():
        raise RuntimeError("Sibling unity-ui-capture skill is required")
    spec = importlib.util.spec_from_file_location("lch_ui_capture", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


CAPTURE = load_capture()


def write(path, value):
    Path(path).write_text(
        json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def as_object(value):
    if isinstance(value, str) and value.lstrip().startswith("{"):
        return json.loads(value)
    return value


def default_output():
    root = CAPTURE.downloads_directory()
    root.mkdir(parents=True, exist_ok=True)
    stem = f"LostCyberHamster_UI_Interaction_{datetime.now():%Y-%m-%d_%H-%M-%S}"
    result = root / stem
    suffix = 2
    while result.exists():
        result = root / f"{stem}_{suffix:02d}"
        suffix += 1
    return result


def inputs(args):
    project = Path(args.project).resolve()
    if not (project / "ProjectSettings" / "ProjectVersion.txt").is_file():
        raise ValueError(f"Not a Unity project: {project}")
    module = CAPTURE.resolve_module(project, "lost-cyber-hamster")
    plan_path = module["_root"] / module["plan"]
    plan = CAPTURE.filter_plan(
        CAPTURE.read(plan_path), args.case_filter, args.state_filter, args.only_filter
    )
    count = CAPTURE.validate(plan)
    adapters = [(module["_root"] / item).resolve() for item in module["adapter"]]
    out = Path(args.out).resolve() if args.out else default_output().resolve()
    lock = (project / module["lockFile"]).resolve()
    return project, module, plan, count, adapters, out, lock


def inspect(args):
    project, module, plan, count, adapters, out, lock = inputs(args)
    value = {
        "project": str(project),
        "module": module["id"],
        "states": count,
        "cases": len(plan["cases"]),
        "adapters": [str(path) for path in adapters],
        "output": str(out),
        "lockFile": str(lock),
    }
    if args.list:
        value["plan"] = CAPTURE.listed_plan(plan)
    print(json.dumps(value, ensure_ascii=False))


def run_test_level(project, timeout, progress, time_scale):
    level = "01_New_York/Morning/test_switch_lane"
    launch = as_object(
        CAPTURE.cli(
            project,
            "lch_test_level_launch",
            ["--level_address", level, "--time_scale", str(time_scale)],
        )
    )
    if not isinstance(launch, dict) or not launch.get("requestId"):
        raise RuntimeError("lch_test_level_launch returned no requestId")
    request_id = launch["requestId"]
    last_state = launch.get("state")
    if progress:
        print(json.dumps({"phase": "test-level", "state": last_state}, ensure_ascii=False), flush=True)
    deadline = time.monotonic() + timeout
    last_error = None
    while time.monotonic() < deadline:
        try:
            status = as_object(CAPTURE.cli(project, "lch_test_level_status"))
            if not isinstance(status, dict) or status.get("requestId") != request_id:
                time.sleep(.35)
                continue
            state = status.get("state")
            if progress and state != last_state:
                last_state = state
                print(json.dumps({"phase": "test-level", "state": state}, ensure_ascii=False), flush=True)
            if state == "completed":
                if "WIN" not in str(status.get("testResult", "")).upper():
                    raise RuntimeError(f"Test level did not win: {status.get('testResult')}")
                return status
            if state in ("failed", "busy"):
                raise RuntimeError(f"Test level {state}: {status.get('message')}")
            last_error = None
        except (RuntimeError, subprocess.TimeoutExpired) as error:
            last_error = str(error)
        time.sleep(.35)
    raise TimeoutError(f"Test level timed out; last transport error: {last_error}")


def build_source(out, token, adapters):
    capture_source = (CAPTURE_SKILL / "scripts" / "Capture.cs").read_text(encoding="utf-8")
    marker = "    public static class Capture"
    if marker not in capture_source:
        raise RuntimeError("unity-ui-capture API prelude changed")
    prelude = capture_source.split(marker, 1)[0] + "}\n"
    runner = (SCRIPT_DIR / "InteractionRunner.cs").read_text(encoding="utf-8")
    for key, value in {
        "__PLAN_PATH__": str(out / "interaction-plan.json"),
        "__OUTPUT_PATH__": str(out),
        "__TASK_KEY__": token,
    }.items():
        runner = runner.replace(key, json.dumps(value))
    adapter_source = "\n".join(path.read_text(encoding="utf-8-sig") for path in adapters)
    return prelude + "\n" + runner + "\n" + adapter_source


def execute(args):
    project, module, plan, count, adapters, out, lock = inputs(args)
    if out == project / "Assets" or project / "Assets" in out.parents:
        raise ValueError("Output must be outside Assets")
    if out.exists() and any(out.iterdir()):
        raise ValueError("Output must be new or empty")
    out.mkdir(parents=True, exist_ok=True)
    write(out / "plan.json", plan)
    write(out / "interaction-plan.json", CAPTURE.runtime_plan(plan))
    token = "ui-interaction-" + uuid.uuid4().hex
    owner = f"owner={token} phase=behavioral-ui-test\n"
    lock.parent.mkdir(parents=True, exist_ok=True)
    try:
        with lock.open("x", encoding="utf-8") as handle:
            handle.write(owner)
    except OSError as error:
        if lock.exists():
            raise RuntimeError(f"Integration lock is busy: {lock}") from error
        raise

    started_at = time.monotonic()
    run = {
        "success": False,
        "token": token,
        "module": module["id"],
        "states": count,
        "cleanup": {"playStopped": None, "profileRestored": None, "lockReleased": False},
    }
    started = False
    safe_release = True
    try:
        status = CAPTURE.editor_status(project, out)
        if status["playing"] or status["compiling"] or status["updating"] or status["dirty"]:
            raise RuntimeError("Editor must be stopped, settled and have a clean scene")
        run["editor"] = status
        if args.with_test_level:
            run["testLevel"] = run_test_level(
                project,
                args.test_level_timeout,
                args.progress,
                args.test_level_time_scale,
            )
            CAPTURE.wait_editor(project, out, False)
        revision = subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=project, capture_output=True, text=True
        )
        run["revision"] = revision.stdout.strip() if revision.returncode == 0 else None
        source = build_source(out, token, adapters)
        script_path = out / "interaction.cs"
        script_path.write_text(source, encoding="utf-8")
        write(out / "run.json", run)

        started, safe_release = True, False
        try:
            CAPTURE.cli(project, "editor_play")
        except (RuntimeError, subprocess.TimeoutExpired) as error:
            run["playRequestNote"] = str(error)
        CAPTURE.wait_editor(project, out, True)
        CAPTURE.cli(
            project,
            "run_script",
            ["--file", str(script_path), "--entry", "UiGallery.InteractionRunner.Start"],
        )
        deadline = time.monotonic() + args.timeout
        last = None
        while time.monotonic() < deadline:
            result_path = out / "interaction-result.json"
            if result_path.exists():
                try:
                    result = CAPTURE.read(result_path)
                except ValueError:
                    time.sleep(.2)
                    continue
                current = result.get("current")
                if args.progress and current != last:
                    last = current
                    print(
                        json.dumps(
                            {
                                "phase": result.get("phase"),
                                "current": current,
                                "actions": len(result.get("actions", [])),
                                "discovered": len(result.get("candidates", [])),
                            },
                            ensure_ascii=False,
                        ),
                        flush=True,
                    )
                if result.get("done"):
                    run["cleanup"]["profileRestored"] = result.get("restored", False)
                    if result.get("error") or result.get("cleanupError"):
                        raise RuntimeError(result.get("error") or result.get("cleanupError"))
                    run["success"] = bool(result.get("success"))
                    break
            time.sleep(.4)
        else:
            raise TimeoutError("Interaction run timed out; cleanup must be verified")
    except BaseException as error:
        run["error"] = str(error)
    finally:
        if started:
            try:
                CAPTURE.eval_code(
                    project,
                    out,
                    "System.AppDomain.CurrentDomain.SetData("
                    + json.dumps(token + ":cancel")
                    + ",true); return true;",
                )
                settled = False
                for _ in range(8):
                    settled = CAPTURE.eval_code(
                        project,
                        out,
                        "var t=System.AppDomain.CurrentDomain.GetData("
                        + json.dumps(token)
                        + ") as System.Threading.Tasks.Task; return t==null || t.IsCompleted;",
                    )
                    if settled:
                        break
                    time.sleep(.5)
                if not settled:
                    CAPTURE.cli(project, "editor_stop")
                    raise RuntimeError("Interaction task did not settle; profile must be checked")
                if run["cleanup"]["profileRestored"] is None:
                    result_path = out / "interaction-result.json"
                    run["cleanup"]["profileRestored"] = (
                        CAPTURE.read(result_path).get("restored", False)
                        if result_path.exists()
                        else True
                    )
                try:
                    CAPTURE.cli(project, "editor_stop")
                except (RuntimeError, subprocess.TimeoutExpired):
                    pass
                run["cleanup"]["playStopped"] = not CAPTURE.wait_editor(project, out, False)["playing"]
                safe_release = bool(
                    run["cleanup"]["playStopped"] and run["cleanup"]["profileRestored"]
                )
            except Exception as error:
                run["cleanup"]["error"] = str(error)
        if safe_release and lock.exists() and lock.read_text(encoding="utf-8") == owner:
            lock.unlink()
            run["cleanup"]["lockReleased"] = True
        run["success"] = bool(run["success"] and safe_release)
        run["durationSeconds"] = round(time.monotonic() - started_at, 2)
        write(out / "run.json", run)

    result_path = out / "interaction-result.json"
    result = CAPTURE.read(result_path) if result_path.exists() else {}
    actions = result.get("actions", [])
    summary = {
        "success": run["success"],
        "states": count,
        "discovered": len(result.get("candidates", [])),
        "actions": len(actions),
        "passed": sum(item.get("status") == "passed" for item in actions),
        "guarded": sum(item.get("status") == "guarded" for item in actions),
        "disabled": sum(item.get("status") == "disabled" for item in actions),
        "failed": sum(item.get("status") == "failed" for item in actions),
        "discoveryFailures": len(result.get("discoveryFailures", [])),
        "output": str(out),
        "error": run.get("error"),
        "cleanup": run["cleanup"],
        "testLevel": run.get("testLevel"),
        "durationSeconds": run["durationSeconds"],
    }
    write(out / "summary.json", summary)
    print(json.dumps(summary, ensure_ascii=False), flush=True)
    return 0 if summary["success"] else 1


def add_filters(parser):
    parser.add_argument("--project", required=True)
    parser.add_argument("--out")
    parser.add_argument("--case", dest="case_filter", action="append", default=[])
    parser.add_argument("--state", dest="state_filter", action="append", default=[])
    parser.add_argument("--only", dest="only_filter", action="append", default=[])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    command = sub.add_parser("inspect")
    add_filters(command)
    command.add_argument("--list", action="store_true")
    command = sub.add_parser("run")
    add_filters(command)
    command.add_argument("--with-test-level", action="store_true")
    command.add_argument("--test-level-timeout", type=int, default=180)
    command.add_argument("--test-level-time-scale", type=float, default=3.0)
    command.add_argument("--timeout", type=int, default=900)
    command.add_argument("--progress", action="store_true")
    args = parser.parse_args()
    if args.command == "inspect":
        inspect(args)
        return 0
    return execute(args)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, RuntimeError, TimeoutError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
