import json
from pathlib import Path
import tempfile
import unittest

import gallery


def svg(width=1, height=1):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}"/>'


class GalleryTests(unittest.TestCase):
    def test_portable_images_groups_and_partial_results(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            cases = []
            for i in range(2):
                folder = root / str(i)
                folder.mkdir()
                (folder / "same.svg").write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{i+1}" height="1"/>')
                cases.append({"id": f"group-{i}", "title": f"Group {i}", "states": [
                    {"id": "same", "title": "Image", "file": f"{i}/same.svg"}]})
            data = {"title": "</script><script>bad</script>", "cases": cases}
            manifest = root / "input.json"
            manifest.write_text(json.dumps(data))
            result = gallery.build(manifest, root / "output")
            self.assertTrue(result["complete"])
            images = [c["states"][0]["file"] for c in result["cases"]]
            self.assertNotEqual(*images)
            for i, image in enumerate(images):
                self.assertEqual((root / "output" / image).read_bytes(), (root / str(i) / "same.svg").read_bytes())
            self.assertNotIn(data["title"], (root / "output/index.html").read_text(encoding="utf-8"))
            # The exported manifest alone can rebuild a moved/copied gallery.
            self.assertTrue(gallery.build(root / "output/manifest.json", root / "portable")["complete"])
            data["cases"][0]["states"].append({"id": "missing", "title": "Missing", "captured": False, "error": "Unavailable"})
            manifest.write_text(json.dumps(data))
            self.assertFalse(gallery.build(manifest, root / "partial")["complete"])

    def test_missing_image_and_collision_preserve_existing_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            image = root / "test.svg"
            image.write_text(svg())
            data = {"title": "Images", "cases": [{"id": "g", "title": "G", "states": [
                {"id": "s", "title": "S", "file": "test.svg"}]}]}
            manifest = root / "input.json"
            manifest.write_text(json.dumps(data))
            gallery.build(manifest, root / "output")
            before = (root / "output/images/0001.svg").read_bytes()
            image.write_text(svg(width=9))
            with self.assertRaises(ValueError):
                gallery.build(manifest, root / "output")
            self.assertEqual(before, (root / "output/images/0001.svg").read_bytes())
            image.unlink()
            with self.assertRaises(ValueError):
                gallery.build(manifest, root / "missing")

    def test_capture_folder_builds_linked_sidecar_gallery(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            capture = root / "capture"
            shot = capture / "01_Screen" / "case_state.svg"
            shot.parent.mkdir(parents=True)
            shot.write_text(svg())
            (capture / "capture-manifest.json").write_text(json.dumps({
                "title": "Captured UI",
                "cases": [{"id": "case", "title": "Case", "source": "view.cs", "states": [{
                    "id": "state", "title": "State", "file": "01_Screen/case_state.svg", "captured": True
                }]}],
                "captured": 1,
                "expected": 1,
                "complete": True
            }), encoding="utf-8")
            out = root / "capture_gallery"
            result = gallery.build(capture, out, mode="linked")
            self.assertTrue(result["complete"])
            self.assertFalse((out / "images").exists())
            manifest = json.loads((out / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(manifest["cases"][0]["states"][0]["file"], "../capture/01_Screen/case_state.svg")
            serve_root, index = gallery.serve_root(out)
            self.assertEqual(serve_root, root)
            self.assertEqual(index, "capture_gallery/index.html")
            self.assertEqual(gallery.default_output(capture), root / "capture_gallery")

    def test_plain_folder_import_groups_top_level_directories(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "shots"
            (root / "Shop").mkdir(parents=True)
            (root / "Profile").mkdir(parents=True)
            (root / "Shop" / "locked-state.svg").write_text(svg())
            (root / "Profile" / "empty-state.svg").write_text(svg())
            result = gallery.build(root, root.parent / "shots_gallery", mode="linked")
            self.assertTrue(result["complete"])
            self.assertEqual([case["title"] for case in result["cases"]], ["Profile", "Shop"])
            self.assertEqual(result["cases"][0]["source"], "Profile")
            self.assertEqual(result["cases"][1]["states"][0]["title"], "locked state")

    def test_linked_serve_rejects_far_output_root(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            src = root / "src"
            out = root / "nested" / "gallery"
            (src / "shot.svg").parent.mkdir(parents=True)
            src.mkdir(exist_ok=True)
            (src / "shot.svg").write_text(svg())
            gallery.build(src, out, mode="linked")
            with self.assertRaises(ValueError):
                gallery.serve_root(out)


if __name__ == "__main__":
    unittest.main()
