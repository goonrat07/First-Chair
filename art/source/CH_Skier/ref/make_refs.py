"""Orthographic reference sheet for CH_Skier, drawn from our own proportion table.

Not art: the measuring stick the blockout and forms gates check against. The figure is a set of
simple 3D primitives (tapered capsules, rounded boxes, ellipsoids) projected orthographically, so the
front and side views agree by construction. Every number is in the table P below. None is measured
from a photo or a screenshot; asset-brief.md lists them all as inferred. The look follows
art/reference/style-notes-gma2.md (our notes, no copyrighted image is used or stored).

  python3 make_refs.py  -> front.png, side.png      flat colour views (REF planes in the master)
                           front_matte.png, side_matte.png   silhouettes for world_gate.py (4x supersampled)
                           sheet.png                 both views with a ruler and key heights, for review
                           refs.json                 scale, axis column, ground row, table, palette

Coordinates: metres, Z up, the figure faces -Y, origin on the ground between the feet.
Front view: az 0, camera at -Y, image right = world +X (the figure's left).
Side view:  az 90, camera at +X, image right = world +Y (the figure's back), so it faces image left.
"""
import json, math, os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SS = 4                                  # draw at 4x, downsample (FIRST-CHAIR.md, Lessons)
PPM = 400.0                             # pixels per metre in every delivered image
PAD_TOP, PAD_BOTTOM = 0.10, 0.10        # metres of margin above the helmet and below the soles
HALF_W = {"front": 0.95, "side": 0.45}  # half width of each view's world window; the axis is the centre

# PROPORTION TABLE (metres, degrees). All inferred: a stylised adult 1.75 m tall in boots, helmet and
# mittens about 15 percent oversized, boxy jacket, baggy trousers, relaxed A-pose.
# Points are for the figure's left side (+X); the right side mirrors them.
P = {
    "height": 1.75,
    # head: helmet, goggles, neck warmer, hood collar
    "helmet_centre": [0.0, 0.01, 1.60], "helmet_radii": [0.13, 0.155, 0.15], "helmet_rim_z": 1.535, "helmet_rim_tilt": 0.125,
    "helmet_panel_z": 1.69,
    "goggle_centre": [0.0, -0.135, 1.60], "goggle_size": [0.22, 0.06, 0.085], "goggle_r": 0.03,
    "lens_centre": [0.0, -0.166, 1.60], "lens_size": [0.19, 0.012, 0.06], "lens_r": 0.02,
    "strap_z": [1.578, 1.622],
    # neck warmer lowered to a band around the neck and collar, hood bunched behind the neck, lower face and
    # jaw showing under the goggles (user, 2026-10-20; second pass after the forms review)
    "neck_warmer_centre": [0.0, 0.02, 1.4075], "neck_warmer_size": [0.26, 0.25, 0.045], "neck_warmer_r": 0.02,
    "collar_centre": [0.0, 0.03, 1.40], "collar_size": [0.22, 0.21, 0.05], "collar_r": 0.03,
    "hood_centre": [0.0, 0.10, 1.465], "hood_radii": [0.14, 0.09, 0.045],
    "head_centre": [0.0, -0.02, 1.54], "head_radii": [0.088, 0.095, 0.10],
    "jaw_centre": [0.0, -0.06, 1.47], "jaw_size": [0.13, 0.09, 0.07], "jaw_r": 0.025,
    "chin_centre": [0.0, -0.09, 1.455], "chin_size": [0.085, 0.05, 0.045], "chin_r": 0.015,
    "nose_centre": [0.0, -0.118, 1.525], "nose_size": [0.034, 0.05, 0.05], "nose_r": 0.01,
    "mouth_centre": [0.0, -0.105, 1.487], "mouth_size": [0.045, 0.008, 0.009],
    # jacket
    "torso_centre": [0.0, 0.02, 1.13], "torso_size": [0.46, 0.30, 0.62], "torso_r": 0.07,
    "hem_centre": [0.0, 0.02, 0.88], "hem_size": [0.48, 0.32, 0.12], "hem_r": 0.04,
    "shoulder_joint": [0.18, 0.02, 1.36], "shoulder_r": 0.085,
    "zip_size": [0.025, 0.01, 0.58], "chest_stripe_x": 0.08, "chest_stripe_z": 1.30,
    "chest_stripe_size": [0.025, 0.01, 0.16],
    # arms (A-pose)
    "arm_angle_deg": 40.0, "upper_arm_len": 0.29, "forearm_len": 0.26,
    "sleeve_r": [0.085, 0.075, 0.066],          # shoulder, elbow, sleeve end
    "forearm_end": -0.03,                       # the sleeve ends this far before the wrist (along the arm)
    "cuff": [-0.04, 0.02, 0.06],                # from, to (along the arm from the wrist), radius; flat ends
    "mitten_offset": 0.10, "mitten_size": [0.19, 0.12, 0.085], "mitten_r": 0.03,   # length, width, thickness
    "thumb": [-0.04, -0.02, -0.085, 0.025],     # along-arm start, along-arm end, y end, radius
    "sleeve_stripe_r": 0.014,
    # trousers
    "pelvis_centre": [0.0, 0.03, 0.86], "pelvis_size": [0.38, 0.28, 0.18], "pelvis_r": 0.07,
    "hip_joint": [0.10, 0.03, 0.88], "knee": [0.12, 0.0, 0.50], "trouser_hem": [0.13, 0.01, 0.29],
    "thigh_r": [0.105, 0.095], "shin_r": [0.095, 0.10],
    "knee_patch_centre": [0.12, -0.085, 0.51], "knee_patch_radii": [0.065, 0.02, 0.075],
    # boots (sole at z = 0, ski socket under the boot centre)
    "boot_x": 0.13,
    "boot_foot_centre": [-0.03, 0.065], "boot_foot_size": [0.115, 0.32, 0.13], "boot_foot_r": 0.03,
    "boot_shaft_centre": [0.02, 0.19], "boot_shaft_size": [0.12, 0.16, 0.30], "boot_shaft_r": 0.04,
    "cuff_band_centre": [0.02, 0.26], "cuff_band_size": [0.13, 0.17, 0.06], "cuff_band_r": 0.02,
}

# PALETTE: the user's "First Chair" outfit (2026-10-20), replacing the GMA2-derived colours. Entries marked
# "placeholder" are my picks for colours the user did not set, for the art-director or the user to confirm.
U = "user choice 2026-10-20"
PALETTE = {
    "jacket": ("#F2B705", U), "stripe": ("#22304A", U), "cuff": ("#22304A", U), "mitten": ("#D7263D", U),
    "trousers": ("#22304A", U), "knee_patch": ("#18223A", U), "boot": ("#2B2F38", U), "boot_band": ("#D7263D", U),
    "helmet": ("#2C3A57", U), "helmet_panel": ("#4A5B7D", U), "goggle_frame": ("#15181E", U),
    "goggle_lens": ("#E2557A", U), "neck_warmer": ("#D7263D", U),
    "skin": ("#D9A07A", "placeholder: light-medium skin tone"),
    "mouth": ("#9A5A4E", "placeholder: mouth, a darker skin tone"),
}


def add(a, b):
    return [a[i] + b[i] for i in range(3)]


def mul(a, s):
    return [x * s for x in a]


AXES = ([1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0])


def cap(p0, p1, r0, r1, col, zmin=None):
    return {"kind": "cap", "p": [p0, p1], "r": [r0, r1], "col": col, "zmin": zmin, "zmax": None}


def cyl(p0, p1, r0, r1, col):
    """Tapered cylinder with flat ends."""
    return {"kind": "cyl", "p": [p0, p1], "r": [r0, r1], "col": col, "zmin": None, "zmax": None}


def box(c, size, r, col, axes=AXES):
    return {"kind": "box", "c": c, "size": size, "r": r, "axes": axes, "col": col, "zmin": None, "zmax": None}


def ell(c, radii, col, zmin=None, zmax=None):
    return {"kind": "ell", "c": c, "radii": radii, "col": col, "zmin": zmin, "zmax": zmax}


def arm_frame(side):
    """Shoulder, elbow, wrist, arm direction and the 'outside of the arm' normal for side +1 (left) or -1."""
    a = math.radians(P["arm_angle_deg"])
    d = [side * math.cos(a), 0.0, -math.sin(a)]
    n_out = [side * math.sin(a), 0.0, math.cos(a)]
    sj = P["shoulder_joint"]
    S = [side * sj[0], sj[1], sj[2]]
    E = add(S, mul(d, P["upper_arm_len"]))
    W = add(E, mul(d, P["forearm_len"]))
    return S, E, W, d, n_out


def arm(side):
    S, E, W, d, n_out = arm_frame(side)
    r = P["sleeve_r"]
    c0, c1, cr = P["cuff"]
    M = add(W, mul(d, P["mitten_offset"]))
    t0, t1, ty, tr = P["thumb"]
    stripe_off = mul(n_out, r[0] * 0.65)
    return [
        cap(S, E, r[0], r[1], "jacket"),
        cap(add(add(S, mul(d, 0.04)), stripe_off), add(E, mul(n_out, r[1] * 0.65)), P["sleeve_stripe_r"],
            P["sleeve_stripe_r"], "stripe"),
        cyl(E, add(W, mul(d, P["forearm_end"])), r[1], r[2], "jacket"),
        cyl(add(W, mul(d, c0)), add(W, mul(d, c1)), cr, cr, "cuff"),
        box(M, P["mitten_size"], P["mitten_r"], "mitten", axes=(d, [0.0, 1.0, 0.0], [-n_out[0], 0.0, -n_out[2]])),
        cap(add(M, mul(d, t0)), add(add(M, mul(d, t1)), [0.0, ty, 0.0]), tr, tr, "mitten"),
    ]


def leg(side):
    def m(p):
        return [side * p[0], p[1], p[2]]
    bx, (fy, fz), (sy, sz), (by, bz) = side * P["boot_x"], P["boot_foot_centre"], P["boot_shaft_centre"], P["cuff_band_centre"]
    return [
        box([bx, fy, fz], P["boot_foot_size"], P["boot_foot_r"], "boot"),
        box([bx, sy, sz], P["boot_shaft_size"], P["boot_shaft_r"], "boot"),
        box([bx, by, bz], P["cuff_band_size"], P["cuff_band_r"], "boot_band"),
        cap(m(P["hip_joint"]), m(P["knee"]), P["thigh_r"][0], P["thigh_r"][1], "trousers"),
        cap(m(P["knee"]), m(P["trouser_hem"]), P["shin_r"][0], P["shin_r"][1], "trousers", zmin=P["trouser_hem"][2]),
        ell(m(P["knee_patch_centre"]), P["knee_patch_radii"], "knee_patch"),
    ]


def torso():
    sj = P["shoulder_joint"]
    zc = [0.0, P["torso_centre"][1] - P["torso_size"][1] / 2 - P["zip_size"][1] / 2 + 0.005, P["torso_centre"][2]]
    out = [
        box(P["pelvis_centre"], P["pelvis_size"], P["pelvis_r"], "trousers"),
        box(P["hem_centre"], P["hem_size"], P["hem_r"], "jacket"),
        box(P["torso_centre"], P["torso_size"], P["torso_r"], "jacket"),
        cap([-sj[0], sj[1], sj[2]], [sj[0], sj[1], sj[2]], P["shoulder_r"], P["shoulder_r"], "jacket"),
        box(zc, P["zip_size"], 0.0, "stripe"),
    ]
    for s in (-1, 1):
        out.append(box([s * P["chest_stripe_x"], zc[1], P["chest_stripe_z"]], P["chest_stripe_size"], 0.0, "stripe"))
    for p in out[4:]:                         # zip and chest stripes: flat decals, drawn in the front view only
        p["front_only"] = True
    return out


def head(view):
    hc, hr = P["helmet_centre"], P["helmet_radii"]
    strap_r = [hr[0] + 0.004, hr[1] + 0.004, hr[2] + 0.004]
    hood = ell(P["hood_centre"], P["hood_radii"], "jacket")
    collar = box(P["collar_centre"], P["collar_size"], P["collar_r"], "jacket")
    band = box(P["neck_warmer_centre"], P["neck_warmer_size"], P["neck_warmer_r"], "neck_warmer")
    face = ell(P["head_centre"], P["head_radii"], "skin")
    jaw = box(P["jaw_centre"], P["jaw_size"], P["jaw_r"], "skin")
    chin = box(P["chin_centre"], P["chin_size"], P["chin_r"], "skin")
    nose = box(P["nose_centre"], P["nose_size"], P["nose_r"], "skin")
    mouth = box(P["mouth_centre"], P["mouth_size"], 0.0, "mouth")
    mouth["front_only"] = True
    shell = ell(hc, hr, "helmet", zmin=P["helmet_rim_z"] - P["helmet_rim_tilt"] * 0.11)  # front view: rim near the back
    shell["tilt"] = (P["helmet_rim_tilt"], P["helmet_rim_z"])
    helmet = [shell,
              ell(hc, strap_r, "goggle_frame", zmin=P["strap_z"][0], zmax=P["strap_z"][1]),
              ell(hc, hr, "helmet_panel", zmin=P["helmet_panel_z"])]
    goggles = [box(P["goggle_centre"], P["goggle_size"], P["goggle_r"], "goggle_frame"),
               box(P["lens_centre"], P["lens_size"], P["lens_r"], "goggle_lens")]
    # far first: hood behind the neck, collar rim, head, the band in front of the neck, chin over the band
    return [hood, collar, face, band, jaw, chin, nose, mouth] + helmet + goggles


def figure(view):
    """Primitives in painter's order for one view (far first)."""
    if view == "front":
        order = [leg(-1), leg(1), torso(), arm(-1), arm(1), head(view)]
    else:                                     # camera at +X: the right side (-X) is far
        order = [arm(-1), leg(-1), leg(1), torso(), head(view), arm(1)]
    return [p for group in order for p in group if view == "front" or not p.get("front_only")]


# 2D GEOMETRY (u = horizontal world coordinate of the view, z = height)

def proj(p, view):
    return (p[0] if view == "front" else p[1], p[2])


def disc(c, r, n=48):
    if r <= 0:
        return [c]
    return [(c[0] + r * math.cos(2 * math.pi * i / n), c[1] + r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def hull(pts):
    pts = sorted(set(pts))
    if len(pts) < 3:
        return pts
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, hi = [], []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(hi) >= 2 and cross(hi[-2], hi[-1], p) <= 0:
            hi.pop()
        hi.append(p)
    return lo[:-1] + hi[:-1]


def clip(poly, z0, keep_above):
    """Sutherland-Hodgman against the horizontal line z = z0."""
    out = []
    for i in range(len(poly)):
        a, b = poly[i - 1], poly[i]
        ina = a[1] >= z0 if keep_above else a[1] <= z0
        inb = b[1] >= z0 if keep_above else b[1] <= z0
        if inb != ina:
            t = (z0 - a[1]) / (b[1] - a[1])
            out.append((a[0] + t * (b[0] - a[0]), z0))
        if inb:
            out.append(b)
    return out


def clip_tilt(poly, k, z0):
    """Keep the part above the line z = z0 - k * u (side view, u = world y): the rim is higher at the front."""
    out = []
    for i in range(len(poly)):
        a, b = poly[i - 1], poly[i]
        fa, fb = a[1] + k * a[0] - z0, b[1] + k * b[0] - z0
        if (fa >= 0) != (fb >= 0):
            t = fa / (fa - fb)
            out.append((a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])))
        if fb >= 0:
            out.append(b)
    return out


def outline(prim, view):
    """Exact orthographic outline of a primitive as a convex polygon (sampled), in (u, z) metres."""
    if prim["kind"] == "cap":
        pts = disc(proj(prim["p"][0], view), prim["r"][0]) + disc(proj(prim["p"][1], view), prim["r"][1])
    elif prim["kind"] == "cyl":               # end circles sampled in 3D, then projected
        p0, p1 = prim["p"]
        ax = [p1[i] - p0[i] for i in range(3)]
        ln = math.sqrt(sum(v * v for v in ax))
        ax = [v / ln for v in ax]
        ref = [0.0, 1.0, 0.0] if abs(ax[1]) < 0.9 else [1.0, 0.0, 0.0]
        u = [ax[1] * ref[2] - ax[2] * ref[1], ax[2] * ref[0] - ax[0] * ref[2], ax[0] * ref[1] - ax[1] * ref[0]]
        un = math.sqrt(sum(v * v for v in u))
        u = [v / un for v in u]
        w = [ax[1] * u[2] - ax[2] * u[1], ax[2] * u[0] - ax[0] * u[2], ax[0] * u[1] - ax[1] * u[0]]
        pts = []
        for c, r in ((p0, prim["r"][0]), (p1, prim["r"][1])):
            for i in range(64):
                t = 2 * math.pi * i / 64
                pts.append(proj(add(c, add(mul(u, r * math.cos(t)), mul(w, r * math.sin(t)))), view))
    elif prim["kind"] == "box":
        r = prim["r"]
        h = [max(0.0, s / 2 - r) for s in prim["size"]]
        ex, ey, ez = prim["axes"]
        pts = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    corner = add(add(add(prim["c"], mul(ex, sx * h[0])), mul(ey, sy * h[1])), mul(ez, sz * h[2]))
                    pts += disc(proj(corner, view), r)
    else:
        c = proj(prim["c"], view)
        a, b = prim["radii"][0 if view == "front" else 1], prim["radii"][2]
        pts = [(c[0] + a * math.cos(2 * math.pi * i / 96), c[1] + b * math.sin(2 * math.pi * i / 96)) for i in range(96)]
    poly = hull(pts)
    zmin = prim["zmin"]
    if prim.get("tilt") and view == "side":
        poly = clip_tilt(poly, *prim["tilt"])
        zmin = None
    if zmin is not None:
        poly = clip(poly, zmin, True)
    if prim["zmax"] is not None:
        poly = clip(poly, prim["zmax"], False)
    return poly


def draw_view(view, matte):
    hw = HALF_W[view]
    top = P["height"] + PAD_TOP
    w, h = int(round(2 * hw * PPM)) * SS, int(round((top + PAD_BOTTOM) * PPM)) * SS
    img = Image.new("RGB", (w, h), "white")
    d = ImageDraw.Draw(img)
    for prim in figure(view):
        poly = outline(prim, view)
        if len(poly) < 3:
            continue
        px = [((u + hw) * PPM * SS, (top - z) * PPM * SS) for u, z in poly]
        d.polygon(px, fill="black" if matte else PALETTE[prim["col"]][0])
    if matte:                                 # box filter + 50 percent threshold (PR_Ski lesson)
        small = img.convert("L").resize((w // SS, h // SS), Image.BOX)
        return small.point(lambda v: 0 if v < 128 else 255).convert("RGB")
    return img.resize((w // SS, h // SS), Image.LANCZOS)


def font(size):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


def key_measurements():
    """Key heights and widths of the reference figure (metres), from the table."""
    S, E, W, d, n_out = arm_frame(1)
    M = add(W, mul(d, P["mitten_offset"]))
    hs = P["mitten_size"]
    tip = add(M, mul(d, hs[0] / 2))
    rr = P["mitten_r"]
    tip_x = M[0] + abs(d[0]) * (hs[0] / 2 - rr) + abs(n_out[0]) * (hs[2] / 2 - rr) + rr
    return {
        "total_height": P["height"],
        "goggle_line_z": P["goggle_centre"][2],
        "helmet_rim_z": P["helmet_rim_z"],
        "helmet_width": 2 * P["helmet_radii"][0], "helmet_depth": 2 * P["helmet_radii"][1],
        "helmet_height_rim_to_top": round(P["height"] - P["helmet_rim_z"], 3),
        "neck_warmer_z": [P["neck_warmer_centre"][2] - P["neck_warmer_size"][2] / 2,
                          P["neck_warmer_centre"][2] + P["neck_warmer_size"][2] / 2],
        "shoulder_top_z": round(P["shoulder_joint"][2] + P["shoulder_r"], 3),
        "shoulder_joint": P["shoulder_joint"],
        "shoulder_width": round(2 * (P["shoulder_joint"][0] + P["shoulder_r"]), 3),
        "jacket_width": P["torso_size"][0], "jacket_depth": P["torso_size"][1],
        "jacket_hem_z": round(P["hem_centre"][2] - P["hem_size"][2] / 2, 3),
        "elbow": [round(v, 3) for v in E], "wrist": [round(v, 3) for v in W],
        "palm_centre": [round(v, 3) for v in M],
        "fingertip_span": round(2 * tip_x, 3),
        "fingertip_z": round(tip[2], 3),
        "seat_width": P["pelvis_size"][0],
        "crotch_z": round(P["pelvis_centre"][2] - P["pelvis_size"][2] / 2, 3),
        "hip_joint": P["hip_joint"], "knee": P["knee"],
        "trouser_hem_z": P["trouser_hem"][2],
        "boot_band_z": [P["cuff_band_centre"][1] - P["cuff_band_size"][2] / 2,
                        P["cuff_band_centre"][1] + P["cuff_band_size"][2] / 2],
        "boot_length": P["boot_foot_size"][1], "boot_width": P["boot_foot_size"][0],
        "boot_centres_apart": 2 * P["boot_x"],
        "boot_centre_y": P["boot_foot_centre"][0],
    }


def main():
    info = {}
    views = {}
    for view, az in (("front", 0), ("side", 90)):
        colour, matte = draw_view(view, False), draw_view(view, True)
        colour.save(os.path.join(HERE, view + ".png"))
        matte.save(os.path.join(HERE, view + "_matte.png"))
        views[view] = colour
        info[view] = {"file": view + ".png", "matte": view + "_matte.png", "az": az, "m_per_px": 1.0 / PPM,
                      "axis_col": colour.width / 2.0, "ground_row": (P["height"] + PAD_TOP) * PPM,
                      "image_px": [colour.width, colour.height]}
        bb = matte.convert("L").point(lambda v: 255 - v).getbbox()   # measured extent of the silhouette
        info[view]["matte_extent_m"] = {"horizontal": [round((bb[0] - colour.width / 2.0) / PPM, 4),
                                                       round((bb[2] - colour.width / 2.0) / PPM, 4)],
                                        "top_z": round((P["height"] + PAD_TOP) - bb[1] / PPM, 4)}
    H = views["front"].height
    ground_frac = PAD_BOTTOM * PPM / H
    info["init_master"] = {"height": P["height"], "ground_frac": round(ground_frac, 6),
                           "subject_frac": round(ground_frac + P["height"] * PPM / H, 6)}

    # review sheet: ruler, key heights, both views
    km = key_measurements()
    ruler_w, gap, head_h = 150, 40, 70
    sw = ruler_w + views["front"].width + gap + views["side"].width + 20
    sheet = Image.new("RGB", (sw, H + head_h + 40), "white")
    d = ImageDraw.Draw(sheet)
    g0 = head_h + info["front"]["ground_row"]
    lines = [("helmet top", km["total_height"]), ("goggles", km["goggle_line_z"]),
             ("shoulder", km["shoulder_top_z"]), ("jacket hem", km["jacket_hem_z"]),
             ("knee", P["knee"][2]), ("trouser hem", km["trouser_hem_z"]), ("ground", 0.0)]
    for label, z in lines:
        y = g0 - z * PPM
        d.line([(ruler_w - 10, y), (sw - 10, y)], fill=(215, 215, 222), width=1)
        d.text((4, y - 9), "%s %.2f" % (label, z), fill=(90, 90, 100), font=font(14))
    sheet.paste(views["front"], (ruler_w, head_h), None)
    sheet.paste(views["side"], (ruler_w + views["front"].width + gap, head_h), None)
    for lbl, z in lines:                      # redraw guides faintly over the white background only
        y = int(g0 - z * PPM)
        for x in range(ruler_w - 10, sw - 10, 6):
            if sheet.getpixel((x, y)) == (255, 255, 255):
                sheet.putpixel((x, y), (200, 200, 210))
    for i in range(0, 8):                     # metre ruler, ticks every 0.25 m
        z = i * 0.25
        y = g0 - z * PPM
        d.line([(ruler_w - 30, y), (ruler_w - 14, y)], fill="black", width=2)
    d.line([(ruler_w - 22, g0), (ruler_w - 22, g0 - 1.75 * PPM)], fill="black", width=2)
    d.text((10, 8), "CH_Skier reference: our proportion table (refs.json), not a measured image. "
           "%d px/m, A-pose 40 deg, faces -Y" % PPM, fill="black", font=font(18))
    d.text((ruler_w, 36), "FRONT (az 0, image right = +X = figure's left)", fill="black", font=font(16))
    d.text((ruler_w + views["front"].width + gap, 36), "SIDE (az 90, faces left)", fill="black", font=font(16))
    sheet.save(os.path.join(HERE, "sheet.png"))

    with open(os.path.join(HERE, "refs.json"), "w") as f:
        json.dump({"source": "our own proportion table for a stylised adult skier (all inferred); look from "
                             "art/reference/style-notes-gma2.md; no external image used",
                   "px_per_m": PPM, "m_per_px": 1.0 / PPM, "supersample": SS,
                   "front": info["front"], "side": info["side"], "init_master": info["init_master"],
                   "key_measurements_m": km, "proportions_m": P,
                   "palette": {k: {"hex": v[0], "source": v[1]} for k, v in PALETTE.items()}}, f, indent=1)
    print(json.dumps({"front": info["front"], "side": info["side"], "init_master": info["init_master"],
                      "key": km}, indent=1))


if __name__ == "__main__":
    main()
