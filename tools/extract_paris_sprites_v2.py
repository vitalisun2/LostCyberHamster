from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


SRC = Path(r"G:\My Drive\LostCyberHamster\Buffer\Paris_Afternoon.png")
ROOT = Path(r"G:\My Drive\Дела\Lch\Ассеты\Paris_Afternoon\Version 2")
SPRITES = ROOT / "Sprites"
BACKGROUNDS = ROOT / "Backgrounds"
QA = ROOT / "QA"
for directory in (SPRITES, BACKGROUNDS, QA):
    directory.mkdir(parents=True, exist_ok=True)

for obsolete_debug_file in (
    "debug_grid_central.png",
    "debug_grid_edges.png",
    "debug_grid_flatiron.png",
    "debug_grid_corner.png",
    "debug_grid_gray_blue_boundary.png",
):
    (QA / obsolete_debug_file).unlink(missing_ok=True)

source = Image.open(SRC).convert("RGBA")
W, H = source.size
source_alpha = source.getchannel("A")


def polygons_mask(polygons: list[list[tuple[int, int]]]) -> Image.Image:
    mask = Image.new("L", (W, H), 0)
    draw = ImageDraw.Draw(mask)
    for polygon in polygons:
        draw.polygon(polygon, fill=255)
    return Image.composite(source_alpha, Image.new("L", (W, H), 0), mask)


# Semantic groups. Polygons separate neighbouring architecture and stop at the
# visible building/ground contact instead of using rectangular crops.
specs = [
    {
        "id": "building_01_left_edge",
        "title": "Left edge building",
        "kind": "building",
        "polygons": [
            [(0, 575), (51, 575), (51, 716), (0, 716)],
            [(49, 575), (79, 647), (49, 647)],
        ],
    },
    {
        "id": "building_02_small_green_shop",
        "title": "Small green shop",
        "kind": "building",
        "polygons": [[(47, 650), (84, 650), (84, 718), (47, 718)]],
    },
    {
        "id": "building_03_curved_facade",
        "title": "Curved facade building",
        "kind": "building",
        "polygons": [[(78, 568), (155, 568), (155, 714), (78, 714)]],
    },
    {
        "id": "building_04_blue_mansard",
        "title": "Blue mansard building",
        "kind": "building",
        "polygons": [[(154, 522), (354, 522), (354, 720), (154, 720)]],
    },
    {
        "id": "building_05_white_awnings",
        "title": "White building with red awnings",
        "kind": "building",
        "polygons": [[(352, 532), (487, 532), (487, 719), (352, 719)]],
    },
    {
        "id": "complex_06_moulin_rouge",
        "title": "Moulin Rouge and two adjacent buildings",
        "kind": "complex",
        "polygons": [
            [(486, 585), (584, 585), (584, 718), (486, 718)],
            [(570, 525), (748, 525), (748, 719), (570, 719)],
            [(738, 592), (807, 592), (871, 651), (817, 719), (738, 719)],
        ],
    },
    {
        "id": "building_07_flatiron",
        "title": "Central flatiron building",
        "kind": "building",
        "polygons": [[(854, 560), (1005, 560), (1005, 695), (985, 709), (965, 710), (930, 700), (900, 676), (875, 662), (854, 651)]],
    },
    {
        "id": "building_08_corner_block",
        "title": "Corner block building",
        "kind": "building",
        "polygons": [[(996, 580), (1138, 580), (1138, 687), (1100, 694), (1070, 700), (1040, 702), (1015, 695), (996, 688)]],
    },
    {
        "id": "complex_09_le_consulat",
        "title": "Le Consulat courtyard complex",
        "kind": "complex",
        # The courtyard and street are intentionally holes between the five
        # architecture-only polygons. They belong to the empty background.
        "polygons": [
            [(1124, 615), (1255, 615), (1255, 707), (1124, 699)],
            [(1242, 611), (1291, 611), (1291, 681), (1272, 683), (1255, 707), (1247, 696)],
            [(1260, 574), (1391, 574), (1391, 690), (1260, 690)],
            [(1368, 610), (1434, 610), (1434, 700), (1418, 697), (1390, 684), (1368, 681)],
            [(1418, 574), (1529, 574), (1529, 711), (1418, 711)],
        ],
    },
    {
        "id": "building_10_gray_block",
        "title": "Gray block building",
        "kind": "building",
        "polygons": [[(1529, 557), (1674, 557), (1674, 716), (1529, 716)]],
    },
    {
        "id": "building_11_blue_corner",
        "title": "Blue-gray corner building",
        "kind": "building",
        "polygons": [[(1674, 515), (1819, 515), (1819, 716), (1674, 716)]],
    },
    {
        "id": "building_12_pink_low",
        "title": "Pink low building",
        "kind": "building",
        "polygons": [[(1815, 615), (1905, 615), (1905, 701), (1815, 679)]],
    },
    {
        "id": "building_13_white_tower",
        "title": "White tower building",
        "kind": "building",
        "polygons": [[(1903, 548), (1965, 548), (1965, 698), (1903, 680)]],
    },
    {
        "id": "building_14_right_edge",
        "title": "Right edge building",
        "kind": "building",
        "polygons": [[(1958, 621), (1999, 621), (1999, 709), (1958, 709)]],
    },
]


# Small street props remain on the background. They are removed from building
# masks, while hidden facade fragments are reconstructed for movable sprites.
prop_polygons = [
    [(568, 711), (575, 706), (583, 710), (586, 716), (583, 734), (571, 734), (568, 718)],
    [(586, 710), (595, 704), (604, 709), (607, 716), (604, 734), (590, 734), (587, 718)],
    [(605, 709), (614, 704), (623, 710), (626, 716), (622, 734), (610, 734), (606, 718)],
    [(744, 706), (753, 700), (763, 705), (767, 713), (764, 735), (750, 735), (745, 715)],
    [(766, 706), (776, 699), (786, 705), (790, 713), (786, 736), (772, 736), (767, 715)],
    [(789, 705), (800, 699), (811, 705), (815, 713), (811, 736), (796, 736), (790, 715)],
    [(932, 679), (936, 674), (944, 674), (949, 680), (948, 688), (943, 692), (943, 722), (938, 722), (938, 692), (933, 688)],
]
props_mask = Image.new("L", (W, H), 0)
pd = ImageDraw.Draw(props_mask)
for polygon in prop_polygons:
    pd.polygon(polygon, fill=255)
props_alpha = Image.composite(source_alpha, Image.new("L", (W, H), 0), props_mask)


def repair_occluded_facade(base: Image.Image, identifier: str) -> Image.Image:
    if identifier not in {"complex_06_moulin_rouge", "building_07_flatiron"}:
        return base
    rgba = np.array(base).copy()
    prop_pixels = np.array(props_alpha) > 0
    if identifier == "complex_06_moulin_rouge":
        # Restore only pixels hidden by foreground pots. Every visible source
        # pixel outside their silhouettes remains byte-identical.
        for x0, x1, sample_y, y0, y1 in [
            (564, 630, 698, 702, 720),
            (741, 821, 694, 698, 720),
        ]:
            region = prop_pixels[y0 : y1 + 1, x0 : x1 + 1]
            replacement = np.repeat(rgba[sample_y : sample_y + 1, x0 : x1 + 1, :], y1 - y0 + 1, axis=0)
            rgba[y0 : y1 + 1, x0 : x1 + 1, :][region] = replacement[region]
    else:
        # Restore only the exact sign silhouette from an adjacent facade strip.
        region = prop_pixels[650:723, 931:951]
        replacement = rgba[650:723, 951:971, :]
        rgba[650:723, 931:951, :][region] = replacement[region]
    return Image.fromarray(rgba, "RGBA")


manifest = {
    "source": str(SRC),
    "source_size": [W, H],
    "extended_size": [3000, H],
    "sprites": [],
}

all_buildings = Image.new("L", (W, H), 0)

for spec in specs:
    raw_mask = polygons_mask(spec["polygons"])
    background_raw_mask = polygons_mask(spec.get("background_polygons", spec["polygons"]))
    # Street props stay out of building sprites.
    background_mask_arr = np.array(background_raw_mask)
    prop_arr = np.array(props_alpha)
    background_mask_arr[prop_arr > 0] = 0
    background_mask = Image.fromarray(background_mask_arr, "L")
    mask = raw_mask.copy()

    prepared = repair_occluded_facade(source.copy(), spec["id"])
    bbox = mask.getbbox()
    if bbox is None:
        raise RuntimeError(f"Empty mask: {spec['id']}")
    sprite = prepared.copy()
    sprite.putalpha(mask)
    sprite = sprite.crop(bbox)
    path = SPRITES / f"{spec['id']}.png"
    sprite.save(path)

    # Background removal mask keeps original street props visible.
    all_buildings = Image.fromarray(
        np.maximum(np.array(all_buildings), np.array(background_mask)).astype(np.uint8), "L"
    )
    manifest["sprites"].append(
        {
            "id": spec["id"],
            "title": spec["title"],
            "kind": spec["kind"],
            "file": str(path.relative_to(ROOT)),
            "source_bbox": list(bbox),
            "size": list(sprite.size),
            "source_position": [bbox[0], bbox[1]],
        }
    )

# Remove narrow architectural remnants at occlusion boundaries. These polygons
# are outside road surfaces and affect background only, never sprite content.
cleanup_mask = Image.new("L", (W, H), 0)
cd = ImageDraw.Draw(cleanup_mask)
for polygon in [
    [(49, 570), (79, 570), (79, 650), (49, 650)],
    [(478, 535), (592, 535), (592, 688), (478, 688)],
    [(1808, 510), (1823, 510), (1823, 678), (1808, 678)],
    [(1890, 535), (1970, 535), (1970, 678), (1890, 678)],
]:
    cd.polygon(polygon, fill=255)
cleanup_arr = np.array(cleanup_mask)
all_buildings = Image.fromarray(
    np.maximum(np.array(all_buildings), cleanup_arr).astype(np.uint8), "L"
)


def textured_sidewalk() -> Image.Image:
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    arr = np.zeros((H, W, 4), dtype=np.uint8)
    rng = np.random.default_rng(20260922)
    top = np.interp(np.arange(W), [0, 500, 1000, 1500, 1999], [688, 686, 690, 688, 690]).astype(int)
    bottom = np.interp(np.arange(W), [0, 500, 1000, 1500, 1999], [747, 746, 747, 748, 746]).astype(int)
    for x in range(W):
        y0, y1 = int(top[x]), int(bottom[x])
        noise = rng.integers(-4, 5, size=(max(0, y1 - y0 + 1), 1), dtype=np.int16)
        base = np.array([108, 109, 114], dtype=np.int16)
        colors = np.clip(base + noise, 0, 255).astype(np.uint8)
        if y1 >= y0:
            arr[y0 : y1 + 1, x, :3] = colors
            arr[y0 : y1 + 1, x, 3] = 255
    layer = Image.fromarray(arr, "RGBA")
    ld = ImageDraw.Draw(layer)
    ld.line([(x, int(top[x])) for x in range(W)], fill=(36, 37, 40, 255), width=2)
    ld.line([(x, int(bottom[x])) for x in range(W)], fill=(39, 40, 44, 255), width=2)
    return layer


# Rebuild empty original-width street: generated pavement underneath, then exact
# original road/curb/props pixels everywhere outside architecture masks.
empty = textured_sidewalk()
remove_arr = np.array(all_buildings)
# Replace retained pixels directly, including their original semi-transparent
# edge values and transparent pixels. Alpha compositing would alter RGBA at road contours.
empty_arr_exact = np.array(empty)
source_arr_exact = np.array(source)
keep_pixels = remove_arr == 0
empty_arr_exact[keep_pixels] = source_arr_exact[keep_pixels]
empty = Image.fromarray(empty_arr_exact, "RGBA")
empty_path = BACKGROUNDS / "Paris_Afternoon_empty_street_2000x1000.png"
empty.save(empty_path)


# Extend at x=1250, inside the rebuilt continuous sidewalk. Existing pixels are
# not scaled; a 1000px pavement segment is synthesized row-by-row between halves.
split_x = 1250
insert_w = 1000
extended = Image.new("RGBA", (W + insert_w, H), (0, 0, 0, 0))
extended.alpha_composite(empty.crop((0, 0, split_x, H)), (0, 0))
extended.alpha_composite(empty.crop((split_x, 0, W, H)), (split_x + insert_w, 0))

left_context = np.array(empty.crop((split_x - 96, 0, split_x, H)))
right_context = np.array(empty.crop((split_x, 0, split_x + 96, H)))
insert = np.zeros((H, insert_w, 4), dtype=np.uint8)
rng = np.random.default_rng(20260923)
for y in range(H):
    samples = np.concatenate([left_context[y], right_context[y]], axis=0)
    opaque = samples[samples[:, 3] > 200]
    if len(opaque) < 8:
        continue
    median = np.median(opaque, axis=0).astype(np.int16)
    # Preserve dark horizontal ink/curb rows; add restrained pavement texture.
    jitter = 0 if median[:3].mean() < 65 else 3
    row_noise = rng.integers(-jitter, jitter + 1, size=(insert_w, 1), dtype=np.int16)
    insert[y, :, :3] = np.clip(median[:3] + row_noise, 0, 255).astype(np.uint8)
    insert[y, :, 3] = median[3]

insert_image = Image.fromarray(insert, "RGBA")
extended.alpha_composite(insert_image, (split_x, 0))
extended_path = BACKGROUNDS / "Paris_Afternoon_empty_street_3000x1000.png"
extended.save(extended_path)

manifest["backgrounds"] = [
    {"file": str(empty_path.relative_to(ROOT)), "size": list(empty.size)},
    {"file": str(extended_path.relative_to(ROOT)), "size": list(extended.size)},
]
manifest["extension"] = {
    "split_x": split_x,
    "insert_width": insert_w,
    "method": "translate halves and synthesize matching sidewalk rows; no scaling",
}
(ROOT / "scene-manifest.json").write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
)


def checker(size: tuple[int, int], tile: int = 16) -> Image.Image:
    bg = Image.new("RGBA", size, (46, 46, 49, 255))
    draw = ImageDraw.Draw(bg)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            if (x // tile + y // tile) % 2:
                draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=(70, 70, 74, 255))
    return bg


# Contact sheet used as a mandatory visual gate.
font = ImageFont.load_default()
cards = []
for item in manifest["sprites"]:
    img = Image.open(ROOT / item["file"]).convert("RGBA")
    preview = checker(img.size)
    preview.alpha_composite(img)
    preview.thumbnail((430, 190), Image.Resampling.LANCZOS)
    card = Image.new("RGB", (450, 230), (25, 25, 28))
    card.paste(preview.convert("RGB"), ((450 - preview.width) // 2, 8))
    ImageDraw.Draw(card).text((8, 207), f"{item['id']}  {img.width}x{img.height}", fill=(245, 245, 245), font=font)
    cards.append(card)

cols = 3
rows = (len(cards) + cols - 1) // cols
sheet = Image.new("RGB", (450 * cols, 230 * rows), (18, 18, 20))
for i, card in enumerate(cards):
    sheet.paste(card, ((i % cols) * 450, (i // cols) * 230))
sheet.save(QA / "sprites_contact_sheet.png")


def composite_on_checker(img: Image.Image) -> Image.Image:
    bg = checker(img.size, 24)
    bg.alpha_composite(img)
    return bg.convert("RGB")


empty_preview = empty.copy()
empty_preview.thumbnail((1500, 750), Image.Resampling.LANCZOS)
composite_on_checker(empty_preview).save(QA / "empty_street_2000_preview.png")
extended_preview = extended.copy()
extended_preview.thumbnail((1500, 500), Image.Resampling.LANCZOS)
composite_on_checker(extended_preview).save(QA / "empty_street_3000_preview.png")

# Reassemble the original-width scene as a visual integrity check. Foreground
# props are restored last because they originally overlap building facades.
reconstruction = empty.copy()
for item in manifest["sprites"]:
    sprite = Image.open(ROOT / item["file"]).convert("RGBA")
    reconstruction.alpha_composite(sprite, tuple(item["source_position"]))
props_layer = source.copy()
props_layer.putalpha(props_alpha)
reconstruction.alpha_composite(props_layer)
reconstruction.save(QA / "reconstruction_2000.png")

src_arr = np.array(source, dtype=np.int16)
empty_arr = np.array(empty, dtype=np.int16)
recon_arr = np.array(reconstruction, dtype=np.int16)
removal_arr = np.array(all_buildings)
preserve_domain = removal_arr == 0
preserved_equal = np.all(src_arr == empty_arr, axis=2) & preserve_domain
preserved_total = int(preserve_domain.sum())
preserved_exact = int(preserved_equal.sum())

source_domain = src_arr[:, :, 3] > 0
rgb_diff = np.abs(src_arr[:, :, :3] - recon_arr[:, :, :3]).mean(axis=2)
mean_reconstruction_difference = float(rgb_diff[source_domain].mean()) if source_domain.any() else 0.0
diff_preview = np.zeros((H, W, 4), dtype=np.uint8)
diff_preview[:, :, 0] = np.clip(rgb_diff * 5, 0, 255).astype(np.uint8)
diff_preview[:, :, 3] = np.where(source_domain, 255, 0).astype(np.uint8)
Image.fromarray(diff_preview, "RGBA").save(QA / "reconstruction_difference.png")
all_buildings.save(QA / "building_removal_mask.png")

# Regression gate for the reported failure: the open courtyard directly below
# Le Consulat must remain fully transparent in the grouped architecture sprite.
consulat_item = next(item for item in manifest["sprites"] if item["id"] == "complex_09_le_consulat")
consulat_alpha = np.array(Image.open(ROOT / consulat_item["file"]).convert("RGBA"))[:, :, 3]
consulat_x, consulat_y = consulat_item["source_position"]
courtyard_surface_overlap = int(
    (
        consulat_alpha[
            691 - consulat_y : 712 - consulat_y,
            1275 - consulat_x : 1400 - consulat_x,
        ]
        > 0
    ).sum()
)
if courtyard_surface_overlap:
    raise RuntimeError(f"Le Consulat sprite captured {courtyard_surface_overlap} courtyard pixels")

qa_report = {
    "source_size": [W, H],
    "extended_size": list(extended.size),
    "sprite_count": len(manifest["sprites"]),
    "preserved_source_pixels": preserved_total,
    "preserved_source_pixels_exact": preserved_exact,
    "preserved_source_percent": round(100.0 * preserved_exact / preserved_total, 6) if preserved_total else 100.0,
    "mean_reconstruction_rgb_difference_on_source_pixels": round(mean_reconstruction_difference, 6),
    "le_consulat_courtyard_surface_overlap_pixels": courtyard_surface_overlap,
    "opaque_black_pixels_empty_background": int(
        ((empty_arr[:, :, 3] > 0) & np.all(empty_arr[:, :, :3] == 0, axis=2)).sum()
    ),
}
(QA / "qa-report.json").write_text(
    json.dumps(qa_report, ensure_ascii=False, indent=2), encoding="utf-8"
)

gallery_manifest = {
    "title": "Paris Afternoon — Version 2",
    "description": "Финальные спрайты, пустая улица и контрольная обратная сборка.",
    "cases": [
        {
            "id": "backgrounds",
            "title": "Пустая улица",
            "states": [
                {
                    "id": "original-width",
                    "title": "Без зданий — 2000×1000",
                    "file": "Backgrounds/Paris_Afternoon_empty_street_2000x1000.png",
                    "note": "Повороты дорог и сохранённые пиксели исходника оставлены без изменений.",
                },
                {
                    "id": "extended",
                    "title": "Расширенная улица — 3000×1000",
                    "file": "Backgrounds/Paris_Afternoon_empty_street_3000x1000.png",
                    "note": "Добавлено 1000 px тротуара без масштабирования исходных частей.",
                },
            ],
        },
        {
            "id": "sprites",
            "title": "Спрайты зданий",
            "states": [
                {
                    "id": item["id"].replace("_", "-"),
                    "title": item["title"],
                    "file": item["file"].replace("\\", "/"),
                    "note": f"{item['kind']}; исходная позиция {item['source_position']}; размер {item['size']}",
                }
                for item in manifest["sprites"]
            ],
        },
        {
            "id": "qa",
            "title": "Контроль качества",
            "states": [
                {"id": "contact-sheet", "title": "Все спрайты", "file": "QA/sprites_contact_sheet.png"},
                {"id": "reconstruction", "title": "Обратная сборка", "file": "QA/reconstruction_2000.png"},
                {"id": "difference", "title": "Карта отличий", "file": "QA/reconstruction_difference.png"},
                {"id": "removal-mask", "title": "Маска удаления", "file": "QA/building_removal_mask.png"},
            ],
        },
    ],
}
(ROOT / "gallery-manifest.json").write_text(
    json.dumps(gallery_manifest, ensure_ascii=False, indent=2), encoding="utf-8"
)

print(f"Version 2 written to {ROOT}")
print(f"Sprites: {len(manifest['sprites'])}")
print(f"Backgrounds: {empty.size}, {extended.size}")
print(json.dumps(qa_report, ensure_ascii=False))
