"""Compatibility entrypoint for existing tasks; use unity-ui-capture/scripts/capture.py."""
from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).resolve().parents[2] / "unity-ui-capture/scripts/capture.py"), run_name="__main__")
