"""
LOOK RENDER (tree-kit, Phase 4)
Colour review renders of a tree on white snow under a strong low sun with blue sky-lit shadows
(art/reference/style-notes-gma2.md, "Overall look"). Cycles on the CPU, so it runs headless.
The lighting is a review stage only; the game's lighting is built in Unity.

blender --background --python .claude/skills/tree-kit/scripts/look_render.py -- \
  --blend art/source/EN_Conifer/EN_Conifer_master.blend --params art/source/EN_Conifer/EN_Conifer_params.json \
  --out art/source/EN_Conifer/review/04_look --collections LOD0

Shots (--shots, comma list, default all):
  threequarter   az 35, el 12, 50 mm, fitted to the tree
  front, top     orthographic, fitted
  game           the follow camera from params camera.game (az, el, lens, distance) aimed at a 1.75 m
                 skier proxy standing by the trunk (look.skier_proxy_xy), as the player sees it; tall
                 trees are cut by the frame top (accepted at the 2026-10-20 review). render_info.json
                 also reports the tree's on-screen box with the camera aimed at the tree's mid-height.
  cluster_game   params cluster.count linked duplicates (shared mesh, as Unity instances them) with seeded
                 random scale and rotation, game camera angle, distance fitted to the cluster
  cluster_low    the same cluster from a low eye-level angle
Camera fitting and view directions come from blender-image-to-3d's review_render.py (imported, not copied).
"""
import argparse
import json
import math
import os
import random
import sys

import bpy
import bmesh
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "blender-image-to-3d", "scripts")))
import tree_params as TP  # noqa: E402
import review_render as RR  # noqa: E402

SHOTS = {
    "threequarter": {"az": 35, "el": 12, "lens": 50, "res": (1200, 1600)},
    "front": {"az": 0, "el": 0, "lens": 0, "res": (1000, 1600)},
    "top": {"top": True, "res": (1200, 1200)},
    "game": {"game": True, "res": (1920, 1080)},
    "cluster_game": {"cluster": True, "res": (1920, 1080)},
    "cluster_low": {"cluster": True, "az": 30, "el": 6, "lens": 35, "res": (1920, 1080)},
}


def parse(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--blend", required=True)
    p.add_argument("--params", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--collections", default="LOD0")
    p.add_argument("--shots", default=",".join(SHOTS))
    p.add_argument("--samples", type=int, default=48)
    p.add_argument("--res-scale", type=float, default=1.0)
    return p.parse_args(argv)


def material(name, rgb, rough):
    m = bpy.data.materials.new(name)
    if bpy.app.version < (5, 0, 0):
        m.use_nodes = True
    b = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if b is None:
        b = m.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        o = next((n for n in m.node_tree.nodes if n.type == "OUTPUT_MATERIAL"), None) or m.node_tree.nodes.new("ShaderNodeOutputMaterial")
        m.node_tree.links.new(b.outputs["BSDF"], o.inputs["Surface"])
    b.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1)
    b.inputs["Roughness"].default_value = rough
    return m


def stage(scene, L):
    """Snow ground, a sky that lights blue but reads pale to the camera, and one low warm sun."""
    me = bpy.data.meshes.new("_look_ground")
    s = 300.0
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    me.materials.append(material("_look_snow_ground", L["ground"], 0.95))
    g = bpy.data.objects.new("_look_ground", me)
    scene.collection.objects.link(g)
    w = bpy.data.worlds.new("_look_world")
    scene.world = w
    if bpy.app.version < (5, 0, 0):
        w.use_nodes = True
    nt = w.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    light = nt.nodes.new("ShaderNodeBackground")
    light.inputs[0].default_value = (*L["sky_light"], 1)
    light.inputs[1].default_value = 1.0
    view = nt.nodes.new("ShaderNodeBackground")
    view.inputs[0].default_value = (*L["sky_view"], 1)
    view.inputs[1].default_value = 1.0
    lp = nt.nodes.new("ShaderNodeLightPath")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(light.outputs[0], mix.inputs[1])
    nt.links.new(view.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    ld = bpy.data.lights.new("_look_sun", "SUN")
    ld.energy, ld.color, ld.angle = L["sun_strength"], L["sun_colour"], math.radians(0.6)
    sun = bpy.data.objects.new("_look_sun", ld)
    d = RR.view_direction(L["sun_az"], L["sun_el"])
    sun.rotation_mode = "QUATERNION"
    sun.rotation_quaternion = (-d).to_track_quat("-Z", "Y")
    scene.collection.objects.link(sun)


def skier_proxy(scene, loc):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.22, radius2=0.22, depth=1.75)
    bmesh.ops.translate(bm, vec=(0, 0, 0.875), verts=bm.verts)
    me = bpy.data.meshes.new("_look_skier_proxy")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material("_look_proxy", (0.05, 0.05, 0.06), 0.6))
    o = bpy.data.objects.new("_look_skier_proxy", me)
    o.location = loc
    scene.collection.objects.link(o)
    return o


def cluster(scene, objs, C):
    rng = random.Random(C["seed"])
    pts = [(0.0, 0.0)]
    tries = 0
    while len(pts) < C["count"] and tries < 5000:
        tries += 1
        p = (rng.uniform(-C["radius_m"], C["radius_m"]), rng.uniform(-C["radius_m"], C["radius_m"]))
        if math.hypot(*p) <= C["radius_m"] and all(math.hypot(p[0] - q[0], p[1] - q[1]) >= C["min_spacing_m"] for q in pts):
            pts.append(p)
    made = []
    for x, y in pts:
        s = rng.uniform(*C["scale"])
        rz = rng.uniform(0, 2 * math.pi)
        for o in objs:
            c = o.copy()                       # linked duplicate: the mesh is shared, like an instance
            c.location, c.rotation_euler, c.scale = (x, y, 0), (0, 0, rz), (s, s, s)
            scene.collection.objects.link(c)
            made.append(c)
    return made, pts


def px_box(scene, cam, objs):
    W = scene.render.resolution_x
    H = scene.render.resolution_y
    xs, ys = [], []
    for o in objs:
        for c in o.bound_box:
            v = world_to_camera_view(scene, cam, o.matrix_world @ Vector(c))
            xs.append(v.x)
            ys.append(v.y)
    return {"height_px": round((max(ys) - min(ys)) * H, 1), "width_px": round((max(xs) - min(xs)) * W, 1),
            "in_frame": min(xs) >= 0 and max(xs) <= 1 and min(ys) >= 0 and max(ys) <= 1,
            "top_y_frac": round(max(ys), 3), "bottom_y_frac": round(min(ys), 3)}


def aim(cam, target, dirv, dist, lens):
    cam.data.type, cam.data.lens, cam.data.sensor_fit, cam.data.sensor_width = "PERSP", lens, "HORIZONTAL", 36.0
    cam.location = target + dirv * dist
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (-dirv).to_track_quat("-Z", "Y")
    cam.data.clip_start, cam.data.clip_end = 0.05, 2000.0


def main(a):
    P = TP.load(a.params)
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.blend))
    scene = bpy.context.scene
    os.makedirs(a.out, exist_ok=True)
    names = [n for n in a.collections.split(",") if n]
    tree = []
    for n in names:
        c = bpy.data.collections.get(n)
        tree += [o for o in c.all_objects if o.type == "MESH"] if c else []
    if not tree:
        raise RuntimeError("nothing to render in collections " + a.collections)
    RR.apply_visibility(scene, tree)
    stage(scene, P["look"])
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = a.samples
    try:
        scene.cycles.use_denoising = True
        scene.cycles.denoiser = "OPENIMAGEDENOISE"
    except Exception as e:   # noqa: BLE001
        print("WARNING denoiser unavailable:", e)
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.resolution_percentage = 100
    cam = bpy.data.objects.new("_look_cam", bpy.data.cameras.new("_look_cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam

    g = P["camera"]["game"]
    H = max((o.matrix_world @ Vector(c)).z for o in tree for c in o.bound_box)
    proxy = skier_proxy(scene, Vector(P["look"]["skier_proxy_xy"] + [0.0]))
    copies, pts = [], []
    info = {"blend": os.path.abspath(a.blend), "collections": names, "look": P["look"], "shots": {}}
    for shot in [s for s in a.shots.split(",") if s]:
        spec = SHOTS[shot]
        W, Hh = int(spec["res"][0] * a.res_scale), int(spec["res"][1] * a.res_scale)
        scene.render.resolution_x, scene.render.resolution_y = W, Hh
        if spec.get("cluster") and not copies:
            copies, pts = cluster(scene, tree, P["cluster"])
            for o in tree:
                o.hide_render = True       # the originals sit at the origin; copy 0 replaces them
        for o in copies:
            o.hide_render = not spec.get("cluster")
        for o in tree:
            o.hide_render = bool(spec.get("cluster"))
        proxy.hide_render = shot not in ("game", "threequarter")
        subject = copies if spec.get("cluster") else tree
        corners, lo, hi = RR.world_corners(subject)
        center = (lo + hi) / 2
        rec = {}
        if spec.get("game"):
            dirv = RR.view_direction(g["az"], g["el"])
            skier = Vector((proxy.location.x, proxy.location.y, g["skier_target_z_m"]))
            aim(cam, skier, dirv, g["dist_m"], g["lens"])
            rec["aim"] = "follow camera on the skier proxy at (%.2f, %.2f, %.3f), %.1f m" % (skier.x, skier.y, skier.z, g["dist_m"])
        elif spec.get("cluster") and "az" not in spec:
            dirv = RR.view_direction(g["az"], g["el"])
            rec["dist_m"] = round(RR.fit_camera(cam, corners, center, dirv, W, Hh, g["lens"], 1.08), 2)
        elif spec.get("top"):
            rec["dist_m"] = round(RR.fit_camera(cam, corners, center, Vector((0, 0, 1)), W, Hh, 0, 1.15), 2)
        else:
            dirv = RR.view_direction(spec["az"], spec["el"])
            rec["dist_m"] = round(RR.fit_camera(cam, corners, center, dirv, W, Hh, spec["lens"], 1.12), 2)
        rec.update({"camera": [round(v, 3) for v in cam.location], "lens": cam.data.lens if cam.data.type == "PERSP" else 0,
                    "ortho_scale": cam.data.ortho_scale if cam.data.type == "ORTHO" else None, "res": [W, Hh]})
        bpy.context.view_layer.update()
        rec["tree_box"] = px_box(scene, cam, subject)
        if shot == "game":
            # For the record: the same camera aimed at the tree's mid-height (whole tree, if it fits at all).
            aim(cam, Vector((0, 0, H / 2)), dirv, g["dist_m"], g["lens"])
            bpy.context.view_layer.update()
            rec["aimed_at_tree_mid_height"] = px_box(scene, cam, tree)
            aim(cam, skier, dirv, g["dist_m"], g["lens"])
            bpy.context.view_layer.update()
        if spec.get("cluster"):
            rec["instances"] = [{"xy": [round(x, 2), round(y, 2)], "scale": round(c.scale[0], 3),
                                 "rot_z_deg": round(math.degrees(c.rotation_euler.z), 1)}
                                for (x, y), c in zip(pts, copies[::max(1, len(tree))])]
        path = os.path.join(a.out, shot + ".png")
        scene.render.filepath = os.path.abspath(path)
        bpy.ops.render.render(write_still=True)
        info["shots"][shot] = rec
        print("SHOT", shot, json.dumps(rec))
    with open(os.path.join(a.out, "render_info.json"), "w") as f:
        json.dump(info, f, indent=2)


if __name__ == "__main__":
    main(parse(sys.argv))
