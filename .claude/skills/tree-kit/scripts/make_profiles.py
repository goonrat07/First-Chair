"""
MAKE PROFILES (tree-kit, Phase 1)
Draws the silhouette reference from the parameters: front (az 0), side (az 90) and top mattes, plus a
profile sheet for the user. These are our own drawings, so they are committed. Plain Python + Pillow.

python3 .claude/skills/tree-kit/scripts/make_profiles.py \
  --params art/source/EN_Conifer/EN_Conifer_params.json --out art/source/EN_Conifer/ref [--lod 0]

Each piece of the layout (one loft segment of a part) is projected and filled as its convex hull,
drawn at 4x and box-downsampled with a 50 percent threshold (FIRST-CHAIR.md lesson: Pillow's fill
includes its outline). refs.json records the world registration world_gate.py and tree_gate.py
need (m_per_px, axis_col, ground_row; top: x0 and y1, the world left and top edges) and the layout's
measured proportions.
"""
import argparse
import json
import math
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tree_params as TP  # noqa: E402

SS = 4
PROJ = {"front": lambda p: (p[0], p[2]), "side": lambda p: (p[1], p[2]), "top": lambda p: (p[0], p[1])}
AZ = {"front": 0, "side": 90}


def hull(pts):
    pts = sorted(set(pts))
    if len(pts) <= 2:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(up) >= 2 and cross(up[-2], up[-1], p) <= 0:
            up.pop()
        up.append(p)
    return lo[:-1] + up[:-1]


def pieces(parts):
    for part in parts:
        rings = part["rings"]
        for a, b in zip(rings, rings[1:]):
            yield a + b
        if part["apex"]:
            yield rings[-1] + [part["apex"]]


def snap(v, mpp):
    return math.ceil(v / mpp - 1e-9) * mpp


def draw(parts, view, mpp, margin):
    polys = [[PROJ[view](p) for p in pc] for pc in pieces(parts)]
    us = [u for pl in polys for u, _ in pl]
    vs = [v for pl in polys for _, v in pl]
    if view == "top":
        ext = snap(max(abs(min(us)), abs(max(us)), abs(min(vs)), abs(max(vs))) + margin, mpp)
        u0, v1, v0 = -ext, ext, -ext
    else:
        ext = snap(max(abs(min(us)), abs(max(us))) + margin, mpp)
        u0, v1, v0 = -ext, snap(max(vs) + margin, mpp), -snap(margin, mpp)
    W, H = int(round(2 * ext / mpp)), int(round((v1 - v0) / mpp))
    big = Image.new("L", (W * SS, H * SS), 255)
    d = ImageDraw.Draw(big)
    for pl in polys:
        h = hull(pl)
        if len(h) >= 3:
            d.polygon([((u - u0) / mpp * SS, (v1 - v) / mpp * SS) for u, v in h], fill=0)
    img = big.resize((W, H), Image.BOX).point(lambda x: 0 if x < 128 else 255)
    info = {"file": "%s_matte.png" % view, "m_per_px": mpp, "image_px": [W, H]}
    if view == "top":
        info.update({"x0": u0, "y1": v1})
    else:
        info.update({"az": AZ[view], "axis_col": round(-u0 / mpp, 3), "ground_row": round(v1 / mpp, 3)})
    return img, info


def sheet(imgs, infos, meas, title, path):
    """Human-readable sheet: the three mattes at one scale, 1 m ticks, the design ratios."""
    pad, mpp = 40, infos["front"]["m_per_px"]
    W = sum(i.size[0] for i in imgs.values()) + pad * 4 + 60
    H = max(i.size[1] for i in imgs.values()) + pad * 2 + 150
    out = Image.new("RGB", (W, H), (246, 248, 250))
    d = ImageDraw.Draw(out)
    x = pad + 60
    for view in ("front", "side", "top"):
        im = imgs[view]
        tint = Image.new("RGB", im.size, (24, 58, 40))
        out.paste(tint, (x, pad + 20), im.point(lambda v: 255 - v))
        d.text((x, pad), view + (" (az %d)" % AZ[view] if view in AZ else " (looking down, +Y up)"), fill=(20, 20, 20))
        if view == "front":
            gy = pad + 20 + infos[view]["ground_row"]
            for m in range(0, int(meas["height_m"]) + 2):
                y = gy - m / mpp
                d.line([(x - 18, y), (x - 6, y)], fill=(90, 90, 90))
                d.text((x - 52, y - 6), "%d m" % m, fill=(90, 90, 90))
            d.line([(x - 6, gy), (x + im.size[0] * 2 + pad, gy)], fill=(150, 150, 150))
        x += im.size[0] + pad
    r = meas["ratios"]
    lines = [title,
             "height %.2f m   crown width %.2f m   clear trunk to %.2f m   trunk base dia %.2f m   %d branches   %d tris"
             % (meas["height_m"], meas["crown_width_m"], meas["lowest_crown_z_m"], meas["trunk_base_diameter_m"],
                meas["branches"], meas["tris"]),
             "height : crown width %.2f   clear trunk %.0f%%   height : trunk dia %.0f   spike %.0f%% of height"
             % (r["height_to_crown_width"], 100 * r["clear_trunk_frac"], r["height_to_trunk_diameter"],
                100 * meas.get("spike_frac", 0))]
    for i, t in enumerate(lines):
        d.text((pad, H - 130 + i * 22), t, fill=(20, 20, 20))
    out.save(path)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--params", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--lod", type=int, default=0)
    p.add_argument("--variant", default=None)
    a = p.parse_args()
    P = TP.load(a.params)
    parts = TP.layout(P, a.lod, a.variant)
    mpp, margin = P["profile"]["m_per_px"], P["profile"]["margin_m"]
    os.makedirs(a.out, exist_ok=True)
    imgs, infos = {}, {}
    for view in ("front", "side", "top"):
        imgs[view], infos[view] = draw(parts, view, mpp, margin)
        imgs[view].save(os.path.join(a.out, infos[view]["file"]))
    meas = TP.measures(parts)
    name, _ = TP.variant_of(P, a.variant)
    refs = {"asset": P["asset"], "variant": name, "lod": a.lod, "params": os.path.basename(a.params),
            "drawn_by": "tree-kit make_profiles.py from the parameters (our own drawing)",
            "measures": meas, **infos}
    with open(os.path.join(a.out, "refs.json"), "w") as f:
        json.dump(refs, f, indent=2)
    sheet(imgs, infos, meas, "%s %s LOD%d profile, drawn from %s" % (P["asset"], name, a.lod,
          os.path.basename(a.params)), os.path.join(a.out, "profiles_sheet.png"))
    print("PROFILES", json.dumps({k: infos[k]["image_px"] for k in infos}), "MEASURES", json.dumps(meas))


if __name__ == "__main__":
    main()
