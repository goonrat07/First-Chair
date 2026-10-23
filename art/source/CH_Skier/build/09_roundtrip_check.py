"""Reimport CH_Skier.fbx into an empty scene and compare it with asset-manifest.json and animation-contract.json at
the REST pose (the skill's roundtrip.py measures whatever pose the importer leaves active). Renders the reimport in
clay at rest and in the CarveLeft and Tuck takes.

  $BLENDER_BIN -b --python build/09_roundtrip_check.py -- --dir <export dir> --out review/09_export
"""
import bpy, os, sys, json, math, argparse
from mathutils import Vector as V


def render(scene, path, d, target, dist=4.2):
    cam = scene.camera
    cam.location = target + d * dist
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (-d).to_track_quat("-Z", "Y")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main(a):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    os.makedirs(a.out, exist_ok=True)
    man = json.load(open(os.path.join(a.dir, "asset-manifest.json")))
    con = json.load(open(os.path.join(a.dir, "animation-contract.json")))
    bpy.ops.import_scene.fbx(filepath=os.path.join(a.dir, "CH_Skier.fbx"))
    arms = [o for o in scene.objects if o.type == "ARMATURE"]
    for o in arms:
        o.data.pose_position = "REST"
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    err, tris, pts = [], {}, []
    for o in scene.objects:
        if o.type == "MESH":
            eo = o.evaluated_get(dg)
            me = eo.to_mesh()
            tris[o.name] = sum(len(p.vertices) - 2 for p in me.polygons)
            pts += [eo.matrix_world @ v.co for v in me.vertices]
            eo.to_mesh_clear()
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    height = hi[2] - lo[2]
    if abs(height - 1.75) / 1.75 > 0.01:
        err.append("height %.4f" % height)
    if lo[2] < -0.005:
        err.append("below ground %.4f" % lo[2])
    lods = []
    for l in man["lods"]:
        got = sum(tris.get(n, -10 ** 6) for n in l["objects"])
        lods.append({"level": l["level"], "manifest": l["tris"], "reimport": got})
        if got != l["tris"]:
            err.append("LOD%d tris %d vs %d" % (l["level"], got, l["tris"]))
    bones = sorted(b.name for o in arms for b in o.data.bones)
    want = sorted(man["skeleton"][0]["deform_bones"])
    if bones != want:
        err.append("bones differ: %s" % sorted(set(bones) ^ set(want)))
    acts = {act.name: act for act in bpy.data.actions}
    clips = []
    for c in con["clips"]:
        hit = [n for n in acts if n == c["name"] or n.endswith("|" + c["name"])]
        rec = {"clip": c["name"], "imported_action": hit[0] if hit else None}
        if hit:
            fs, fe = acts[hit[0]].frame_range
            rec["frames"] = [round(fs, 3), round(fe, 3)]
            rec["length_s"] = round((fe - fs) / 30.0, 4)
            if abs(rec["length_s"] - c["length_s"]) > 1e-3:
                err.append("clip %s length %.4f vs %.4f" % (c["name"], rec["length_s"], c["length_s"]))
        else:
            err.append("clip missing: " + c["name"])
        clips.append(rec)
    empties = [{"name": o.name, "parent": o.parent.name if o.parent else None, "parent_bone": o.parent_bone or None,
                "world": [round(v, 4) for v in o.matrix_world.translation]} for o in scene.objects if o.type == "EMPTY"]
    for s in man["sockets"]:
        e = next((x for x in empties if x["name"] == s["name"]), None)
        if not e:
            err.append("socket missing " + s["name"])
        elif max(abs(p - q) for p, q in zip(e["world"], s["world_location"])) > 0.002:
            err.append("socket %s moved %s vs %s" % (s["name"], e["world"], s["world_location"]))
    rep = {"file": os.path.join(a.dir, "CH_Skier.fbx"), "height_m_rest": round(height, 4),
           "bounds_rest": {"min": [round(v, 4) for v in lo], "max": [round(v, 4) for v in hi]}, "tris": tris, "lods": lods,
           "bones": bones, "imported_actions": sorted(acts), "clips": clips, "empties": empties,
           "armatures": [{"name": o.name, "rotation_deg": [round(math.degrees(v), 2) for v in o.rotation_euler]} for o in arms],
           "errors": err, "pass": not err}
    with open(os.path.join(a.out, "roundtrip_check.json"), "w") as f:
        json.dump(rep, f, indent=2)
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light, sh.color_type, sh.single_color = "STUDIO", "SINGLE", (0.6, 0.6, 0.6)
    sh.show_shadows, sh.show_cavity = True, True
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.3, 0.33, 0.38)
    scene.render.resolution_x, scene.render.resolution_y = 520, 640
    cam = bpy.data.objects.new("_cam", bpy.data.cameras.new("_cam"))
    cam.data.lens = 50
    scene.collection.objects.link(cam)
    scene.camera = cam
    d = V((math.sin(math.radians(35)), -math.cos(math.radians(35)), 0.3)).normalized()
    tgt = V((0, 0, 0.85))
    render(scene, os.path.join(a.out, "reimport_clay_rest.png"), d, tgt)
    for clip in ("CarveLeft", "Tuck"):
        hit = [n for n in acts if n == clip or n.endswith("|" + clip)]
        if hit and arms:
            arms[0].data.pose_position = "POSE"
            ad = arms[0].animation_data or arms[0].animation_data_create()
            ad.action = acts[hit[0]]
            if hasattr(ad, "action_slot") and len(acts[hit[0]].slots):
                ad.action_slot = acts[hit[0]].slots[0]
            scene.frame_set(0)
            render(scene, os.path.join(a.out, "reimport_clay_%s.png" % clip), d, V((0, 0, 0.6)))
    print("RT_ERRORS", err)
    print("RT_SUMMARY", json.dumps({"height": rep["height_m_rest"], "lods": lods, "actions": rep["imported_actions"],
                                    "empties": [(e["name"], e["parent"], e["parent_bone"]) for e in empties]}))
    sys.exit(1 if err else 0)


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--dir", required=True)
    p.add_argument("--out", required=True)
    main(p.parse_args(sys.argv[sys.argv.index("--") + 1:]))
