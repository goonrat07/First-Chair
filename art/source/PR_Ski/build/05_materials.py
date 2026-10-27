"""PR_Ski materials: flat colours by role (First Chair: no normal maps or detail textures on props).
Topsheet = upward faces of the ski body, base and sidewall = the rest of the body, binding = toe and heel, with
the light accent on the toe's top cap and the heel lever (story 012).
Colours are placeholders until the art bible exists.

  $BLENDER_BIN --background --python build/05_materials.py -- --master PR_Ski_master.blend
"""
import os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402
import bpy  # noqa: E402

T.PHASE = "05_materials"
T.OWNER_TAG = "phase:" + T.PHASE


def build(scene, args):
    low = bpy.data.objects["PR_Ski_LOD0"]
    me = low.data
    mats = [
        T.material("M_Ski_Topsheet", (0.85, 0.32, 0.08, 1), roughness=0.4),   # placeholder warm orange
        T.material("M_Ski_Base", (0.02, 0.02, 0.02, 1), roughness=0.3),       # black P-tex and sidewall
        T.material("M_Ski_Binding", (0.12, 0.12, 0.13, 1), roughness=0.5),    # dark grey plastic
        T.material("M_Ski_BindingAccent", (0.4233, 0.4678, 0.5395, 1), roughness=0.45),   # story 012: light grey
        #   toe cap and heel lever, the skier's buckle colour (#AEB6C2)
    ]
    me.materials.clear()
    for m in mats:
        bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf:      # the viewport colour too, so Workbench reviews (pose sheets) show the real colours
            m.diffuse_color = bsdf.inputs["Base Color"].default_value[:]
        me.materials.append(m)
    part = me.attributes["part"].data
    accent = me.attributes["accent"].data
    for i, poly in enumerate(me.polygons):
        if part[i].value > 0:
            poly.material_index = 3 if accent[i].value else 2
        elif poly.normal.z > 0.5:
            poly.material_index = 0
        else:
            poly.material_index = 1
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
