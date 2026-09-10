"""Scoped checks for capture evidence and gallery serialization; no Unity required."""
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch
import zlib

import gallery


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

    @patch("gallery.shutil.which", return_value="unity")
    @patch("gallery.subprocess.run")
    def test_pipeline_nested_failure_and_missing_result(self, run, which):
        run.return_value.returncode = 0
        for value in ({"success": True, "data": {"result": {"success": False, "error": "Compile failed"}}}, {"success": False, "data": None}):
            run.return_value.stdout = json.dumps(value)
            with self.assertRaises(RuntimeError):
                gallery.cli(Path.cwd(), "run_script")


if __name__ == "__main__":
    unittest.main()
