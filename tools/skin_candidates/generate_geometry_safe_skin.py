"""Generate geometry-safe Unity skateboard skin sheets from source PNG files.

The tool recolors only RGB values inside existing sprite pixels. Canvas size,
cell grid, frame order, transparent pixels, and every alpha byte stay equal to
the source sheet. The built-in ``cyberpunk-pulse`` profile keeps default pants,
forearms, hands, and face while styling jacket, collar, cyber eye, and board.

Example:
    python tools/skin_candidates/generate_geometry_safe_skin.py \
        --source-root path/to/skateboard_mode/default \
        --output-root path/to/candidate/skateboard_mode \
        --sheets Run_1.png Jump.png
"""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path
import sys
from typing import Callable, Sequence

import numpy as np
from PIL import Image


DEFAULT_CELL_WIDTH = 240
DEFAULT_CELL_HEIGHT = 650
DEFAULT_SHEETS = (
    "Run_1.png",
    "Run_2.png",
    "Run_3.png",
    "Jump.png",
    "Double_Jump.png",
)

CYAN = np.array((0, 236, 247), dtype=np.uint8)
MAGENTA = np.array((248, 35, 205), dtype=np.uint8)
GRAPHITE = np.array((43, 45, 53), dtype=np.float32)
BOARD_DARK = np.array((29, 32, 39), dtype=np.float32)

FrameStats = dict[str, int]
Profile = Callable[[np.ndarray], FrameStats]


def connected_components(mask: np.ndarray) -> list[np.ndarray]:
    """Return 4-connected component coordinates, largest component first."""
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    components: list[np.ndarray] = []
    for y, x in zip(*np.where(mask)):
        if seen[y, x]:
            continue
        queue = deque(((int(y), int(x)),))
        seen[y, x] = True
        coordinates: list[tuple[int, int]] = []
        while queue:
            current_y, current_x = queue.popleft()
            coordinates.append((current_y, current_x))
            for delta_y, delta_x in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                next_y = current_y + delta_y
                next_x = current_x + delta_x
                if (
                    0 <= next_y < height
                    and 0 <= next_x < width
                    and mask[next_y, next_x]
                    and not seen[next_y, next_x]
                ):
                    seen[next_y, next_x] = True
                    queue.append((next_y, next_x))
        components.append(np.asarray(coordinates, dtype=np.int32))
    components.sort(key=len, reverse=True)
    return components


def component_mask(shape: tuple[int, int], coordinates: np.ndarray) -> np.ndarray:
    """Build a boolean mask from ``(y, x)`` component coordinates."""
    result = np.zeros(shape, dtype=bool)
    if len(coordinates):
        result[coordinates[:, 0], coordinates[:, 1]] = True
    return result


def grow_from_seeds(seeds: np.ndarray, candidates: np.ndarray) -> np.ndarray:
    """Flood candidate pixels from seeds without crossing source outlines."""
    height, width = seeds.shape
    result = np.zeros_like(seeds, dtype=bool)
    queue: deque[tuple[int, int]] = deque()
    for y, x in zip(*np.where(seeds & candidates)):
        result[y, x] = True
        queue.append((int(y), int(x)))
    while queue:
        current_y, current_x = queue.popleft()
        for delta_y, delta_x in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            next_y = current_y + delta_y
            next_x = current_x + delta_x
            if (
                0 <= next_y < height
                and 0 <= next_x < width
                and candidates[next_y, next_x]
                and not result[next_y, next_x]
            ):
                result[next_y, next_x] = True
                queue.append((next_y, next_x))
    return result


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    """Dilate a boolean mask with a four-neighbour kernel."""
    result = mask.copy()
    for _ in range(radius):
        padded = np.pad(result, 1, constant_values=False)
        result = (
            padded[1:-1, 1:-1]
            | padded[:-2, 1:-1]
            | padded[2:, 1:-1]
            | padded[1:-1, :-2]
            | padded[1:-1, 2:]
        )
    return result


def erode(mask: np.ndarray, radius: int) -> np.ndarray:
    """Erode a boolean mask with a four-neighbour kernel."""
    result = mask.copy()
    for _ in range(radius):
        padded = np.pad(result, 1, constant_values=False)
        result = (
            padded[1:-1, 1:-1]
            & padded[:-2, 1:-1]
            & padded[2:, 1:-1]
            & padded[1:-1, :-2]
            & padded[1:-1, 2:]
        )
    return result


def color_distance(rgb: np.ndarray, color: tuple[int, int, int]) -> np.ndarray:
    """Return per-pixel Euclidean RGB distance from ``color``."""
    delta = rgb.astype(np.int16) - np.asarray(color, dtype=np.int16)
    return np.sqrt(np.sum(delta.astype(np.float32) ** 2, axis=2))


def recolor_shaded(
    frame: np.ndarray,
    mask: np.ndarray,
    base: np.ndarray,
    source_luma: float,
) -> None:
    """Apply a base color while retaining source luminance variation."""
    luminance = frame[:, :, :3].astype(np.float32).mean(axis=2)
    scale = np.clip((luminance / source_luma) ** 0.72, 0.58, 1.27)
    shaded = np.clip(base[None, None, :] * scale[:, :, None], 0, 255).astype(
        np.uint8
    )
    frame[:, :, :3][mask] = shaded[mask]


def recolor_cyberpunk_pulse(frame: np.ndarray) -> FrameStats:
    """Apply Cyberpunk Pulse without changing face, pants, forearms, or hands."""
    frame_height, frame_width = frame.shape[:2]
    rgb = frame[:, :, :3]
    alpha = frame[:, :, 3]
    opaque = alpha > 0
    channel_spread = (
        rgb.max(axis=2).astype(np.int16) - rgb.min(axis=2).astype(np.int16)
    )
    gray = rgb.astype(np.float32).mean(axis=2)

    # Source forearms and hands use a separate 154-gray material and stay default.
    jacket_seeds = opaque & (channel_spread <= 3) & (gray >= 76) & (gray <= 94)
    jacket_candidates = opaque & (channel_spread <= 5) & (gray >= 54) & (gray <= 111)
    jacket = grow_from_seeds(jacket_seeds, jacket_candidates)
    recolor_shaded(frame, jacket, GRAPHITE, 85.0)

    # Default blue hood/collar becomes split cyan and magenta trim.
    hood = opaque & (color_distance(rgb, (74, 97, 121)) < 42)

    # Largest white component is shirt. Small white components stay untouched.
    white = opaque & np.all(rgb >= 235, axis=2)
    white_components = connected_components(white)
    shirt = (
        component_mask(white.shape, white_components[0])
        if white_components
        else np.zeros_like(white)
    )
    shirt_coordinates = np.argwhere(shirt)
    if len(shirt_coordinates):
        x_center = float(
            (shirt_coordinates[:, 1].min() + shirt_coordinates[:, 1].max()) / 2.0
        )
    else:
        jacket_coordinates = np.argwhere(jacket)
        x_center = (
            float(jacket_coordinates[:, 1].mean())
            if len(jacket_coordinates)
            else frame_width / 2.0
        )

    x_grid = np.broadcast_to(np.arange(frame_width)[None, :], opaque.shape)
    frame[:, :, :3][hood & (x_grid <= x_center)] = CYAN
    frame[:, :, :3][hood & (x_grid > x_center)] = MAGENTA

    piping = np.zeros_like(jacket)
    if len(shirt_coordinates):
        piping = jacket & dilate(shirt, 5)
        frame[:, :, :3][piping & (x_grid <= x_center)] = CYAN
        frame[:, :, :3][piping & (x_grid > x_center)] = MAGENTA

        # Compact chest marks stay inside source jacket pixels.
        y_min, x_min = shirt_coordinates.min(axis=0)
        y_max, x_max = shirt_coordinates.max(axis=0) + 1
        if len(shirt_coordinates) >= 300 and (y_max - y_min) >= 18:
            bar_y = int(y_min + max(5, (y_max - y_min) * 0.20))
            bar_x_max = int(x_min - 4)
            bar_x_min = int(max(0, bar_x_max - max(9, (x_max - x_min) * 0.16)))
            bar = np.zeros_like(jacket)
            bar[
                max(0, bar_y - 1) : min(frame_height, bar_y + 2),
                bar_x_min : max(0, bar_x_max),
            ] = True
            frame[:, :, :3][bar & jacket] = CYAN

            node_x = int(min(frame_width - 1, x_min + 3))
            node_y = int(max(0, y_min - 4))
            y_grid, node_x_grid = np.ogrid[:frame_height, :frame_width]
            node = jacket & (
                ((node_x_grid - node_x) ** 2 + (y_grid - node_y) ** 2) <= 9
            )
            frame[:, :, :3][node] = MAGENTA

    # Source board gray differs from hand gray. Flooding retains board shading.
    board_seed_candidates = (
        opaque & (channel_spread <= 4) & (gray >= 116) & (gray <= 133)
    )
    board_seeds = np.zeros_like(board_seed_candidates)
    for coordinates in connected_components(board_seed_candidates):
        if len(coordinates) >= 28:
            board_seeds[coordinates[:, 0], coordinates[:, 1]] = True
    board_candidates = (
        opaque & (channel_spread <= 7) & (gray >= 99) & (gray <= 147)
    )
    board = grow_from_seeds(board_seeds, board_candidates)
    recolor_shaded(frame, board, BOARD_DARK, 124.0)
    board_edge = board & ~erode(board, 2)
    frame[:, :, :3][board_edge] = CYAN

    # Source reds identify cyber eye and wheels. No face stripe is introduced.
    eye = opaque & (color_distance(rgb, (246, 79, 78)) < 45)
    wheels = opaque & (color_distance(rgb, (255, 115, 123)) < 48)
    frame[:, :, :3][eye] = CYAN
    frame[:, :, :3][wheels] = MAGENTA

    return {
        "jacket_pixels": int(jacket.sum()),
        "piping_pixels": int(piping.sum()),
        "board_pixels": int(board.sum()),
        "eye_pixels": int(eye.sum()),
        "wheel_pixels": int(wheels.sum()),
    }


PROFILES: dict[str, Profile] = {
    "cyberpunk-pulse": recolor_cyberpunk_pulse,
}


def process_sheet(
    source: Path,
    output: Path,
    profile: Profile,
    cell_width: int,
    cell_height: int,
) -> list[FrameStats]:
    """Process one sheet and verify exact canvas and alpha preservation."""
    if not source.is_file():
        raise FileNotFoundError(f"Source sheet not found: {source}")
    if source.resolve() == output.resolve():
        raise ValueError(f"Source and output paths match: {source}")

    with Image.open(source) as opened_image:
        source_image = opened_image.convert("RGBA")
    source_pixels = np.asarray(source_image, dtype=np.uint8)
    source_alpha = source_pixels[:, :, 3].copy()
    result = source_pixels.copy()
    width, height = source_image.size
    if width % cell_width or height % cell_height:
        raise ValueError(
            f"Sheet grid is not divisible for {source.name}: "
            f"canvas={width}x{height}, cell={cell_width}x{cell_height}"
        )

    frame_stats: list[FrameStats] = []
    columns = width // cell_width
    rows = height // cell_height
    for row in range(rows):
        for column in range(columns):
            y_min = row * cell_height
            y_max = y_min + cell_height
            x_min = column * cell_width
            x_max = x_min + cell_width
            frame = result[y_min:y_max, x_min:x_max]
            if not np.any(frame[:, :, 3]):
                frame_stats.append({"empty": 1})
                continue
            frame_stats.append(profile(frame))

    if not np.array_equal(result[:, :, 3], source_alpha):
        raise RuntimeError(f"Alpha changed before saving {source.name}")

    output.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(result, "RGBA").save(output, format="PNG", optimize=False)
    with Image.open(output) as saved_image:
        saved_rgba = saved_image.convert("RGBA")
    saved_pixels = np.asarray(saved_rgba, dtype=np.uint8)
    if saved_rgba.size != source_image.size:
        raise RuntimeError(f"Canvas changed after saving {source.name}")
    if not np.array_equal(saved_pixels[:, :, 3], source_alpha):
        raise RuntimeError(f"Alpha changed after saving {source.name}")
    return frame_stats


def positive_integer(value: str) -> int:
    """Parse a positive CLI integer."""
    parsed = int(value)
    if parsed <= 0:
        raise argparse.ArgumentTypeError("value must be greater than zero")
    return parsed


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument(
        "--sheets",
        nargs="+",
        default=list(DEFAULT_SHEETS),
        metavar="PNG",
        help="Sheet filenames. Defaults to all skateboard sheets.",
    )
    parser.add_argument(
        "--cell-width", type=positive_integer, default=DEFAULT_CELL_WIDTH
    )
    parser.add_argument(
        "--cell-height", type=positive_integer, default=DEFAULT_CELL_HEIGHT
    )
    parser.add_argument(
        "--profile",
        choices=tuple(PROFILES),
        default="cyberpunk-pulse",
    )
    return parser.parse_args(argv)


def run(args: argparse.Namespace) -> None:
    """Validate paths and process requested sheet filenames."""
    source_root = args.source_root.resolve()
    output_root = args.output_root.resolve()
    if not source_root.is_dir():
        raise NotADirectoryError(f"Source root not found: {source_root}")

    profile = PROFILES[args.profile]
    for sheet_name in args.sheets:
        sheet_path = Path(sheet_name)
        if sheet_path.name != sheet_name or sheet_path.suffix.lower() != ".png":
            raise ValueError(f"Sheet must be a PNG filename without directories: {sheet_name}")
        source = source_root / sheet_name
        output = output_root / sheet_name
        stats = process_sheet(
            source,
            output,
            profile,
            args.cell_width,
            args.cell_height,
        )
        empty_count = sum(frame.get("empty", 0) for frame in stats)
        print(
            f"{sheet_name}: frames={len(stats)}, empty={empty_count}, "
            f"profile={args.profile}"
        )


def main(argv: Sequence[str] | None = None) -> int:
    """CLI entry point. Return nonzero on processing errors."""
    args = parse_args(argv)
    try:
        run(args)
    except (OSError, ValueError, RuntimeError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
