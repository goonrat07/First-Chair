"""PR_SkiPole materials: flat colours by role, assigned by height along the pole.
Colours are placeholders until the art bible exists.

  $BLENDER_BIN --background --python build/05_materials.py -- --master PR_SkiPole_master.blend
"""
import os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402
import bpy  # noqa: E402

T.PHASE = "05_materials"
T.OWNER_TAG = "phase:" + T.PHASE

TIP_TOP, BASKET_TOP, GRIP_BOTTOM = 0.020, 0.088, 1.000   # heights from ref/front_matte.png


def build(scene, args):
    low = bpy.data.objects["PR_SkiPole_LOD0"]
    me = low.data
    mats = [
        T.material("M_Pole_Shaft", (0.72, 0.74, 0.76, 1), roughness=0.35),   # brushed aluminium, flat colour
        T.material("M_Pole_Plastic", (0.03, 0.03, 0.03, 1), roughness=0.6),   # grip and basket
        T.material("M_Pole_Tip", (0.25, 0.25, 0.26, 1), roughness=0.4),       # steel tip
    ]
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    for poly in me.polygons:
        z = poly.center.z
        if z < TIP_TOP:
            poly.material_index = 2
        elif z < BASKET_TOP or z >= GRIP_BOTTOM - 1e-4:
            poly.material_index = 1
        else:
            poly.material_index = 0
    T.log("materials assigned", [m.name for m in mats])


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
