"""Dimensioned reference drawing for PR_Ski, written as clean orthographic mattes.

The numbers are typical published all-mountain ski dimensions (no specific product):
177 cm long, 133 / 92 / 118 mm at tip / waist / tail, a rockered tip and a small tail kick,
17 mm thick underfoot. Binding blocks are generic alpine toe and heel pieces.
Coordinates: s = distance from the tip end along the ski (m), z = height above the base plane (m).
Story 012 (the chase camera shows the skis behind the boots): shaped bindings replace the two blocks. BINDINGS below
is the design, as data: refs.json carries it, 03_forms.py builds from it and the mattes are drawn from it.

  python3 make_refs.py      -> side_matte.png, top_matte.png, refs.json (in this folder)
"""
import json, math, os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
L = 1.77            # length
W_TIP, W_WAIST, W_TAIL = 0.133, 0.092, 0.118
S_TIP_WIDE = 0.15   # widest point of the shovel, from the tip end
S_BOOT = 0.965      # boot centre (and waist), about 8 cm behind the middle
S_TAIL_WIDE = 1.69  # widest point of the tail
ROCKER_LEN, ROCKER_H = 0.30, 0.060
KICK_LEN, KICK_H = 0.15, 0.030
T_FOOT, T_END = 0.017, 0.007
# BINDINGS (story 012; inferred from generic alpine bindings, no specific product). Each part is
#   ("loft", role, [(s, half width, z bottom, z top, top chamfer), ...]) along the ski, or
#   ("box", role, (s0, s1, x0, x1, z0, z1), mirrored) with x across the ski (mirrored: also at -x), or
#   ("bar", role, ((s0, z0), (s1, z1)), half width, thickness), a slab between two points in the s-z plane.
# Roles: "binding" (dark plastic) or "accent" (light grey). Loft faces on the top between the stations listed in
# ACCENT_TOP take the accent: the toe's top cap. The skier's boot toe is at s 0.805 and its heel at 1.125
# (CH_Skier, boot 0.32 long on SOCKET_boot), so each piece reaches a little under the boot, as a real jaw does.
# The heel stays within the old block's 0.085 m height, so the ski's size is unchanged.
SKI_TOP = 0.017
BINDINGS = {
    "toe": [
        ("box", "binding", (0.742, 0.852, -0.033, 0.033, SKI_TOP, 0.026), False),                 # base plate
        ("loft", "binding", [(0.748, 0.016, SKI_TOP, 0.029, 0.004), (0.760, 0.026, SKI_TOP, 0.044, 0.008),
                             (0.775, 0.031, SKI_TOP, 0.056, 0.010), (0.790, 0.033, SKI_TOP, 0.062, 0.010),
                             (0.808, 0.033, SKI_TOP, 0.062, 0.010), (0.814, 0.030, SKI_TOP, 0.050, 0.008)]),
        ("box", "binding", (0.796, 0.818, 0.030, 0.040, 0.040, 0.066), True),                      # jaw wings
    ],
    "heel": [
        ("box", "binding", (1.100, 1.222, -0.034, 0.034, SKI_TOP, 0.026), False),                 # base plate
        ("loft", "binding", [(1.112, 0.028, SKI_TOP, 0.052, 0.008), (1.124, 0.034, SKI_TOP, 0.080, 0.010),
                             (1.145, 0.036, SKI_TOP, 0.085, 0.012), (1.172, 0.035, SKI_TOP, 0.084, 0.012),
                             (1.195, 0.031, SKI_TOP, 0.070, 0.010), (1.210, 0.025, SKI_TOP, 0.048, 0.008)]),
        ("bar", "accent", ((1.180, 0.080), (1.238, 0.058)), 0.020, 0.008),                          # step-in lever
        ("box", "binding", (1.128, 1.144, 0.030, 0.0535, 0.0195, 0.0275), True),                  # brake root
        ("box", "binding", (1.128, 1.262, 0.048, 0.054, 0.019, 0.026), True),                      # brake arm
        ("box", "binding", (1.252, 1.285, 0.044, 0.057, SKI_TOP, 0.032), True),                    # brake pad
    ],
}
ACCENT_TOP = {"toe": (0.775, 0.808)}


def bar_corners(p0, p1, thick):
    """The bar's outline in the s-z plane: p0 to p1, thick across."""
    (s0, z0), (s1, z1) = p0, p1
    ds, dz = s1 - s0, z1 - z0
    n = (ds * ds + dz * dz) ** 0.5
    ns, nz = -dz / n * thick / 2, ds / n * thick / 2
    return [(s0 - ns, z0 - nz), (s1 - ns, z1 - nz), (s1 + ns, z1 + nz), (s0 + ns, z0 + nz)]


def binding_side(part):
    """A binding part's outline in the side view, as (s, z) points."""
    kind = part[0]
    if kind == "loft":
        sec = part[2]
        return [(s, zt) for s, _, _, zt, _ in sec] + [(s, zb) for s, _, zb, _, _ in reversed(sec)]
    if kind == "box":
        s0, s1, _, _, z0, z1 = part[2]
        return [(s0, z0), (s1, z0), (s1, z1), (s0, z1)]
    return bar_corners(part[2][0], part[2][1], part[4])


def binding_top(part):
    """A binding part's outlines in the top view, as lists of (s, x) points (two for mirrored boxes)."""
    kind = part[0]
    if kind == "loft":
        sec = part[2]
        return [[(s, hw) for s, hw, _, _, _ in sec] + [(s, -hw) for s, hw, _, _, _ in reversed(sec)]]
    if kind == "box":
        s0, s1, x0, x1, _, _ = part[2]
        out = [[(s0, x0), (s1, x0), (s1, x1), (s0, x1)]]
        if part[3]:
            out.append([(s0, -x0), (s1, -x0), (s1, -x1), (s0, -x1)])
        return out
    (s0, _), (s1, _) = part[2]
    hw = part[3]
    return [[(s0, -hw), (s1, -hw), (s1, hw), (s0, hw)]]


def binding_parts():
    return [p for piece in BINDINGS.values() for p in piece]


def half_width(s):
    if s < S_TIP_WIDE:                                   # rounded shovel
        u = (S_TIP_WIDE - s) / S_TIP_WIDE
        return W_TIP / 2 * math.sqrt(max(0.0, 1 - u * u))
    if s > S_TAIL_WIDE:                                  # rounded tail
        u = (s - S_TAIL_WIDE) / (L - S_TAIL_WIDE)
        return W_TAIL / 2 * math.sqrt(max(0.0, 1 - u * u))
    # sidecut: parabola through tip-wide, waist and tail-wide points
    x0, x1, x2 = S_TIP_WIDE, S_BOOT, S_TAIL_WIDE
    y0, y1, y2 = W_TIP / 2, W_WAIST / 2, W_TAIL / 2
    return (y0 * (s - x1) * (s - x2) / ((x0 - x1) * (x0 - x2)) +
            y1 * (s - x0) * (s - x2) / ((x1 - x0) * (x1 - x2)) +
            y2 * (s - x0) * (s - x1) / ((x2 - x0) * (x2 - x1)))


def base_z(s):
    if s < ROCKER_LEN:
        return ROCKER_H * ((ROCKER_LEN - s) / ROCKER_LEN) ** 2
    if s > L - KICK_LEN:
        return KICK_H * ((s - (L - KICK_LEN)) / KICK_LEN) ** 2
    return 0.0


def thickness(s):
    a, b, c, d = 0.15, 0.60, 1.30, 1.65
    if s <= a or s >= d:
        f = 0.0
    elif s < b:
        f = (s - a) / (b - a)
    elif s > c:
        f = (d - s) / (d - c)
    else:
        f = 1.0
    return T_END + (T_FOOT - T_END) * f


SS = 4   # supersampling: draw at 4x and downsample, so the polygon's outline pixels add no width


def finish(img, path):
    """Downsample a 4x drawing to its final size and threshold at 50 percent coverage."""
    small = img.convert("L").resize((img.width // SS, img.height // SS), Image.BOX)
    small.point(lambda v: 0 if v < 128 else 255).convert("RGB").save(path)


def main():
    ppm = 1200.0 * SS                                     # pixels per metre while drawing (1200 in the file)
    pad = 40 * SS
    samples = [L * i / 2000 for i in range(2001)]

    # SIDE (camera at +X looking -X; image right = world +Y = toward the tail)
    top_z = max(max(base_z(s) + thickness(s) for s in samples), max(z for p in binding_parts() for _, z in binding_side(p)))
    w, h = int(L * ppm) + 2 * pad, int(top_z * ppm) + 2 * pad
    img = Image.new("RGB", (w, h), "white")
    d = ImageDraw.Draw(img)
    ground_row = h - pad
    axis_col = pad + S_BOOT * ppm                         # world y = 0 at the boot centre
    def px(s, z):
        return (pad + s * ppm, ground_row - z * ppm)
    outline = [px(s, base_z(s) + thickness(s)) for s in samples] + [px(s, base_z(s)) for s in reversed(samples)]
    d.polygon(outline, fill="black")
    for part in binding_parts():
        d.polygon([px(s, z) for s, z in binding_side(part)], fill="black")
    finish(img, os.path.join(HERE, "side_matte.png"))
    side = {"file": "side_matte.png", "az": 90, "m_per_px": SS / ppm, "axis_col": axis_col / SS,
            "ground_row": ground_row / SS, "image_px": [w // SS, h // SS], "subject_height_m": top_z}

    # TOP (camera above looking down, image up = world +Y = tail at the top, tip at the bottom)
    tw, th = int(W_TIP * ppm) + 2 * pad, int(L * ppm) + 2 * pad
    img = Image.new("RGB", (tw, th), "white")
    d = ImageDraw.Draw(img)
    cx = tw / 2
    def tp(s, x):
        return (cx + x * ppm, th - pad - s * ppm)
    outline = [tp(s, half_width(s)) for s in samples] + [tp(s, -half_width(s)) for s in reversed(samples)]
    d.polygon(outline, fill="black")
    for part in binding_parts():                          # the brake arms and pads stand past the edges
        for poly in binding_top(part):
            d.polygon([tp(s, x) for s, x in poly], fill="black")
    finish(img, os.path.join(HERE, "top_matte.png"))
    top = {"file": "top_matte.png", "m_per_px": SS / ppm, "image_px": [tw // SS, th // SS]}

    with open(os.path.join(HERE, "refs.json"), "w") as f:
        json.dump({"source": "typical published all-mountain ski dimensions; no specific product",
                   "length_m": L, "widths_m": [W_TIP, W_WAIST, W_TAIL], "boot_centre_from_tip_m": S_BOOT,
                   "side": side, "top": top, "ski_top_m": SKI_TOP, "bindings": BINDINGS,
                   "accent_top": ACCENT_TOP}, f, indent=2)
    print(json.dumps({"side": side, "top": top}, indent=1))


if __name__ == "__main__":
    main()
