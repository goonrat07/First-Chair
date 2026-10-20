"""CH_Skier blockout: primary masses in the rigging rest pose (relaxed A-pose, arms 40 deg down).

  $BLENDER_BIN --background --python build/02_blockout.py -- --master CH_Skier_master.blend

Primitive volumes only: bevelled boxes for the jacket, pelvis, collar, neck warmer, goggles, mittens and
boots, tapered cylinders with joint spheres for the limbs, a cut ellipsoid for the helmet. Stripes, zip,
knee patches and the goggle strap are surface detail for later phases. Joint centres (shoulder, elbow,
wrist, hip, knee, ankle) sit at the proportion-table positions, and the four sockets are placed now.
Every constant is from ref/refs.json "proportions_m" (our own table, all inferred, see asset-brief.md);
the comment names the reference view that checks it. Colours come from refs.json "palette" (preview only).
"""
import os, sys, math, json, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402  (the skill's helpers; this file is a phase script built on them)
import bpy, bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

T.PHASE = "02_blockout"
T.OWNER_TAG = "phase:" + T.PHASE
HERE = os.path.dirname(os.path.abspath(__file__))
REFS = os.path.normpath(os.path.join(HERE, "../ref/refs.json"))

# ARMS (front view): shoulder joint, A-pose angle, segment lengths, sleeve radii
SHOULDER = (0.18, 0.02, 1.36)     # front: shoulder top 1.445 = 1.36 + 0.085; width 0.53 at the caps
ARM_DEG = 40.0                     # front: arms 40 deg below horizontal (rest pose for rigging)
UPPER, FORE = 0.29, 0.26           # front: elbow and wrist positions along the arm line
SLEEVE_R = (0.085, 0.075, 0.066)   # front and side: baggy sleeve at shoulder, elbow, sleeve end
FOREARM_END = -0.03                # front: sleeve ends 0.03 before the wrist
CUFF = (-0.04, 0.02, 0.06)         # front: dark cuff from / to along the arm from the wrist, radius
MITTEN = (0.19, 0.12, 0.085)       # front: length 0.19, thickness 0.085; side: width 0.12 (about 15% oversized)
MITTEN_OFF, MITTEN_R = 0.10, 0.03
THUMB = (-0.04, -0.02, -0.085, 0.025)  # side: thumb points forward (-Y)
# TORSO (front and side)
TORSO = ((0.0, 0.02, 1.13), (0.46, 0.30, 0.62), 0.07)    # jacket box: z 0.82 to 1.44, boxy, oversized
HEM = ((0.0, 0.02, 0.88), (0.48, 0.32, 0.12), 0.04)      # jacket hem flare, bottom at 0.82
PELVIS = ((0.0, 0.03, 0.86), (0.38, 0.28, 0.18), 0.07)   # trouser seat 0.38 wide, crotch 0.77
COLLAR = ((0.0, 0.03, 1.46), (0.30, 0.26, 0.12), 0.05)   # hood collar, front: 0.30 wide
NECK_WARMER = ((0.0, -0.02, 1.49), (0.17, 0.19, 0.14), 0.05)  # front: 0.17 wide, z 1.42 to 1.56
# HEAD (front and side)
HELMET = ((0.0, 0.01, 1.60), (0.13, 0.155, 0.15), 1.53)  # ellipsoid radii, rim cut at z 1.53; top 1.75
GOGGLES = ((0.0, -0.135, 1.60), (0.22, 0.06, 0.085), 0.03)
LENS = ((0.0, -0.166, 1.60), (0.19, 0.012, 0.06), 0.005)
# LEGS (front and side)
HIP = (0.10, 0.03, 0.88)
KNEE = (0.12, 0.0, 0.50)           # front and side: knee height 0.50
HEM_PT = (0.13, 0.01, 0.29)        # front: trouser hem at 0.29, flat
THIGH_R, SHIN_R = (0.105, 0.095), (0.095, 0.10)          # baggy, flares a little over the boot
BOOT_X = 0.13                      # front: boot centres 0.26 apart (feet hip-width)
BOOT_FOOT = ((-0.03, 0.065), (0.115, 0.32, 0.13), 0.03)  # side: 0.32 long, toe at y -0.19; sole z 0
BOOT_SHAFT = ((0.02, 0.19), (0.12, 0.16, 0.30), 0.04)    # side: cuff top 0.34 (under the trousers)
BOOT_BAND = ((0.02, 0.26), (0.13, 0.17, 0.06), 0.02)     # side: rust band z 0.23 to 0.29
ANKLE_Z = 0.11                     # inferred joint height inside the boot


def hex_lin(h):
    def c(v):
        v /= 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return (c(int(h[1:3], 16)), c(int(h[3:5], 16)), c(int(h[5:7], 16)), 1.0)


PALETTE = {k: v["hex"] for k, v in json.load(open(REFS))["palette"].items()}


def mat(role):
    name = "M_CH_Skier_" + role
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    rgba = hex_lin(PALETTE[role])
    m.diffuse_color = rgba
    try:
        m.use_nodes = True
    except Exception:
        pass
    if m.node_tree:
        b = m.node_tree.nodes.get("Principled BSDF")
        if b:
            b.inputs["Base Color"].default_value = rgba
            b.inputs["Roughness"].default_value = 0.7
    return m


def paint(ob, role):
    ob.data.materials.clear()
    ob.data.materials.append(mat(role))
    return ob


def rbox(name, c, size, r, role, rot_y=0.0):
    ob = T.box(name, size, location=c, rotation=(0.0, math.radians(rot_y), 0.0))
    if r > 0:
        T.bevel(ob, width=r, segments=2)
    return paint(ob, role)


def limb(name, p0, p1, r0, r1, role, segs=16):
    """Tapered cylinder from p0 to p1, flat ends."""
    p0, p1 = Vector(p0), Vector(p1)
    v = p1 - p0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r0, radius2=r1, depth=v.length)
    bmesh.ops.translate(bm, vec=(0, 0, v.length / 2), verts=bm.verts)
    rot = Vector((0, 0, 1)).rotation_difference(v.normalized()).to_matrix()
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=rot, verts=bm.verts)
    return paint(T.new_mesh_object(name, bm, "LOW", location=p0), role)


def ball(name, c, r, role):
    return paint(T.sphere(name, r, location=c, u=16, v=8), role)


def helmet(name, c, radii, zmin, role):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=1.0)
    bmesh.ops.scale(bm, vec=radii, verts=bm.verts)
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, zmin),
                           plane_no=(0, 0, 1), clear_inner=True)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    return paint(T.new_mesh_object(name, bm, "LOW"), role)


def socket(name, loc, rot_z=math.pi):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type, ob.empty_display_size = "ARROWS", 0.08
    ob.location, ob.rotation_euler = loc, (0.0, 0.0, rot_z)   # +Y = the figure's forward (-Y world)
    T.col("SOCKETS").objects.link(ob)
    return T.own(ob)


def arm_frame(s):
    a = math.radians(ARM_DEG)
    d = Vector((s * math.cos(a), 0.0, -math.sin(a)))
    n_out = Vector((s * math.sin(a), 0.0, math.cos(a)))
    S = Vector((s * SHOULDER[0], SHOULDER[1], SHOULDER[2]))
    E = S + d * UPPER
    W = E + d * FORE
    return S, E, W, d, n_out


def build(scene, args):
    for c, size, r, role, n in (TORSO + ("jacket", "Torso"), HEM + ("jacket", "Hem"), PELVIS + ("trousers", "Pelvis"),
                                COLLAR + ("jacket", "Collar"), NECK_WARMER + ("neck_warmer", "NeckWarmer")):
        rbox("CH_Skier_%s_block" % n, c, size, r, role)
    sl, sr = arm_frame(1)[0], arm_frame(-1)[0]
    limb("CH_Skier_ShoulderBar_block", sr, sl, SLEEVE_R[0], SLEEVE_R[0], "jacket")
    helmet("CH_Skier_Helmet_block", HELMET[0], HELMET[1], HELMET[2], "helmet")
    rbox("CH_Skier_Goggles_block", GOGGLES[0], GOGGLES[1], GOGGLES[2], "goggle_frame")
    rbox("CH_Skier_Lens_block", LENS[0], LENS[1], LENS[2], "goggle_lens")
    a = ARM_DEG
    for s, sfx in ((1, "L"), (-1, "R")):
        S, E, W, d, n_out = arm_frame(s)
        ball("CH_Skier_Shoulder_%s_block" % sfx, S, SLEEVE_R[0], "jacket")
        limb("CH_Skier_UpperArm_%s_block" % sfx, S, E, SLEEVE_R[0], SLEEVE_R[1], "jacket")
        ball("CH_Skier_Elbow_%s_block" % sfx, E, SLEEVE_R[1], "jacket")
        limb("CH_Skier_Forearm_%s_block" % sfx, E, W + d * FOREARM_END, SLEEVE_R[1], SLEEVE_R[2], "jacket")
        limb("CH_Skier_Cuff_%s_block" % sfx, W + d * CUFF[0], W + d * CUFF[1], CUFF[2], CUFF[2], "cuff")
        M = W + d * MITTEN_OFF
        rbox("CH_Skier_Mitten_%s_block" % sfx, M, MITTEN, MITTEN_R, "mitten", rot_y=a if s > 0 else -a)
        t0 = M + d * THUMB[0]
        t1 = M + d * THUMB[1] + Vector((0.0, THUMB[2], 0.0))
        limb("CH_Skier_Thumb_%s_block" % sfx, t0, t1, THUMB[3], THUMB[3], "mitten", segs=12)
        ball("CH_Skier_ThumbTip_%s_block" % sfx, t1, THUMB[3], "mitten")
        hip = Vector((s * HIP[0], HIP[1], HIP[2]))
        knee = Vector((s * KNEE[0], KNEE[1], KNEE[2]))
        hem = Vector((s * HEM_PT[0], HEM_PT[1], HEM_PT[2]))
        ball("CH_Skier_Hip_%s_block" % sfx, hip, THIGH_R[0], "trousers")
        limb("CH_Skier_Thigh_%s_block" % sfx, hip, knee, THIGH_R[0], THIGH_R[1], "trousers")
        ball("CH_Skier_Knee_%s_block" % sfx, knee, THIGH_R[1], "trousers")
        limb("CH_Skier_Shin_%s_block" % sfx, knee, hem, SHIN_R[0], SHIN_R[1], "trousers")
        bx = s * BOOT_X
        for part, (yz, size, r), role in (("BootFoot", BOOT_FOOT, "boot"), ("BootShaft", BOOT_SHAFT, "boot"),
                                          ("BootBand", BOOT_BAND, "boot_band")):
            rbox("CH_Skier_%s_%s_block" % (part, sfx), (bx, yz[0], yz[1]), size, r, role)
        socket("SOCKET_ski_%s" % sfx, (bx, BOOT_FOOT[0][0], 0.0))          # under the sole, boot centre
        socket("SOCKET_hand_%s" % sfx, M - n_out * 0.02)                    # palm centre, inside the mitten
    T.log("blockout built")
    measure(args)


def measure(args):
    """World bounds of the evaluated LOW meshes, a few key extents and the triangle count."""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    lo, hi, tris, per = [1e9] * 3, [-1e9] * 3, 0, {}
    for ob in T.col("LOW").all_objects:
        if ob.type != "MESH":
            continue
        eo = ob.evaluated_get(dg)
        me = eo.to_mesh()
        pts = [eo.matrix_world @ v.co for v in me.vertices]
        tris += sum(len(p.vertices) - 2 for p in me.polygons)
        eo.to_mesh_clear()
        a = [min(p[i] for p in pts) for i in range(3)]
        b = [max(p[i] for p in pts) for i in range(3)]
        per[ob.name] = [round(v, 4) for v in a + b]
        lo, hi = [min(lo[i], a[i]) for i in range(3)], [max(hi[i], b[i]) for i in range(3)]
    def get(n, i):
        return per["CH_Skier_%s_block" % n][i]
    out = {"bbox_min": [round(v, 4) for v in lo], "bbox_max": [round(v, 4) for v in hi],
           "total_height": round(hi[2] - lo[2], 4), "fingertip_span": round(hi[0] - lo[0], 4),
           "depth": round(hi[1] - lo[1], 4), "shoulder_top_z": get("ShoulderBar", 5),
           "shoulder_width": round(get("Shoulder_L", 3) - get("Shoulder_R", 0), 4),
           "jacket_hem_z": get("Hem", 2), "helmet_width": round(get("Helmet", 3) - get("Helmet", 0), 4),
           "helmet_rim_z": get("Helmet", 2), "seat_width": round(get("Pelvis", 3) - get("Pelvis", 0), 4),
           "trouser_hem_z": get("Shin_L", 2), "boot_length": round(get("BootFoot_L", 4) - get("BootFoot_L", 1), 4),
           "tris_evaluated": tris, "objects": per}
    path = os.path.join(os.path.dirname(os.path.abspath(args.master)), "review", T.PHASE, "measure.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    json.dump(out, open(path, "w"), indent=1)
    T.log("measure", json.dumps({k: v for k, v in out.items() if k != "objects"}))


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
