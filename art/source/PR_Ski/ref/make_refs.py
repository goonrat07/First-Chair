"""Dimensioned reference drawing for PR_Ski, written as clean orthographic mattes.

The numbers are typical published all-mountain ski dimensions (no specific product):
177 cm long, 133 / 92 / 118 mm at tip / waist / tail, a rockered tip and a small tail kick,
17 mm thick underfoot. Binding blocks are generic alpine toe and heel pieces.
Coordinates: s = distance from the tip end along the ski (m), z = height above the base plane (m).

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
TOE = (0.75, 0.84, 0.070, 0.062)   # s from, s to, width, top height above base
HEEL = (1.09, 1.20, 0.075, 0.085)


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
    top_z = max(max(base_z(s) + thickness(s) for s in samples), TOE[3], HEEL[3])
    w, h = int(L * ppm) + 2 * pad, int(top_z * ppm) + 2 * pad
    img = Image.new("RGB", (w, h), "white")
    d = ImageDraw.Draw(img)
    ground_row = h - pad
    axis_col = pad + S_BOOT * ppm                         # world y = 0 at the boot centre
    def px(s, z):
        return (pad + s * ppm, ground_row - z * ppm)
    outline = [px(s, base_z(s) + thickness(s)) for s in samples] + [px(s, base_z(s)) for s in reversed(samples)]
    d.polygon(outline, fill="black")
    for s0, s1, _, ztop in (TOE, HEEL):
        d.rectangle([px(s0, ztop), px(s1, 0.0)], fill="black")
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
    finish(img, os.path.join(HERE, "top_matte.png"))
    top = {"file": "top_matte.png", "m_per_px": SS / ppm, "image_px": [tw // SS, th // SS]}

    with open(os.path.join(HERE, "refs.json"), "w") as f:
        json.dump({"source": "typical published all-mountain ski dimensions; no specific product",
                   "length_m": L, "widths_m": [W_TIP, W_WAIST, W_TAIL], "boot_centre_from_tip_m": S_BOOT,
                   "side": side, "top": top}, f, indent=2)
    print(json.dumps({"side": side, "top": top}, indent=1))


if __name__ == "__main__":
    main()
