"""Scoped checks for capture evidence and gallery serialization; no Unity required."""
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch
import zlib

import capture as gallery


def plan():
    return {"title": "UI", "cases": [{"id": "case", "title": "Case", "source": "view.cs", "states": [
        {"id": "state", "title": "State", "fixture": "custom", "expected": ["root"]}]}]}


def png():
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(b"\x00\xff\x00\x00")) + chunk(b"IEND", b"")


class GalleryTests(unittest.TestCase):
    def test_rejects_traversal_and_duplicate_states(self):
        value = plan()
        value["cases"][0]["id"] = "../escape"
        with self.assertRaises(ValueError):
            gallery.validate(value)
        value = plan()
        value["cases"][0]["states"] *= 2
        with self.assertRaises(ValueError):
            gallery.validate(value)
        value = plan()
        value["cases"][0]["folder"] = "../escape"
        with self.assertRaises(ValueError):
            gallery.validate(value)

    def test_detects_truncated_png(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "test.png"
            path.write_bytes(png())
            self.assertEqual(gallery.png_info(path)["width"], 1)
            path.write_bytes(png()[:-12])
            with self.assertRaises(ValueError):
                gallery.png_info(path)

    def test_gallery_preserves_partial_state_and_escapes_embedded_data(self):
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            value = plan()
            value["title"] = '</script><script>alert("bad")</script>'
            gallery.write(out / "plan.json", value)
            result = gallery.build(out)
            self.assertFalse(result["complete"])
            self.assertEqual(result["captured"], 0)
            self.assertEqual(len(result["cases"][0]["states"]), 1)
            html = (out / "index.html").read_text(encoding="utf-8")
            self.assertNotIn(value["title"], html)
            gallery.write(out / "capture-result.json", {"frames": [{"id": "case--state", "success": True, "width": 1, "height": 1}]})
            gallery.write(out / "run.json", {"success": True})
            (out / "case--state.png").write_bytes(png())
            self.assertTrue(gallery.build(out)["complete"])

    def test_png_only_audit_supports_case_folders(self):
        with tempfile.TemporaryDirectory() as directory:
            out = Path(directory)
            value = plan()
            value["cases"][0]["folder"] = "01_Screen"
            gallery.write(out / "plan.json", value)
            gallery.write(out / "capture-result.json", {"frames": [{
                "id": "case--state", "file": "01_Screen/case_state.png",
                "success": True, "width": 1, "height": 1
            }]})
            gallery.write(out / "run.json", {"success": True})
            (out / "01_Screen").mkdir()
            (out / "01_Screen/case_state.png").write_bytes(png())
            result = gallery.audit(out)
            self.assertTrue(result["complete"])
            self.assertTrue((out / "capture-manifest.json").is_file())
            self.assertFalse((out / "index.html").exists())

    def test_runtime_plan_adds_folder_retry_and_continue_policy(self):
        value = plan()
        value.update(defaultAttempts=2, continueOnError=True)
        value["cases"][0]["folder"] = "01_Screen"
        runtime = gallery.runtime_plan(value)
        self.assertTrue(runtime["continueOnError"])
        self.assertEqual(runtime["shots"][0]["attempts"], 2)
        self.assertEqual(runtime["shots"][0]["file"], "01_Screen/case_state.png")

    def test_module_detection_and_downloads_default(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            modules = root / "modules"
            module_root = modules / "demo"
            module_root.mkdir(parents=True)
            project = root / "project"
            (project / "Assets").mkdir(parents=True)
            (project / "Assets/marker.txt").write_text("ok")
            gallery.write(module_root / "adapter.json", {
                "id": "demo", "projectMarkers": ["Assets/marker.txt"],
                "adapter": ["Adapter.cs"], "plan": "plan.json", "outputName": "Demo_UI"
            })
            with patch.object(gallery, "ADAPTERS", modules):
                module = gallery.resolve_module(project)
            self.assertEqual(module["id"], "demo")
            with patch.object(gallery, "downloads_directory", return_value=root / "Downloads"):
                output = gallery.default_output(project, module)
            self.assertEqual(output.parent, root / "Downloads")
            self.assertTrue(output.name.startswith("Demo_UI_"))

    def test_bundled_modules_have_valid_inputs(self):
        modules = gallery.available_modules()
        self.assertTrue(modules)
        for module in modules:
            root = module["_root"]
            self.assertGreater(gallery.validate(gallery.read(root / module["plan"])), 0)
            for adapter in module["adapter"]:
                self.assertTrue((root / adapter).is_file())

    @patch("capture.shutil.which", return_value="unity")
    @patch("capture.subprocess.run")
    def test_pipeline_nested_failure_and_missing_result(self, run, which):
        run.return_value.returncode = 0
        for value in ({"success": True, "data": {"result": {"success": False, "error": "Compile failed"}}}, {"success": False, "data": None}):
            run.return_value.stdout = json.dumps(value)
            with self.assertRaises(RuntimeError):
                gallery.cli(Path.cwd(), "run_script")


if __name__ == "__main__":
    unittest.main()
