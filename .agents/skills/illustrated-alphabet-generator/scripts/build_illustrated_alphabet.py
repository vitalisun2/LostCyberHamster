#!/usr/bin/env python3
"""Normalize illustrated Latin/Cyrillic sheets and derive contour kerning."""

import argparse
import base64
import json
from pathlib import Path

from PIL import Image


ALPHABETS = {
    "latin": ("ABCDEFGHIJKLM", "NOPQRSTUVWXYZ"),
    "cyrillic": ("АБВГДЕЁЖЗИЙ", "КЛМНОПРСТУФ", "ХЦЧШЩЪЫЬЭЮЯ"),
}


def count_ink_vertical(alpha, x, y0, y1, threshold):
    pixels = alpha.load()
    return sum(1 for y in range(y0, y1) if pixels[x, y] > threshold)


def count_ink_horizontal(alpha, y, threshold):
    pixels = alpha.load()
    return sum(1 for x in range(alpha.width) if pixels[x, y] > threshold)


def find_boundary(alpha, y0, y1, ideal, radius, threshold):
    best_x = ideal
    best_score = None
    for x in range(max(1, ideal - radius), min(alpha.width - 2, ideal + radius) + 1):
        score = sum(count_ink_vertical(alpha, sample_x, y0, y1, threshold) for sample_x in (x - 1, x, x + 1))
        candidate = score + abs(x - ideal) * 0.02
        if best_score is None or candidate < best_score:
            best_score = candidate
            best_x = x
    return best_x


def find_row_boundaries(alpha, row_count, threshold):
    boundaries = [0]
    nominal_height = alpha.height / row_count
    radius = max(12, round(nominal_height * 0.24))
    for row in range(1, row_count):
        ideal = round(row * nominal_height)
        best_y = ideal
        best_score = None
        for y in range(max(1, ideal - radius), min(alpha.height - 2, ideal + radius) + 1):
            score = sum(count_ink_horizontal(alpha, sample_y, threshold) for sample_y in (y - 1, y, y + 1))
            candidate = score + abs(y - ideal) * 0.02
            if best_score is None or candidate < best_score:
                best_score = candidate
                best_y = y
        boundaries.append(best_y)
    boundaries.append(alpha.height)
    return boundaries


def validate_source_alpha(image, path, threshold):
    alpha = image.getchannel("A")
    minimum, maximum = alpha.getextrema()
    if maximum <= threshold:
        raise ValueError(f"No visible pixels: {path}")
    if minimum > threshold:
        raise ValueError(f"No transparent pixels; background may be baked into RGB: {path}")
    border = []
    pixels = alpha.load()
    for x in range(alpha.width):
        border.extend((pixels[x, 0], pixels[x, alpha.height - 1]))
    for y in range(alpha.height):
        border.extend((pixels[0, y], pixels[alpha.width - 1, y]))
    transparent_ratio = sum(value <= threshold for value in border) / len(border)
    if transparent_ratio < 0.8:
        raise ValueError(f"Sheet border is not mostly transparent ({transparent_ratio:.1%}): {path}")


def clean_components(image, threshold, minimum_component_ratio):
    alpha = image.getchannel("A")
    pixels = alpha.load()
    width, height = alpha.size
    seen = set()
    components = []
    for y in range(height):
        for x in range(width):
            if (x, y) in seen or pixels[x, y] <= threshold:
                continue
            stack = [(x, y)]
            seen.add((x, y))
            component = []
            while stack:
                px, py = stack.pop()
                component.append((px, py))
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    if 0 <= nx < width and 0 <= ny < height and (nx, ny) not in seen and pixels[nx, ny] > threshold:
                        seen.add((nx, ny))
                        stack.append((nx, ny))
            components.append(component)
    if not components:
        raise ValueError("Empty glyph cell")
    minimum_area = max(map(len, components)) * minimum_component_ratio
    keep = {point for component in components if len(component) >= minimum_area for point in component}
    source = image.load()
    for y in range(height):
        for x in range(width):
            if (x, y) not in keep:
                red, green, blue, _ = source[x, y]
                source[x, y] = (red, green, blue, 0)
    return image


def row_bounds(alpha, y0, y1, threshold):
    band = alpha.crop((0, y0, alpha.width, y1))
    bbox = band.point(lambda value: 255 if value > threshold else 0).getbbox()
    if bbox is None:
        raise ValueError(f"Empty row {y0}:{y1}")
    return y0 + bbox[1], y0 + bbox[3]


def normalize_glyph(image, cell_height, content_height, side_padding, threshold):
    alpha = image.getchannel("A")
    bbox = alpha.point(lambda value: 255 if value > threshold else 0).getbbox()
    if bbox is None:
        raise ValueError("Empty glyph after cleanup")
    glyph = image.crop(bbox)
    scale = content_height / glyph.height
    content_width = max(1, round(glyph.width * scale))
    glyph = glyph.resize((content_width, content_height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (content_width + side_padding * 2, cell_height), (0, 0, 0, 0))
    canvas.alpha_composite(glyph, (side_padding, (cell_height - content_height) // 2))
    return canvas


def split_sheet(path, rows, output_root, args, metrics):
    image = Image.open(path).convert("RGBA")
    validate_source_alpha(image, path, args.alpha_threshold)
    alpha = image.getchannel("A")
    row_boundaries = find_row_boundaries(alpha, len(rows), args.alpha_threshold)
    for row_index, letters in enumerate(rows):
        y0, y1 = row_boundaries[row_index], row_boundaries[row_index + 1]
        content_y0, content_y1 = row_bounds(alpha, y0, y1, args.alpha_threshold)
        nominal_width = image.width / len(letters)
        radius = max(8, round(nominal_width * 0.18))
        boundaries = [0]
        for column in range(1, len(letters)):
            ideal = round(column * nominal_width)
            boundaries.append(find_boundary(alpha, content_y0, content_y1, ideal, radius, args.alpha_threshold))
        boundaries.append(image.width)
        for column, letter in enumerate(letters):
            crop = image.crop((boundaries[column], content_y0, boundaries[column + 1], content_y1))
            crop = clean_components(crop, args.alpha_threshold, args.minimum_component_ratio)
            canvas = normalize_glyph(crop, args.cell_height, args.content_height, args.side_padding, args.alpha_threshold)
            target = output_root / f"u{ord(letter):04x}.png"
            target.parent.mkdir(parents=True, exist_ok=True)
            canvas.save(target, optimize=True)
            metrics[letter] = round(canvas.width / canvas.height, 4)


def load_rendered_glyph(path, height):
    image = Image.open(path).convert("RGBA")
    width = max(1, round(image.width * height / image.height))
    return image.resize((width, height), Image.Resampling.LANCZOS)


def pair_margin(left, right, threshold, contact_overlap):
    left_alpha = left.getchannel("A")
    right_alpha = right.getchannel("A")
    left_pixels = left_alpha.load()
    right_pixels = right_alpha.load()
    required_start = 0
    shared_row = False
    for y in range(min(left.height, right.height)):
        left_x = [x for x in range(left.width) if left_pixels[x, y] > threshold]
        right_x = [x for x in range(right.width) if right_pixels[x, y] > threshold]
        if not left_x or not right_x:
            continue
        shared_row = True
        required_start = max(required_start, max(left_x) - min(right_x) + 1)
    if not shared_row:
        return 0
    return required_start - left.width - contact_overlap


def build_kerning(name, rows, output_root, args):
    letters = "".join(rows)
    glyphs = {letter: load_rendered_glyph(output_root / f"u{ord(letter):04x}.png", args.kerning_height) for letter in letters}
    values = []
    for left in letters:
        for right in letters:
            margin = pair_margin(glyphs[left], glyphs[right], args.kerning_alpha_threshold, args.contact_overlap)
            if not -128 <= margin <= 127:
                raise ValueError(f"Kerning out of signed-byte range: {left}{right}={margin}")
            values.append(margin)
    encoded = base64.b64encode(bytes(value & 0xFF for value in values)).decode("ascii")
    return {
        "name": name,
        "letters": letters,
        "size": len(letters),
        "renderHeight": args.kerning_height,
        "contactOverlap": args.contact_overlap,
        "minimum": min(values),
        "maximum": max(values),
        "rows": [values[index:index + len(letters)] for index in range(0, len(values), len(letters))],
        "signedByteBase64": encoded,
    }


def build_contact_sheet(rows, source_root, target):
    cell_width, cell_height = 112, 144
    preview = Image.new("RGB", (max(map(len, rows)) * cell_width, len(rows) * cell_height), (74, 86, 96))
    for row_index, letters in enumerate(rows):
        for column, letter in enumerate(letters):
            glyph = Image.open(source_root / f"u{ord(letter):04x}.png").convert("RGBA")
            glyph.thumbnail((96, 128), Image.Resampling.LANCZOS)
            x = column * cell_width + (cell_width - glyph.width) // 2
            y = row_index * cell_height + (136 - glyph.height) // 2
            preview.paste(glyph, (x, y), glyph)
    preview.save(target, optimize=True)


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--latin-sheet", type=Path, required=True)
    parser.add_argument("--cyrillic-sheet", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cell-height", type=int, default=256)
    parser.add_argument("--content-height", type=int, default=232)
    parser.add_argument("--side-padding", type=int, default=8)
    parser.add_argument("--alpha-threshold", type=int, default=8)
    parser.add_argument("--minimum-component-ratio", type=float, default=0.025)
    parser.add_argument("--kerning-height", type=int, default=100)
    parser.add_argument("--kerning-alpha-threshold", type=int, default=32)
    parser.add_argument("--contact-overlap", type=int, default=0)
    return parser.parse_args()


def main():
    args = parse_args()
    if args.content_height > args.cell_height:
        raise ValueError("content-height cannot exceed cell-height")
    args.output.mkdir(parents=True, exist_ok=True)
    metrics = {}
    sources = {"latin": args.latin_sheet, "cyrillic": args.cyrillic_sheet}
    kerning = {}
    for name, rows in ALPHABETS.items():
        output_root = args.output / name
        split_sheet(sources[name], rows, output_root, args, metrics)
        build_contact_sheet(rows, output_root, args.output / f"alphabet_{name}_preview.png")
        kerning[name] = build_kerning(name, rows, output_root, args)
    manifest = {
        "alphabets": {name: len("".join(rows)) for name, rows in ALPHABETS.items()},
        "glyphMetrics": metrics,
        "kerning": kerning,
        "settings": {
            "cellHeight": args.cell_height,
            "contentHeight": args.content_height,
            "sidePadding": args.side_padding,
            "alphaThreshold": args.alpha_threshold,
        },
    }
    (args.output / "illustrated_alphabet.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"success": True, "output": str(args.output), "latin": 26, "cyrillic": 33}, ensure_ascii=False))


if __name__ == "__main__":
    main()
