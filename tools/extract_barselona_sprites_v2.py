"""Extract clean Barcelona sprites and a continuous raised paved street."""
from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw

SOURCE = Path(r"G:\My Drive\LostCyberHamster\Buffer\Barselona_Morning_Full.png")
ROOT = Path(r"G:\My Drive\Дела\Lch\Ассеты\Barselona_Morning\Version 2")
OUT = ROOT
WORK = OUT / "Working Files"
QA = WORK / "QA"
W, H = 2000, 1500
# The irregular edge reaches y=1095 at its highest point: a 75 px rise from
# y=1170, the full whole-pixel allowance from the movable houses' mean height.
NEW_TOP, ORIGINAL_TOP, SHIFT = 1097, 1170, 75
PLAZA_PREVIEW_SHIFT = 60  # Its recessed courtyard foundation starts higher.

# Readable houses remain separate.  Only façades that interlock in perspective
# are grouped into one movable architectural complex.
GROUPS = {
    "left_cream_house": [(18,1010),(95,1010),(95,1040),(112,1040),(112,1178),(18,1178)],
    "left_gray_house": [(92,1019),(196,1025),(196,1178),(92,1178)],
    "left_white_house": [(194,1048),(305,1048),(305,1176),(194,1176)],
    "left_tower": [(306,1018),(369,1018),(369,1179),(306,1179)],
    "west_plaza_complex": [
        [(365,1033),(473,1088),(473,1168),(365,1168)],
        [(466,1083),(628,1083),(628,1168),(466,1168)],
        [(619,1080),(718,1029),(718,1174),(619,1174)],
    ],
    "central_tower": [(719,1000),(779,1000),(779,1179),(719,1179)],
    "central_ochre_house": [(777,1031),(893,1031),(893,1177),(777,1177)],
    "central_dark_house": [(889,1018),(1015,1018),(1015,1178),(889,1178)],
    "grand_arcade_complex": [
        [(1009,1019),(1080,1019),(1080,1178),(1009,1178)],
        [(1072,1042),(1211,1042),(1238,1061),(1310,1061),(1310,1185),(1072,1185)],
    ],
    "east_midrise_complex": [
        [(1300,1057),(1400,1057),(1400,1173),(1300,1173)],
        [(1391,1072),(1508,1072),(1508,1168),(1391,1168)],
    ],
    "right_white_house": [(1502,1029),(1628,1029),(1628,1178),(1502,1178)],
    "east_towers_complex": [
        [(1620,1045),(1778,1045),(1778,1189),(1620,1189)],
        [(1770,1057),(1870,1083),(1870,1178),(1770,1178)],
    ],
    "right_perspective_row": [(1862,1083),(1922,1102),(1999,1137),(1999,1159),(1862,1159)],
}

# Verified from the first pass against the source at 200%.  These are facade
# seams, not evenly spaced slices.  They prevent a neighbouring facade from
# appearing as a vertical sliver in an otherwise separate house sprite.
RANGES = {
    "left_cream_house": (0, 125),
    "left_gray_house": (125, 210),
    "left_white_house": (210, 338),
    "left_tower": (338, 405),
    "west_plaza_complex": (405, 789),
    "central_tower": (789, 855),
    "central_ochre_house": (855, 975),
    "central_dark_house": (975, 1110),
    "grand_arcade_complex": (1110, 1323),
    "east_midrise_complex": (1323, 1470),
    "right_white_house": (1470, 1645),
    "east_towers_complex": (1645, 1936),
    "right_perspective_row": (1936, 2000),
}

FLOOR_KNOTS = [
    (0,1205),(40,1210),(105,1214),(212,1213),(338,1212),(405,1215),
    (520,1216),(600,1220),(686,1218),(789,1219),(855,1223),(975,1223),
    (1110,1220),(1164,1221),(1323,1221),(1427,1224),(1470,1221),
    (1540,1220),(1645,1220),(1754,1220),(1815,1227),(1900,1228),
    (1930,1213),(1936,1210),(1940,1204),(1950,1185),(1960,1168),
    (1970,1153),(1980,1143),(1990,1140),(1999,1140),
]

# Visible facade foundations measured in the source.  This is intentionally
# shallower than the curb profile: the grey strip below these coordinates is
# street, never a piece of a house sprite.
GROUND_SEGMENTS = [
    (0,125,1175),(125,210,1177),(210,338,1174),(338,405,1176),
    (405,520,1166),(520,686,1160),(686,789,1174),
    (789,855,1176),(855,975,1175),(975,1110,1175),
    (1110,1164,1175),(1164,1323,1184),
    (1323,1427,1170),(1427,1470,1165),(1470,1645,1178),
    (1645,1754,1175),(1754,1815,1175),(1815,1870,1175),
    (1870,1936,1158),(1936,2000,1140),
]


def polygon_mask(polys: list[tuple[int, int]] | list[list[tuple[int, int]]]) -> np.ndarray:
    if polys and isinstance(polys[0][0], int):  # type: ignore[index]
        polys = [polys]  # type: ignore[assignment]
    im = Image.new("L", (W, H), 0)
    draw = ImageDraw.Draw(im)
    for poly in polys:  # type: ignore[assignment]
        draw.polygon(poly, fill=255)
    return np.asarray(im) > 0


def checkerboard(im: Image.Image, path: Path) -> None:
    yy, xx = np.indices((im.height, im.width))
    v = ((xx // 16 + yy // 16) & 1) * 52 + 174
    base = Image.fromarray(np.dstack((v, v, v, np.full_like(v, 255))).astype(np.uint8), "RGBA")
    base.alpha_composite(im)
    base.convert("RGB").save(path)


def texture_field(src: np.ndarray, height: int) -> np.ndarray:
    """Paint source-derived paving marks without directional stretching."""
    # This is an intact, broad piece of the original platform, before the curb.
    ref = src[1202:1235, :1700, :3]
    ref = cv2.copyMakeBorder(ref, 0, 0, 0, W-ref.shape[1], cv2.BORDER_REFLECT_101)
    rng = np.random.default_rng(913)
    base = np.median(ref.reshape(-1, 3), axis=0).astype(np.float32)
    # Three circular blur scales make a non-directional painted surface.  Each
    # scale is normalized after filtering, so there is texture in both axes but
    # no stretched columns, repeated strips, or square patch grid.
    pigment = np.zeros((height, W), np.float32)
    for sigma, strength in ((1.2, 1.2), (4.5, 1.8), (15.0, 1.0)):
        layer = cv2.GaussianBlur(rng.standard_normal((height, W)).astype(np.float32), (0,0), sigma)
        pigment += layer / max(float(layer.std()), 1e-5) * strength
    warm = cv2.GaussianBlur(rng.standard_normal((height, W)).astype(np.float32), (0,0), 8)
    warm /= max(float(warm.std()), 1e-5)
    field = np.empty((height, W, 3), np.float32)
    for c, tint in enumerate((-0.18, 0.00, 0.14)):
        field[:,:,c] = base[c] + pigment + warm * tint
    # Keep the original artist's small, irregular brush marks.  The intact
    # lower platform has no buildings in this band.  Its high-pass residual is
    # enlarged only vertically into the newly exposed depth; keeping its full
    # horizontal span avoids tiled columns or a repeated texture stamp.
    # This central source swatch is clear pavement at every pixel.  Sampling
    # the whole row would leak facade foundations at the left and far-right
    # perspective exit back into the empty background.
    marks_ref = src[1194:1226, 850:1450, :3].astype(np.float32)
    marks_ref -= cv2.GaussianBlur(marks_ref, (0, 0), 3.5)
    # Stack shifted 32px source swatches with six-pixel cross-fades.  This
    # preserves mark scale in both axes; a vertical resize would turn these
    # brush marks into the same stretched grain that failed the first review.
    marks = np.zeros((height, W, 3), np.float32)
    mark_weight = np.zeros((height, W, 1), np.float32)
    for row, y0 in enumerate(range(0, height, 26)):
        count_y = min(marks_ref.shape[0], height - y0)
        wy = np.ones(count_y, np.float32)
        fade_y = min(6, count_y)
        if y0:
            wy[:fade_y] = np.linspace(0, 1, fade_y, dtype=np.float32)
        if y0 + count_y < height:
            wy[-fade_y:] = np.linspace(1, 0, fade_y, dtype=np.float32)
        for col, x0 in enumerate(range(0, W, 520)):
            patch = np.roll(marks_ref, shift=(row * 7, col * 71), axis=(0, 1))
            if (row + col) & 1:
                patch = patch[:, ::-1]
            count_x = min(patch.shape[1], W - x0)
            wx = np.ones(count_x, np.float32)
            if x0:
                wx[:80] = np.linspace(0, 1, 80, dtype=np.float32)
            if x0 + count_x < W:
                wx[-80:] = np.linspace(1, 0, 80, dtype=np.float32)
            weight = wy[:, None] * wx[None, :]
            marks[y0:y0+count_y, x0:x0+count_x] += patch[:count_y, :count_x] * weight[:, :, None]
            mark_weight[y0:y0+count_y, x0:x0+count_x] += weight[:, :, None]
    marks /= np.maximum(mark_weight, 1e-5)
    # The wide source tone supplies faint hand-painted swirls; the residual
    # keeps the 5--15px grain visible without importing a sharp curb edge.
    tone = src[1194:1226, 850:1450, :3].astype(np.float32)
    tone = cv2.GaussianBlur(tone, (0, 0), 7.0)
    tone = cv2.resize(tone, (W, height), interpolation=cv2.INTER_CUBIC)
    tone -= np.median(tone.reshape(-1, 3), axis=0)
    field += tone * 0.22 + marks * 0.95
    return np.clip(field, 0, 255).astype(np.uint8)


def paste(dst: np.ndarray, src: np.ndarray, x: int, y: int) -> None:
    h, w = src.shape[:2]
    assert 0 <= x and 0 <= y and x+w <= W and y+h <= H
    region = dst[y:y+h, x:x+w]
    take = src[:,:,3] > 0
    region[take] = src[take]


def main() -> None:
    for folder in (OUT/"Sprites", OUT/"Backgrounds", QA):
        folder.mkdir(parents=True, exist_ok=True)
    shutil.copy2(SOURCE, WORK/"source.png")
    source = Image.open(SOURCE).convert("RGBA")
    src = np.asarray(source).copy()
    alpha = src[:,:,3] > 0
    floor = np.interp(np.arange(W), [x for x,_ in FLOOR_KNOTS], [y for _,y in FLOOR_KNOTS]).round().astype(int)
    ground = floor.copy()
    for x0, x1, y in GROUND_SEGMENTS:
        ground[x0:x1] = y
    yy = np.arange(H)[:,None]
    edge_rng = np.random.default_rng(207)
    edge_controls = edge_rng.normal(0, 1.0, 51)
    edge_noise = np.interp(np.arange(W), np.linspace(0, W - 1, len(edge_controls)), edge_controls)
    top_profile = NEW_TOP + np.clip(np.rint(edge_noise), -2, 2).astype(int)
    texture_top = int(top_profile.min())

    # Every pixel has one semantic owner.  Most boundaries are verified facade
    # seams; the plaza and perspective remain complexes because their painted
    # depth layers overlap.  Their bottom is clipped to the visible foundation,
    # so pavement never comes along with a house.
    masks: dict[str,np.ndarray] = {}
    owned = np.zeros((H,W), bool)
    for name, polys in GROUPS.items():
        x0, x1 = RANGES[name]
        domain = np.zeros((H,W), bool)
        domain[:,x0:x1] = True
        if name == "right_perspective_row":
            traced = polygon_mask(polys)
            # The roof begins above the painted exit.  Include the complete
            # opaque silhouette there so it cannot remain duplicated on BG.
            mask = (traced | (domain & (yy < 1140))) & alpha & (yy < ground[None,:])
        else:
            mask = domain & alpha & (yy < ground[None,:])
        if name == "east_towers_complex":
            # The ochre corner has a lower shop annex.  Its visible doors reach
            # y=1209; the generic rear-facade cutoff at y=1175 loses a whole
            # floor and leaves it baked into the supposedly empty street.
            annex = polygon_mask([[(1742,1174),(1868,1174),(1868,1156),
                                   (1931,1156),(1932,1181),(1932,1203),
                                   (1925,1207),(1910,1209),(1880,1209),
                                   (1742,1209)]])
            mask |= annex & domain & alpha
        mask &= ~owned
        if not mask.any():
            raise RuntimeError(f"empty mask: {name}")
        masks[name] = mask
        owned |= mask
    # The curved lower-left platform is street, even though its outline touches
    # the first facade; keep that wedge transparent in the house sprite.
    masks["left_cream_house"] &= ~polygon_mask([[(0,1138),(20,1138),(20,1175),(0,1175)]])

    sprites: list[tuple[str,np.ndarray,tuple[int,int]]] = []
    records: list[dict] = []
    for name, mask in masks.items():
        ys, xs = np.where(mask)
        x0,x1,y0,y1 = int(xs.min()),int(xs.max()+1),int(ys.min()),int(ys.max()+1)
        facade_bottom = y1
        # Preserve a real, inspectable street/courtyard exclusion below every
        # cutout.  It is transparent padding, so no pavement can accidentally
        # travel with a movable house; it also makes the negative region clear
        # in the manifest review images.
        y1 = min(H, y1 + 14)
        local = np.zeros((y1-y0,x1-x0,4), np.uint8)
        local[:facade_bottom-y0][mask[y0:facade_bottom,x0:x1]] = src[y0:facade_bottom,x0:x1][mask[y0:facade_bottom,x0:x1]]
        repair_polygons = []
        Image.fromarray(local,"RGBA").save(OUT/"Sprites"/f"{name}.png")
        checkerboard(Image.fromarray(local,"RGBA"), QA/f"{name}_checkerboard.png")
        forbidden = [[[x0,facade_bottom+1],[x1-1,facade_bottom+1],
                      [x1-1,y1-2],[x0,y1-2]]]
        records.append({
            "id":name, "file":f"Sprites/{name}.png", "source_position":[x0,y0],
            "forbidden_polygons":forbidden, "repair_polygons":repair_polygons,
            "visible_height_px":int(ys.max()-ys.min()+1),
            "movable":True,
            "movement_note":"Move the complete architectural square with its lamp and cars."
                if name == "west_plaza_complex" else "",
        })
        sprites.append((name, local, (x0,y0)))

    union = np.zeros((H,W),bool)
    for mask in masks.values():
        union |= mask
    empty_original = src.copy()
    empty_original[union] = 0

    # Recompose the unpainted empty background first.  This must be exact; it
    # proves that all source architecture belongs to exactly one output sprite.
    reconstruction = empty_original.copy()
    for _,local,(x,y) in sprites:
        paste(reconstruction, local, x, y)
    reconstruction_changed = int(np.count_nonzero(np.any(reconstruction != src, axis=2)))
    if reconstruction_changed:
        raise RuntimeError(f"reconstruction changed {reconstruction_changed} source pixels")

    # Replace the full usable band, including former foundations, in one pass.
    # Road, curb and the perspective exit are then restored directly from source.
    paving = np.zeros((H,W),bool)
    # Cover the full former architecture zone down to the intact lower street
    # plane.  The sprite foundation cut is deliberately higher; anything below
    # it must be paved here so it cannot remain as a ghost when sprites move.
    paving[:,:1936] = (yy >= top_profile[None,:1936]) & (yy < floor[None,:1936])
    exit_mask = polygon_mask([[(1920,1134),(1999,1134),(1999,1370),(1920,1370)]])
    paving &= ~exit_mask
    raised = empty_original.copy()
    field = texture_field(src, int(floor[:1936].max()-texture_top))
    py,px = np.where(paving)
    raised[py,px,:3] = field[py-texture_top,px]
    raised[py,px,3] = 255
    for x in range(1936):
        y = int(top_profile[x])
        if paving[y,x]:
            # A two-pixel, gently wandering rear edge keeps the new sidewalk
            # grounded as a painted plane rather than a flat grey rectangle.
            raised[y,x] = (43,44,42,255)
            if y + 1 < H and paving[y+1,x]:
                raised[y+1,x] = (54,55,52,255)
    road = (yy >= floor[None,:]) | exit_mask
    road &= ~union
    raised[road] = src[road]
    road_diff = int(np.count_nonzero(np.any(raised[road] != src[road], axis=1)))
    if road_diff:
        raise RuntimeError(f"protected road changed {road_diff} pixels")

    Image.fromarray(empty_original,"RGBA").save(WORK/"empty_original.png")
    raised_img = Image.fromarray(raised,"RGBA")
    raised_img.save(OUT/"Backgrounds"/"empty_raised.png")
    Image.fromarray((union|paving).astype(np.uint8)*255,"L").save(QA/"allowed_background_changes.png")
    Image.fromarray(paving.astype(np.uint8)*255,"L").save(QA/"required_paving_mask.png")
    Image.fromarray(road.astype(np.uint8)*255,"L").save(QA/"preserved_road_mask.png")
    Image.fromarray(reconstruction,"RGBA").save(QA/"reconstruction_source_exact.png")

    moved = raised.copy()
    for name,local,(x,y) in sprites:
        preview_shift = PLAZA_PREVIEW_SHIFT if name == "west_plaza_complex" else SHIFT
        paste(moved, local, x, y-preview_shift)
    Image.fromarray(moved,"RGBA").save(QA/"moved_sprites_houses75_plaza60.png")

    compare = Image.new("RGBA",(W*2,H),(24,24,27,255))
    compare.alpha_composite(Image.fromarray(empty_original,"RGBA"),(0,0))
    compare.alpha_composite(raised_img,(W,0))
    compare.convert("RGB").save(QA/"compare_empty_before_after.png")
    road_compare = Image.new("RGBA",(W*2,400),(24,24,27,255))
    road_compare.alpha_composite(source.crop((0,1100,W,1500)),(0,0))
    road_compare.alpha_composite(raised_img.crop((0,1100,W,1500)),(W,0))
    road_compare.convert("RGB").save(QA/"compare_road_curb_exit.png")

    # One scene-wide coordinate system for the user's midpoint criterion:
    # highest opaque roof, lowest opaque foundation, then their arithmetic
    # midpoint.  Every point of the new rear edge must reach that midpoint.
    global_top = min(int(np.where(mask)[0].min()) for mask in masks.values())
    global_bottom = max(int(np.where(mask)[0].max()) for mask in masks.values())
    global_midpoint = (global_top + global_bottom) / 2
    if int(top_profile.max()) > global_midpoint:
        raise RuntimeError("new pavement fails the scene-wide building midpoint")
    review_band = Image.new("RGB", (W * 2, 300), (25, 25, 28))
    review_band.paste(source.crop((0, 950, W, 1250)).convert("RGB"), (0, 0))
    review_band.paste(raised_img.crop((0, 950, W, 1250)).convert("RGB"), (W, 0))
    reviewer = ImageDraw.Draw(review_band)
    for xoffset in (0, W):
        for y, color in ((global_top, (90, 220, 240)),
                         (round(global_midpoint), (255, 85, 215)),
                         (global_bottom, (255, 180, 65))):
            reviewer.line((xoffset, y - 950, xoffset + W - 1, y - 950), fill=color, width=2)
    reviewer.text((10, 8), f"Source: top {global_top}; bottom {global_bottom}", fill="white")
    reviewer.text((W + 10, 8),
                  f"Raised background: midpoint {global_midpoint}; rear edge {top_profile.min()}-{top_profile.max()}",
                  fill="white")
    review_band.save(QA/"height_midpoint_review.png")

    tile_w,tile_h,cols = 560,300,3
    sheet = Image.new("RGB",(tile_w*cols,tile_h*((len(sprites)+cols-1)//cols)),(30,30,33))
    text = ImageDraw.Draw(sheet)
    for i,(name,local,_) in enumerate(sprites):
        im = Image.fromarray(local,"RGBA")
        scale = min((tile_w-22)/im.width,(tile_h-45)/im.height,1.8)
        thumb = im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.NEAREST)
        bg = Image.new("RGBA",(tile_w,tile_h-26),(214,214,214,255))
        d = ImageDraw.Draw(bg)
        for sy in range(0,bg.height,16):
            for sx in range(0,bg.width,16):
                if (sx//16+sy//16)&1: d.rectangle((sx,sy,sx+15,sy+15),fill=(166,166,166,255))
        bg.alpha_composite(thumb,((tile_w-thumb.width)//2,(bg.height-thumb.height)//2))
        x,y=(i%cols)*tile_w,(i//cols)*tile_h
        sheet.paste(bg.convert("RGB"),(x,y))
        text.text((x+8,y+tile_h-20),name,fill=(235,235,235))
    sheet.save(QA/"sprites_contact_sheet_checkerboard.png")

    # The square is one architectural sprite and moves as a complete complex.
    heights=[record["visible_height_px"] for record in records if record["movable"]]
    manifest={
        "qa_contract_version":2,
        "source":"source.png", "empty_background":"../Backgrounds/empty_raised.png",
        "unpainted_empty_background":"empty_original.png",
        "required_cutout_regions":[{"id":"east_corner_shop_annex",
            "polygon":[[1760,1186],[1900,1186],[1900,1205],[1760,1205]]}],
        "movement_preview":"QA/moved_sprites_houses75_plaza60.png",
        "required_preview_opaque_regions":[{"id":"western_square_foundation",
            "polygon":[[520,1085],[685,1085],[685,1094],[520,1094]]}],
        "edited_mask":"QA/allowed_background_changes.png",
        "required_paving_mask":"QA/required_paving_mask.png",
        "preserved_road_mask":"QA/preserved_road_mask.png",
        "sidewalk_rise":{"top_y":texture_top,"original_top_y":ORIGINAL_TOP,
            "building_heights":heights,"max_fraction_of_mean_height":0.5},
        "height_midpoint":{"house_ids":[r["id"] for r in records],
            "paving_intervals_x":[[0,1936]]},
        "sprites":[({k:(f"../{v}" if k=="file" else v) for k,v in r.items() if k!="visible_height_px"} |
            ({"required_anchor_points":[[430,1100],[600,1110],[750,1080]]}
             if r["id"] == "west_plaza_complex" else {})) for r in records],
        "fixed_layers":[],
    }
    (WORK/"review-manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    report={"source_sha256":hashlib.sha256(SOURCE.read_bytes()).hexdigest(),"sprite_count":len(records),
        "reconstruction_changed_pixels":reconstruction_changed,"road_rgba_mismatch_pixels":road_diff,
        "sidewalk_rise_px":ORIGINAL_TOP-texture_top,"mean_visible_height_px":float(np.mean(heights)),
        "allowed_rise_px":float(np.mean(heights)*0.5),
        "height_midpoint":{"highest_roof_y":global_top,"lowest_base_y":global_bottom,
            "scene_midpoint_y":global_midpoint,"rear_edge_y_range":
            [int(top_profile.min()),int(top_profile.max())]}}
    (QA/"build-report.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    gallery = {
        "title":"Barselona Morning — buildings and street",
        "description":"13 independently positionable architectural sprites, including the intact western square.",
        "cases":[
            {"id":"architecture","title":"Architecture",
             "states":[{"id":r["id"].replace("_","-"),"title":r["id"].replace("_"," "),
                        "file":f"../{r['file']}"} for r in records]},
            {"id":"street","title":"Street and review",
             "states":[
                 {"id":"raised-street","title":"Raised street","file":"../Backgrounds/empty_raised.png"},
                 {"id":"original-street","title":"Original empty street","file":"empty_original.png"},
                 {"id":"midpoint-review","title":"Scene-wide midpoint review","file":"QA/height_midpoint_review.png"},
                 {"id":"move-preview","title":"Moved houses 75px; square 60px",
                  "file":"QA/moved_sprites_houses75_plaza60.png"},
             ]},
        ],
    }
    (WORK/"sprite-gallery.json").write_text(json.dumps(gallery,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    (OUT/"README.md").write_text(
        "# Barselona Morning — Version 2\n\n"
        "Готовые ассеты: `Sprites/` и `Backgrounds/empty_raised.png`. "
        "Западная площадь — один перемещаемый архитектурный спрайт.\n\n"
        "Галерея: `Gallery/index.html`. Исходник, скрипт, манифесты, маски, "
        "QA и старые черновики находятся в `Working Files/`.\n\n"
        "Просмотр: `Working Files/QA/moved_sprites_houses75_plaza60.png` и "
        "`Working Files/QA/height_midpoint_review.png`.\n\n"
        f"Верх домов y={global_top}; низ y={global_bottom}; середина y={global_midpoint}. "
        f"Новая кромка y={int(top_profile.min())}..{int(top_profile.max())}. "
        f"Подъём={ORIGINAL_TOP-texture_top}px; ориентир половины средней высоты="
        f"{float(np.mean(heights)*0.5):.3f}px.\n\n"
        "Отчёт: `Working Files/QA/automated/qa-report.json`. "
        "Позиции спрайтов и маски: `Working Files/review-manifest.json`.\n",
        encoding="utf-8")
    print(json.dumps(report,ensure_ascii=False))

if __name__ == "__main__":
    main()
