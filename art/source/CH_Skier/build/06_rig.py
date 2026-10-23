"""CH_Skier Phase 6: deformation skeleton, skin weights, rigid props and sockets.

Skeleton (RIG_DEF, object CH_Skier_Rig, rest = the relaxed A-pose of the mesh, arms 40 deg down): Root at the
ground between the feet, Hips, Spine, Chest, Neck, Head, and per side Shoulder, UpperArm, LowerArm, Hand,
UpperLeg, LowerLeg, Foot. Joint centres from the brief (ARTICULATION). Every bone deforms (FBX
use_armature_deform_only keeps only deforming bones), Root carries no weights.

Weights: the body is 25 open shells that overlap (shoulder puff over sleeve, hem over trousers), so Blender's heat
weighting would weight each shell on its own and overlapping shells would tear apart. Instead every deforming
shell samples ONE analytic weight field of position: trunk blends along z, arm and leg chains blend across each
joint with smoothstep falloffs measured along the bone axes, and arm and leg regions mix into the trunk around
the shoulder and hip (less behind the hip joint, so the seat stays under the jacket back). Overlapping shells therefore get the same weights where they overlap, at every LOD. The
falloffs replace mesh smoothing (which would differ per shell). Rigid pieces: face parts on Head, mittens and
thumbs on Hand; helmet with goggles on Head, boot shells on Foot, boot cuffs on LowerLeg (hinged at the ankle).
At most 4 influences, influences under 0.01 dropped, normalised.

Sockets: SOCKET_ski_L/R on Foot_L/R at the sole under the boot centre (+Y = figure forward, +Z up).
SOCKET_hand_L/R on Hand_L/R at the palm centre: +Z runs up the pole (thumb side), -Z down the shaft, +Y along the
hand toward the fingertips; PR_SkiPole's SOCKET_grip aligns to it.

  $BLENDER_BIN -b --python build/06_rig.py -- --master CH_Skier_master.blend
"""
import os, sys, math, json, argparse
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "../../../../.claude/skills/blender-image-to-3d/assets")))
import build_template as T  # noqa: E402
import bpy, bmesh  # noqa: E402
from mathutils import Vector as V, Matrix as M  # noqa: E402

T.PHASE = "06_rig"
T.OWNER_TAG = "phase:" + T.PHASE
RIG = "CH_Skier_Rig"

# JOINT CENTRES (brief ARTICULATION, left side; right mirrors X)
SH, EL, WR = V((0.18, 0.02, 1.36)), V((0.402, 0.02, 1.174)), V((0.601, 0.02, 1.006))
HIP, KN, AN = V((0.10, 0.03, 0.88)), V((0.12, 0.0, 0.50)), V((0.13, 0.02, 0.11))
CLAV = V((0.04, 0.02, 1.34))            # inferred: clavicle root inside the chest
HAND_LEN = 0.15                          # inferred: wrist to mitten tip direction
TOE = V((0.13, -0.15, 0.06))             # inferred: foot bone tail toward the toe
TRUNK = [("Hips", (0, 0.03, 0.88), (0, 0.02, 1.00)), ("Spine", (0, 0.02, 1.00), (0, 0.02, 1.15)),
         ("Chest", (0, 0.02, 1.15), (0, 0.02, 1.36)), ("Neck", (0, 0.02, 1.36), (0, 0.0, 1.46)),
         ("Head", (0, 0.0, 1.46), (0, 0.0, 1.70))]
SOCKET_SKI = V((0.13, -0.03, 0.0))
SOCKET_HAND = V((0.66507, 0.02, 0.92687))  # palm centre placed in the blockout
A1, A2 = (EL - SH).normalized(), (WR - EL).normalized()
NE = (A1 + A2).normalized()
T1, T2 = (KN - HIP).normalized(), (AN - KN).normalized()
NK = (T1 + T2).normalized()
BODY = ["CH_Skier_Body_LOD0", "CH_Skier_Body_LOD1", "CH_Skier_Body_LOD2"]
RIGID = {"CH_Skier_Helmet": "Head", "CH_Skier_BootShell_L": "Foot_L", "CH_Skier_BootShell_R": "Foot_R",
         "CH_Skier_BootCuff_L": "LowerLeg_L", "CH_Skier_BootCuff_R": "LowerLeg_R"}


def mx(v, sg):
    return V((v[0] * sg, v[1], v[2]))


def bone_list():
    B = [("Root", (0, 0, 0), (0, 0, 0.2), None, 0, True)]
    for i, (n, h, t) in enumerate(TRUNK):
        B.append((n, h, t, "Root" if i == 0 else TRUNK[i - 1][0], 0, True))
    tip = WR + A2 * HAND_LEN
    for s, sg in (("L", 1), ("R", -1)):
        B += [("Shoulder_" + s, mx(CLAV, sg), mx(SH, sg), "Chest", 0, True),
              ("UpperArm_" + s, mx(SH, sg), mx(EL, sg), "Shoulder_" + s, 0, True),
              ("LowerArm_" + s, mx(EL, sg), mx(WR, sg), "UpperArm_" + s, 0, True),
              ("Hand_" + s, mx(WR, sg), mx(tip, sg), "LowerArm_" + s, 0, True),
              ("UpperLeg_" + s, mx(HIP, sg), mx(KN, sg), "Hips", 0, True),
              ("LowerLeg_" + s, mx(KN, sg), mx(AN, sg), "UpperLeg_" + s, 0, True),
              ("Foot_" + s, mx(AN, sg), mx(TOE, sg), "LowerLeg_" + s, 0, True)]
    return B


# WEIGHT FIELD

def ss(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def trunk(z, head_ok=False):
    segs = [("Hips", "Spine", 0.95, 1.05), ("Spine", "Chest", 1.10, 1.20), ("Chest", "Neck", 1.36, 1.42),
            ("Neck", "Head", 1.46, 1.52)]
    cur = {"Hips": 1.0}
    for a, b, z0, z1 in (segs if head_ok else segs[:3]):
        f = ss(z0, z1, z)
        if f <= 0:
            break
        wa = cur.pop(a)
        cur[a], cur[b] = wa * (1 - f), wa * f
    return cur


def arm_chain(pl, s):
    s1 = (pl - SH).dot(A1)
    up = ss(-0.05, 0.05, s1)
    lo = ss(-0.05, 0.05, (pl - EL).dot(NE))
    h = ss(-0.03, 0.015, (pl - WR).dot(A2))
    return {"Shoulder_" + s: 1 - up, "UpperArm_" + s: up * (1 - lo), "LowerArm_" + s: up * lo * (1 - h),
            "Hand_" + s: up * lo * h}, s1


def leg_chain(pl, s):
    lo = ss(-0.06, 0.06, (pl - KN).dot(NK))
    return {"UpperLeg_" + s: 1 - lo, "LowerLeg_" + s: lo}


def add(dst, src, k):
    for b, w in src.items():
        dst[b] = dst.get(b, 0.0) + w * k


def field(p, cls):
    if cls == "head":
        return {"Head": 1.0}
    if cls in ("hand_L", "hand_R"):
        return {"Hand_" + cls[-1]: 1.0}
    tr = trunk(p.z)
    if cls in ("hood", "neck"):
        return tr
    # legs: side split only near the crotch, so a knee never takes weight from the other leg
    hard = 1.0 if p.x > 0 else 0.0
    sL = hard + (ss(-0.05, 0.05, p.x) - hard) * ss(0.66, 0.76, p.z)
    w = {}
    for s, sg, ws in (("L", 1, sL), ("R", -1, 1 - sL)):
        if ws <= 0:
            continue
        pl = V((p.x * sg, p.y, p.z))
        m = ss(-0.03, 0.10, (pl - HIP).dot(T1))
        # behind the hip joint the seat and the jacket back stay on Hips, or the seat pokes through the jacket back
        # when the thigh flexes (joints sheet, first pass)
        m *= 1.0 - 0.85 * ss(0.05, 0.13, p.y - HIP.y)
        add(w, tr, ws * (1 - m))
        add(w, leg_chain(pl, s), ws * m)
    if cls in ("sleeve", "cuff", "puff", "jacket"):
        s, sg = ("L", 1) if p.x >= 0 else ("R", -1)
        chain, s1 = arm_chain(V((p.x * sg, p.y, p.z)), s)
        m = ss(-0.07, 0.04, s1) * (ss(1.24, 1.34, p.z) if cls == "jacket" else 1.0)
        if m > 0:
            w = {b: v * (1 - m) for b, v in w.items()}
            add(w, chain, m)
    return w


def finish(w, maxinf=4, tiny=0.01):
    items = sorted(((b, v) for b, v in w.items() if v > 1e-6), key=lambda x: -x[1])[:maxinf]
    tot = sum(v for _, v in items)
    items = [(b, v / tot) for b, v in items]
    items = [(b, v) for b, v in items if v >= tiny]
    tot = sum(v for _, v in items)
    return [(b, v / tot) for b, v in items]


def shells(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    seen, out = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        st, comp = [v], []
        seen.add(v.index)
        while st:
            x = st.pop()
            comp.append(x.index)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index)
                    st.append(y)
        out.append(comp)
    bm.free()
    return out


def classify(me, comp):
    cs = set(comp)
    count = {}
    for p in me.polygons:
        if p.vertices[0] in cs:
            n = me.materials[p.material_index].name
            count[n] = count.get(n, 0) + 1
    mat = max(count, key=count.get)
    pts = [me.vertices[i].co for i in comp]
    c = sum(pts, V()) / len(pts)
    xmax = max(abs(p.x) for p in pts)
    side = "L" if c.x > 0 else "R"
    if "Skin" in mat or "Mouth" in mat:
        return "head"
    if "Red" in mat:
        return "neck" if c.z > 1.3 else "hand_" + side
    if "Navy" in mat:
        return "cuff" if abs(c.x) > 0.45 else "legs"
    if "KneePatch" in mat:
        return "legs"
    if xmax > 0.45:
        return "sleeve"
    if c.z > 1.40 and abs(c.x) < 0.05:
        return "hood"
    if 0.10 < abs(c.x) < 0.30 and c.z > 1.25:
        return "puff"
    return "jacket"


def weight_body(ob, arm):
    me = ob.data
    ob.vertex_groups.clear()
    groups = {}
    classes = {}
    stats = {"max_influences": 0, "unweighted": 0}
    for comp in shells(me):
        cls = classify(me, comp)
        classes[cls] = classes.get(cls, 0) + 1
        for i in comp:
            ws = finish(field(me.vertices[i].co, cls))
            if not ws:
                stats["unweighted"] += 1
            stats["max_influences"] = max(stats["max_influences"], len(ws))
            for b, w in ws:
                g = groups.get(b) or ob.vertex_groups.new(name=b)
                groups[b] = g
                g.add([i], w, "REPLACE")
    stats["shell_classes"] = classes
    return stats


def bind(ob, arm):
    for m in [m for m in ob.modifiers if m.type == "ARMATURE"]:
        ob.modifiers.remove(m)
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    mod.use_vertex_groups = True
    mod.use_deform_preserve_volume = False
    # Triangulate stays first (Phase 4 triangulation lock)
    names = [m.name for m in ob.modifiers]
    if "Triangulate" in names and names.index("Triangulate") > names.index("Armature"):
        bpy.ops.object.select_all(action="DESELECT")
    mw = ob.matrix_world.copy()
    ob.parent = arm
    ob.matrix_parent_inverse = M.Identity(4)
    ob.matrix_world = mw


def sockets(arm):
    bpy.context.view_layer.update()
    out = {}
    for s, sg in (("L", 1), ("R", -1)):
        ski = bpy.data.objects["SOCKET_ski_" + s]
        mw = M.Translation(mx(SOCKET_SKI, sg)) @ M.Rotation(math.pi, 4, "Z")
        ski.parent, ski.parent_type, ski.parent_bone = arm, "BONE", "Foot_" + s
        ski["role"] = "ski"
        ski["note"] = "sole under the boot centre; +Y figure forward, +Z up; PR_Ski SOCKET_boot aligns here"
        hand = bpy.data.objects["SOCKET_hand_" + s]
        y = mx(A2, sg).normalized()
        z = V((0, -1, 0))
        x = y.cross(z)
        R = M((x, y, z)).transposed().to_4x4()
        mwh = M.Translation(mx(SOCKET_HAND, sg)) @ R
        hand.parent, hand.parent_type, hand.parent_bone = arm, "BONE", "Hand_" + s
        hand["role"] = "hand"
        hand["note"] = "palm centre; +Z up the pole (thumb side), -Z down the shaft, +Y toward the fingertips; PR_SkiPole SOCKET_grip aligns here"
        bpy.context.view_layer.update()
        ski.matrix_world = mw
        hand.matrix_world = mwh
        for o in (ski, hand):
            o.matrix_parent_inverse = M.Identity(4) if False else o.matrix_parent_inverse
        out[ski.name] = "Foot_" + s
        out[hand.name] = "Hand_" + s
    bpy.context.view_layer.update()
    for n in out:
        o = bpy.data.objects[n]
        T.log("socket", n, out[n], [round(v, 4) for v in o.matrix_world.translation])
    return out


def build(scene, args):
    T.col("RIG_DEF")
    arm = T.armature(RIG, bone_list(), "RIG_DEF")
    arm.data.display_type = "STICK"
    arm.show_in_front = True
    arm["rest_pose"] = "relaxed A-pose, arms 40 deg below horizontal, feet hip-width (0.26 m) apart"
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    report = {"armature": RIG, "bones": [b.name for b in arm.data.bones], "meshes": {}}
    for n in BODY:
        ob = bpy.data.objects[n]
        report["meshes"][n] = weight_body(ob, arm)
        bind(ob, arm)
    for n, b in RIGID.items():
        ob = bpy.data.objects[n]
        ob.vertex_groups.clear()
        g = ob.vertex_groups.new(name=b)
        g.add(list(range(len(ob.data.vertices))), 1.0, "REPLACE")
        bind(ob, arm)
        report["meshes"][n] = {"rigid": b}
    report["sockets"] = sockets(arm)
    out = os.path.join(os.path.dirname(os.path.abspath(args.master)), "review", T.PHASE)
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, "weights.json"), "w") as f:
        json.dump(report, f, indent=2)
    T.log("weights", json.dumps(report["meshes"]))


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
