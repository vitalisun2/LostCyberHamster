"""Safely promote validated Unity sprite sheets without duplicating GUIDs.

Default mode is read-only dry-run. ``--move`` atomically relocates each
top-level PNG together with its Unity-generated ``.meta`` into a new, empty
target root. Copy promotion is intentionally unsupported because copying a
Unity ``.meta`` duplicates its GUID.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import uuid
from pathlib import Path
from typing import Any, Sequence

from validate_skin_candidate import validate_sheet


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def is_same_or_inside(path: Path, root: Path) -> bool:
    return path == root or root in path.parents


def is_filesystem_root(path: Path) -> bool:
    return path == Path(path.anchor)


def unity_texture_meta(path: Path) -> bool:
    if not path.is_file():
        return False
    text = path.read_text(encoding="utf-8")
    return (
        text.startswith("fileFormatVersion:")
        and bool(next((line for line in text.splitlines() if line.startswith("guid: ")), ""))
        and "\nTextureImporter:\n" in text
    )


def discover_top_level(
    source_root: Path,
    candidate_root: Path,
) -> tuple[list[tuple[Path, Path, Path]], list[str]]:
    matched: list[tuple[Path, Path, Path]] = []
    ignored: list[str] = []
    for candidate in sorted(candidate_root.glob("*.png"), key=lambda path: path.name.lower()):
        source = source_root / candidate.name
        if source.is_file():
            matched.append((candidate, candidate.with_suffix(candidate.suffix + ".meta"), source))
        else:
            ignored.append(candidate.name)
    return matched, ignored


def validate_candidates(
    source_root: Path,
    candidate_root: Path,
    cell_width: int,
    cell_height: int,
) -> tuple[list[dict[str, Any]], list[str]]:
    discovered, ignored = discover_top_level(source_root, candidate_root)
    results: list[dict[str, Any]] = []
    for candidate, candidate_meta, source in discovered:
        try:
            result = validate_sheet(source, candidate, cell_width, cell_height)
        except Exception as error:  # Preserve complete dry-run report.
            result = {
                "sheet": candidate.name,
                "status": "FAIL",
                "failures": [f"validation_error: {type(error).__name__}: {error}"],
            }
        if not unity_texture_meta(candidate_meta):
            result["status"] = "FAIL"
            failures = result.setdefault("failures", [])
            failures.append("candidate meta is missing or not a Unity TextureImporter meta")
        results.append(result)
    return results, ignored


def validate_roots(
    source_root: Path,
    candidate_root: Path,
    target_root: Path,
    allow_existing_empty_target: bool,
) -> bool:
    if not source_root.is_dir():
        raise ValueError(f"source root not found: {source_root}")
    if not candidate_root.is_dir():
        raise ValueError(f"candidate root not found: {candidate_root}")
    if not target_root.is_absolute():
        raise ValueError("target root must resolve to an absolute path")
    if is_filesystem_root(target_root):
        raise ValueError(f"filesystem root cannot be a promotion target: {target_root}")
    if target_root == source_root or target_root == candidate_root:
        raise ValueError("target root must differ from source and candidate roots")
    if is_same_or_inside(target_root, source_root):
        raise ValueError("target root cannot be inside source root")
    if is_same_or_inside(target_root, candidate_root):
        raise ValueError("target root cannot be inside candidate root")
    if is_same_or_inside(source_root, target_root) or is_same_or_inside(candidate_root, target_root):
        raise ValueError("target root cannot contain source or candidate roots")
    if not target_root.parent.is_dir():
        raise ValueError(f"target parent must already exist: {target_root.parent}")

    target_preexisting = target_root.exists()
    if target_preexisting and not allow_existing_empty_target:
        raise ValueError("target already exists; use --allow-existing-empty-target only for an empty directory")
    if target_preexisting:
        if not target_root.is_dir():
            raise ValueError(f"target exists and is not a directory: {target_root}")
        if any(target_root.iterdir()):
            raise ValueError("existing target must be empty; existing files are never overwritten")
    return target_preexisting


def preflight_destinations(
    validated: list[dict[str, Any]],
    candidate_root: Path,
    target_root: Path,
) -> list[dict[str, Any]]:
    plan: list[dict[str, Any]] = []
    target_device = target_root.parent.stat().st_dev
    for result in validated:
        name = result["sheet"]
        png = candidate_root / name
        meta = png.with_suffix(png.suffix + ".meta")
        if png.parent != candidate_root or meta.parent != candidate_root:
            raise ValueError(f"only top-level sheets can be promoted: {name}")
        if not png.is_file() or not meta.is_file():
            raise ValueError(f"candidate pair disappeared during preflight: {name}")
        if png.stat().st_dev != target_device or meta.stat().st_dev != target_device:
            raise ValueError("candidate and target must share a filesystem for move-only promotion")
        destination_png = target_root / png.name
        destination_meta = target_root / meta.name
        if destination_png.exists() or destination_meta.exists():
            raise ValueError(f"target collision: {destination_png} or {destination_meta}")
        plan.append(
            {
                "sheet": name,
                "png": png,
                "meta": meta,
                "destination_png": destination_png,
                "destination_meta": destination_meta,
                "png_sha256": file_sha256(png),
                "meta_sha256": file_sha256(meta),
            }
        )
    return plan


def move_pairs_atomically(
    plan: list[dict[str, Any]],
    target_root: Path,
    target_preexisting: bool,
) -> None:
    staging = target_root.parent / f".{target_root.name}.promotion-{uuid.uuid4().hex}"
    if staging.exists():
        raise RuntimeError(f"staging path already exists: {staging}")
    staging.mkdir()
    moved: list[tuple[Path, Path]] = []
    target_removed = False
    try:
        for item in plan:
            for source_key in ("png", "meta"):
                source = item[source_key]
                staged = staging / source.name
                source.replace(staged)
                moved.append((source, staged))

        for item in plan:
            staged_png = staging / item["png"].name
            staged_meta = staging / item["meta"].name
            if file_sha256(staged_png) != item["png_sha256"]:
                raise RuntimeError(f"PNG hash changed while moving: {item['sheet']}")
            if file_sha256(staged_meta) != item["meta_sha256"]:
                raise RuntimeError(f"meta hash changed while moving: {item['sheet']}")

        if target_preexisting:
            target_root.rmdir()
            target_removed = True
        staging.replace(target_root)
    except Exception:
        if staging.exists():
            for original, staged in reversed(moved):
                if staged.exists() and not original.exists():
                    staged.replace(original)
            if staging.exists() and not any(staging.iterdir()):
                staging.rmdir()
        if target_preexisting and target_removed and not target_root.exists():
            target_root.mkdir()
        raise

    for item in plan:
        destination_png = target_root / item["png"].name
        destination_meta = target_root / item["meta"].name
        if file_sha256(destination_png) != item["png_sha256"]:
            raise RuntimeError(f"promoted PNG hash mismatch: {item['sheet']}")
        if file_sha256(destination_meta) != item["meta_sha256"]:
            raise RuntimeError(f"promoted meta hash mismatch: {item['sheet']}")


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", required=True, type=Path)
    parser.add_argument("--candidate-root", required=True, type=Path)
    parser.add_argument("--target-root", required=True, type=Path)
    parser.add_argument("--cell-width", type=int, default=240)
    parser.add_argument("--cell-height", type=int, default=650)
    parser.add_argument(
        "--move",
        action="store_true",
        help="Execute move-only promotion. Without this flag, run read-only dry-run.",
    )
    parser.add_argument(
        "--allow-existing-empty-target",
        action="store_true",
        help="Allow an existing empty target directory. Existing files are never overwritten.",
    )
    return parser.parse_args(argv)


def run(args: argparse.Namespace) -> int:
    if args.cell_width <= 0 or args.cell_height <= 0:
        raise ValueError("cell dimensions must be positive")
    source_root = args.source_root.resolve()
    candidate_root = args.candidate_root.resolve()
    target_root = args.target_root.resolve()
    target_preexisting = validate_roots(
        source_root,
        candidate_root,
        target_root,
        args.allow_existing_empty_target,
    )
    results, ignored = validate_candidates(
        source_root,
        candidate_root,
        args.cell_width,
        args.cell_height,
    )
    if not results:
        raise ValueError("no matching top-level candidate PNG sheets found")
    failed = [result for result in results if result["status"] != "PASS"]
    summary = {
        "mode": "MOVE" if args.move else "DRY_RUN",
        "status": "FAIL" if failed else "PASS",
        "source_root": str(source_root),
        "candidate_root": str(candidate_root),
        "target_root": str(target_root),
        "target_preexisting": target_preexisting,
        "sheets": [
            {
                "sheet": result["sheet"],
                "status": result["status"],
                "failures": result.get("failures", []),
            }
            for result in results
        ],
        "ignored_top_level_pngs": ignored,
    }
    if failed:
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return 1

    plan = preflight_destinations(results, candidate_root, target_root)
    summary["pairs"] = [
        {
            "sheet": item["sheet"],
            "png_sha256": item["png_sha256"],
            "meta_sha256": item["meta_sha256"],
        }
        for item in plan
    ]
    if args.move:
        move_pairs_atomically(plan, target_root, target_preexisting)
        summary["action"] = "moved validated PNG+meta pairs; GUIDs preserved without duplication"
    else:
        summary["action"] = "dry-run only; no files changed"
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        return run(args)
    except (OSError, ValueError, RuntimeError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
