#!/usr/bin/env python3
"""Render localized text from an illustrated alphabet manifest."""

import argparse
import base64
import json
from pathlib import Path

from PIL import Image


def decode_kerning(data):
    raw = base64.b64decode(data["signedByteBase64"])
    size = data["size"]
    if len(raw) != size * size:
        raise ValueError("Kerning matrix size mismatch")
    values = [value if value < 128 else value - 256 for value in raw]
    return data["letters"], size, values


def glyph_path(root, letter):
    if "A" <= letter <= "Z":
        alphabet = "latin"
    elif letter == "Ё" or "А" <= letter <= "Я":
        alphabet = "cyrillic"
    else:
        raise ValueError(f"Unsupported character: {letter!r}")
    return root / alphabet / f"u{ord(letter):04x}.png"


def margin_for(left, right, tables):
    for letters, size, values in tables.values():
        left_index = letters.find(left)
        right_index = letters.find(right)
        if left_index >= 0 and right_index >= 0:
            return values[left_index * size + right_index]
    return 0


def render(args):
    manifest = json.loads((args.alphabet_root / "illustrated_alphabet.json").read_text(encoding="utf-8"))
    tables = {name: decode_kerning(value) for name, value in manifest["kerning"].items()}
    height = next(iter(manifest["kerning"].values()))["renderHeight"]
    pieces = []
    total = 0
    previous = None
    for letter in args.text.upper():
        if letter.isspace():
            pieces.append((None, args.space_width, 0))
            total += args.space_width
            previous = None
            continue
        glyph = Image.open(glyph_path(args.alphabet_root, letter)).convert("RGBA")
        width = max(1, round(glyph.width * height / glyph.height))
        glyph = glyph.resize((width, height), Image.Resampling.LANCZOS)
        left_margin = margin_for(previous, letter, tables) if previous else 0
        pieces.append((glyph, width, left_margin))
        total += width + left_margin + args.tracking
        previous = letter
    word = Image.new("RGBA", (max(1, total + 32), height + 32), (0, 0, 0, 0))
    x = 16
    for glyph, width, left_margin in pieces:
        x += left_margin
        if glyph is not None:
            word.alpha_composite(glyph, (x, 16))
        x += width + args.tracking
    bbox = word.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Rendered word is empty")
    word = word.crop(bbox)
    if args.scale != 1:
        word = word.resize(
            (max(1, round(word.width * args.scale)), max(1, round(word.height * args.scale))),
            Image.Resampling.LANCZOS,
        )
    if args.angle:
        word = word.rotate(args.angle, Image.Resampling.BICUBIC, expand=True)
    if args.background:
        background = Image.open(args.background).convert("RGBA")
        if args.size:
            width, height = map(int, args.size.lower().split("x", 1))
            background = background.resize((width, height), Image.Resampling.LANCZOS)
        background.alpha_composite(word, ((background.width - word.width) // 2, (background.height - word.height) // 2))
        result = background
    else:
        result = word
    args.output.parent.mkdir(parents=True, exist_ok=True)
    result.save(args.output, optimize=True)
    print(json.dumps({"success": True, "output": str(args.output), "size": result.size}, ensure_ascii=False))


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--alphabet-root", type=Path, required=True)
    parser.add_argument("--text", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--scale", type=float, default=1.0)
    parser.add_argument("--tracking", type=int, default=0, help="Extra advance after every glyph, in kerning-height pixels")
    parser.add_argument("--space-width", type=int, default=38)
    parser.add_argument("--angle", type=float, default=0)
    parser.add_argument("--background", type=Path)
    parser.add_argument("--size", help="Optional resized background WIDTHxHEIGHT")
    return parser.parse_args()


if __name__ == "__main__":
    render(parse_args())
