"""CH_Skier Phase 8 subset: sixteen pose clips (nine riding poses, five grabs and the two racer carves) as Blender Actions,
0 to 30 at 30 fps, root at the origin.

Poses are solved, not hand-keyed: each spec gives the hips, trunk bends, wrist targets, elbow and knee pole
directions, pole shaft directions and the ski placement (yaw, edge roll, pitch). Legs and arms are two-bone IK,
the hand turns so its socket's -Z runs down the pole, and a foot with z=None is dropped until its ski's lowest edge
touches the snow. Snow plane: PR_Ski's base is 0.045 below its SOCKET_boot, so flat skis put the soles at z=0 and
the snow at z=-0.045. CarveRight is the exact mirror of CarveLeft's spec (mirrored rest skeleton, mirrored input).
Each Action also gets its own NLA track and strip with the same name, so the FBX takes are named exactly like the
clips (09_export.py exports strips, not "Armature|Action" pairs). POSE_TEST holds the real PR_Ski and PR_SkiPole
parented to the sockets for the pose sheet; it never exports.

  $BLENDER_BIN -b --python build/08_poses.py -- --master CH_Skier_master.blend
"""
import os, sys, math, json, argparse
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "../../../../.claude/skills/blender-image-to-3d/assets")))
import build_template as T  # noqa: E402
import bpy  # noqa: E402
from mathutils import Vector as V, Matrix as M  # noqa: E402

T.PHASE = "08_poses"
T.OWNER_TAG = "phase:" + T.PHASE
REPO = os.path.normpath(os.path.join(HERE, "../../../.."))
RIG, F0, F1 = "CH_Skier_Rig", 0, 30
SNOW, SKI_HALF, SKI_BASE, SKI_Y, L_POLE = -0.045, 0.0665, -0.045, (-0.964, 0.0, 0.804), 1.10


def E(p=0, r=0, y=0):
    return M.Rotation(math.radians(y), 3, "Z") @ M.Rotation(math.radians(r), 3, "Y") @ M.Rotation(math.radians(p), 3, "X")


def frame(d, b):
    y = d.normalized()
    x = (b - y * b.dot(y)).normalized()
    return M((x, y, x.cross(y))).transposed()


def two_bone(A, Tg, L1, L2, pole):
    d = Tg - A
    dist = d.length
    dn = d / dist
    dc = min(max(dist, abs(L1 - L2) + 1e-4), L1 + L2 - 1e-4)
    a = (L1 * L1 - L2 * L2 + dc * dc) / (2 * dc)
    h = math.sqrt(max(L1 * L1 - a * a, 0.0))
    pp = (pole - dn * pole.dot(dn)).normalized()
    return A + dn * a + pp * h, A + dn * dc, max(dist - dc, 0.0), pp


def perp(v, d):
    d = d.normalized()
    return (v - d * v.dot(d)).normalized()


def snow_dir(W, xy):
    dz = SNOW - W.z
    hz = math.sqrt(max(L_POLE * L_POLE - dz * dz, 1e-6))
    h = V((xy[0] - W.x, xy[1] - W.y, 0)).normalized()
    return (h * hz + V((0, 0, dz))).normalized()


class Rig:
    def __init__(self, arm):
        self.arm = arm
        bs = arm.data.bones
        self.R = {b.name: b.matrix_local.copy() for b in bs}
        self.H = {b.name: b.head_local.copy() for b in bs}
        self.par = {b.name: (b.parent.name if b.parent else None) for b in bs}
        self.msock, self.sock0 = {}, {}
        for s, sg in (("L", 1), ("R", -1)):
            y = (bs["Hand_" + s].tail_local - bs["Hand_" + s].head_local).normalized()
            z = V((0, -1, 0))
            self.msock[s] = M((y.cross(z), y, z)).transposed()
            self.sock0[s] = V((0.13 * sg, -0.03, 0.0))

    def solve(self, sp):
        R, H, P, G, info = self.R, self.H, {}, {}, {}

        def place(b, q, h):
            P[b] = M.Translation(h) @ (q @ R[b].to_3x3()).to_4x4()
            G[b] = q

        def carry(par, pt):
            return P[par] @ (R[par].inverted() @ pt)

        place("Root", M.Identity(3), V((0, 0, 0)))
        feet = {}
        for s in "LR":
            pos, rot = sp["foot_" + s]
            Qf = E(*rot)
            z = pos[2]
            if z is None:
                z = SNOW - min((Qf @ V((ex, ey, SKI_BASE))).z for ex in (-SKI_HALF, SKI_HALF) for ey in SKI_Y)
            So = V((pos[0], pos[1], z))
            feet[s] = (Qf, So + Qf @ (H["Foot_" + s] - self.sock0[s]), So)
        hp = sp["hips"]
        if hp[0] == "auto":
            v = V(hp[1])
            tg = [feet[s][1] + feet[s][0] @ V((v.x * sg, v.y, v.z)) for s, sg in (("L", 1), ("R", -1))]
            hpos = (tg[0] + tg[1]) / 2 + (H["Hips"] - (H["UpperLeg_L"] + H["UpperLeg_R"]) / 2)
            hrot = hp[2]
        else:
            hpos, hrot = V(hp[0]), hp[1]
        place("Hips", E(*hrot), V(hpos))
        for b in ("Spine", "Chest", "Neck", "Head"):
            pr = self.par[b]
            place(b, G[pr] @ E(*sp.get(b.lower(), (0, 0, 0))), carry(pr, H[b]))
        for s in "LR":
            place("Shoulder_" + s, G["Chest"] @ E(*sp.get("clav_" + s, (0, 0, 0))), carry("Chest", H["Shoulder_" + s]))
            S = carry("Shoulder_" + s, H["UpperArm_" + s])
            fr, w = sp["wrist_" + s]
            W = V(w) if fr == "w" else carry("Chest", V(w))
            S0, E0, W0 = H["UpperArm_" + s], H["LowerArm_" + s], H["Hand_" + s]
            Ej, Wc, err, pp = two_bone(S, W, (E0 - S0).length, (W0 - E0).length, V(sp["elbow_" + s]))
            info["arm_reach_err_" + s] = round(err, 4)
            d0 = W0 - S0
            b0, b1 = d0.cross(perp(V((0, 1, 0)), d0)).normalized(), (Wc - S).cross(pp).normalized()
            place("UpperArm_" + s, frame(Ej - S, b1) @ frame(E0 - S0, b0).transposed(), S)
            place("LowerArm_" + s, frame(Wc - Ej, b1) @ frame(W0 - E0, b0).transposed(), Ej)
            fore = (Wc - Ej).normalized()
            pk, pv = sp["pole_" + s]
            pdir = snow_dir(Wc + fore * 0.066, pv) if pk == "snow" else V(pv).normalized()
            Z = -pdir
            ref = fore if abs(fore.dot(Z)) < 0.92 else (fore + V((0, 0, -1))).normalized()
            Y = perp(ref, Z)
            place("Hand_" + s, M((Y.cross(Z), Y, Z)).transposed() @ self.msock[s].transposed(), Wc)
            info["pole_tip_z_" + s] = round((Wc + fore * 0.066 + pdir * L_POLE).z, 3)
        for s in "LR":
            Qf, A, So = feet[s]
            Hj = carry("Hips", H["UpperLeg_" + s])
            H0, K0, A0 = H["UpperLeg_" + s], H["LowerLeg_" + s], H["Foot_" + s]
            kd = sp.get("knee_" + s)
            Kj, Ac, err, pp = two_bone(Hj, A, (K0 - H0).length, (A0 - K0).length, Qf @ (V(kd) if kd else V((0, -1, 0))))
            info["leg_reach_err_" + s] = round(err, 4)
            d0 = A0 - H0
            b0, b1 = d0.cross(perp(V((0, -1, 0)), d0)).normalized(), (Ac - Hj).cross(pp).normalized()
            place("UpperLeg_" + s, frame(Kj - Hj, b1) @ frame(K0 - H0, b0).transposed(), Hj)
            place("LowerLeg_" + s, frame(Ac - Kj, b1) @ frame(A0 - K0, b0).transposed(), Kj)
            place("Foot_" + s, Qf, Ac)
            info["sole_z_" + s] = round(So.z, 4)
        return P, info

    def key(self, P, f, prev):
        for pb in self.arm.pose.bones:
            n, pr = pb.name, self.par[pb.name]
            Bm = ((P[pr] @ self.R[pr].inverted() @ self.R[n]).inverted() @ P[n]) if pr else (self.R[n].inverted() @ P[n])
            loc, q, _ = Bm.decompose()
            if n in prev and prev[n].dot(q) < 0:
                q.negate()
            prev[n] = q.copy()
            pb.rotation_mode = "QUATERNION"
            pb.location, pb.rotation_quaternion, pb.scale = loc, q, (1, 1, 1)
            pb.keyframe_insert("location", frame=f, group=n)
            pb.keyframe_insert("rotation_quaternion", frame=f, group=n)


# POSE SPECS (left side written; sym() mirrors _L entries to _R)

def mv(v):
    return None if v is None else (-v[0], v[1], v[2])


def mr(r):
    return (r[0], -r[1], -r[2])


def mirror_entry(k, v):
    if k.startswith("foot_"):
        return (mv(v[0]) if v[0][2] is not None else (-v[0][0], v[0][1], None), mr(v[1]))
    if k.startswith("wrist_"):
        return (v[0], mv(v[1]))
    if k.startswith("pole_"):
        return (v[0], (-v[1][0], v[1][1]) if v[0] == "snow" else mv(v[1]))
    if k.startswith(("elbow_", "knee_")):
        return mv(v)
    if k.startswith("clav_") or k in ("spine", "chest", "neck", "head"):
        return mr(v)
    if k == "hips":
        return ("auto", mv(v[1]), mr(v[2])) if v[0] == "auto" else (mv(v[0]), mr(v[1]))
    return v


def sym(d):
    out = dict(d)
    for k, v in d.items():
        if k.endswith("_L"):
            out[k[:-2] + "_R"] = mirror_entry(k, v)
    return out


def mirror_spec(d):
    out = {}
    for k, v in d.items():
        k2 = k[:-2] + ("_R" if k.endswith("_L") else "_L") if k.endswith(("_L", "_R")) else k
        out[k2] = mirror_entry(k, v)
    return out


FLAT = ((0.13, -0.03, None), (0, 0, 0))
STANCE = sym({"hips": ((0, 0.10, 0.77), (18, 0, 0)), "spine": (6, 0, 0), "chest": (6, 0, 0), "neck": (-12, 0, 0),
              "head": (-14, 0, 0), "wrist_L": ("w", (0.30, -0.30, 0.93)), "elbow_L": (0.6, 1.0, 0.0),
              "pole_L": ("dir", (0.12, 0.62, -0.78)), "foot_L": FLAT})
CARVE_L = {"foot_L": ((0.17, -0.10, None), (0, 50, 0)), "foot_R": ((-0.10, 0.08, None), (0, 50, 0)),
           "hips": ("auto", (0, 0.08, 0.68), (15, 22, 0)), "spine": (2, -8, 0), "chest": (4, -6, 0),
           "neck": (-10, -4, 0), "head": (-8, -4, 0),
           "wrist_L": ("c", (0.30, -0.42, 1.12)), "elbow_L": (0.6, 1, 0), "pole_L": ("dir", (0.30, 0.85, -0.30)),
           "wrist_R": ("c", (-0.34, -0.16, 0.95)), "elbow_R": (-0.6, 1, 0), "pole_R": ("dir", (-0.15, 0.85, -0.35))}
# Story 008: the carve with W held, low like a racer. CarveLeft's skis and edges, with the hips lower and further forward,
# the trunk bent more over the skis, the hands out in front and the poles tucked back.
RACE_CARVE_L = dict(CARVE_L, **{"hips": ("auto", (0, 0.12, 0.58), (30, 26, 0)), "spine": (6, -10, 0), "chest": (8, -8, 0),
                                "neck": (-24, -4, 0), "head": (-18, -4, 0),
                                "wrist_L": ("c", (0.30, -0.42, 1.12)), "pole_L": ("dir", (0.25, 0.90, -0.20)),
                                "wrist_R": ("c", (-0.34, -0.40, 1.09)), "pole_R": ("dir", (-0.15, 0.95, -0.10))})
TUCK = sym({"hips": ((0, 0.24, 0.56), (50, 0, 0)), "spine": (12, 0, 0), "chest": (12, 0, 0), "neck": (-35, 0, 0),
            "head": (-30, 0, 0), "wrist_L": ("w", (0.07, -0.55, 0.80)), "elbow_L": (0.5, 0.0, -1.0),
            "pole_L": ("dir", (0.38, 0.90, -0.22)), "foot_L": FLAT})
CROUCH = sym({"hips": ((0, 0.16, 0.63), (28, 0, 0)), "spine": (8, 0, 0), "chest": (8, 0, 0), "neck": (-18, 0, 0),
              "head": (-20, 0, 0), "wrist_L": ("w", (0.30, -0.45, 0.88)), "elbow_L": (0.6, 1, 0),
              "pole_L": ("dir", (0.12, 0.62, -0.78)), "foot_L": FLAT})
AIR = sym({"hips": ((0, 0.06, 0.86), (8, 0, 0)), "spine": (4, 0, 0), "chest": (4, 0, 0), "neck": (-6, 0, 0),
           "head": (-6, 0, 0), "wrist_L": ("w", (0.64, -0.08, 1.12)), "elbow_L": (0, 1, -0.4),
           "pole_L": ("dir", (0.35, 0.55, -0.76)), "foot_L": ((0.13, -0.10, 0.20), (-8, 0, 0))})
BRAKE = {"foot_L": ((0.065, -0.13, None), (0, -25, -60)), "foot_R": ((-0.065, 0.095, None), (0, -25, -60)),
         "knee_L": (-0.4, -1, 0), "knee_R": (-0.4, -1, 0),
         "hips": ((0, 0.18, 0.66), (25, 0, -25)), "spine": (6, 0, 12), "chest": (6, 0, 13), "neck": (-16, 0, 0),
         "head": (-14, 0, 0), "wrist_L": ("w", (0.32, -0.30, 0.92)), "elbow_L": (0.6, 1, 0),
         "pole_L": ("dir", (0.15, 0.6, -0.78)), "wrist_R": ("w", (-0.28, -0.30, 0.92)), "elbow_R": (-0.6, 1, 0),
         "pole_R": ("dir", (-0.15, 0.6, -0.78))}
CRASH = {"hips": ((0.0, 0.0, 0.30), (10, 78, 0)), "spine": (0, -15, 0), "chest": (0, -10, 0), "neck": (0, -12, 0),
         "head": (0, -10, 0), "wrist_L": ("w", (0.58, -0.18, 0.18)), "elbow_L": (0.3, 0.6, -0.4),
         "pole_L": ("dir", (0.25, 0.95, 0.12)), "wrist_R": ("w", (0.10, -0.28, 0.36)), "elbow_R": (0.0, 0.6, 1.0),
         "pole_R": ("dir", (-0.3, 0.8, 0.3)), "foot_L": ((-0.60, -0.20, None), (0, 70, 0)),
         "foot_R": ((-0.40, 0.12, None), (0, 70, 0))}


def on_ski(foot, along, up=0.0, across=0.0):
    """A point on a ski, in the rig's frame: <along> metres from the boot socket toward the tail (+) or the tip (-),
    <up> above the ski's base, <across> toward the ski's own +X."""
    (x, y, z), rot = foot
    return tuple(V((x, y, z)) + E(*rot) @ V((across, along, SKI_BASE + up)))


# Grabs (the sim's GrabKind; story 007, user 2026-10-24): knees pulled up and spread so the trunk folds between them,
# one hand on a ski with its pole trailing back, the other arm out for balance with its pole. Each wrist target sits
# GRAB_PALM above the ski's base so the palm, not the wrist, meets the ski. The trunk angles and heights below came from
# a search (every combination on a grid) for the hand reaching its target with the knees clear of the chest and head;
# all five reach with zero error (the solver's arm_reach_err, logged in review/06_rig/poses.json). Tail and Japan were
# searched again after the user's playtest (2026-10-24: "the knees are going 180 degrees backwards ... the torso isn't
# moving with the legs"): their first specs put the feet 10 cm under an upright trunk, so each leg folded flat (170
# degrees at the knee) with the shin pointing straight back. Now the knee bends 95 to 140 degrees, the shin points down
# (no more than 0.75 of it backward), the feet are not behind the hips, and the trunk leans 20 to 55 degrees forward
# with the legs, like Mute and Tip; of those, the pose closest to a 38 degree lean, a 120 degree knee and the thighs
# 10 degrees above level.
GRAB_PALM = 0.085
FREE_ARM_L = {"wrist_L": ("c", (0.62, -0.10, 1.10)), "elbow_L": (0.2, 1, -0.5), "pole_L": ("dir", (0.40, 0.55, -0.73))}
FREE_ARM_R = {k[:-2] + "_R": mirror_entry(k, v) for k, v in FREE_ARM_L.items()}


def grab(hand, ski, along, across, foot, hips_z, hip_pitch, chest):
    """A grab: <hand> ("L" or "R") on ski <ski> at <along>/<across> (see on_ski). <foot> is the left foot's
    ((x, y, z), (pitch, roll, yaw)), mirrored for the right; <chest> is (pitch, roll, yaw), half of it in the spine's
    roll and yaw and 0.6 of its pitch."""
    feet = {"foot_L": foot, "foot_R": mirror_entry("foot_L", foot)}
    cp, cr, cy = chest
    sg = 1 if hand == "L" else -1
    return {"hips": ((0, 0.10, hips_z), (hip_pitch, 0, 0)), "spine": (cp * 0.6, cr * 0.5, cy * 0.5),
            "chest": (cp, cr * 0.5, cy * 0.5), "neck": (-hip_pitch * 0.8, 0, 0), "head": (-hip_pitch * 0.6, 0, 0),
            **feet, "knee_L": (0.7, -1, 0), "knee_R": (-0.7, -1, 0), **(FREE_ARM_R if hand == "L" else FREE_ARM_L),
            "wrist_" + hand: ("w", on_ski(feet["foot_" + ski], along, GRAB_PALM, across)),
            "elbow_" + hand: (sg * 0.6, 0.4, 0.6), "pole_" + hand: ("dir", (sg * 0.25, 0.75, -0.6))}


GRABS = {
    # Mute: the left hand across the body onto the right ski, in front of the boot; folded well forward.
    "GrabMute": grab("L", "R", -0.16, 0.0, ((0.16, -0.04, 0.55), (-6, 0, 0)), 0.80, 40, (30, 0, 0)),
    # Tip: a pike, skis out in front and tips up; the right hand near the right ski's tip.
    "GrabTip": grab("R", "R", -0.55, 0.0, ((0.16, -0.30, 0.55), (-30, 0, 0)), 0.80, 40, (10, 0, 0)),
    # Tail: knees up, tips down so the tails rise; folded forward and leaning right, the right hand reaches back onto the
    # right ski's tail, outside edge.
    "GrabTail": grab("R", "R", 0.32, -0.07, ((0.16, -0.06, 0.40), (30, 0, 0)), 0.80, 15, (30, -30, -20)),
    # Japan: knees up, tips well down; folded forward and leaning left, the trunk turned, the left hand behind the left
    # boot on the ski's outside edge.
    "GrabJapan": grab("L", "L", 0.20, 0.07, ((0.16, -0.06, 0.40), (40, 0, 0)), 0.80, 15, (30, 30, 20)),
    # Safety: the right hand on the right ski's outside edge under the boot, leaning down to that side.
    "GrabSafety": grab("R", "R", 0.02, -0.07, ((0.16, -0.04, 0.55), (-6, 0, 0)), 0.80, 10, (10, -30, 0)),
}

POLE_KEYS = [  # (t, spec) double-pole cycle; t=1 repeats t=0
    (0.0, {"hips": ((0, 0.06, 0.80), (12, 0, 0)), "spine": (4, 0, 0), "chest": (4, 0, 0), "neck": (-8, 0, 0), "head": (-8, 0, 0),
           "wrist_L": ("w", (0.28, -0.45, 1.05)), "pole_L": ("snow", (0.30, -0.15))}),
    (0.3, {"hips": ((0, 0.13, 0.72), (28, 0, 0)), "spine": (10, 0, 0), "chest": (10, 0, 0), "neck": (-20, 0, 0), "head": (-18, 0, 0),
           "wrist_L": ("w", (0.26, -0.12, 0.78)), "pole_L": ("snow", (0.32, 0.45))}),
    (0.5, {"hips": ((0, 0.12, 0.74), (30, 0, 0)), "spine": (10, 0, 0), "chest": (8, 0, 0), "neck": (-20, 0, 0), "head": (-16, 0, 0),
           "wrist_L": ("w", (0.26, 0.22, 0.82)), "pole_L": ("dir", (0.10, 0.90, -0.25))}),
    (0.75, {"hips": ((0, 0.08, 0.78), (18, 0, 0)), "spine": (6, 0, 0), "chest": (6, 0, 0), "neck": (-12, 0, 0), "head": (-12, 0, 0),
            "wrist_L": ("w", (0.28, -0.25, 0.98)), "pole_L": ("dir", (0.08, 0.45, -0.85))}),
]


def stance_at(t):
    b = (1 - math.cos(2 * math.pi * t)) / 2
    d = dict(STANCE)
    (x, y, z), r = STANCE["hips"]
    d["hips"] = ((x, y, z - 0.012 * b), r)
    d["chest"] = (STANCE["chest"][0] + 1.5 * b, 0, 0)
    for s in "LR":
        fr, w = STANCE["wrist_" + s]
        d["wrist_" + s] = (fr, (w[0], w[1], w[2] - 0.008 * b))
    return d


def flat_key(spec):
    """numeric vector form of a pole key (pole 'snow' resolved to a direction at the key's wrist)"""
    W = V(spec["wrist_L"][1])
    pk, pv = spec["pole_L"]
    pdir = snow_dir(W + V((0.0, -0.05, -0.04)), pv) if pk == "snow" else V(pv).normalized()
    return {"hp": list(spec["hips"][0]) + list(spec["hips"][1]), "sp": list(spec["spine"]), "ch": list(spec["chest"]),
            "nk": list(spec["neck"]), "hd": list(spec["head"]), "w": list(W), "p": list(pdir)}


def pole_at(t):
    keys = [(k[0], flat_key(k[1])) for k in POLE_KEYS]
    n = len(keys)
    ts = [k[0] for k in keys] + [1.0]
    i = max(j for j in range(n) if ts[j] <= t + 1e-9) if t < 1.0 else 0
    u = 0.0 if t >= 1.0 else (t - ts[i]) / (ts[i + 1] - ts[i])
    P0, P1, P2, P3 = (keys[(i + k) % n][1] for k in (-1, 0, 1, 2))
    v = {}
    for name in P1:
        v[name] = [0.5 * (2 * b + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u * u + (-a + 3 * b - 3 * c + d) * u ** 3)
                   for a, b, c, d in zip(P0[name], P1[name], P2[name], P3[name])]
    return sym({"hips": (tuple(v["hp"][:3]), tuple(v["hp"][3:])), "spine": tuple(v["sp"]), "chest": tuple(v["ch"]),
                "neck": tuple(v["nk"]), "head": tuple(v["hd"]), "wrist_L": ("w", tuple(v["w"])),
                "elbow_L": (0.6, 1.0, -0.3), "pole_L": ("dir", tuple(v["p"])), "foot_L": FLAT})


CLIPS = [  # name, spec function of t in [0, 1], loop, static
    ("Stance", stance_at, True, False),
    ("CarveLeft", lambda t: CARVE_L, False, True),
    ("CarveRight", lambda t: mirror_spec(CARVE_L), False, True),
    ("Tuck", lambda t: TUCK, False, True),
    ("Crouch", lambda t: CROUCH, False, True),
    ("Air", lambda t: AIR, False, True),
    ("Brake", lambda t: BRAKE, False, True),
    ("Pole", pole_at, True, False),
    ("Crash", lambda t: CRASH, False, True),
] + [(name, (lambda spec: lambda t: spec)(spec), False, True) for name, spec in GRABS.items()] + [
    ("RaceCarveLeft", lambda t: RACE_CARVE_L, False, True),
    ("RaceCarveRight", lambda t: mirror_spec(RACE_CARVE_L), False, True),
]


def make_clips(arm):
    rig = Rig(arm)
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = None
    for tr in list(ad.nla_tracks):
        ad.nla_tracks.remove(tr)
    report = {}
    for name, fn, loop, static in CLIPS:
        old = bpy.data.actions.get(name)
        if old:
            bpy.data.actions.remove(old)
        act = bpy.data.actions.new(name)
        ad.action = act
        prev, infos = {}, []
        for f in ([F0, F1] if static else range(F0, F1 + 1)):
            P, info = rig.solve(fn((f - F0) / (F1 - F0)))
            rig.key(P, f, prev)
            infos.append(info)
        act["loop"] = loop
        act["root_motion"] = False
        act.use_frame_range = True
        act.frame_start, act.frame_end = F0, F1
        ad.action = None
        tr = ad.nla_tracks.new()
        tr.name = name
        st = tr.strips.new(name, F0, act)
        st.name = name
        if hasattr(st, "action_slot") and getattr(act, "slots", None) and len(act.slots) and st.action_slot is None:
            st.action_slot = act.slots[0]
        worst = {k: (max if "err" in k else min)(i[k] for i in infos) for k in infos[0]}
        report[name] = {"loop": loop, "static": static, **worst}
        T.log(name, report[name])
    for pb in arm.pose.bones:
        pb.location, pb.rotation_quaternion = (0, 0, 0), (1, 0, 0, 0)
    return report


def append(path, names):
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in names]
    return {o.name.split(".")[0]: o for o in dst.objects}


def attach_props():
    col = T.col("POSE_TEST")
    out = {}
    for path, mesh_name, sock_name, target, tag in (
            ("art/source/PR_Ski/PR_Ski_master.blend", "PR_Ski_LOD0", "SOCKET_boot", "SOCKET_ski_", "Ski"),
            ("art/source/PR_SkiPole/PR_SkiPole_master.blend", "PR_SkiPole_LOD0", "SOCKET_grip", "SOCKET_hand_", "Pole")):
        got = append(os.path.join(REPO, path), (mesh_name, sock_name))
        mesh, sock = got[mesh_name], got[sock_name]
        rel = (sock.matrix_parent_inverse @ sock.matrix_basis) if sock.parent == mesh else (mesh.matrix_world.inverted() @ sock.matrix_world)  # appended matrix_world is stale
        bpy.data.objects.remove(sock, do_unlink=True)
        for s in "LR":
            o = mesh if s == "L" else mesh.copy()
            o.name = "TEST_%s_%s" % (tag, s)
            for c in list(o.users_collection):
                c.objects.unlink(o)
            col.objects.link(o)
            T.own(o)
            o.parent = bpy.data.objects[target + s]
            o.parent_type = "OBJECT"
            o.matrix_parent_inverse = M.Identity(4)
            o.matrix_basis = rel.inverted()
            out[o.name] = target + s
    return out


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = F0, F1
    arm = bpy.data.objects[RIG]
    rep = make_clips(arm)
    rep["_props"] = attach_props()
    path = os.path.join(os.path.dirname(os.path.abspath(a.master)), "review", "06_rig", "poses.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        json.dump(rep, f, indent=2)
    T.save_master(a.master)
    T.log("saved", a.master)
