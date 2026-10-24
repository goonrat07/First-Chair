"""Pose-sheet renders for the Phase 6/8 gate: every clip in CLIPS at front (ortho), side (ortho) and the game
camera direction (az 30, el 45, 35 mm, pulled in), LOD0 body with helmet, boots and the real PR_Ski and PR_SkiPole
from POSE_TEST on the sockets, over a snow plane at z -0.045. Workbench, material colours. Also measures the
lowest body, boot, ski and pole points per pose. Compose with build/compose_pose_sheet.py (Pillow).

  $BLENDER_BIN -b --python build/pose_sheet.py -- --master CH_Skier_master.blend --out review/06_rig/poses \
    --closeup Tuck,Crouch,Brake,CarveLeft,Crash
"""
import bpy, os, sys, math, json, argparse
from mathutils import Vector as V

SNOW = -0.045
CLIPS = ["Stance", "CarveLeft", "CarveRight", "Tuck", "Crouch", "Air", "Brake", "Pole", "Crash"]
GRABS = ["GrabMute", "GrabTip", "GrabTail", "GrabJapan", "GrabSafety"]
FRAME = {"Pole": 9}
CYCLE = {"Pole": [0, 6, 12, 18, 24], "Stance": [0, 15]}


def main(a):
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.master))
    scene = bpy.context.scene
    os.makedirs(a.out, exist_ok=True)

    def walk(lc):
        lc.exclude = False
        for c in lc.children:
            walk(c)
    walk(bpy.context.view_layer.layer_collection)
    keep = set()
    for cn in ("LOW_LOD0", "LOW_PROPS", "POSE_TEST"):
        keep |= {o.name for o in bpy.data.collections[cn].all_objects}
    for o in scene.objects:
        o.hide_render = o.name not in keep
        o.hide_set(o.name not in keep)
    me = bpy.data.meshes.new("_snow")
    s = 6
    me.from_pydata([(-s, -s, SNOW), (s, -s, SNOW), (s, s, SNOW), (-s, s, SNOW)], [], [(0, 1, 2, 3)])
    mat = bpy.data.materials.new("_snow")
    mat.diffuse_color = (0.80, 0.84, 0.90, 1)
    me.materials.append(mat)
    snow = bpy.data.objects.new("_snow", me)
    scene.collection.objects.link(snow)
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light, sh.color_type = "STUDIO", "MATERIAL"
    sh.show_shadows, sh.shadow_intensity = True, 0.35
    sh.show_cavity, sh.cavity_type = True, "WORLD"
    sh.show_object_outline = True
    scene.display.light_direction = (0.45, -0.55, 0.7)
    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.30, 0.36, 0.44)
    scene.render.film_transparent = False
    scene.render.resolution_x = scene.render.resolution_y = a.res
    scene.render.image_settings.file_format = "PNG"
    cam = bpy.data.objects.new("_cam", bpy.data.cameras.new("_cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    az, el = math.radians(30), math.radians(45)
    views = {"front": (V((0, -1, 0)), "ORTHO"), "side": (V((1, 0, 0)), "ORTHO"),
             "game": (V((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))), "PERSP")}
    arm = bpy.data.objects["CH_Skier_Rig"]
    ad = arm.animation_data
    dg = bpy.context.evaluated_depsgraph_get()
    meas = {}

    def shoot(path, d, typ, target=V((0, 0, 0.72)), scale=3.1):
        cam.data.type = typ
        if typ == "ORTHO":
            cam.data.ortho_scale = scale
            cam.location = target + d * 10
        else:
            cam.data.lens = 35
            cam.location = target + d * 3.9
        cam.rotation_mode = "QUATERNION"
        cam.rotation_quaternion = (-d).to_track_quat("-Z", "Y")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)

    for clip in CLIPS + GRABS:
        act = bpy.data.actions[clip]
        ad.action = act
        if hasattr(ad, "action_slot") and len(act.slots):
            ad.action_slot = act.slots[0]
        scene.frame_set(FRAME.get(clip, 15))
        dg.update()
        m = {}
        for o in scene.objects:
            if o.type != "MESH" or o.hide_render or o.name == "_snow":
                continue
            eo = o.evaluated_get(dg)
            z, i = min(((eo.matrix_world @ v.co).z, v.index) for v in eo.to_mesh().vertices)
            eo.to_mesh_clear()
            key = "ski" if "Ski_" in o.name else "pole" if "Pole" in o.name else "boot" if "Boot" in o.name else "body"
            if z < m.get(key, 9):
                m[key] = round(z, 4)
                if key == "body" and o.vertex_groups:
                    g = max(o.data.vertices[i].groups, key=lambda g: g.weight)
                    m["body_lowest_bone"] = o.vertex_groups[g.group].name
        meas[clip] = m
        for vn, (d, typ) in views.items():
            shoot(os.path.join(a.out, "%s_%s.png" % (clip, vn)), d, typ)
        for f in CYCLE.get(clip, []):
            scene.frame_set(f)
            shoot(os.path.join(a.out, "cycle_%s_%02d.png" % (clip, f)), views["side"][0], "ORTHO")
    for clip in [c for c in a.closeup.split(",") if c]:   # joint close-ups: front and back three-quarter
        act = bpy.data.actions[clip]
        ad.action = act
        if hasattr(ad, "action_slot") and len(act.slots):
            ad.action_slot = act.slots[0]
        scene.frame_set(FRAME.get(clip, 15))
        for vn, az_d, el_d in (("fq", -40, 20), ("bq", 145, 20)):
            az2, el2 = math.radians(az_d), math.radians(el_d)
            d = V((math.sin(az2) * math.cos(el2), -math.cos(az2) * math.cos(el2), math.sin(el2)))
            shoot(os.path.join(a.out, "closeup_%s_%s.png" % (clip, vn)), d, "ORTHO", V((0, 0, 0.62)), 1.7)
    with open(os.path.join(a.out, "lowest_points.json"), "w") as f:
        json.dump({"snow_z": SNOW, "lowest_z_by_part": meas}, f, indent=2)
    print("LOWEST", json.dumps(meas))


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--res", type=int, default=420)
    p.add_argument("--closeup", default="", help="comma list of clips for joint close-ups")
    main(p.parse_args(sys.argv[sys.argv.index("--") + 1:]))
