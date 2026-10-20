"""PR_SkiPole blockout: shaft, basket and grip as plain cylinders.

  $BLENDER_BIN --background --python build/02_blockout.py -- --master PR_SkiPole_master.blend
"""
import os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402

T.PHASE = "02_blockout"
T.OWNER_TAG = "phase:" + T.PHASE


def build(scene, args):
    # all from ref/front_matte.png
    T.cylinder("PR_SkiPole_Shaft_block", 0.008, 1.0, segments=8, base_at_origin=True)     # 16 mm mean, 0 to 1.0 m
    T.cylinder("PR_SkiPole_Basket_block", 0.0325, 0.010, segments=8, location=(0, 0, 0.075),
               base_at_origin=True)                                                   # 65 mm at 0.075 to 0.085 m
    T.cylinder("PR_SkiPole_Grip_block", 0.018, 0.20, segments=8, location=(0, 0, 1.0),
               base_at_origin=True)                                                   # 36 mm, 1.0 to 1.2 m
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
