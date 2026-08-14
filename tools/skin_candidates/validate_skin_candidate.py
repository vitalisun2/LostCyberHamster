"""Validate generated sprite sheets against source PNGs and Unity metadata."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image


IMPORTER_KEYS = (
    "mipMapMode",
    "enableMipMap",
    "sRGBTexture",
    "isReadable",
    "filterMode",
    "wrapU",
    "wrapV",
    "wrapW",
    "spriteMode",
    "spriteMeshType",
    "alignment",
    "spritePivot",
    "spritePixelsToUnits",
    "spriteGenerateFallbackPhysicsShape",
    "alphaUsage",
    "alphaIsTransparency",
    "textureType",
    "textureShape",
    "nPOTScale",
    "maxTextureSize",
    "textureCompression",
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def alpha_bbox(alpha: np.ndarray) -> list[int] | None:
    ys, xs = np.where(alpha > 0)
    if xs.size == 0:
        return None
    return [int(xs.min()), int(ys.min()), int(xs.max() + 1), int(ys.max() + 1)]


def png_ihdr(path: Path) -> dict[str, Any]:
    header = path.read_bytes()[:33]
    if len(header) < 33 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        return {"valid_png": False, "rgba8": False}
    width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", header[16:29]
    )
    return {
        "valid_png": True,
        "width": width,
        "height": height,
        "bit_depth": bit_depth,
        "color_type": color_type,
        "color_type_name": {0: "gray", 2: "RGB", 3: "indexed", 4: "gray-alpha", 6: "RGBA"}.get(
            color_type, "unknown"
        ),
        "compression": compression,
        "filter": filtering,
        "interlace": interlace,
        "rgba8": bit_depth == 8 and color_type == 6,
    }


def first_value(text: str, key: str) -> str | None:
    match = re.search(rf"^\s+{re.escape(key)}:\s*(.*?)\s*$", text, re.MULTILINE)
    return match.group(1) if match else None


def importer_contract(text: str) -> dict[str, str | None]:
    return {key: first_value(text, key) for key in IMPORTER_KEYS}


def platform_block(text: str) -> str:
    match = re.search(
        r"^  platformSettings:\s*$\n(?P<body>.*?)(?=^  spriteSheet:)",
        text,
        re.MULTILINE | re.DOTALL,
    )
    if not match:
        return ""
    return "\n".join(line.rstrip() for line in match.group("body").splitlines()).strip()


def sprite_blocks(text: str) -> list[str]:
    match = re.search(
        r"^  spriteSheet:\s*$\n(?P<body>.*?)(?=^    outline:|^  spritePackingTag:)",
        text,
        re.MULTILINE | re.DOTALL,
    )
    if not match:
        return []
    body = match.group("body")
    starts = [item.start() for item in re.finditer(r"^    - serializedVersion:\s*\d+\s*$", body, re.MULTILINE)]
    return [body[start : starts[index + 1] if index + 1 < len(starts) else len(body)] for index, start in enumerate(starts)]


def parse_vector2(value: str) -> list[float]:
    match = re.fullmatch(r"\{x:\s*([^,]+),\s*y:\s*([^}]+)\}", value.strip())
    if not match:
        raise ValueError(f"Invalid Vector2: {value!r}")
    return [float(match.group(1)), float(match.group(2))]


def parse_sprite(block: str) -> dict[str, Any]:
    def get(pattern: str) -> str:
        match = re.search(pattern, block, re.MULTILINE)
        if not match:
            raise ValueError(f"Missing sprite field: {pattern}")
        return match.group(1).strip()

    physics_match = re.search(
        r"^      physicsShape:\s*(?P<inline>\[\])?\s*$\n(?P<body>.*?)(?=^      tessellationDetail:)",
        block,
        re.MULTILINE | re.DOTALL,
    )
    if not physics_match:
        physics = ""
    elif physics_match.group("inline"):
        physics = "[]"
    else:
        physics = "\n".join(line.rstrip() for line in physics_match.group("body").splitlines()).strip()

    return {
        "name": get(r"^      name:\s*(.*?)$"),
        "rect": {
            "x": float(get(r"^        x:\s*(.*?)$")),
            "y": float(get(r"^        y:\s*(.*?)$")),
            "width": float(get(r"^        width:\s*(.*?)$")),
            "height": float(get(r"^        height:\s*(.*?)$")),
        },
        "alignment": int(get(r"^      alignment:\s*(.*?)$")),
        "pivot": parse_vector2(get(r"^      pivot:\s*(.*?)$")),
        "physics_shape": physics,
    }


def read_meta(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {"exists": False, "errors": [f"missing meta: {path}"], "contract": {}, "platform": "", "sprites": []}
    text = path.read_text(encoding="utf-8")
    errors: list[str] = []
    sprites: list[dict[str, Any]] = []
    try:
        sprites = [parse_sprite(block) for block in sprite_blocks(text)]
    except ValueError as error:
        errors.append(str(error))
    if not sprites:
        errors.append("sprite sheet has no parsed sprites")
    return {
        "exists": True,
        "errors": errors,
        "contract": importer_contract(text),
        "platform": platform_block(text),
        "sprites": sprites,
    }


def compare_meta(source_path: Path, candidate_path: Path) -> dict[str, Any]:
    source = read_meta(source_path)
    candidate = read_meta(candidate_path)
    source_sprites = source["sprites"]
    candidate_sprites = candidate["sprites"]
    contract_differences = [
        key
        for key in IMPORTER_KEYS
        if source["contract"].get(key) != candidate["contract"].get(key)
    ]
    result = {
        "source_meta": str(source_path),
        "candidate_meta": str(candidate_path),
        "source_exists": source["exists"],
        "candidate_exists": candidate["exists"],
        "errors": source["errors"] + candidate["errors"],
        "source_contract": source["contract"],
        "candidate_contract": candidate["contract"],
        "importer_contract_match": not contract_differences,
        "importer_contract_differences": contract_differences,
        "platform_settings_match": source["platform"] == candidate["platform"] and bool(source["platform"]),
        "source_sprite_count": len(source_sprites),
        "candidate_sprite_count": len(candidate_sprites),
        "sprite_count_match": len(source_sprites) == len(candidate_sprites) and bool(source_sprites),
        "sprite_names_match": [item["name"] for item in source_sprites]
        == [item["name"] for item in candidate_sprites],
        "sprite_rects_match": [item["rect"] for item in source_sprites]
        == [item["rect"] for item in candidate_sprites],
        "sprite_pivots_match": [
            (item["alignment"], item["pivot"]) for item in source_sprites
        ]
        == [(item["alignment"], item["pivot"]) for item in candidate_sprites],
        "custom_physics_shapes_match": [item["physics_shape"] for item in source_sprites]
        == [item["physics_shape"] for item in candidate_sprites],
        "source_sprites": source_sprites,
        "candidate_sprites": candidate_sprites,
    }
    result["pass"] = bool(
        result["source_exists"]
        and result["candidate_exists"]
        and not result["errors"]
        and result["importer_contract_match"]
        and result["platform_settings_match"]
        and result["sprite_count_match"]
        and result["sprite_names_match"]
        and result["sprite_rects_match"]
        and result["sprite_pivots_match"]
        and result["custom_physics_shapes_match"]
    )
    return result


def arrays_equal(left: np.ndarray, right: np.ndarray) -> bool:
    return left.shape == right.shape and bool(np.array_equal(left, right))


def mismatch_count(left: np.ndarray, right: np.ndarray) -> int | None:
    if left.shape != right.shape:
        return None
    return int(np.count_nonzero(left != right))


def validate_sheet(source_path: Path, candidate_path: Path, cell_width: int, cell_height: int) -> dict[str, Any]:
    errors: list[str] = []
    source_ihdr = png_ihdr(source_path)
    candidate_ihdr = png_ihdr(candidate_path)
    with Image.open(source_path) as source_image, Image.open(candidate_path) as candidate_image:
        source_mode = source_image.mode
        candidate_mode = candidate_image.mode
        source_size = source_image.size
        candidate_size = candidate_image.size
        source_rgba = np.asarray(source_image.convert("RGBA"), dtype=np.uint8)
        candidate_rgba = np.asarray(candidate_image.convert("RGBA"), dtype=np.uint8)

    source_alpha = source_rgba[:, :, 3]
    candidate_alpha = candidate_rgba[:, :, 3]
    dimensions_match = source_size == candidate_size
    alpha_match = arrays_equal(source_alpha, candidate_alpha)
    mask_match = arrays_equal(source_alpha > 0, candidate_alpha > 0)
    grid_integral = source_size[0] % cell_width == 0 and source_size[1] % cell_height == 0
    columns = source_size[0] // cell_width if grid_integral else 0
    rows = source_size[1] // cell_height if grid_integral else 0

    meta = compare_meta(
        source_path.with_suffix(source_path.suffix + ".meta"),
        candidate_path.with_suffix(candidate_path.suffix + ".meta"),
    )
    source_sprites = meta["source_sprites"]
    sprite_rect_contract = bool(source_sprites)
    occupied_cells: set[tuple[int, int]] = set()
    frames: list[dict[str, Any]] = []
    for sprite in source_sprites:
        rect = sprite["rect"]
        x = int(rect["x"])
        y = int(rect["y"])
        width = int(rect["width"])
        height = int(rect["height"])
        top = source_size[1] - y - height
        rect_valid = (
            rect["x"] == x
            and rect["y"] == y
            and rect["width"] == width
            and rect["height"] == height
            and width == cell_width
            and height == cell_height
            and x % cell_width == 0
            and y % cell_height == 0
            and x >= 0
            and top >= 0
            and x + width <= source_size[0]
            and top + height <= source_size[1]
        )
        sprite_rect_contract = sprite_rect_contract and rect_valid
        if not rect_valid or not dimensions_match:
            frames.append({"name": sprite["name"], "rect_valid": rect_valid, "alpha_match": False, "mask_match": False})
            continue
        occupied_cells.add((x // cell_width, top // cell_height))
        source_cell = source_alpha[top : top + height, x : x + width]
        candidate_cell = candidate_alpha[top : top + height, x : x + width]
        frames.append(
            {
                "name": sprite["name"],
                "rect_unity": [x, y, width, height],
                "cell": [x // cell_width, top // cell_height],
                "rect_valid": True,
                "source_bbox": alpha_bbox(source_cell),
                "candidate_bbox": alpha_bbox(candidate_cell),
                "bbox_match": alpha_bbox(source_cell) == alpha_bbox(candidate_cell),
                "alpha_match": arrays_equal(source_cell, candidate_cell),
                "alpha_mismatch_pixels": mismatch_count(source_cell, candidate_cell),
                "mask_match": arrays_equal(source_cell > 0, candidate_cell > 0),
                "mask_mismatch_pixels": mismatch_count(source_cell > 0, candidate_cell > 0),
            }
        )

    cells: list[dict[str, Any]] = []
    empty_cell_spill = 0
    if grid_integral and dimensions_match:
        for row in range(rows):
            for column in range(columns):
                y0 = row * cell_height
                x0 = column * cell_width
                source_cell = source_alpha[y0 : y0 + cell_height, x0 : x0 + cell_width]
                candidate_cell = candidate_alpha[y0 : y0 + cell_height, x0 : x0 + cell_width]
                meta_empty = (column, row) not in occupied_cells
                spill = meta_empty and bool(np.any(candidate_cell > 0))
                empty_cell_spill += int(spill)
                cells.append(
                    {
                        "cell": [column, row],
                        "meta_empty": meta_empty,
                        "source_bbox": alpha_bbox(source_cell),
                        "candidate_bbox": alpha_bbox(candidate_cell),
                        "bbox_match": alpha_bbox(source_cell) == alpha_bbox(candidate_cell),
                        "alpha_match": arrays_equal(source_cell, candidate_cell),
                        "mask_match": arrays_equal(source_cell > 0, candidate_cell > 0),
                        "spill": spill,
                    }
                )

    checks = {
        "dimensions_match": dimensions_match,
        "source_dimensions": list(source_size),
        "candidate_dimensions": list(candidate_size),
        "source_mode": source_mode,
        "candidate_mode": candidate_mode,
        "source_png": source_ihdr,
        "candidate_png": candidate_ihdr,
        "candidate_rgba8": candidate_mode == "RGBA" and bool(candidate_ihdr.get("rgba8")),
        "full_alpha_match": alpha_match,
        "full_alpha_mismatch_pixels": mismatch_count(source_alpha, candidate_alpha),
        "full_mask_match": mask_match,
        "full_mask_mismatch_pixels": mismatch_count(source_alpha > 0, candidate_alpha > 0),
        "source_alpha_sha256": sha256(source_alpha.tobytes()),
        "candidate_alpha_sha256": sha256(candidate_alpha.tobytes()),
        "source_alpha_bbox": alpha_bbox(source_alpha),
        "candidate_alpha_bbox": alpha_bbox(candidate_alpha),
        "grid_integral": grid_integral,
        "grid": [columns, rows],
        "cell_size": [cell_width, cell_height],
        "source_meta_frame_count": len(source_sprites),
        "sprite_rect_contract": sprite_rect_contract,
        "all_frame_bboxes_match": bool(frames) and all(frame.get("bbox_match", False) for frame in frames),
        "all_frame_alpha_match": bool(frames) and all(frame["alpha_match"] for frame in frames),
        "all_frame_masks_match": bool(frames) and all(frame["mask_match"] for frame in frames),
        "all_cells_match": bool(cells)
        and all(cell["bbox_match"] and cell["alpha_match"] and cell["mask_match"] for cell in cells),
        "empty_cell_spill": empty_cell_spill,
        "meta_match": meta["pass"],
    }
    required = (
        "dimensions_match",
        "candidate_rgba8",
        "full_alpha_match",
        "full_mask_match",
        "grid_integral",
        "sprite_rect_contract",
        "all_frame_bboxes_match",
        "all_frame_alpha_match",
        "all_frame_masks_match",
        "all_cells_match",
        "meta_match",
    )
    if not source_ihdr.get("rgba8"):
        errors.append("source PNG is not RGBA8")
    failures = [name for name in required if not checks[name]]
    if empty_cell_spill:
        failures.append("empty_cell_spill")
    failures.extend(errors)
    return {
        "sheet": candidate_path.name,
        "source": str(source_path),
        "candidate": str(candidate_path),
        "status": "PASS" if not failures else "FAIL",
        "failures": failures,
        "checks": checks,
        "frames": frames,
        "cells": cells,
        "meta": meta,
    }


def discover_sheets(source_root: Path, candidate_root: Path) -> tuple[list[tuple[Path, Path, Path]], list[str]]:
    sheets: list[tuple[Path, Path, Path]] = []
    ignored: list[str] = []
    for candidate in sorted(candidate_root.rglob("*.png"), key=lambda path: path.as_posix().lower()):
        relative = candidate.relative_to(candidate_root)
        if any(part.startswith((".", "_")) for part in relative.parts[:-1]):
            ignored.append(relative.as_posix())
            continue
        source = source_root / relative
        if not source.is_file():
            ignored.append(relative.as_posix())
            continue
        sheets.append((relative, source, candidate))
    return sheets, ignored


def markdown_report(report: dict[str, Any]) -> str:
    lines = [
        "# Skin candidate QA",
        "",
        f"Status: **{report['status']}**",
        "",
        "| Sheet | Status | Dimensions | RGBA8 | Alpha | Cells | Meta | Failures |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for sheet in report["sheets"]:
        checks = sheet["checks"]
        dims = "x".join(str(value) for value in checks.get("candidate_dimensions", [])) or "unknown"
        failures = ", ".join(str(value) for value in sheet["failures"]) or "none"
        failures = failures.replace("|", "\\|")
        lines.append(
            f"| {sheet['sheet']} | {sheet['status']} | {dims} | "
            f"{'PASS' if checks.get('candidate_rgba8') else 'FAIL'} | "
            f"{'PASS' if checks.get('full_alpha_match') else 'FAIL'} | "
            f"{'PASS' if checks.get('all_cells_match') and checks.get('empty_cell_spill') == 0 else 'FAIL'} | "
            f"{'PASS' if checks.get('meta_match') else 'FAIL'} | {failures} |"
        )
    lines.append("")
    return "\n".join(lines)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", required=True, type=Path)
    parser.add_argument("--candidate-root", required=True, type=Path)
    parser.add_argument("--output-json", required=True, type=Path)
    parser.add_argument("--output-md", type=Path)
    parser.add_argument("--cell-width", type=int, default=240)
    parser.add_argument("--cell-height", type=int, default=650)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if args.cell_width <= 0 or args.cell_height <= 0:
        raise SystemExit("cell dimensions must be positive")
    source_root = args.source_root.resolve()
    candidate_root = args.candidate_root.resolve()
    if not source_root.is_dir() or not candidate_root.is_dir():
        raise SystemExit("source-root and candidate-root must be existing directories")

    discovered, ignored = discover_sheets(source_root, candidate_root)
    sheets: list[dict[str, Any]] = []
    for relative, source, candidate in discovered:
        try:
            result = validate_sheet(source, candidate, args.cell_width, args.cell_height)
            result["sheet"] = relative.as_posix()
        except Exception as error:  # Keep batch report useful when one sheet is malformed.
            result = {
                "sheet": relative.as_posix(),
                "source": str(source),
                "candidate": str(candidate),
                "status": "FAIL",
                "failures": [f"validation_error: {type(error).__name__}: {error}"],
                "checks": {},
                "frames": [],
                "cells": [],
                "meta": {},
            }
        sheets.append(result)

    status = "PASS" if sheets and all(sheet["status"] == "PASS" for sheet in sheets) else "FAIL"
    report = {
        "status": status,
        "source_root": str(source_root),
        "candidate_root": str(candidate_root),
        "cell_size": [args.cell_width, args.cell_height],
        "discovered_sheet_count": len(sheets),
        "ignored_candidate_pngs": ignored,
        "sheets": sheets,
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if args.output_md:
        args.output_md.parent.mkdir(parents=True, exist_ok=True)
        args.output_md.write_text(markdown_report(report), encoding="utf-8")

    print(
        json.dumps(
            {
                "status": status,
                "sheets": [{"sheet": sheet["sheet"], "status": sheet["status"]} for sheet in sheets],
                "ignored_candidate_pngs": ignored,
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    if status != "PASS":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
