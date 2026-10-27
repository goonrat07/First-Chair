"""PR_Ski topology: join the HIGH forms into one delivery mesh in LOW, triangulate, unwrap.
One LOD (First Chair budget for skis, poles, boots: one LOD, 100 to 600 tris).

  $BLENDER_BIN --background --python build/04_topology.py -- --master PR_Ski_master.blend
"""
import math, os, sys, argparse
import bmesh

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402
import bpy  # noqa: E402

T.PHASE = "04_topology"
T.OWNER_TAG = "phase:" + T.PHASE

PARTS = ["PR_Ski_Body", "PR_Ski_Toe", "PR_Ski_Heel"]


def build(scene, args):
    srcs = [bpy.data.objects[n] for n in PARTS]
    low = T.join_meshes("PR_Ski_LOD0", srcs, collection="LOW")
    # keep which part each face came from, for the material phase
    me = low.data
    attr = me.attributes.new("part", "INT", "FACE")
    accent = me.attributes.new("accent", "INT", "FACE")    # story 012: the bindings' light accent (material 1)
    i = 0
    for part, o in enumerate(srcs):
        for poly in o.data.polygons:
            attr.data[i].value = part
            accent.data[i].value = 1 if part > 0 and poly.material_index == 1 else 0
            i += 1
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    T.shade_smooth(low, math.radians(30))
    T.smart_uv(low, angle=66.0, margin=0.02, seams_from_sharp=False)
    for o in srcs:                                  # sources stay in the master, out of renders
        o.hide_render = True
    T.log("LOD0 tris", len(me.polygons))


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
