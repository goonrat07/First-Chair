"""
EXPORT CHECK (tree assets, tree-kit Phase 6; EN_Conifer first, shared since story 016)
Reads the exported FBX two ways and compares it with asset-manifest.json and the params:
1. raw: Blender's FBX parser reads the file's own nodes, as Unity's importer receives them: global
   axes and unit scale, each model's name, parent and local transform, each geometry's vertices and
   polygons, each material's DiffuseColor. Measured in the file's up axis, in metres
   (vertex units x UnitScaleFactor / 100, which is how Unity converts FBX units).
2. reimport: Blender's FBX importer in a blank scene: tris, height and materials per mesh.
Then renders LOD0 (left) and LOD1 (right) side by side with Workbench: clay and flat colour.
Writes export_check.json, export_clay.png and export_colour.png to --out, prints CHECK lines and a
VERDICT, and exits 1 on any FAIL.

blender --background --python tools/blender/trees/export_check.py -- \
  --fbx Assets/Art/Environment/EN_Conifer/EN_Conifer.fbx --manifest Assets/Art/Environment/EN_Conifer/asset-manifest.json \
  --params art/source/EN_Conifer/EN_Conifer_params.json --variant snowy_spruce --out art/source/EN_Conifer/review/export
"""
import bpy, sys, os, re, json, math, argparse
from mathutils import Vector
from io_scene_fbx import parse_fbx

LOD_RE = re.compile(r"_LOD(\d+)$")
H_TOL = 0.01        # height within 1 percent
ORIGIN_TOL_M = 0.005
COL_TOL = 1e-3
XF_TOL = 1e-4


def parse(argv):
    p = argparse.ArgumentParser(description="Check an exported tree FBX against its manifest and params")
    p.add_argument("--fbx", required=True)
    p.add_argument("--manifest", required=True)
    p.add_argument("--params", required=True)
    p.add_argument("--variant", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--res", type=int, default=1000)
    p.add_argument("--spacing", type=float, default=4.5, help="metres between the LODs in the render")
    return p.parse_args(argv)


def kids(e, id_):
    return [c for c in e.elems if c.id == id_] if e else []


def kid(e, id_):
    k = kids(e, id_)
    return k[0] if k else None


def p70(e):
    out = {}
    for P in kids(kid(e, b"Properties70"), b"P"):
        out[P.props[0].decode()] = [v.decode() if isinstance(v, bytes) else v for v in P.props[4:]]
    return out


def fbx_name(e):
    return e.props[1].split(b"\x00\x01")[0].decode()


def read_raw(path):
    root, version = parse_fbx.parse(path)
    gs = p70(kid(root, b"GlobalSettings"))
    unit = float(gs.get("UnitScaleFactor", [1.0])[0])
    to_m = unit / 100.0
    axes = {k: gs.get(k, [None])[0] for k in ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis", "CoordAxisSign")}
    objs = kid(root, b"Objects")
    models = {e.props[0]: e for e in kids(objs, b"Model")}
    geoms = {e.props[0]: e for e in kids(objs, b"Geometry")}
    mats = {e.props[0]: e for e in kids(objs, b"Material")}
    links = [(c.props[1], c.props[2]) for c in kids(kid(root, b"Connections"), b"C") if c.props[0] == b"OO"]
    raw = {"fbx_version": version, "axes": axes, "unit_scale_factor": unit, "models": {}, "materials": {}}
    for e in mats.values():
        raw["materials"][fbx_name(e)] = {"DiffuseColor": [round(float(v), 5) for v in p70(e).get("DiffuseColor", [])[:3]]}
    u, sgn = int(axes["UpAxis"]), int(axes["UpAxisSign"])
    h = [i for i in range(3) if i != u]
    for uid, m in models.items():
        props = p70(m)
        entry = {"type": m.props[2].decode(),
                 "parent": "scene root" if (uid, 0) in links else "other",
                 "lcl_translation": [float(v) for v in props.get("Lcl Translation", [0, 0, 0])[:3]],
                 "lcl_rotation_deg": [float(v) for v in props.get("Lcl Rotation", [0, 0, 0])[:3]],
                 "lcl_scaling": [float(v) for v in props.get("Lcl Scaling", [1, 1, 1])[:3]],
                 "materials": [fbx_name(mats[c]) for c, p in links if p == uid and c in mats]}
        for g in [geoms[c] for c, p in links if p == uid and c in geoms]:
            v = list(kid(g, b"Vertices").props[0])
            pts = [(v[i] * to_m, v[i + 1] * to_m, v[i + 2] * to_m) for i in range(0, len(v), 3)]
            tris = n = 0
            for i in kid(g, b"PolygonVertexIndex").props[0]:
                n += 1
                if i < 0:
                    tris += n - 2
                    n = 0
            up = [p[u] * sgn for p in pts]
            lo, hi = min(up), max(up)
            base = [p for p in pts if p[u] * sgn - lo <= ORIGIN_TOL_M]
            cx, cz = sum(p[h[0]] for p in base) / len(base), sum(p[h[1]] for p in base) / len(base)
            entry.update(tris=tris, verts=len(pts), base_m=round(lo, 4), height_m=round(hi - lo, 4),
                         base_centre_offset_m=round(math.hypot(cx, cz), 4),
                         trunk_base_radius_m=round(max(math.hypot(p[h[0]], p[h[1]]) for p in base), 4),
                         crown_width_m=round(2 * max(math.hypot(p[h[0]], p[h[1]]) for p in pts), 4))
        raw["models"][fbx_name(m)] = entry
    return raw


def principled_colour(mat):
    if mat.node_tree:
        for n in mat.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED":
                return [round(float(x), 5) for x in list(n.inputs["Base Color"].default_value)[:3]]
    return [round(float(x), 5) for x in list(mat.diffuse_color)[:3]]


def reimport(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    dg = bpy.context.evaluated_depsgraph_get()
    out = {"meshes": {}, "materials": {}, "other_objects": []}
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            out["other_objects"].append("%s (%s)" % (o.name, o.type))
            continue
        eo = o.evaluated_get(dg)
        me = eo.to_mesh()
        zs = [(eo.matrix_world @ v.co).z for v in me.vertices]
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        eo.to_mesh_clear()
        out["meshes"][o.name] = {"tris": tris, "height_m": round(max(zs) - min(zs), 4), "base_z_m": round(min(zs), 4),
                                 "blender_rotation_deg": [round(math.degrees(r), 2) for r in o.rotation_euler],
                                 "uv": bool(o.data.uv_layers), "materials": [m.name for m in o.data.materials if m]}
    for m in bpy.data.materials:
        if m.users:
            out["materials"][m.name] = {"base_colour_linear": principled_colour(m)}
    return out


def view_direction(az_deg, el_deg):
    az, el = math.radians(az_deg), math.radians(el_deg)
    return Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))


def render(out_dir, res, spacing):
    scene = bpy.context.scene
    meshes = sorted([o for o in scene.objects if o.type == "MESH"], key=lambda o: o.name)
    for i, o in enumerate(meshes):
        o.location.x += i * spacing
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    d = view_direction(35, 12)
    q = (-d).to_track_quat("-Z", "Y")
    R = q.to_matrix()
    xa, ya = R @ Vector((1, 0, 0)), R @ Vector((0, 1, 0))
    xs, ys, zs = [p.dot(xa) for p in pts], [p.dot(ya) for p in pts], [p.dot(d) for p in pts]
    cam = bpy.data.objects.new("_chk_cam", bpy.data.cameras.new("_chk_cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = q
    cam.data.type = "ORTHO"
    cam.data.sensor_fit = "HORIZONTAL"
    cam.data.ortho_scale = max(max(xs) - min(xs), max(ys) - min(ys)) * 1.1
    cam.location = xa * (min(xs) + max(xs)) / 2 + ya * (min(ys) + max(ys)) / 2 + d * (max(zs) + 20)
    cam.data.clip_end = 1000.0
    scene.world = bpy.data.worlds.new("_chk_world")
    scene.world.color = (0.42, 0.5, 0.62)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.view_settings.view_transform = "Standard"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.show_shadows = True
    scene.render.film_transparent = False
    scene.render.resolution_x = scene.render.resolution_y = res
    files = {}
    for tag, ctype in (("clay", "SINGLE"), ("colour", "MATERIAL")):
        scene.display.shading.color_type = ctype
        scene.display.shading.single_color = (0.5, 0.5, 0.5)
        if ctype == "MATERIAL":     # Workbench draws the viewport colour; copy the imported base colour into it
            for m in bpy.data.materials:
                m.diffuse_color = principled_colour(m) + [1.0]
        scene.render.filepath = os.path.join(out_dir, "export_%s.png" % tag)
        bpy.ops.render.render(write_still=True)
        files[tag] = scene.render.filepath
    return files, [o.name for o in meshes]


def main(a):
    os.makedirs(a.out, exist_ok=True)
    path = os.path.abspath(a.fbx)
    with open(a.manifest) as f:
        man = json.load(f)
    with open(a.params) as f:
        params = json.load(f)
    colours = params["variants"][a.variant]["colours_linear"]
    expect_h = params["shape"]["height_m"]
    checks = []

    def check(name, ok, value, target):
        checks.append({"check": name, "verdict": "PASS" if ok else "FAIL", "value": value, "target": target})
        print("CHECK", "PASS" if ok else "FAIL", "|", name, "|", value, "|", target)

    raw = read_raw(path)
    check("up axis is +Y", raw["axes"]["UpAxis"] == 1 and raw["axes"]["UpAxisSign"] == 1, raw["axes"], "UpAxis 1, UpAxisSign 1")
    want = {"LOD%d" % l["level"]: l for l in man["lods"]}
    names = {n for l in man["lods"] for n in l["objects"]}
    extra = sorted(set(raw["models"]) - names)
    check("no models beyond the LOD meshes", not extra, extra, "none")
    for lod, l in sorted(want.items()):
        name = l["objects"][0]
        m = raw["models"].get(name)
        check("%s present as a Mesh model" % name, bool(m) and m["type"] == "Mesh", m and m["type"], "Mesh")
        if not m:
            continue
        check("%s parent" % name, m["parent"] == "scene root", m["parent"], "scene root")
        check("%s name suffix" % name, name.endswith("_" + lod), name, "ends in _" + lod)
        xf_ok = all(abs(v) <= XF_TOL for v in m["lcl_translation"] + m["lcl_rotation_deg"]) and \
            all(abs(v - 1) <= XF_TOL for v in m["lcl_scaling"])
        check("%s local transform identity" % name, xf_ok,
              {"t": m["lcl_translation"], "r_deg": m["lcl_rotation_deg"], "s": m["lcl_scaling"]}, "t 0, r 0, s 1")
        check("%s tris (raw FBX)" % name, m.get("tris") == l["tris"], m.get("tris"), l["tris"])
        hm = m.get("height_m", 0)
        check("%s height m (raw FBX, unit-scaled)" % name, abs(hm - expect_h) / expect_h <= H_TOL, hm, "%.2f +/- 1%%" % expect_h)
        check("%s base on y = 0" % name, abs(m.get("base_m", 1)) <= ORIGIN_TOL_M, m.get("base_m"), "|y| <= %.3f" % ORIGIN_TOL_M)
        check("%s trunk base on the up axis" % name, m.get("base_centre_offset_m", 1) <= ORIGIN_TOL_M,
              m.get("base_centre_offset_m"), "<= %.3f m" % ORIGIN_TOL_M)
        check("%s materials" % name, sorted(m["materials"]) == sorted(man["materials"]), sorted(m["materials"]), man["materials"])
    for mname in man["materials"]:
        role = mname.rsplit("_", 1)[-1].lower()
        got = raw["materials"].get(mname, {}).get("DiffuseColor", [])
        exp = colours.get(role)
        ok = bool(got) and exp is not None and all(abs(g - e) <= COL_TOL for g, e in zip(got, exp))
        check("%s DiffuseColor (raw FBX) = params %s colour" % (mname, role), ok, got, exp)

    rim = reimport(path)
    for lod, l in sorted(want.items()):
        name = l["objects"][0]
        r = rim["meshes"].get(name)
        check("%s tris (reimport)" % name, bool(r) and r["tris"] == l["tris"], r and r["tris"], l["tris"])
        hm = r["height_m"] if r else 0
        check("%s height m (reimport)" % name, abs(hm - expect_h) / expect_h <= H_TOL, hm, "%.2f +/- 1%%" % expect_h)
    for mname in man["materials"]:
        role = mname.rsplit("_", 1)[-1].lower()
        got = rim["materials"].get(mname, {}).get("base_colour_linear", [])
        exp = colours.get(role)
        ok = bool(got) and exp is not None and all(abs(g - e) <= COL_TOL for g, e in zip(got, exp))
        check("%s base colour (reimport)" % mname, ok, got, exp)

    files, order = render(a.out, a.res, a.spacing)
    verdict = "PASS" if all(c["verdict"] == "PASS" for c in checks) else "FAIL"
    report = {"fbx": path, "manifest": os.path.abspath(a.manifest), "verdict": verdict, "checks": checks,
              "raw_fbx": raw, "reimport": rim, "renders": files, "render_order_left_to_right": order}
    with open(os.path.join(a.out, "export_check.json"), "w") as f:
        json.dump(report, f, indent=2)
    print("RENDERS", files)
    print("VERDICT", verdict)
    sys.exit(0 if verdict == "PASS" else 1)


if __name__ == "__main__":
    main(parse(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []))
