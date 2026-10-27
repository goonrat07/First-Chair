"""Dimensioned reference drawing for PR_SkiPole, written as a clean orthographic matte.

Typical alpine pole for a 1.8 m skier (no specific product): 1.20 m from tip to grip top,
aluminium shaft 18 mm at the top tapering to 12 mm, 65 mm piste basket 8 cm above the tip,
moulded grip about 20 cm long. The pole is round, so front and side views are the same. Story 012 (the chase camera
shows the poles larger): a finger ridge at the bottom of the grip, a rounder knob, and a basket with a raised ring.
Profile: (radius, height above the tip) in metres, bottom to top.

  python3 make_refs.py      -> front_matte.png, refs.json (in this folder)
"""
import json, os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
PROFILE = [
    (0.000, 0.000), (0.004, 0.000), (0.006, 0.020),     # carbide tip
    (0.006, 0.070), (0.011, 0.072), (0.011, 0.077),     # ferrule up to the basket hub
    (0.024, 0.078),                                     # basket disc, underside
    (0.026, 0.074), (0.0325, 0.075), (0.0325, 0.086),   # its raised outer ring (story 012: a ringed basket)
    (0.026, 0.088), (0.024, 0.083),                     #   ring top, back down to the disc
    (0.011, 0.084), (0.009, 0.092),                     # hub top
    (0.007, 0.094),                                     # shaft bottom (14 mm with the ferrule)
    (0.009, 1.000),                                     # shaft top (18 mm)
    (0.013, 1.000), (0.021, 1.022), (0.021, 1.030),     # grip: the finger ridge at its bottom (story 012),
    (0.016, 1.036), (0.0175, 1.060), (0.0175, 1.150),   #   just under the mitten, then the grip in the fist
    (0.019, 1.168), (0.021, 1.182), (0.019, 1.196),     # rounded knob, above the mitten
    (0.010, 1.200), (0.000, 1.200),                     # grip top
]
SS = 4


def main():
    ppm, pad = 1000.0 * SS, 40 * SS
    H = max(z for _, z in PROFILE)
    R = max(r for r, _ in PROFILE)
    w, h = int(2 * R * ppm) + 2 * pad, int(H * ppm) + 2 * pad
    img = Image.new("RGB", (w, h), "white")
    d = ImageDraw.Draw(img)
    cx, ground = w / 2, h - pad
    right = [(cx + r * ppm, ground - z * ppm) for r, z in PROFILE]
    left = [(cx - r * ppm, ground - z * ppm) for r, z in reversed(PROFILE)]
    d.polygon(right + left, fill="black")
    small = img.convert("L").resize((w // SS, h // SS), Image.BOX)
    small.point(lambda v: 0 if v < 128 else 255).convert("RGB").save(os.path.join(HERE, "front_matte.png"))
    front = {"file": "front_matte.png", "az": 0, "m_per_px": SS / ppm, "axis_col": cx / SS,
             "ground_row": ground / SS, "image_px": [w // SS, h // SS], "subject_height_m": H}
    with open(os.path.join(HERE, "refs.json"), "w") as f:
        json.dump({"source": "typical alpine pole dimensions for a 1.8 m skier; no specific product",
                   "profile_r_z_m": PROFILE, "front": front}, f, indent=2)
    print(json.dumps(front))


if __name__ == "__main__":
    main()
