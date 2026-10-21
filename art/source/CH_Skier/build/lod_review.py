"""Phase 4 review: LOD silhouettes against the Phase 3 forms, a clay compare sheet and a greyscale gameplay strip.

  python3 build/lod_review.py --dir review/04_topology --forms review/03_forms
Reads <dir>/lod0..lod2 (review_render.py clay renders, world_gate.py silhouettes) and the Phase 3 renders in <forms>.
Writes <dir>/clay_compare.png, <dir>/gameplay_strip_grey.png, <dir>/sil_diff_lod<n>_<view>.png and
<dir>/lod_gates.json (observations: the gate thresholds come from the brief and are applied by the reader).
"""
import argparse, json, os
from PIL import Image, ImageDraw, ImageFont

BG = (236, 236, 240)
PH3 = {"front": 0.9806, "side": 0.9612}   # Phase 3 forms pass 2, world_gate IoU (asset-brief.md, Gate results)
TOL = 0.005                               # LOD0 may be at most this far under Phase 3


def font(s):
    try:
        return ImageFont.load_default(size=s)
    except TypeError:
        return ImageFont.load_default()


def flat(path):
    im = Image.open(path).convert("RGBA")
    alpha = im.getchannel("A")
    if alpha.getextrema()[0] < 250:
        box = alpha.getbbox()
    else:
        c = sum(im.getpixel((0, 0))[:3]) // 3
        box = Image.eval(im.convert("L"), lambda v: 255 if abs(v - c) > 12 else 0).getbbox()
    bg = Image.new("RGBA", im.size, BG + (255,))
    bg.alpha_composite(im)
    return bg.convert("RGB"), box or (0, 0, im.width, im.height)


def mask(path, size=None):
    im = Image.open(path)
    if im.mode in ("RGBA", "LA") and im.getchannel("A").getextrema()[0] < 250:
        m = im.getchannel("A").point(lambda v: 255 if v > 127 else 0)
    else:
        g = im.convert("L")
        c = g.getpixel((0, 0))
        m = g.point(lambda v: 255 if abs(v - c) > 127 else 0)
    return m.resize(size, Image.NEAREST) if size and m.size != size else m


def tile(path, h, pad=12):
    im, b = flat(path)
    im = im.crop((max(0, b[0] - pad), max(0, b[1] - pad), min(im.width, b[2] + pad), min(im.height, b[3] + pad)))
    return im.resize((max(1, int(im.width * h / im.height)), h), Image.LANCZOS)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--forms", required=True)
    a = ap.parse_args()
    meas = json.load(open(os.path.join(a.dir, "measure.json")))["tris"]
    rows = [("Phase 3 forms (3,414 tris)", a.forms)] + [
        ("LOD%d (body %d + props %d)" % (i, meas["body_LOD%d" % i], meas["props_total"]),
         os.path.join(a.dir, "lod%d" % i)) for i in range(3)]
    out = {"thresholds_lod0": {v: round(PH3[v] - TOL, 4) for v in PH3}, "views": {}}
    for i in range(3):
        for v in ("front", "side"):
            g = json.load(open(os.path.join(a.dir, "lod%d" % i, "gate_%s.json" % v)))
            ref = mask(os.path.join(a.forms, "sil_%s.png" % v))
            m = mask(os.path.join(a.dir, "lod%d" % i, "sil_%s.png" % v), ref.size)
            px = list(zip(ref.getdata(), m.getdata()))
            inter = sum(1 for r, s in px if r and s)
            union = sum(1 for r, s in px if r or s)
            lost = sum(1 for r, s in px if r and not s)
            added = sum(1 for r, s in px if s and not r)
            out["views"]["LOD%d %s" % (i, v)] = {
                "world_gate_iou": g["iou"], "band_width_max_abs": max(abs(x) for x in g["band_width_diff"]),
                "iou_vs_phase3_forms": round(inter / union, 4), "px_lost_vs_forms": lost, "px_added_vs_forms": added,
                "lod0_floor": round(PH3[v] - TOL, 4) if i == 0 else None}
            dimg = Image.new("RGB", ref.size, (255, 255, 255))
            dimg.putdata([(70, 70, 70) if r and s else (220, 30, 30) if r else (30, 90, 220) if s else
                          (255, 255, 255) for r, s in px])
            dimg.save(os.path.join(a.dir, "sil_diff_lod%d_%s.png" % (i, v)))
    json.dump(out, open(os.path.join(a.dir, "lod_gates.json"), "w"), indent=1)
    # clay compare sheet: rows Phase 3, LOD0, LOD1, LOD2; columns front, side, game camera
    H = 330
    grid = [[tile(os.path.join(d, "clay_%s.png" % v), H) for v in ("front", "side", "ref")] for _, d in rows]
    W = 230 + max(sum(t.width + 16 for t in r) for r in grid)
    sheet = Image.new("RGB", (W, 40 + len(rows) * (H + 16)), "white")
    dr = ImageDraw.Draw(sheet)
    dr.text((12, 10), "CH_Skier Phase 4: clay, Phase 3 forms against LOD0, LOD1, LOD2 (front, side, game camera)",
            fill="black", font=font(16))
    for k, ((label, _), r) in enumerate(zip(rows, grid)):
        y = 40 + k * (H + 16)
        dr.text((12, y + H // 2), label, fill="black", font=font(14))
        x = 230
        for t in r:
            sheet.paste(t, (x, y))
            x += t.width + 16
    sheet.save(os.path.join(a.dir, "clay_compare.png"))
    # greyscale gameplay strip at the game camera (80 px tall), 1x and 4x
    gp = [(label, flat(os.path.join(d, "clay_gameplay_80px.png"))[0].convert("L")) for label, d in rows]
    cw = max(g.width for _, g in gp) * 4 + 30
    strip = Image.new("L", (len(gp) * cw + 20, max(g.height for _, g in gp) * 4 + 140), 255)
    dr = ImageDraw.Draw(strip)
    dr.text((10, 8), "Game camera (az 30, el 45, 35 mm), 80 px tall, greyscale: 1x above, 4x nearest below",
            fill=0, font=font(14))
    for k, (label, g) in enumerate(gp):
        x = 10 + k * cw
        strip.paste(g, (x, 34))
        strip.paste(g.resize((g.width * 4, g.height * 4), Image.NEAREST), (x, 40 + g.height))
        dr.text((x, 50 + g.height * 5), label, fill=0, font=font(13))
    strip.save(os.path.join(a.dir, "gameplay_strip_grey.png"))
    print(json.dumps(out, indent=1))


if __name__ == "__main__":
    main()
