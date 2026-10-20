"""
BUILD TREE (tree-kit, Phases 2, 4 and 5)
Parameters in, tree out. Rebuilds the whole master .blend from the parameters JSON, so every run is
reproducible and rerunning any phase is safe; change the JSON, never the mesh by hand.

blender --background --python .claude/skills/tree-kit/scripts/build_tree.py -- \
  --params art/source/EN_Conifer/EN_Conifer_params.json \
  --out art/source/EN_Conifer/EN_Conifer_master.blend --lods 0,1 --variant snowy_spruce

Writes one mesh object per LOD, <ASSET>_LOD<n>, origin at the trunk base on the ground, in collection
LOD<n> under LOW (the collection validate.py and export_delivery.py read). One material per role used
(M_<Short>_Bark, _Needle, _Snow; each carries a "role" custom property), colours from the variant,
flat shaded, no UVs or textures.
"""
import argparse
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tree_params as TP  # noqa: E402

ROLES = ("bark", "needle", "snow")


def parse(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--params", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--lods", default="0", help="comma list of LOD levels to build (billboards are not built here)")
    p.add_argument("--variant", default=None, help="key in the params 'variants' (default: params 'variant')")
    return p.parse_args(argv)


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def centroid(pts):
    n = float(len(pts))
    return (sum(p[0] for p in pts) / n, sum(p[1] for p in pts) / n, sum(p[2] for p in pts) / n)


def newell(pts):
    nx = ny = nz = 0.0
    for i, p in enumerate(pts):
        q = pts[(i + 1) % len(pts)]
        nx += (p[1] - q[1]) * (p[2] + q[2])
        ny += (p[2] - q[2]) * (p[0] + q[0])
        nz += (p[0] - q[0]) * (p[1] + q[1])
    return (nx, ny, nz)


def make_material(name, role, rgb, rough):
    mat = bpy.data.materials.new(name)
    if bpy.app.version < (5, 0, 0):
        mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None) or nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    rgba = (rgb[0], rgb[1], rgb[2], 1.0)
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = rough
    mat.diffuse_color = rgba
    mat.roughness = rough
    mat["role"] = role
    return mat


def mesh_from_parts(name, parts, mats_by_role):
    verts, faces, roles = [], [], []

    def add(p):
        verts.append(tuple(p))
        return len(verts) - 1
    for part in parts:
        rings = [[add(p) for p in ring] for ring in part["rings"]]
        apex = add(part["apex"]) if part["apex"] else None
        m = len(part["rings"][0])
        segs = []
        for a, b in zip(rings, rings[1:]):
            for k in range(m):
                segs.append(([a[k], a[(k + 1) % m], b[(k + 1) % m], b[k]], part["roles"][k], a + b))
        if apex is not None:
            last = rings[-1]
            for k in range(m):
                segs.append(([last[k], last[(k + 1) % m], apex], part["roles"][k], last + [apex]))
        for f, role, ref in segs:
            pts = [verts[i] for i in f]
            n, out = newell(pts), sub(centroid(pts), centroid([verts[i] for i in ref]))
            if n[0] * out[0] + n[1] * out[1] + n[2] * out[2] < 0:   # outward normals for convex sections
                f = f[::-1]
            faces.append(f)
            roles.append(role)
    used = [r for r in ROLES if r in roles]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    for r in used:
        me.materials.append(mats_by_role[r])
    me.polygons.foreach_set("material_index", [used.index(r) for r in roles])
    try:
        me.shade_flat()
    except AttributeError:
        for poly in me.polygons:
            poly.use_smooth = False
    me.update()
    me.validate()
    return me


def main(a):
    P = TP.load(a.params)
    vname, var = TP.variant_of(P, a.variant)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene["asset_name"] = P["asset"]
    scene["variant"] = vname
    scene["params"] = os.path.basename(a.params)
    scene["generator"] = "tree-kit build_tree.py"
    scene["authoring_up"], scene["authoring_forward"] = "Z", "-Y"

    low = bpy.data.collections.new("LOW")
    scene.collection.children.link(low)
    scene.collection.children.link(bpy.data.collections.new("REF"))

    short = P["asset"].split("_", 1)[-1]
    cols, rough = var["colours_linear"], var.get("roughness", {})
    mats = {r: make_material("M_%s_%s" % (short, r.capitalize()), r, cols[r], rough.get(r, 0.9))
            for r in ROLES if cols.get(r) is not None}
    if "snow" not in mats:
        mats["snow"] = mats["needle"]          # snowless variants: snow faces take the needle material

    report = {}
    for level in [int(x) for x in a.lods.split(",") if x != ""]:
        lod = P["lods"][level]
        if lod.get("kind") == "billboard":
            print("SKIP LOD%d: billboards are built in Phase 5 by their own script" % level)
            continue
        parts = TP.layout(P, level, vname)
        coll = bpy.data.collections.new("LOD%d" % level)
        low.children.link(coll)
        name = "%s_LOD%d" % (P["asset"], level)
        ob = bpy.data.objects.new(name, mesh_from_parts(name, parts, mats))
        coll.objects.link(ob)
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        ob["lod"], ob["tris"], ob["variant"] = level, tris, vname
        report["LOD%d" % level] = tris
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(a.out))
    print("BUILT", P["asset"], vname, report, "->", a.out)


if __name__ == "__main__":
    main(parse(sys.argv))
