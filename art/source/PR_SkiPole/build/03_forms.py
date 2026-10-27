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
