"""PR_Ski forms: exact sidecut, rocker, tail kick and thickness taper, plus the binding pieces.
Replaces the blockout. Built in HIGH; 04_topology makes the delivery mesh in LOW.

  $BLENDER_BIN --background --python build/03_forms.py -- --master PR_Ski_master.blend
"""
import math, os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402

T.PHASE = "03_forms"
T.OWNER_TAG = "phase:" + T.PHASE

# Measured from the dimension drawing (ref/refs.json, ref/top_matte.png, ref/side_matte.png)
L = 1.77                                  # length (top, side)
W_TIP, W_WAIST, W_TAIL = 0.133, 0.092, 0.118   # widths (top)
S_TIP_WIDE, S_BOOT, S_TAIL_WIDE = 0.15, 0.965, 1.69   # positions from the tip end (top)
ROCKER_LEN, ROCKER_H = 0.30, 0.060        # tip rise (side)
KICK_LEN, KICK_H = 0.15, 0.030            # tail kick (side)
T_FOOT, T_END = 0.017, 0.007              # thickness underfoot and at the ends (side)
TOE = (0.75, 0.84, 0.070, 0.062)          # s from, s to (side); width inferred; top height (side)
HEEL = (1.09, 1.20, 0.075, 0.085)

# Dense through the rocker and kick, where the side silhouette curves; ends 1 mm in from the tips
STATIONS = [0.001, 0.02, 0.05, 0.09, 0.12, 0.15, 0.185, 0.22, 0.26, 0.30, 0.45, 0.60, 0.78, 0.965,
            1.15, 1.30, 1.45, 1.55, 1.62, 1.66, 1.69, 1.72, 1.745, 1.769]


def half_width(s):
    if s < S_TIP_WIDE:
        u = (S_TIP_WIDE - s) / S_TIP_WIDE
        return W_TIP / 2 * math.sqrt(max(0.0, 1 - u * u))
    if s > S_TAIL_WIDE:
        u = (s - S_TAIL_WIDE) / (L - S_TAIL_WIDE)
        return W_TAIL / 2 * math.sqrt(max(0.0, 1 - u * u))
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
    f = 0.0 if (s <= a or s >= d) else (s - a) / (b - a) if s < b else (d - s) / (d - c) if s > c else 1.0
    return T_END + (T_FOOT - T_END) * f


def y(s):
    return s - S_BOOT        # tip toward -Y, origin under the boot centre


def build(scene, args):
    T.clear_owned("phase:02_blockout")    # forms replace the blockout
    sections = []
    for s in STATIONS:
        hw, zb = half_width(s), base_z(s)
        zt = zb + thickness(s)
        sections.append([(-hw, y(s), zb), (hw, y(s), zb), (hw, y(s), zt), (-hw, y(s), zt)])
    T.loft("PR_Ski_Body", sections, collection="HIGH", smooth=False)
    for name, (s0, s1, w, ztop) in (("PR_Ski_Toe", TOE), ("PR_Ski_Heel", HEEL)):
        zb = T_FOOT
        T.box(name, (w, s1 - s0, ztop - zb), collection="HIGH", location=(0, y((s0 + s1) / 2), zb),
              base_at_origin=True)
    T.log("forms built,", len(STATIONS), "stations")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
