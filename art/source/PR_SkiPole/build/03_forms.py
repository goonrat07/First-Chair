"""PR_SkiPole forms: the whole pole as one lathed profile (tip, basket, tapered shaft, grip).
Replaces the blockout. Built in HIGH; 04_topology makes the delivery mesh in LOW.

  $BLENDER_BIN --background --python build/03_forms.py -- --master PR_SkiPole_master.blend
"""
import os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402

T.PHASE = "03_forms"
T.OWNER_TAG = "phase:" + T.PHASE

SEGMENTS = 8   # round enough at ~55 px on screen; 16 doubles the tris for no visible gain

# (radius, height above the tip) in metres, read off ref/front_matte.png (ref/refs.json)
PROFILE = [
    (0.000, 0.000), (0.004, 0.000), (0.006, 0.020),     # carbide tip
    (0.006, 0.074), (0.0325, 0.076), (0.0325, 0.084),   # basket
    (0.007, 0.088),                                     # shaft bottom
    (0.009, 1.000),                                     # shaft top
    (0.016, 1.000), (0.018, 1.060), (0.017, 1.170),     # grip
    (0.020, 1.185), (0.012, 1.200), (0.000, 1.200),     # grip top
]


def build(scene, args):
    T.clear_owned("phase:02_blockout")
    # profile_spin works in the XZ plane: (x = radius, z = height)
    T.profile_spin("PR_SkiPole_Body", PROFILE, collection="HIGH", steps=SEGMENTS)
    T.log("forms built,", len(PROFILE), "profile points x", SEGMENTS, "segments")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
