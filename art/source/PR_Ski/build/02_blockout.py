"""PR_Ski blockout: primary masses only (plank with a coarse tip rise, two binding blocks).

  $BLENDER_BIN --background --python build/02_blockout.py -- --master PR_Ski_master.blend
"""
import os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402  (the skill's helpers; this file is a phase script built on them)

T.PHASE = "02_blockout"
T.OWNER_TAG = "phase:" + T.PHASE

S_BOOT = 0.965   # boot centre from the tip end, ref/side_matte.png column 1198 (world y = 0)


def y(s):
    """Distance from the tip end -> world Y (tip toward -Y, boot centre at 0)."""
    return s - S_BOOT


def build(scene, args):
    # coarse stations: (s from tip, half width, base z, thickness); ref/top_matte.png and ref/side_matte.png
    stations = [
        (0.01, 0.025, 0.058, 0.007),   # tip end, rocker height 0.060 (side)
        (0.07, 0.058, 0.037, 0.007),   # rocker curve (side)
        (0.15, 0.0665, 0.015, 0.007),  # widest shovel 0.133 (top), rocker 0.015 (side)
        (0.22, 0.064, 0.004, 0.008),   # rocker curve (side)
        (0.30, 0.060, 0.0, 0.010),     # rocker ends (side)
        (0.965, 0.046, 0.0, 0.017),    # waist 0.092 at the boot centre (top), 17 mm thick (side)
        (1.62, 0.057, 0.0, 0.010),     # tail kick starts (side)
        (1.69, 0.059, 0.008, 0.007),   # widest tail 0.118 (top), kick 0.008 (side)
        (1.73, 0.048, 0.017, 0.007),   # kick curve (side)
        (1.76, 0.020, 0.028, 0.007),   # tail end, kick height 0.030 (side)
    ]
    sections = []
    for s, hw, zb, t in stations:
        sections.append([(-hw, y(s), zb), (hw, y(s), zb), (hw, y(s), zb + t), (-hw, y(s), zb + t)])
    T.loft("PR_Ski_Body_block", sections, collection="LOW", smooth=False)
    # binding blocks: toe s 0.75-0.84, 0.062 tall; heel s 1.09-1.20, 0.085 tall (side); widths inferred
    T.box("PR_Ski_Toe_block", (0.070, 0.09, 0.062 - 0.017), location=(0, y(0.795), 0.017), base_at_origin=True)
    T.box("PR_Ski_Heel_block", (0.075, 0.11, 0.085 - 0.017), location=(0, y(1.145), 0.017), base_at_origin=True)
    T.log("blockout built")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
