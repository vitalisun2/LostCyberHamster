from __future__ import annotations

import argparse
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


SOURCE_BOX = (1150, 350, 1220, 425)


def clean_transparent_pixels(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8).copy()
    rgba[rgba[:, :, 3] == 0, :3] = 0
    return Image.fromarray(rgba, mode="RGBA")


def extract_right_arrow(source: Image.Image) -> Image.Image:
    crop = source.crop(SOURCE_BOX).convert("RGB")
    rgb = np.asarray(crop, dtype=np.uint8)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)

    orange = (
        (hsv[:, :, 0] >= 3)
        & (hsv[:, :, 0] <= 35)
        & (hsv[:, :, 1] >= 105)
        & (hsv[:, :, 2] >= 105)
    ).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(orange, 8)
    if count <= 1:
        raise RuntimeError("Arrow body not found in source image")

    body_label = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    body = (labels == body_label).astype(np.uint8)
    body_stats = stats[body_label]
    body_left = int(body_stats[cv2.CC_STAT_LEFT])
    body_top = int(body_stats[cv2.CC_STAT_TOP])
    body_width = int(body_stats[cv2.CC_STAT_WIDTH])
    body_height = int(body_stats[cv2.CC_STAT_HEIGHT])

    support = cv2.dilate(
        body,
        cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9)),
    ).astype(bool)
    yy, xx = np.indices(body.shape)
    # Horizontal guide lines touch the button's back edge in the composite.
    # Keep the rounded black border, but exclude those guide pixels.
    support &= xx >= body_left
    support &= xx <= body_left + body_width + 1
    support &= yy >= body_top - 4
    support &= yy <= body_top + body_height + 4

    dark_or_colored = (
        (hsv[:, :, 1] >= 45)
        | (np.min(rgb, axis=2) <= 115)
    ).astype(np.uint8)
    background_mask = cv2.dilate(
        dark_or_colored,
        cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)),
    )
    background = cv2.inpaint(rgb, background_mask, 5, cv2.INPAINT_TELEA)

    source_rgb = rgb.astype(np.float32)
    background_rgb = background.astype(np.float32)
    darker = np.max(
        np.maximum(background_rgb - source_rgb, 0.0)
        / np.maximum(background_rgb, 1.0),
        axis=2,
    )
    brighter = np.max(
        np.maximum(source_rgb - background_rgb, 0.0)
        / np.maximum(255.0 - background_rgb, 1.0),
        axis=2,
    )
    alpha = np.maximum(darker, brighter)
    alpha = np.clip((alpha - 0.025) / 0.90, 0.0, 1.0)
    alpha *= support
    alpha[alpha < 0.035] = 0.0

    foreground = np.zeros_like(source_rgb)
    visible = alpha > 0.0
    foreground[visible] = (
        source_rgb[visible]
        - (1.0 - alpha[visible, None]) * background_rgb[visible]
    ) / alpha[visible, None]
    foreground = np.clip(foreground, 0.0, 255.0)
    neutral_edge = (
        (alpha < 0.30)
        & ((np.max(foreground, axis=2) - np.min(foreground, axis=2)) < 50.0)
    )
    foreground[neutral_edge] *= 0.15

    alpha_u8 = np.rint(alpha * 255.0).astype(np.uint8)
    ys, xs = np.nonzero(alpha_u8)
    if len(xs) == 0:
        raise RuntimeError("Arrow alpha mask is empty")

    padding = 2
    left = max(0, int(xs.min()) - padding)
    top = max(0, int(ys.min()) - padding)
    right = min(rgb.shape[1], int(xs.max()) + padding + 1)
    bottom = min(rgb.shape[0], int(ys.max()) + padding + 1)
    rgba = np.dstack((foreground.astype(np.uint8), alpha_u8))
    return clean_transparent_pixels(
        Image.fromarray(rgba[top:bottom, left:right], mode="RGBA")
    )


def save_arrow(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    clean_transparent_pixels(image).save(path, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Extract transparent Hero scroll arrows from Select Levels art."
    )
    parser.add_argument("source", type=Path)
    parser.add_argument("select_output", type=Path)
    parser.add_argument("hero_output", type=Path)
    parser.add_argument("unity_output", type=Path)
    args = parser.parse_args()

    source = Image.open(args.source)
    right = extract_right_arrow(source)
    arrows = {
        "button_scroll_up.png": right.transpose(Image.Transpose.ROTATE_90),
        "button_scroll_down.png": right.transpose(Image.Transpose.ROTATE_270),
    }

    for filename, arrow in arrows.items():
        select_path = args.select_output / filename
        save_arrow(arrow, select_path)
        for output_directory in (args.hero_output, args.unity_output):
            output_directory.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(select_path, output_directory / filename)


if __name__ == "__main__":
    main()
