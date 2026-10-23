"""
MANIFEST EXTRAS (EN_Conifer, tree-kit Phase 6)
Adds the tree's own fields to the asset-manifest.json that export_delivery.py wrote next to the FBX:
height, trunk base radius and crown width per LOD (measured on the master .blend's LOD meshes, metres,
Z up, origin at the trunk base), each material's role, linear base colour (what the FBX carries as
DiffuseColor), sRGB hex and roughness, the variant and version, the per-LOD tri budgets, and the
source and licence. Run it after export_delivery.py; it only adds keys.

blender --background --python art/source/EN_Conifer/build/manifest_extras.py -- \
  --blend art/source/EN_Conifer/EN_Conifer_master.blend --params art/source/EN_Conifer/EN_Conifer_params.json \
  --manifest Assets/Art/Environment/EN_Conifer/asset-manifest.json --variant snowy_spruce --root .
"""
import bpy, sys, os, re, json, math, argparse

LOD_RE = re.compile(r"_LOD(\d+)$")
BASE_BAND_M = 0.005   # vertices this close to the lowest point count as the trunk base ring


def parse(argv):
    p = argparse.ArgumentParser(description="Add tree fields to an exported tree's asset-manifest.json")
    p.add_argument("--blend", required=True)
    p.add_argument("--params", required=True)
    p.add_argument("--manifest", required=True)
    p.add_argument("--variant", required=True)
    p.add_argument("--version", type=int, default=2)
    p.add_argument("--status", default="approved for use in the test build")
    p.add_argument("--root", default=".", help="repo root, for the relative paths written to the manifest")
    return p.parse_args(argv)


def srgb_hex(c):
    def enc(v):
        v = max(0.0, min(1.0, v))
        return int(round(255 * (v * 12.92 if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055)))
    return "#%02X%02X%02X" % tuple(enc(v) for v in c[:3])


def measure(o):
    dg = bpy.context.evaluated_depsgraph_get()
    eo = o.evaluated_get(dg)
    me = eo.to_mesh()
    pts = [eo.matrix_world @ v.co for v in me.vertices]
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    eo.to_mesh_clear()
    lo, hi = min(p.z for p in pts), max(p.z for p in pts)
    base = [p for p in pts if p.z - lo <= BASE_BAND_M]
    return {"object": o.name, "tris": tris, "height_m": round(hi - lo, 4), "base_z_m": round(lo, 4),
            "trunk_base_radius_m": round(max(math.hypot(p.x, p.y) for p in base), 4),
            "crown_width_m": round(2 * max(math.hypot(p.x, p.y) for p in pts), 4)}


def main(a):
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.blend))
    root = os.path.abspath(a.root)
    rel = lambda p: os.path.relpath(os.path.abspath(p), root)
    with open(a.params) as f:
        params = json.load(f)
    with open(a.manifest) as f:
        man = json.load(f)
    lods = {}
    for o in bpy.data.collections["LOW"].all_objects:
        m = LOD_RE.search(o.name)
        if o.type == "MESH" and m:
            lods[int(m.group(1))] = o
    per_lod = {"LOD%d" % l: measure(o) for l, o in sorted(lods.items())}
    budgets = {l["level"]: l.get("budget_tris") for l in params["lods"]}
    for entry in man["lods"]:
        entry["budget_tris"] = budgets.get(entry["level"])
    mats = {}
    for o in lods.values():
        for mat in o.data.materials:
            if not mat or mat.name in mats:
                continue
            bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if mat.node_tree else None
            col = list(bsdf.inputs["Base Color"].default_value)[:3] if bsdf else list(mat.diffuse_color)[:3]
            rough = bsdf.inputs["Roughness"].default_value if bsdf else mat.roughness
            mats[mat.name] = {"role": mat.get("role"), "base_colour_linear": [round(v, 4) for v in col],
                              "base_colour_srgb_hex": srgb_hex(col), "roughness": round(rough, 3),
                              "carried_as": "FBX DiffuseColor, linear RGB; no textures, no UVs"}
    lod0 = per_lod["LOD0"]
    man["target_height_m"] = params["shape"]["height_m"]
    man["tree"] = {"kind": params["kind"], "variant": a.variant, "version": a.version, "status": a.status,
                   "height_m": lod0["height_m"], "trunk_base_radius_m": lod0["trunk_base_radius_m"],
                   "crown_width_m": lod0["crown_width_m"], "origin": "trunk base, on the ground",
                   "per_lod": per_lod, "in_game_scale": params["cluster"]["scale"],
                   "note": "12 m base tree; instances scaled 0.667 to 1.167 give 8 to 14 m trees (user decision 2026-10-20)"}
    man["material_colours"] = mats
    man["provenance"] = {"params": rel(a.params), "blend": rel(a.blend), "generator": "tree-kit build_tree.py (procedural)",
                         "style_reference": "art/reference/style-notes-gma2.md (our notes; no third-party images in the repo)",
                         "licence": "ours: First Chair's own procedural mesh and colours; no third-party meshes, images or textures"}
    man["unity"] = {"lod_group": "the importer builds it from the _LOD0/_LOD1 name suffixes (not verified in the editor)",
                    "gpu_instancing": "turn on for the three materials (not verified in the editor)",
                    "billboard_lod2": "not built: tree-kit has no billboard script yet (asset-brief.md)"}
    with open(a.manifest, "w") as f:
        json.dump(man, f, indent=2)
    print("MANIFEST", a.manifest)
    for k, v in per_lod.items():
        print("MEASURED", k, v)
    for k, v in mats.items():
        print("MATERIAL", k, v["role"], v["base_colour_linear"], v["base_colour_srgb_hex"], v["roughness"])


if __name__ == "__main__":
    main(parse(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []))
