"""
TREE GATE (tree-kit, Phases 2, 3 and 5)
Measured checks for a tree master, one report per run:
  - triangles per LOD against the params budget, and equal to the layout's own count
  - origin: object at the world origin with no rotation or scale, trunk base on z = 0 and on the axis
  - proportions measured on the mesh (height, lowest crown point, crown width, trunk base diameter and
    their ratios) against the layout the profiles were drawn from
  - top-view silhouette IoU, world registered against ref/top_matte.png (rendered here)
  - front and side IoU from the image-to-3d pack's world_gate.py, read from --world-gate-dir
    (gate_front.json, gate_side.json for LOD0, gate_front_lod<n>.json for lower LODs)

blender --background --python .claude/skills/tree-kit/scripts/tree_gate.py -- \
  --blend art/source/EN_Conifer/EN_Conifer_master.blend --params art/source/EN_Conifer/EN_Conifer_params.json \
  --refs art/source/EN_Conifer/ref/refs.json --out art/source/EN_Conifer/review/03_gates \
  --lods 0,1 --world-gate-dir art/source/EN_Conifer/review/03_gates

Verdicts per check and overall: PASS, FAIL, NOT ASSESSED (an input was missing, so the check did not run;
it outranks PASS, never FAIL). Writes <out>/gates.json, sil_top_lod<n>.png and diff_top_lod<n>.png
(grey both, red reference only, blue model only). Exit 1 on FAIL, 2 on NOT ASSESSED, 0 on PASS.
"""
import argparse
import json
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tree_params as TP  # noqa: E402

RANK = {"PASS": 0, "NOT ASSESSED": 1, "FAIL": 2}


def parse(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--blend", required=True)
    p.add_argument("--params", required=True)
    p.add_argument("--refs", required=True, help="refs.json written by make_profiles.py (LOD0 profile)")
    p.add_argument("--out", required=True)
    p.add_argument("--lods", default="0")
    p.add_argument("--variant", default=None)
    p.add_argument("--world-gate-dir", default="", help="folder holding world_gate.py's gate_<name>.json files")
    p.add_argument("--scale", type=int, default=4, help="render supersampling per matte pixel")
    return p.parse_args(argv)


class Report:
    def __init__(self):
        self.checks = []

    def add(self, lod, name, verdict, value, target, note=""):
        self.checks.append({"lod": lod, "check": name, "verdict": verdict, "value": value, "target": target,
                            "note": note})

    def within(self, lod, name, value, want, tol_frac):
        ok = abs(value - want) <= abs(want) * tol_frac
        self.add(lod, name, "PASS" if ok else "FAIL", round(value, 4),
                 "%.4f +/- %.0f%%" % (want, tol_frac * 100))

    def verdict(self):
        return max((c["verdict"] for c in self.checks), key=RANK.get, default="NOT ASSESSED")


def lod_objects(level):
    c = bpy.data.collections.get("LOD%d" % level)
    return [o for o in c.all_objects if o.type == "MESH"] if c else []


def mesh_points(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    tris, pts = 0, {"bark": [], "crown": []}
    for o in objs:
        eo = o.evaluated_get(dg)
        me = eo.to_mesh()
        mw = o.matrix_world
        roles = [(s.material.get("role", "") if s.material else "") for s in o.material_slots]
        for p in me.polygons:
            tris += len(p.vertices) - 2
            role = roles[p.material_index] if p.material_index < len(roles) else ""
            key = "bark" if role == "bark" else "crown"
            pts[key] += [tuple(mw @ me.vertices[i].co) for i in p.vertices]
        eo.to_mesh_clear()
    return tris, pts


def measure_mesh(pts):
    allp = pts["bark"] + pts["crown"]
    minz = min(p[2] for p in allp)
    base = [p for p in pts["bark"] if p[2] < minz + 1e-4]
    m = {"height_m": max(p[2] for p in allp), "min_z_m": minz,
         "lowest_crown_z_m": min(p[2] for p in pts["crown"]),
         "crown_width_m": 2 * max(math.hypot(p[0], p[1]) for p in pts["crown"]),
         "trunk_base_diameter_m": 2 * max(math.hypot(p[0], p[1]) for p in base)}
    m["ratios"] = TP.ratios(m)
    centre = (sum(p[0] for p in base) / len(base), sum(p[1] for p in base) / len(base))
    return m, centre


def load_rgba(path):
    img = bpy.data.images.load(os.path.abspath(path), check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)[::-1]


def save_rgba(path, arr):
    h, w = arr.shape[:2]
    img = bpy.data.images.new("_gate_out", w, h, alpha=True)
    img.pixels.foreach_set(np.ascontiguousarray(arr[::-1], dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def top_iou(objs, top, matte_path, S, out, tag):
    scene = bpy.context.scene
    keep = {o.name for o in objs}
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o.name not in keep
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light, sh.color_type, sh.single_color = "FLAT", "SINGLE", (0.0, 0.0, 0.0)
    sh.show_shadows = sh.show_cavity = sh.show_object_outline = False
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    W, H = top["image_px"]
    mpp = top["m_per_px"]
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = W * S, H * S, 100
    cd = bpy.data.cameras.new("_gate_top")
    cd.type, cd.sensor_fit = "ORTHO", ("HORIZONTAL" if W >= H else "VERTICAL")
    cd.ortho_scale, cd.clip_start, cd.clip_end = max(W, H) * mpp, 0.01, 500.0
    cam = bpy.data.objects.new("_gate_top", cd)
    scene.collection.objects.link(cam)
    cam.location = (top["x0"] + W * mpp / 2, top["y1"] - H * mpp / 2, 100.0)
    cam.rotation_euler = (0.0, 0.0, 0.0)
    scene.camera = cam
    sil = os.path.join(out, "sil_top_%s.png" % tag)
    scene.render.filepath = os.path.abspath(sil)
    bpy.ops.render.render(write_still=True)
    model = load_rgba(sil)[..., 3].reshape(H, S, W, S).mean(axis=(1, 3)) >= 0.5
    ref = load_rgba(matte_path)[..., 0] < 0.5
    both, ro, mo = ref & model, ref & ~model, model & ~ref
    union = int((ref | model).sum())
    diff = np.ones((H, W, 4), np.float32)
    diff[both] = (0.55, 0.55, 0.55, 1)
    diff[ro] = (1.0, 0.2, 0.2, 1)
    diff[mo] = (0.2, 0.4, 1.0, 1)
    save_rgba(os.path.join(out, "diff_top_%s.png" % tag), diff)
    bpy.data.objects.remove(cam)
    return {"iou": float(both.sum()) / union if union else 0.0,
            "ref_only_pct": 100.0 * ro.sum() / max(1, ref.sum()), "render_only_pct": 100.0 * mo.sum() / max(1, ref.sum())}


def world_gate_iou(R, d, name, lod, target, label):
    path = os.path.join(d, "gate_%s.json" % name) if d else ""
    if not path or not os.path.exists(path):
        R.add(lod, label, "NOT ASSESSED", None, ">= %.2f" % target, "missing %s (run world_gate.py first)" % (path or "--world-gate-dir"))
        return
    with open(path) as f:
        g = json.load(f)
    R.add(lod, label, "PASS" if g["iou"] >= target else "FAIL", round(g["iou"], 4), ">= %.2f" % target,
          "world_gate.py %s; ref_only %.1f%%, render_only %.1f%%" % (os.path.basename(path), g.get("ref_only_pct", -1), g.get("render_only_pct", -1)))


def main(a):
    P = TP.load(a.params)
    G = P["gates"]
    with open(a.refs) as f:
        refs = json.load(f)
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.blend))
    os.makedirs(a.out, exist_ok=True)
    R = Report()
    measured = {}
    for level in [int(x) for x in a.lods.split(",") if x != ""]:
        lod = "LOD%d" % level
        objs = lod_objects(level)
        if not objs:
            R.add(lod, "exists", "NOT ASSESSED", None, "collection %s with meshes" % lod, "nothing built for this LOD")
            continue
        parts = TP.layout(P, level, a.variant)
        want = TP.measures(parts)
        tris, pts = mesh_points(objs)
        lo, hi = P["lods"][level]["budget_tris"]
        R.add(lod, "tris in budget", "PASS" if lo <= tris <= hi else "FAIL", tris, "%d to %d" % (lo, hi))
        R.add(lod, "tris equal layout", "PASS" if tris == want["tris"] else "FAIL", tris, want["tris"])
        tol = G["origin_tol_m"]
        for o in objs:
            ident = (o.location.length <= 1e-6 and max(abs(v) for v in o.rotation_euler) <= 1e-6
                     and max(abs(v - 1) for v in o.scale) <= 1e-6 and o.parent is None)
            R.add(lod, "transform identity " + o.name, "PASS" if ident else "FAIL",
                  [list(o.location), list(o.rotation_euler), list(o.scale)], "loc 0, rot 0, scale 1, no parent")
        m, centre = measure_mesh(pts)
        R.add(lod, "base on ground", "PASS" if abs(m["min_z_m"]) <= tol else "FAIL", round(m["min_z_m"], 5), "|z| <= %g m" % tol)
        off = math.hypot(*centre)
        R.add(lod, "trunk base on axis", "PASS" if off <= tol else "FAIL", round(off, 5), "<= %g m" % tol)
        pt = G["proportion_tol_frac"]
        for k in ("height_m", "lowest_crown_z_m", "crown_width_m", "trunk_base_diameter_m"):
            R.within(lod, k, m[k], want[k], pt)
        for k in ("height_to_crown_width", "clear_trunk_frac", "height_to_trunk_diameter"):
            R.within(lod, k, m["ratios"][k], want["ratios"][k], pt)
        if level == 0:
            R.within(lod, "height vs params", m["height_m"], P["shape"]["height_m"], pt)
        target = G["iou_min"] if level == 0 else G["lod_iou_min"]
        matte = os.path.join(os.path.dirname(a.refs), refs["top"]["file"])
        if os.path.exists(matte):
            t = top_iou(objs, refs["top"], matte, a.scale, a.out, lod.lower())
            R.add(lod, "top IoU vs LOD0 profile", "PASS" if t["iou"] >= target else "FAIL", round(t["iou"], 4),
                  ">= %.2f" % target, "ref_only %.1f%%, render_only %.1f%%" % (t["ref_only_pct"], t["render_only_pct"]))
        else:
            R.add(lod, "top IoU vs LOD0 profile", "NOT ASSESSED", None, ">= %.2f" % target, "missing " + matte)
        if level == 0:
            world_gate_iou(R, a.world_gate_dir, "front", lod, target, "front IoU vs profile")
            world_gate_iou(R, a.world_gate_dir, "side", lod, target, "side IoU vs profile")
        else:
            world_gate_iou(R, a.world_gate_dir, "front_lod%d" % level, lod, target, "front IoU vs LOD0 profile")
        measured[lod] = {"tris": tris, "mesh": m, "layout": want}
    verdict = R.verdict()
    rep = {"blend": os.path.abspath(a.blend), "params": os.path.abspath(a.params), "verdict": verdict,
           "checks": R.checks, "measured": measured,
           "not_assessed": [c["check"] + " " + c["lod"] for c in R.checks if c["verdict"] == "NOT ASSESSED"]}
    with open(os.path.join(a.out, "gates.json"), "w") as f:
        json.dump(rep, f, indent=2)
    for c in R.checks:
        print("GATE %-5s %-13s %-34s %s (target %s) %s" % (c["lod"], c["verdict"], c["check"], c["value"], c["target"], c["note"]))
    print("VERDICT", verdict)
    sys.exit({"PASS": 0, "FAIL": 1, "NOT ASSESSED": 2}[verdict])


if __name__ == "__main__":
    main(parse(sys.argv))
