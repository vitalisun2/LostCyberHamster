"""Extend a source-derived illustrated street upward using a painted texture reference.

Existing road and all source pixels outside the allowed edit mask stay RGBA-exact.
The painted reference supplies only newly revealed pavement, never architecture.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def load_rgba(path: Path) -> np.ndarray:
    return np.array(Image.open(path).convert("RGBA"))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--previous", type=Path, required=True)
    parser.add_argument("--paint-reference", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--top", type=int, default=659)
    parser.add_argument("--original-top", type=int, default=718)
    parser.add_argument("--join", type=int, default=718)
    parser.add_argument("--main-road-edge", type=int, default=740)
    parser.add_argument("--paint-crop-top", type=int, default=683)
    parser.add_argument("--paint-crop-bottom", type=int, default=720)
    args = parser.parse_args()

    source = load_rgba(args.source)
    h, w = source.shape[:2]
    if (w, h) != (2000, 1000) or not 0 <= args.top < args.join < h:
        raise ValueError("Expected 2000x1000 source and valid top/join coordinates")
    prev = args.previous
    out = args.out
    for folder in (out / "Sprites", out / "Backgrounds", out / "QA", out / "Original"):
        folder.mkdir(parents=True, exist_ok=True)
    source_copy = out / "Original" / args.source.name
    shutil.copy2(args.source, source_copy)

    previous_manifest = json.loads((prev / "scene-manifest.json").read_text(encoding="utf-8"))
    records = previous_manifest["sprites"]
    for rec in records:
        target = out / rec["file"]
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(prev / rec["file"], target)
    house_heights = []
    for rec in records:
        bbox = Image.open(out / rec["file"]).getbbox()
        if bbox is None:
            raise ValueError(f"Empty building sprite: {rec['id']}")
        house_heights.append(bbox[3] - bbox[1])
    mean_house_height = float(np.mean(house_heights))
    sidewalk_rise = args.original_top - args.top
    full_sidewalk_depth = args.main_road_edge - args.top
    max_sidewalk_rise = mean_house_height / 2
    if sidewalk_rise <= 0 or sidewalk_rise > max_sidewalk_rise:
        raise ValueError(
            f"Sidewalk rise {sidewalk_rise}px must be positive and no more than "
            f"half mean house height {max_sidewalk_rise:.2f}px"
        )

    # Rebuild the removal mask from cleaned movable sprites. The earlier broad
    # mask included right-hand paving and would erase the open road turn.
    removal = np.zeros((h, w), dtype=bool)
    for rec in records:
        sprite = load_rgba(out / rec["file"])
        x, y = rec["source_position"]
        sh, sw = sprite.shape[:2]
        removal[y:y + sh, x:x + sw] |= sprite[:, :, 3] > 0
    Image.fromarray((removal.astype(np.uint8) * 255), "L").save(
        out / "QA" / "building_removal_mask.png"
    )

    # Scale the painted candidate to the source coordinate space, then use only
    # its interior brush texture. Its generated curb, road and skyline are discarded.
    candidate = Image.open(args.paint_reference).convert("RGBA").resize((w, h), Image.Resampling.LANCZOS)
    texture = candidate.crop((0, args.paint_crop_top, w, args.paint_crop_bottom)).resize(
        (w, args.join - args.top), Image.Resampling.BICUBIC
    )
    painted = np.array(texture)
    painted[:, :, 3] = 255
    # Match muted source sidewalk colour near the join using opaque source samples.
    sample_y0, sample_y1 = args.join, min(args.join + 18, h)
    source_sample = source[sample_y0:sample_y1, 60:1300]
    source_visible = source_sample[source_sample[:, :, 3] > 0][:, :3]
    painted_sample = painted[-20:, 60:1300, :3].reshape(-1, 3)
    source_median = np.median(source_visible, axis=0)
    painted_median = np.median(painted_sample, axis=0)
    correction = np.clip(np.round(source_median - painted_median), -8, 8).astype(np.int16)
    painted[:, :, :3] = np.clip(painted[:, :, :3].astype(np.int16) + correction, 0, 255).astype(np.uint8)

    texture_canvas = np.zeros_like(source)
    texture_canvas[args.top:args.join] = painted
    y_grid = np.arange(h)[:, None]
    new_blank = (source[:, :, 3] == 0) & (y_grid >= args.top) & (y_grid < args.join)
    architecture = removal & (y_grid >= args.top) & (y_grid < args.join)
    architecture_above_paving = removal & (y_grid < args.top)

    # Explicit foreground road/exit protection. These source pixels are copied
    # directly even if an earlier coarse architecture mask covered them.
    protected = np.zeros((h, w), dtype=bool)
    protected[args.join:760] = source[args.join:760, :, 3] > 0
    protected[687:args.join, :34] = source[687:args.join, :34, 3] > 0
    protected[690:args.join, 1970:] = source[690:args.join, 1970:, 3] > 0
    paint_edit = (new_blank | architecture) & ~protected
    clear_edit = architecture_above_paving & ~protected
    edit = paint_edit | clear_edit
    street = source.copy()
    street[paint_edit] = texture_canvas[paint_edit]
    street[clear_edit] = [0, 0, 0, 0]
    # A thin ink edge defines the new pavement boundary.
    street[args.top, paint_edit[args.top], :3] = [30, 35, 38]
    street[protected] = source[protected]

    street_path = out / "Sprites" / "Paris_Night_street_extended.png"
    Image.fromarray(street, "RGBA").save(street_path)
    background_path = out / "Backgrounds" / "Paris_Night_background_extended.png"
    shutil.copy2(street_path, background_path)
    Image.fromarray((edit.astype(np.uint8) * 255), "L").save(out / "QA" / "allowed_background_changes.png")
    Image.fromarray((protected.astype(np.uint8) * 255), "L").save(out / "QA" / "preserved_road_mask.png")
    required = np.zeros((h, w), dtype=np.uint8)
    required[args.top:args.join, :] = 255
    Image.fromarray(required, "L").save(out / "QA" / "required_paving_mask.png")
    paint_copy = out / "QA" / "paint_reference.png"
    if args.paint_reference.resolve() != paint_copy.resolve():
        shutil.copy2(args.paint_reference, paint_copy)

    # Source-position reconstruction is a sanity check; the new pavement remains
    # visible where buildings are moved in game.
    reconstruction = Image.fromarray(street, "RGBA")
    for rec in records:
        reconstruction.alpha_composite(Image.open(out / rec["file"]).convert("RGBA"), tuple(rec["source_position"]))
    reconstruction.save(out / "QA" / "reconstruction.png")
    checker = Image.new("RGBA", (w, h), (25, 27, 30, 255))
    checker.alpha_composite(Image.fromarray(street, "RGBA"))
    checker.save(out / "QA" / "extended_street_preview.png")

    # Final visual gate: compare the ground itself and a representative 50px
    # upward house move at original scene coordinates.
    old_street = Image.open(prev / "Backgrounds" / "Paris_Night_background_empty.png").convert("RGBA")
    comparison_box = (350, 620, 1150, 760)

    def comparison_tile(image: Image.Image, title: str, box: tuple[int, int, int, int] = comparison_box) -> Image.Image:
        region = image.crop(box)
        board = Image.new("RGBA", region.size, (225, 225, 225, 255))
        draw = ImageDraw.Draw(board)
        for cy in range(0, board.height, 16):
            for cx in range(0, board.width, 16):
                if (cx // 16 + cy // 16) % 2:
                    draw.rectangle((cx, cy, cx + 15, cy + 15), fill=(185, 185, 185, 255))
        board.alpha_composite(region)
        board = board.resize((board.width * 2, board.height * 2), Image.Resampling.NEAREST)
        tile = Image.new("RGB", (board.width, board.height + 32), (28, 29, 31))
        tile.paste(board.convert("RGB"), (0, 32))
        ImageDraw.Draw(tile).text((8, 9), title, fill=(255, 255, 255))
        return tile

    def side_by_side(before: Image.Image, after: Image.Image, path: Path) -> None:
        canvas = Image.new("RGB", (before.width + after.width, before.height), (28, 29, 31))
        canvas.paste(before, (0, 0))
        canvas.paste(after, (before.width, 0))
        canvas.save(path)

    side_by_side(
        comparison_tile(old_street, "Version 1: narrow ground"),
        comparison_tile(Image.fromarray(street, "RGBA"),
                        f"Version 2: old y={args.original_top}, top y={args.top}, rise={sidewalk_rise}px"),
        out / "QA" / "sidewalk_height_comparison.png",
    )
    moved_old = old_street.copy()
    moved_new = Image.fromarray(street, "RGBA")
    for rec in records:
        sprite = Image.open(out / rec["file"]).convert("RGBA")
        x, y = rec["source_position"]
        moved_old.alpha_composite(sprite, (x, y - 50))
        moved_new.alpha_composite(sprite, (x, y - 50))
    side_by_side(
        comparison_tile(moved_old, "Version 1: houses moved up 50px"),
        comparison_tile(moved_new, "Version 2: houses moved up 50px"),
        out / "QA" / "moved_houses_comparison.png",
    )
    road_box = (1350, 660, 2000, 760)
    side_by_side(
        comparison_tile(Image.fromarray(source, "RGBA"), "Source: right road exit", road_box),
        comparison_tile(Image.fromarray(street, "RGBA"), "Version 2: right road exit", road_box),
        out / "QA" / "road_exit_comparison.png",
    )

    source_diff = np.any(street != source, axis=2)
    road_diff = np.any(street[protected] != source[protected], axis=1)
    retained_diff = source_diff & ~edit
    required_transparent = (required > 0) & (street[:, :, 3] == 0)
    recon = np.array(reconstruction)
    recon_diff = np.any(recon != source, axis=2)
    report = {
        "source_size": [w, h],
        "painted_top_y": args.top,
        "original_sidewalk_top_y": args.original_top,
        "join_y": args.join,
        "main_road_edge_y": args.main_road_edge,
        "mean_house_height": round(mean_house_height, 3),
        "max_sidewalk_rise": round(max_sidewalk_rise, 3),
        "sidewalk_rise": sidewalk_rise,
        "full_sidewalk_depth": full_sidewalk_depth,
        "newly_painted_pixels": int(edit.sum()),
        "required_paving_transparent_pixels": int(required_transparent.sum()),
        "protected_road_rgba_mismatch_pixels": int(road_diff.sum()),
        "retained_rgba_mismatch_pixels": int(retained_diff.sum()),
        "reconstruction_diff_pixels": int(recon_diff.sum()),
        "texture_reference_color_correction_rgb": correction.tolist(),
    }
    (out / "QA" / "extension-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    manifest = dict(previous_manifest)
    manifest.update({
        "source": str(source_copy),
        "source_sha256": hashlib.sha256(args.source.read_bytes()).hexdigest(),
        "background": "Backgrounds/Paris_Night_background_extended.png",
        "sidewalk": "Sprites/Paris_Night_street_extended.png",
        "sidewalk_position": [0, 0],
        "sidewalk_size": [w, h],
        "painted_top_y": args.top,
        "original_sidewalk_top_y": args.original_top,
        "main_road_edge_y": args.main_road_edge,
        "mean_house_height": round(mean_house_height, 3),
        "sidewalk_rise": sidewalk_rise,
        "full_sidewalk_depth": full_sidewalk_depth,
        "required_paving_mask": "QA/required_paving_mask.png",
        "preserved_road_mask": "QA/preserved_road_mask.png",
        "allowed_background_changes": "QA/allowed_background_changes.png",
        "layer_order": ["Backgrounds/Paris_Night_background_extended.png", "Sprites/building_*.png"],
    })
    (out / "scene-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    review = {
        "source": "Original/" + args.source.name,
        "empty_background": manifest["background"],
        "edited_mask": manifest["allowed_background_changes"],
        "sidewalk_rise": {
            "top_y": args.top,
            "original_top_y": args.original_top,
            "building_heights": house_heights,
            "max_fraction_of_mean_height": 0.5,
        },
        "required_paving_mask": manifest["required_paving_mask"],
        "preserved_road_mask": manifest["preserved_road_mask"],
        "sprites": [
            {
                "id": rec["id"], "file": rec["file"], "source_position": rec["source_position"],
                "forbidden_polygons": [[
                    [0, 719], [1318, 719], [1390, 716], [1500, 710],
                    [1650, 704], [1780, 701], [2000, 699], [2000, 760], [0, 760]
                ]],
                "repair_polygons": [],
            }
            for rec in records
        ],
    }
    (out / "review-manifest.json").write_text(json.dumps(review, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
