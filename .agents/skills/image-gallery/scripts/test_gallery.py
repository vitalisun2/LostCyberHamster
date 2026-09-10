import json
from pathlib import Path
import tempfile
import unittest

import gallery


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
            image.write_text('<svg xmlns="http://www.w3.org/2000/svg"/>')
            data = {"title": "Images", "cases": [{"id": "g", "title": "G", "states": [
                {"id": "s", "title": "S", "file": "test.svg"}]}]}
            manifest = root / "input.json"
            manifest.write_text(json.dumps(data))
            gallery.build(manifest, root / "output")
            before = (root / "output/images/0001.svg").read_bytes()
            image.write_text('<svg xmlns="http://www.w3.org/2000/svg" width="9"/>')
            with self.assertRaises(ValueError):
                gallery.build(manifest, root / "output")
            self.assertEqual(before, (root / "output/images/0001.svg").read_bytes())
            image.unlink()
            with self.assertRaises(ValueError):
                gallery.build(manifest, root / "missing")


if __name__ == "__main__":
    unittest.main()
