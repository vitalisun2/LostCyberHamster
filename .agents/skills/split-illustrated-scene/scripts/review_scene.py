"""Review source-derived scene sprites and a rebuilt background."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


def resolve(manifest: Path, value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else manifest.parent / path


def rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def mask_for_polygons(
    size: tuple[int, int], polygons: list[list[list[int]]], origin: tuple[int, int]
) -> np.ndarray:
    mask = Image.new("1", size, 0)
    draw = ImageDraw.Draw(mask)
    for polygon in polygons:
        if len(polygon) < 3:
            raise ValueError("Polygon needs at least three points")
        draw.polygon([(int(x) - origin[0], int(y) - origin[1]) for x, y in polygon], fill=1)
    return np.asarray(mask, dtype=bool)


def checker(size: tuple[int, int], tile: int = 16) -> Image.Image:
    background = Image.new("RGBA", size, (222, 222, 222, 255))
    draw = ImageDraw.Draw(background)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            if ((x // tile) + (y // tile)) % 2:
                draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=(170, 170, 170, 255))
    return background


def on_checker(image: Image.Image) -> Image.Image:
    result = checker(image.size)
    result.alpha_composite(image)
    return result.convert("RGB")


def save_sprite_review(
    source_crop: Image.Image,
    sprite: Image.Image,
    forbidden: np.ndarray,
    path: Path,
) -> None:
    left = on_checker(source_crop)
    right = on_checker(sprite)
    if forbidden.any():
        outline = Image.fromarray((forbidden.astype(np.uint8) * 255), "L")
        tinted = Image.new("RGB", right.size, (220, 40, 40))
        right.paste(tinted, (0, 0), outline.point(lambda alpha: alpha // 5))
    scale = 2 if source_crop.width <= 650 else 1
    left = left.resize((left.width * scale, left.height * scale), Image.Resampling.NEAREST)
    right = right.resize((right.width * scale, right.height * scale), Image.Resampling.NEAREST)
    gap, header = 12, 28
    canvas = Image.new("RGB", (left.width + right.width + gap, max(left.height, right.height) + header), (30, 30, 32))
    canvas.paste(left, (0, header))
    canvas.paste(right, (left.width + gap, header))
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.load_default()
    draw.text((8, 7), "SOURCE CROP", fill="white", font=font)
    draw.text((left.width + gap + 8, 7), "SPRITE; RED = FORBIDDEN REGION", fill="white", font=font)
    canvas.save(path)


def save_contact_sheet(items: list[tuple[str, Image.Image]], path: Path) -> None:
    card_size = (360, 240)
    cols = 3
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new("RGB", (card_size[0] * cols, card_size[1] * rows), (25, 25, 27))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for index, (identifier, sprite) in enumerate(items):
        preview = on_checker(sprite)
        preview.thumbnail((card_size[0] - 16, card_size[1] - 35), Image.Resampling.NEAREST)
        x = (index % cols) * card_size[0]
        y = (index // cols) * card_size[1]
        sheet.paste(preview, (x + (card_size[0] - preview.width) // 2, y + 8))
        draw.text((x + 8, y + card_size[1] - 21), identifier, fill="white", font=font)
    sheet.save(path)


def review(manifest: Path, out: Path) -> dict:
    config = json.loads(manifest.read_text(encoding="utf-8"))
    out.mkdir(parents=True, exist_ok=True)
    source_path = resolve(manifest, config["source"])
    source = rgba(source_path)
    background = rgba(resolve(manifest, config["empty_background"]))
    edited_mask = Image.open(resolve(manifest, config["edited_mask"])).convert("L")
    if background.size != source.size or edited_mask.size != source.size:
        raise ValueError("Source, original-width background, and edited mask must have equal sizes")

    errors: list[str] = []
    warnings: list[str] = []
    source_pixels = np.asarray(source)
    background_pixels = np.asarray(background)
    allowed = np.asarray(edited_mask) > 0
    retained = ~allowed
    retained_exact = np.all(source_pixels == background_pixels, axis=2) & retained
    mismatch_count = int(retained.sum() - retained_exact.sum())
    if mismatch_count:
        errors.append(f"Background changed {mismatch_count} pixels outside edited_mask")

    overlay = source.copy()
    red = Image.new("RGBA", source.size, (255, 30, 30, 100))
    overlay.alpha_composite(Image.composite(red, Image.new("RGBA", source.size), edited_mask))
    on_checker(overlay).save(out / "allowed_edit_overlay.png")
    on_checker(background).save(out / "empty_background_review.png")

    extension_report = None
    if "extended_background" in config:
        extended = rgba(resolve(manifest, config["extended_background"]))
        extension = config.get("extension")
        if extension is None:
            errors.append("extended_background requires extension coordinates")
        else:
            split = int(extension["split_x"])
            insert = int(extension["insert_width"])
            if not 0 <= split <= source.width or insert <= 0:
                raise ValueError("Invalid split_x or insert_width")
            if extended.size != (source.width + insert, source.height):
                errors.append("Extended background has unexpected size")
            else:
                actual = np.asarray(extended)
                left_mismatch = int(np.count_nonzero(np.any(actual[:, :split] != background_pixels[:, :split], axis=2)))
                right_mismatch = int(
                    np.count_nonzero(np.any(actual[:, split + insert :] != background_pixels[:, split:], axis=2))
                )
                extension_report = {
                    "size": list(extended.size),
                    "split_x": split,
                    "insert_width": insert,
                    "left_segment_mismatch_pixels": left_mismatch,
                    "right_segment_mismatch_pixels": right_mismatch,
                }
                if left_mismatch or right_mismatch:
                    errors.append("Extension changed an original-width background segment")
        on_checker(extended).save(out / "extended_background_review.png")

    sprites = config.get("sprites", [])
    if not sprites:
        raise ValueError("Manifest has no sprites")
    seen: set[str] = set()
    sprite_reports: list[dict] = []
    contact_items: list[tuple[str, Image.Image]] = []
    reconstruction = background.copy()
    for spec in sprites:
        identifier = spec["id"]
        if identifier in seen:
            raise ValueError(f"Duplicate sprite id: {identifier}")
        seen.add(identifier)
        sprite = rgba(resolve(manifest, spec["file"]))
        x, y = [int(value) for value in spec["source_position"]]
        if x < 0 or y < 0 or x + sprite.width > source.width or y + sprite.height > source.height:
            raise ValueError(f"Sprite {identifier} lies outside source image")
        source_crop = source.crop((x, y, x + sprite.width, y + sprite.height))
        pixels = np.asarray(sprite)
        source_crop_pixels = source_pixels[y : y + sprite.height, x : x + sprite.width]
        visible = pixels[:, :, 3] > 0
        repairs = mask_for_polygons(sprite.size, spec.get("repair_polygons", []), (x, y))
        forbidden_polygons = spec.get("forbidden_polygons", [])
        forbidden = mask_for_polygons(sprite.size, forbidden_polygons, (x, y))
        source_mismatch = int(
            np.count_nonzero(visible & ~repairs & np.any(pixels != source_crop_pixels, axis=2))
        )
        forbidden_overlap = int(np.count_nonzero(visible & forbidden))
        if source_mismatch:
            errors.append(f"{identifier}: {source_mismatch} visible pixels differ from source outside repair polygons")
        if forbidden_overlap:
            errors.append(f"{identifier}: {forbidden_overlap} opaque pixels in forbidden ground regions")
        if not forbidden_polygons:
            warnings.append(f"{identifier}: no forbidden ground region marked; inspect cutout manually")
        elif not forbidden.any():
            errors.append(f"{identifier}: forbidden ground polygons do not intersect sprite bounds")
        reconstruction.alpha_composite(sprite, (x, y))
        save_sprite_review(source_crop, sprite, forbidden, out / f"{identifier}_review.png")
        contact_items.append((identifier, sprite))
        sprite_reports.append(
            {
                "id": identifier,
                "size": list(sprite.size),
                "source_position": [x, y],
                "visible_pixels": int(visible.sum()),
                "source_mismatch_pixels": source_mismatch,
                "forbidden_opaque_pixels": forbidden_overlap,
                "forbidden_region_pixels": int(forbidden.sum()),
                "forbidden_polygon_count": len(forbidden_polygons),
            }
        )

    save_contact_sheet(contact_items, out / "sprites_contact_sheet.png")
    reconstruction.save(out / "reconstruction.png")
    reconstruction_pixels = np.asarray(reconstruction).astype(np.int16)
    difference = np.abs(source_pixels.astype(np.int16) - reconstruction_pixels)
    intensity = np.clip(difference[:, :, :3].max(axis=2) * 4, 0, 255).astype(np.uint8)
    heatmap = np.zeros((source.height, source.width, 4), dtype=np.uint8)
    heatmap[:, :, 0] = intensity
    heatmap[:, :, 3] = 255
    Image.fromarray(heatmap, "RGBA").save(out / "reconstruction_difference.png")

    report = {
        "source": str(source_path),
        "source_sha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
        "source_size": list(source.size),
        "sprite_count": len(sprite_reports),
        "background_edited_pixels": int(allowed.sum()),
        "background_retained_pixels": int(retained.sum()),
        "background_retained_exact_pixels": int(retained_exact.sum()),
        "background_retained_mismatch_pixels": mismatch_count,
        "extension": extension_report,
        "sprites": sprite_reports,
        "reconstruction_mean_rgb_difference": round(float(difference[:, :, :3].mean()), 6),
        "errors": errors,
        "manual_review_notes": warnings,
    }
    (out / "qa-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    report = review(args.manifest.resolve(), args.out.resolve())
    print(
        json.dumps(
            {
                "sprites": report["sprite_count"],
                "retained_mismatch_pixels": report["background_retained_mismatch_pixels"],
                "errors": report["errors"],
                "manual_review_notes": len(report["manual_review_notes"]),
                "report": str(args.out.resolve() / "qa-report.json"),
            },
            ensure_ascii=False,
        )
    )
    return 1 if report["errors"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
