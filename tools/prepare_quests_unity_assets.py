from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter


CANVAS_SIZE = (1672, 940)
CARD_CROP = (660, 398, 988, 808)
PROGRESS_CROP = (369, 641, 620, 676)
TABS_CROP = (505, 292, 1167, 403)
BUTTON_SOURCE_BOX = (488, 704, 621, 767)
COIN_SOURCE_BOX = (366, 703, 428, 767)
BUTTON_FINAL_BOX = (368, 704, 618, 765)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def antialiased_mask(
    size: tuple[int, int],
    draw_shapes,
    scale: int = 4,
) -> Image.Image:
    mask = Image.new("L", (size[0] * scale, size[1] * scale), 0)
    draw_shapes(ImageDraw.Draw(mask), scale)
    return mask.resize(size, Image.Resampling.LANCZOS)


def clean_transparent_pixels(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8).copy()
    rgba[rgba[:, :, 3] == 0, :3] = 0
    return Image.fromarray(rgba, mode="RGBA")


def normalized_blur(
    rgb: np.ndarray,
    valid: np.ndarray,
    sigma: float,
) -> np.ndarray:
    weights = cv2.GaussianBlur(valid.astype(np.float32), (0, 0), sigma)
    channels = []
    for index in range(3):
        weighted = cv2.GaussianBlur(
            rgb[:, :, index].astype(np.float32) * valid,
            (0, 0),
            sigma,
        )
        channels.append(weighted / np.maximum(weights, 1e-4))
    return np.stack(channels, axis=2)


def make_card(daily: Image.Image) -> Image.Image:
    card = daily.crop(CARD_CROP).convert("RGB")
    rgb = np.asarray(card, dtype=np.uint8)
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    valid = (
        (rgb[:, :, 0] > 135)
        & (rgb[:, :, 1] > 105)
        & (rgb[:, :, 2] > 65)
        & (hsv[:, :, 0] < 36)
        & (hsv[:, :, 1] < 120)
        & (rgb[:, :, 0].astype(np.int16) - rgb[:, :, 2] > 28)
    ).astype(np.float32)
    valid[:18, :] = 0
    valid[-18:, :] = 0
    valid[:, :18] = 0
    valid[:, -18:] = 0
    surface = normalized_blur(rgb, valid, sigma=26)

    sample = rgb[46:106, 35:95].astype(np.float32)
    sample_smooth = cv2.GaussianBlur(sample, (0, 0), 3.0)
    texture = sample - sample_smooth
    texture = np.tile(texture, (7, 6, 1))[: card.height, : card.width]
    surface = np.clip(surface + texture * 0.55, 0, 255)

    blend = np.zeros((card.height, card.width), dtype=np.uint8)
    cv2.rectangle(blend, (24, 20), (304, 385), 255, thickness=-1)
    blend = cv2.GaussianBlur(blend, (0, 0), 7.0).astype(np.float32) / 255.0
    cleaned = rgb.astype(np.float32) * (1 - blend[:, :, None]) + surface * blend[:, :, None]
    card = Image.fromarray(np.clip(cleaned, 0, 255).astype(np.uint8), mode="RGB")

    def draw_card(draw: ImageDraw.ImageDraw, scale: int) -> None:
        draw.rounded_rectangle(
            (8 * scale, 8 * scale, 320 * scale, 400 * scale),
            radius=34 * scale,
            fill=255,
        )

    rgba = card.convert("RGBA")
    rgba.putalpha(antialiased_mask(card.size, draw_card))
    return clean_transparent_pixels(rgba)


def make_progress_track(daily: Image.Image) -> Image.Image:
    track = daily.crop(PROGRESS_CROP).convert("RGB")
    rgb = np.asarray(track, dtype=np.uint8).copy()
    hsv = np.asarray(track.convert("HSV"), dtype=np.uint8)
    for y in range(6, track.height - 6):
        dark = (hsv[y, :, 2] > 25) & (hsv[y, :, 2] < 105) & (hsv[y, :, 1] < 95)
        dark[:24] = False
        dark[-24:] = False
        samples = rgb[y, dark]
        if len(samples):
            rgb[y, 22 : track.width - 22] = np.median(samples, axis=0)
    track = Image.fromarray(rgb, mode="RGB")

    def draw_track(draw: ImageDraw.ImageDraw, scale: int) -> None:
        draw.rounded_rectangle(
            (0, 0, (track.width - 1) * scale, (track.height - 1) * scale),
            radius=17 * scale,
            fill=255,
        )

    rgba = track.convert("RGBA")
    rgba.putalpha(antialiased_mask(track.size, draw_track))
    return clean_transparent_pixels(rgba)


def make_tabs(source: Image.Image) -> Image.Image:
    tabs = source.crop(TABS_CROP).convert("RGBA")

    def draw_tabs(draw: ImageDraw.ImageDraw, scale: int) -> None:
        draw.rounded_rectangle(
            (13 * scale, 15 * scale, 312 * scale, 101 * scale),
            radius=24 * scale,
            fill=255,
        )
        draw.rounded_rectangle(
            (344 * scale, 15 * scale, 644 * scale, 101 * scale),
            radius=24 * scale,
            fill=255,
        )
        draw.rectangle((0, 93 * scale, 661 * scale, 102 * scale), fill=255)

    tabs.putalpha(antialiased_mask(tabs.size, draw_tabs))
    return clean_transparent_pixels(tabs)


def make_blank_button(daily: Image.Image) -> Image.Image:
    source = daily.crop(BUTTON_FINAL_BOX).convert("RGB")
    arr = np.asarray(source, dtype=np.float32).copy()
    hsv = np.asarray(source.convert("HSV"), dtype=np.uint8)
    for y in range(8, source.height - 8):
        green = (
            (hsv[y, :, 0] > 43)
            & (hsv[y, :, 0] < 104)
            & (hsv[y, :, 1] > 88)
            & (hsv[y, :, 2] > 45)
        )
        green[:12] = False
        green[-12:] = False
        samples = arr[y, green]
        color = (
            np.median(samples, axis=0)
            if len(samples)
            else np.array([50, 166, 21], dtype=np.float32)
        )
        arr[y, 12 : source.width - 12] = color

    result = Image.fromarray(arr.astype(np.uint8), mode="RGB")
    width, height = result.size

    def draw_button(draw: ImageDraw.ImageDraw, scale: int) -> None:
        draw.rounded_rectangle(
            (0, 0, (width - 1) * scale, (height - 1) * scale),
            radius=17 * scale,
            fill=255,
        )

    rgba = result.convert("RGBA")
    rgba.putalpha(antialiased_mask((width, height), draw_button))
    return clean_transparent_pixels(rgba)


def make_coin(daily: Image.Image) -> Image.Image:
    button = daily.crop(BUTTON_FINAL_BOX).convert("RGB")
    crop = np.asarray(button, dtype=np.uint8)
    hsv = cv2.cvtColor(crop, cv2.COLOR_RGB2HSV)
    color = cv2.inRange(hsv, np.array((5, 100, 70)), np.array((38, 255, 255)))
    labels_count, labels, stats, _ = cv2.connectedComponentsWithStats(color)
    candidates = []
    for label in range(1, labels_count):
        x, y, width, height, area = stats[label]
        if x > 170 and area > 40:
            candidates.append((area, label))
    if not candidates:
        raise RuntimeError("Coin component was not found")
    label = max(candidates)[1]
    core = (labels == label).astype(np.uint8) * 255
    near_core = cv2.dilate(core, np.ones((11, 11), np.uint8), iterations=1) > 0
    luminance = crop.mean(axis=2)
    yellow_or_outline = (
        ((hsv[:, :, 0] >= 5) & (hsv[:, :, 0] <= 38) & (hsv[:, :, 1] > 55))
        | (luminance < 100)
    )
    mask = (near_core & yellow_or_outline).astype(np.uint8) * 255
    mask = cv2.dilate(mask, np.ones((3, 3), np.uint8), iterations=1)
    mask = cv2.GaussianBlur(mask, (3, 3), 0.45)
    points = cv2.findNonZero((mask > 8).astype(np.uint8))
    x, y, width, height = cv2.boundingRect(points)
    rgba = np.dstack((crop, mask))[y : y + height, x : x + width]
    ellipse = antialiased_mask(
        (width, height),
        lambda draw, scale: draw.ellipse(
            (2 * scale, 2 * scale, (width - 3) * scale, (height - 3) * scale),
            fill=255,
        ),
    )
    rgba[:, :, 3] = np.minimum(rgba[:, :, 3], np.asarray(ellipse, dtype=np.uint8))
    return clean_transparent_pixels(Image.fromarray(rgba, mode="RGBA"))


def write_contact_sheet(
    assets: dict[str, Image.Image],
    path: Path,
    background: str,
) -> None:
    colors = {
        "light": (242, 242, 238, 255),
        "dark": (24, 29, 36, 255),
        "checker": (225, 225, 225, 255),
    }
    sheet = Image.new("RGBA", (1400, 650), colors[background])
    if background == "checker":
        draw = ImageDraw.Draw(sheet)
        tile = 24
        for y in range(0, sheet.height, tile):
            for x in range(0, sheet.width, tile):
                if (x // tile + y // tile) % 2:
                    draw.rectangle(
                        (x, y, x + tile - 1, y + tile - 1),
                        fill=(175, 175, 175, 255),
                    )
    placements = {
        "quests_tabs_daily_active.png": (25, 25),
        "quests_tabs_story_active.png": (713, 25),
        "quest_card_frame.png": (40, 185),
        "quest_reward_button.png": (430, 250),
        "quest_reward_coin.png": (525, 345),
        "quest_progress_track.png": (430, 430),
    }
    for name, position in placements.items():
        sheet.alpha_composite(assets[name], position)
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(path, quality=95)


def resize_9slice(
    image: Image.Image,
    size: tuple[int, int],
    border: tuple[int, int, int, int],
) -> Image.Image:
    left, bottom, right, top = border
    source = image.convert("RGBA")
    output = Image.new("RGBA", size)
    source_x = (0, left, source.width - right, source.width)
    source_y = (0, top, source.height - bottom, source.height)
    target_x = (0, left, size[0] - right, size[0])
    target_y = (0, top, size[1] - bottom, size[1])
    for row in range(3):
        for column in range(3):
            crop = source.crop(
                (
                    source_x[column],
                    source_y[row],
                    source_x[column + 1],
                    source_y[row + 1],
                )
            )
            target_size = (
                target_x[column + 1] - target_x[column],
                target_y[row + 1] - target_y[row],
            )
            if crop.size != target_size:
                crop = crop.resize(target_size, Image.Resampling.BILINEAR)
            output.alpha_composite(crop, (target_x[column], target_y[row]))
    return output


def write_9slice_sheet(assets: dict[str, Image.Image], path: Path) -> None:
    sheet = Image.new("RGB", (1400, 900), (35, 41, 48))
    samples = (
        ("quest_card_frame.png", (328, 410), (260, 410), (430, 410), (42, 42, 42, 42)),
        ("quest_reward_button.png", (250, 61), (180, 61), (340, 61), (18, 18, 18, 18)),
        ("quest_progress_track.png", (251, 35), (160, 35), (360, 35), (18, 10, 18, 10)),
    )
    y = 20
    for name, original, narrow, wide, border in samples:
        x = 20
        for size in (original, narrow, wide):
            sample = resize_9slice(assets[name], size, border)
            sheet.paste(sample, (x, y), sample)
            x += size[0] + 30
        y += max(original[1], narrow[1], wide[1]) + 40
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path, quality=95)


def validate(assets: dict[str, Image.Image]) -> dict:
    expected = {
        "quest_card_frame.png": (328, 410),
        "quest_progress_track.png": (251, 35),
        "quest_reward_button.png": (250, 61),
        "quests_tabs_daily_active.png": (662, 111),
        "quests_tabs_story_active.png": (662, 111),
    }
    results = {}
    for name, image in assets.items():
        alpha = np.asarray(image.convert("RGBA"))[:, :, 3]
        results[name] = {
            "size": image.size,
            "expected_size": expected.get(name),
            "size_pass": expected.get(name) in (None, image.size),
            "alpha_min": int(alpha.min()),
            "alpha_max": int(alpha.max()),
            "transparent_edges_pass": int(alpha.min()) == 0 and int(alpha.max()) == 255,
        }
    overall = all(item["size_pass"] and item["transparent_edges_pass"] for item in results.values())
    return {"assets": results, "overall_pass": overall}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--qa-output", type=Path, required=True)
    parser.add_argument("--coin-source", type=Path)
    parser.add_argument("--background-source", type=Path)
    args = parser.parse_args()

    daily_path = args.source / "quests_daily_final.png"
    story_path = args.source / "quests_story_final.png"
    daily = Image.open(daily_path).convert("RGB")
    story = Image.open(story_path).convert("RGB")
    if daily.size != CANVAS_SIZE or story.size != CANVAS_SIZE:
        raise RuntimeError("Unexpected Quests canvas size")

    coin = (
        Image.open(args.coin_source).convert("RGBA")
        if args.coin_source
        else make_coin(daily)
    )
    assets = {
        "quest_card_frame.png": make_card(daily),
        "quest_progress_track.png": make_progress_track(daily),
        "quest_reward_button.png": make_blank_button(daily),
        "quest_reward_coin.png": clean_transparent_pixels(coin),
        "quests_tabs_daily_active.png": make_tabs(daily),
        "quests_tabs_story_active.png": make_tabs(story),
    }
    args.output.mkdir(parents=True, exist_ok=True)
    for name, image in assets.items():
        image.save(args.output / name)

    report = validate(assets)
    report["sources"] = {
        "daily": {"path": str(daily_path), "sha256": sha256(daily_path)},
        "story": {"path": str(story_path), "sha256": sha256(story_path)},
    }
    if args.coin_source:
        report["sources"]["coin"] = {
            "path": str(args.coin_source),
            "sha256": sha256(args.coin_source),
        }
    if args.background_source:
        background = Image.open(args.background_source).convert("RGB")
        background.save(args.output / "background_quests.png")
        report["sources"]["background"] = {
            "path": str(args.background_source),
            "sha256": sha256(args.background_source),
            "size": background.size,
        }
    args.qa_output.mkdir(parents=True, exist_ok=True)
    for background in ("light", "dark", "checker"):
        write_contact_sheet(
            assets,
            args.qa_output / f"quests_unity_assets_alpha_{background}.jpg",
            background,
        )
    write_9slice_sheet(assets, args.qa_output / "quests_unity_assets_9slice.jpg")
    (args.qa_output / "quests_unity_assets_qa.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if not report["overall_pass"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
