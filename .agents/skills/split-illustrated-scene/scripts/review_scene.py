"""Review source-derived scene sprites and a rebuilt background."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
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
    contract_version = int(config.get("qa_contract_version", 1))
    if contract_version >= 2 and "sidewalk_rise" in config and "height_midpoint" not in config:
        errors.append("QA contract v2 requires height_midpoint for a raised sidewalk")
    source_pixels = np.asarray(source)
    background_pixels = np.asarray(background)
    allowed = np.asarray(edited_mask) > 0
    retained = ~allowed
    retained_exact = np.all(source_pixels == background_pixels, axis=2) & retained
    mismatch_count = int(retained.sum() - retained_exact.sum())
    if mismatch_count:
        errors.append(f"Background changed {mismatch_count} pixels outside edited_mask")

    required_paving_report = None
    paving = None
    if "required_paving_mask" in config:
        paving_image = Image.open(resolve(manifest, config["required_paving_mask"])).convert("L")
        if paving_image.size != source.size:
            raise ValueError("required_paving_mask must equal source size")
        paving = np.asarray(paving_image) > 0
        nonopaque = int(np.count_nonzero(paving & (background_pixels[:, :, 3] != 255)))
        required_paving_report = {"pixels": int(paving.sum()), "nonopaque_pixels": nonopaque}
        if nonopaque:
            errors.append(f"Required paving has {nonopaque} nonopaque pixels")

    preserved_road_report = None
    if "preserved_road_mask" in config:
        road_image = Image.open(resolve(manifest, config["preserved_road_mask"])).convert("L")
        if road_image.size != source.size:
            raise ValueError("preserved_road_mask must equal source size")
        road = np.asarray(road_image) > 0
        road_mismatch = int(np.count_nonzero(road & np.any(source_pixels != background_pixels, axis=2)))
        preserved_road_report = {"pixels": int(road.sum()), "rgba_mismatch_pixels": road_mismatch}
        if road_mismatch:
            errors.append(f"Preserved road changed {road_mismatch} RGBA pixels")

    cutout_reports: list[dict] = []
    if "unpainted_empty_background" in config:
        unpainted = rgba(resolve(manifest, config["unpainted_empty_background"]))
        if unpainted.size != source.size:
            raise ValueError("unpainted_empty_background must equal source size")
        unpainted_alpha = np.asarray(unpainted)[:, :, 3]
        for region in config.get("required_cutout_regions", []):
            mask = mask_for_polygons(source.size, [region["polygon"]], (0, 0))
            remaining = int(np.count_nonzero(mask & (unpainted_alpha != 0)))
            cutout_reports.append({"id": region["id"], "remaining_opaque_pixels": remaining})
            if remaining:
                errors.append(f"{region['id']}: {remaining} building pixels remain in unpainted background")
    elif config.get("required_cutout_regions"):
        raise ValueError("required_cutout_regions needs unpainted_empty_background")

    sidewalk_rise_report = None
    rise_excess = None
    if "sidewalk_rise" in config:
        rise = config["sidewalk_rise"]
        heights = [float(value) for value in rise["building_heights"]]
        if not heights or any(value <= 0 for value in heights):
            raise ValueError("sidewalk_rise requires positive building_heights")
        top_y = int(rise["top_y"])
        original_top_y = int(rise["original_top_y"])
        fraction = float(rise.get("max_fraction_of_mean_height", 0.5))
        if not 0 < fraction <= 1 or not 0 <= top_y < original_top_y <= source.height:
            raise ValueError("Invalid sidewalk_rise coordinates or fraction")
        mean_height = float(np.mean(heights))
        actual_rise = original_top_y - top_y
        allowed_rise = mean_height * fraction
        sidewalk_rise_report = {
            "top_y": top_y,
            "original_top_y": original_top_y,
            "building_count": len(heights),
            "mean_building_height": round(mean_height, 3),
            "actual_rise": actual_rise,
            "allowed_rise": round(allowed_rise, 3),
        }
        if actual_rise > allowed_rise:
            rise_excess = f"Sidewalk rise {actual_rise}px exceeds half-mean guide {allowed_rise:.2f}px"

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
    sprite_vertical_bounds: dict[str, tuple[int, int]] = {}
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
        if not visible.any():
            raise ValueError(f"Sprite {identifier} has no opaque pixels")
        visible_ys = np.where(visible)[0]
        sprite_vertical_bounds[identifier] = (y + int(visible_ys.min()), y + int(visible_ys.max()))
        repairs = mask_for_polygons(sprite.size, spec.get("repair_polygons", []), (x, y))
        forbidden_polygons = spec.get("forbidden_polygons", [])
        forbidden = mask_for_polygons(sprite.size, forbidden_polygons, (x, y))
        source_mismatch = int(
            np.count_nonzero(visible & ~repairs & np.any(pixels != source_crop_pixels, axis=2))
        )
        forbidden_overlap = int(np.count_nonzero(visible & forbidden))
        failed_anchors = []
        for point in spec.get("required_anchor_points", []):
            anchor_x, anchor_y = map(int, point)
            local_x, local_y = anchor_x - x, anchor_y - y
            if not (0 <= local_x < sprite.width and 0 <= local_y < sprite.height and visible[local_y, local_x]):
                failed_anchors.append([anchor_x, anchor_y])
        if failed_anchors:
            errors.append(f"{identifier}: architectural anchors missing from one sprite: {failed_anchors}")
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
                "required_anchor_count": len(spec.get("required_anchor_points", [])),
                "failed_anchor_points": failed_anchors,
            }
        )

    save_contact_sheet(contact_items, out / "sprites_contact_sheet.png")
    height_midpoint_report = None
    midpoint_passed = False
    if "height_midpoint" in config:
        midpoint_config = config["height_midpoint"]
        house_ids = midpoint_config["house_ids"]
        spans = midpoint_config["paving_intervals_x"]
        if not house_ids or len(set(house_ids)) != len(house_ids):
            raise ValueError("height_midpoint needs distinct house_ids")
        if not spans or paving is None:
            raise ValueError("height_midpoint needs paving_intervals_x and required_paving_mask")
        unknown = sorted(set(house_ids) - set(sprite_vertical_bounds))
        if unknown:
            raise ValueError(f"height_midpoint has unknown house_ids: {unknown}")
        roof_y = min(sprite_vertical_bounds[identifier][0] for identifier in house_ids)
        base_y = max(sprite_vertical_bounds[identifier][1] for identifier in house_ids)
        midpoint_y = (roof_y + base_y) / 2
        midpoint_row = math.ceil(midpoint_y)
        edge_ys: list[np.ndarray] = []
        missing_columns = 0
        unpaved_midpoint_columns = 0
        overlay = on_checker(background)
        draw = ImageDraw.Draw(overlay)
        for pair in spans:
            if len(pair) != 2:
                raise ValueError("Each paving interval must have two x coordinates")
            x0, x1 = map(int, pair)
            if not 0 <= x0 < x1 <= source.width:
                raise ValueError("Invalid half-open paving interval")
            columns = paving[:, x0:x1]
            present = np.any(columns, axis=0)
            missing_columns += int(np.count_nonzero(~present))
            unpaved_midpoint_columns += int(np.count_nonzero(~paving[midpoint_row, x0:x1]))
            edge = np.argmax(columns, axis=0)
            edge_ys.append(edge[present])
            draw.line((x0, midpoint_row, x1 - 1, midpoint_row), fill=(255, 75, 220), width=2)
            for dx, y_edge in enumerate(edge):
                if present[dx]:
                    draw.point((x0 + dx, int(y_edge)), fill=(60, 240, 100))
        if missing_columns:
            errors.append(f"Required paving is absent in {missing_columns} midpoint span columns")
        if unpaved_midpoint_columns:
            errors.append(f"Required paving misses midpoint in {unpaved_midpoint_columns} span columns")
        all_edges = np.concatenate(edge_ys) if edge_ys else np.array([], dtype=int)
        edge_range = [int(all_edges.min()), int(all_edges.max())] if all_edges.size else None
        if edge_range and edge_range[1] > midpoint_y:
            errors.append(f"Pavement rear edge y={edge_range[1]} misses scene midpoint y={midpoint_y}")
        height_midpoint_report = {
            "house_count": len(house_ids), "highest_roof_y": roof_y,
            "lowest_base_y": base_y, "scene_midpoint_y": midpoint_y,
            "paving_intervals_x": spans, "rear_edge_y_range": edge_range,
            "missing_columns": missing_columns,
            "unpaved_midpoint_columns": unpaved_midpoint_columns,
        }
        midpoint_passed = not missing_columns and not unpaved_midpoint_columns and bool(edge_range) and edge_range[1] <= midpoint_y
        overlay.save(out / "height_midpoint_overlay.png")
    elif "sidewalk_rise" in config:
        warnings.append("height_midpoint missing: scene-wide roof/base midpoint was not checked")
    if rise_excess:
        if midpoint_passed:
            warnings.append(rise_excess + "; scene midpoint criterion passes")
        else:
            errors.append(rise_excess)
    movement_preview_report = None
    if "movement_preview" in config:
        preview = rgba(resolve(manifest, config["movement_preview"]))
        if preview.size != source.size:
            raise ValueError("movement_preview must equal source size")
        preview_alpha = np.asarray(preview)[:, :, 3]
        movement_preview_report = []
        for region in config.get("required_preview_opaque_regions", []):
            mask = mask_for_polygons(source.size, [region["polygon"]], (0, 0))
            transparent = int(np.count_nonzero(mask & (preview_alpha == 0)))
            movement_preview_report.append({"id": region["id"], "transparent_pixels": transparent})
            if transparent:
                errors.append(f"{region['id']}: moved preview has {transparent} transparent gap pixels")
    elif config.get("required_preview_opaque_regions"):
        raise ValueError("required_preview_opaque_regions needs movement_preview")
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
        "qa_contract_version": contract_version,
        "sprite_count": len(sprite_reports),
        "background_edited_pixels": int(allowed.sum()),
        "background_retained_pixels": int(retained.sum()),
        "background_retained_exact_pixels": int(retained_exact.sum()),
        "background_retained_mismatch_pixels": mismatch_count,
        "required_paving": required_paving_report,
        "preserved_road": preserved_road_report,
        "required_cutout_regions": cutout_reports,
        "sidewalk_rise": sidewalk_rise_report,
        "height_midpoint": height_midpoint_report,
        "movement_preview": movement_preview_report,
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
