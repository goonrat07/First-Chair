"""CH_Skier forms (Phase 3): low-poly, flat-shaded forms over the approved blockout, built in HIGH.

  $BLENDER_BIN --background --python build/03_forms.py -- --master CH_Skier_master.blend

GMA2-like construction: faceted lofts and low-segment spheres, flat shading, one material per colour role.
- Jacket: puffy lofted body with a slight hem flare, puffy round shoulders, 16-sided sleeves. The zip, chest
  stripes and sleeve stripes are material splits on existing faces: no extra geometry, nothing to z-fight.
- Hood bunched at the back of the neck, a collar rim, the neck warmer lowered to a band (user, 2026-10-20).
- Lower face under the goggles: head, chin and nose in skin, the mouth as a small darker piece.
- Helmet with its top panel as a material split, a goggle strap ring, goggles curved around the helmet front.
- Trousers lofted per leg with a rounded knee; knee patches as separate faceted pieces.
- Boots: lower shell (foot loft and lower shaft, for the foot bone) and cuff (shin bone) with the rust band
  as a material split.
Dimensions come from the proportion table (ref/refs.json "proportions_m", all inferred). Shape-only choices
(puff radii, section counts) are the "forms choice" constants below. The gate holds the silhouette to the same
front and side mattes as the blockout (IoU 0.90, bands 0.03).
"""
import os, sys, math, json, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402  (the skill's helpers; this file is a phase script built on them)
import bpy, bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

T.PHASE = "03_forms"
T.OWNER_TAG = "phase:" + T.PHASE
HERE = os.path.dirname(os.path.abspath(__file__))
REFS = json.load(open(os.path.normpath(os.path.join(HERE, "../ref/refs.json"))))
P = REFS["proportions_m"]
PALETTE = {k: v["hex"] for k, v in REFS["palette"].items()}
C = "HIGH"

# FORMS CHOICES (metres). Jacket sections (z, half width, half depth): a puffy chest and a slight hem flare.
JACKET = [(0.82, .245, .162), (0.87, .245, .162), (0.93, .232, .152), (1.02, .232, .152), (1.15, .238, .157),
          (1.22, .238, .157), (1.30, .236, .155), (1.38, .222, .146), (1.40, .15, .11), (1.42, .11, .08)]   # neckline under the band
JACKET_YC, PUFF_P = 0.02, 2.6                 # section centre y; superellipse exponent (2 = ellipse)
SHOULDER_PUFF = 0.095                         # front: shoulder top 1.45, width 0.55 (table 1.445, 0.53)
SLEEVE = [(0.0, .088), (0.08, .088), (0.18, .082), (0.29, .076), (0.40, .072)]   # (from shoulder, radius)
SEAT = [(0.77, .13, .09), (0.80, .185, .13), (0.88, .19, .14), (0.95, .18, .13)]
LEG = [(0.92, .105, 0.0), (0.82, .108, 0.0), (0.72, .106, 0.0), (0.62, .102, 0.0), (0.55, .101, -.006),
       (0.50, .100, -.010), (0.45, .098, -.006), (0.38, .096, 0.0), (0.29, .100, 0.0)]   # (z, radius, knee push)
FOOT = [(0.13, .10, .12), (0.10, .115, .13), (0.0, .118, .135), (-0.10, .115, .12), (-0.16, .105, .10),
        (-0.19, .09, .075)]                   # side: (y, width, height), heel to toe; sole on z 0
KNEE_PATCH = ((0.06, 0.022, 0.075), -0.098)  # radii, and y of the centre (1 cm proud of the knee)
# hood and collar as ONE loft: collar inside the band, hood bunched at the back above it, clear of the helmet
# rim (>= 1 cm), the shoulders and the jaw. Rings (z, side, back, front radius) around (0, HOOD_C).
HOOD_C = 0.03
HOOD = [(1.39, 0.10, 0.09, 0.10), (1.425, 0.105, 0.105, 0.09), (1.445, 0.135, 0.15, 0.03),
        (1.475, 0.14, 0.16, 0.02), (1.505, 0.11, 0.13, 0.0)]


def hex_lin(h):
    def c(v):
        v /= 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return (c(int(h[1:3], 16)), c(int(h[3:5], 16)), c(int(h[5:7], 16)), 1.0)


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


def finish(name, bm, roles, bevel=0.0, segs=1, loc=(0, 0, 0), rot=(0, 0, 0)):
    ob = T.new_mesh_object(name, bm, C, location=loc, rotation=rot)
    for r in roles:
        ob.data.materials.append(mat(r))
    if bevel > 0:
        T.bevel(ob, width=bevel, segments=segs, harden=False)
    return ob


def boxed(name, size, loc, role, bevel=0.0, segs=1, rot=(0, 0, 0)):
    ob = T.box(name, size, collection=C, location=loc, rotation=rot)
    ob.data.materials.append(mat(role))
    if bevel > 0:
        T.bevel(ob, width=bevel, segments=segs, harden=False)
    return ob


def ball(name, radii, loc, role, u=12, v=8):
    ob = T.sphere(name, 1.0, collection=C, location=loc, u=u, v=v, scale=radii)
    ob.data.materials.append(mat(role))
    return ob


def loft(bm, rings, cap=True):
    """Quad strips between closed rings (same point count); returns faces[row][column]."""
    vs = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(rings[0])
    grid = []
    for i in range(len(vs) - 1):
        grid.append([bm.faces.new((vs[i][j], vs[i][(j + 1) % n], vs[i + 1][(j + 1) % n], vs[i + 1][j]))
                     for j in range(n)])
    if cap:
        bm.faces.new(vs[0][::-1])
        bm.faces.new(vs[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return grid


def se_ring(z, hw, hd, yc, n=12, p=PUFF_P, xc=0.0):
    out = []
    for k in range(n):
        th = 2 * math.pi * (k + 0.5) / n
        c, s = math.cos(th), math.sin(th)
        out.append((xc + hw * math.copysign(abs(c) ** (2 / p), c), yc + hd * math.copysign(abs(s) ** (2 / p), s), z))
    return out


def torso_ring(z, hw, hd, yc=JACKET_YC, p=PUFF_P):
    """Front points at fixed x so face columns 4 (zip) and 2 and 6 (chest stripes) line up on every section."""
    zh, sx, sh = P["zip_size"][0] / 2, P["chest_stripe_x"], P["chest_stripe_size"][0] / 2
    o1 = max(0.7 * hw, sx + sh + 0.004)
    o2 = max(0.9 * hw, o1 + 0.004)
    xs = [-o2, -o1, -sx - sh, -sx + sh, -zh, zh, sx - sh, sx + sh, o1, o2]
    pts = [(x, yc - hd * max(0.0, 1 - abs(x / hw) ** p) ** (1 / p), z) for x in xs]
    for th in (0.0, 0.2 * math.pi, 0.5 * math.pi, 0.8 * math.pi, math.pi):
        c, s = math.cos(th), math.sin(th)
        pts.append((hw * math.copysign(abs(c) ** (2 / p), c), yc + hd * math.copysign(abs(s) ** (2 / p), s), z))
    return pts


def rr_ring(z, a, b, c, xc=0.0, yc=0.0):
    pts = [(a, -b + c), (a, b - c), (a - c, b), (-a + c, b), (-a, b - c), (-a, -b + c), (-a + c, -b), (a - c, -b)]
    return [(xc + x, yc + y, z) for x, y in pts]


def hood_ring(z, rs, rb, rf, n=12):
    """Ring around the neck: side radius rs, back radius rb (+Y), front radius rf (-Y)."""
    out = []
    for k in range(n):
        th = 2 * math.pi * (k + 0.5) / n
        c, s = math.cos(th), math.sin(th)
        out.append((rs * c, HOOD_C + (rb if s > 0 else rf) * s, z))
    return out


def tube_ring(centre, axis, r, n, ref_dir):
    """Ring around an axis; the face between points n-1 and 0 is centred on ref_dir."""
    a = Vector(axis).normalized()
    u = (Vector(ref_dir) - a * Vector(ref_dir).dot(a)).normalized()
    w = a.cross(u)
    return [tuple(Vector(centre) + r * (math.cos(2 * math.pi * (k + 0.5) / n) * u +
                                        math.sin(2 * math.pi * (k + 0.5) / n) * w)) for k in range(n)]


def build(scene, args):
    # JACKET: body with zip and chest stripes as material splits
    bm = bmesh.new()
    grid = loft(bm, [torso_ring(z, hw, hd) for z, hw, hd in JACKET])
    s0 = P["chest_stripe_z"] - P["chest_stripe_size"][2] / 2
    s1 = P["chest_stripe_z"] + P["chest_stripe_size"][2] / 2
    for i, row in enumerate(grid):
        z0, z1 = JACKET[i][0], JACKET[i + 1][0]
        if z1 <= 1.40 + 1e-6:
            row[4].material_index = 1
        if z0 >= s0 - 1e-6 and z1 <= s1 + 1e-6:
            row[2].material_index = row[6].material_index = 1
    finish("CH_Skier_Jacket_form", bm, ["jacket", "stripe"])
    bm = bmesh.new()
    loft(bm, [se_ring(z, hw, hd, 0.03) for z, hw, hd in SEAT])
    finish("CH_Skier_Seat_form", bm, ["trousers"])
    # NECK: one hood-and-collar loft (no overlapping yellow surfaces), the neck warmer band wrapping the collar
    bm = bmesh.new()
    loft(bm, [hood_ring(z, rs, rb, rf) for z, rs, rb, rf in HOOD])
    finish("CH_Skier_Hood_form", bm, ["jacket"])
    nc, ns = P["neck_warmer_centre"], P["neck_warmer_size"]
    bm = bmesh.new()
    loft(bm, [se_ring(z, ns[0] / 2, ns[1] / 2, nc[1], n=14, p=2.4) for z in (nc[2] - ns[2] / 2, nc[2] + ns[2] / 2)])
    finish("CH_Skier_NeckWarmer_form", bm, ["neck_warmer"])
    # FACE
    ball("CH_Skier_Head_form", P["head_radii"], P["head_centre"], "skin", u=12, v=8)
    boxed("CH_Skier_Jaw_form", P["jaw_size"], P["jaw_centre"], "skin", bevel=P["jaw_r"])
    boxed("CH_Skier_Chin_form", P["chin_size"], P["chin_centre"], "skin", bevel=P["chin_r"])
    boxed("CH_Skier_Nose_form", P["nose_size"], P["nose_centre"], "skin", bevel=P["nose_r"])
    boxed("CH_Skier_Mouth_form", P["mouth_size"], P["mouth_centre"], "mouth")
    # HELMET, top panel split, strap, goggles curved to the helmet front
    hc, hr = P["helmet_centre"], P["helmet_radii"]
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=1.0)
    bmesh.ops.scale(bm, vec=hr, verts=bm.verts)
    bmesh.ops.translate(bm, vec=hc, verts=bm.verts)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, P["helmet_rim_z"]),
                           plane_no=(0, P["helmet_rim_tilt"], 1), clear_inner=True)
    bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    for f in bm.faces:
        if f.calc_center_median().z > P["helmet_panel_z"]:
            f.material_index = 1
    finish("CH_Skier_Helmet_form", bm, ["helmet", "helmet_panel"])

    def ell_r(z):
        return math.sqrt(max(0.0, 1 - ((z - hc[2]) / hr[2]) ** 2))
    rings = []
    for z in P["strap_z"]:
        e = ell_r(z)
        rings.append([(hc[0] + (hr[0] * e + 0.008) * math.cos(2 * math.pi * k / 16),
                       hc[1] + (hr[1] * e + 0.008) * math.sin(2 * math.pi * k / 16), z) for k in range(16)])
    bm = bmesh.new()
    loft(bm, rings)
    finish("CH_Skier_GoggleStrap_form", bm, ["goggle_frame"])
    gc, gs, lc, ls = P["goggle_centre"], P["goggle_size"], P["lens_centre"], P["lens_size"]

    def yh(x):
        return hc[1] - hr[1] * math.sqrt(max(0.0, 1 - (x / hr[0]) ** 2)) * ell_r(gc[2])
    for name, half, z0, z1, yb, yf, role in (
            ("Goggles", gs[0] / 2, gc[2] - gs[2] / 2, gc[2] + gs[2] / 2, 0.012, -0.026, "goggle_frame"),
            ("Lens", ls[0] / 2, lc[2] - ls[2] / 2, lc[2] + ls[2] / 2, -0.022, -0.031, "goggle_lens")):
        xs = [half * t for t in (-1, -0.66, -0.33, 0, 0.33, 0.66, 1)]
        bm = bmesh.new()
        loft(bm, [[(x, yh(x) + yb, z0), (x, yh(x) + yf, z0), (x, yh(x) + yf, z1), (x, yh(x) + yb, z1)] for x in xs])
        finish("CH_Skier_%s_form" % name, bm, [role])
    # ARMS and LEGS
    a = math.radians(P["arm_angle_deg"])
    sj = P["shoulder_joint"]
    for s, sfx in ((1, "L"), (-1, "R")):
        d = Vector((s * math.cos(a), 0.0, -math.sin(a)))
        n_out = Vector((s * math.sin(a), 0.0, math.cos(a)))
        S = Vector((s * sj[0], sj[1], sj[2]))
        E = S + d * P["upper_arm_len"]
        W = E + d * P["forearm_len"]
        ball("CH_Skier_Shoulder_%s_form" % sfx, (SHOULDER_PUFF,) * 3, S - Vector((0, 0, 0.005)), "jacket")
        st = SLEEVE + [(P["upper_arm_len"] + P["forearm_len"] + P["forearm_end"], P["sleeve_r"][2] + 0.001)]
        bm = bmesh.new()
        grid = loft(bm, [tube_ring(S + d * t, d, r, 16, n_out) for t, r in st])
        for i in range(len(st) - 1):
            if st[i + 1][0] <= P["upper_arm_len"] + 1e-6:
                grid[i][15].material_index = 1                 # stripe down the outside of the upper sleeve
        finish("CH_Skier_Sleeve_%s_form" % sfx, bm, ["jacket", "stripe"])
        c0, c1, cr = P["cuff"]
        bm = bmesh.new()
        loft(bm, [tube_ring(W + d * c0, d, cr, 12, n_out), tube_ring(W + d * c1, d, cr, 12, n_out)])
        finish("CH_Skier_Cuff_%s_form" % sfx, bm, ["cuff"])
        M = W + d * P["mitten_offset"]
        boxed("CH_Skier_Mitten_%s_form" % sfx, P["mitten_size"], M, "mitten", bevel=P["mitten_r"], segs=2,
              rot=(0.0, s * a, 0.0))
        t0v, t1v, ty, tr = P["thumb"]
        p0 = M + d * t0v
        p1 = M + d * t1v + Vector((0.0, ty, 0.0))
        bm = bmesh.new()
        loft(bm, [tube_ring(p0, p1 - p0, tr, 8, (0, 0, 1)), tube_ring(p1, p1 - p0, tr, 8, (0, 0, 1))])
        finish("CH_Skier_Thumb_%s_form" % sfx, bm, ["mitten"])
        ball("CH_Skier_ThumbTip_%s_form" % sfx, (tr,) * 3, p1, "mitten", u=8, v=6)
        hip, knee, hem = [Vector((s * P[k][0], P[k][1], P[k][2])) for k in ("hip_joint", "knee", "trouser_hem")]
        rings = []
        for z, r, push in LEG:
            if z >= hip.z:
                c = hip
            elif z >= knee.z:
                c = hip.lerp(knee, (hip.z - z) / (hip.z - knee.z))
            else:
                c = knee.lerp(hem, (knee.z - z) / (knee.z - hem.z))
            rings.append(se_ring(z, r, r, c.y + push, n=12, p=2.0, xc=c.x))
        bm = bmesh.new()
        loft(bm, rings)
        finish("CH_Skier_Trousers_%s_form" % sfx, bm, ["trousers"])
        kp = P["knee_patch_centre"]
        ball("CH_Skier_KneePatch_%s_form" % sfx, KNEE_PATCH[0], (s * kp[0], KNEE_PATCH[1], kp[2]), "knee_patch", u=8, v=6)
        bx = s * P["boot_x"]
        bm = bmesh.new()
        rings = []
        for y, w, h in FOOT:
            ch = 0.025
            rings.append([(bx + x, y, z) for x, z in ((-w / 2, 0.0), (w / 2, 0.0), (w / 2, h - ch), (w / 2 - ch, h),
                                                        (-w / 2 + ch, h), (-w / 2, h - ch))])
        loft(bm, rings)
        sc, ss = P["boot_shaft_centre"], P["boot_shaft_size"]
        loft(bm, [rr_ring(z, ss[0] / 2, ss[1] / 2, 0.03, xc=bx, yc=sc[0]) for z in (0.10, 0.22)])
        finish("CH_Skier_BootShell_%s_form" % sfx, bm, ["boot"])
        bc, bs = P["cuff_band_centre"], P["cuff_band_size"]
        b0, b1, top = bc[1] - bs[2] / 2, bc[1] + bs[2] / 2, sc[1] + ss[2] / 2
        bm = bmesh.new()
        grid = loft(bm, [rr_ring(0.22, ss[0] / 2, ss[1] / 2, 0.03, bx, sc[0]),
                         rr_ring(b0, bs[0] / 2, bs[1] / 2, 0.03, bx, bc[0]),
                         rr_ring(b1, bs[0] / 2, bs[1] / 2, 0.03, bx, bc[0]),
                         rr_ring(b1 + 0.008, ss[0] / 2, ss[1] / 2, 0.03, bx, sc[0]),
                         rr_ring(top, ss[0] / 2, ss[1] / 2, 0.03, bx, sc[0])])
        for f in grid[1]:
            f.material_index = 1
        finish("CH_Skier_BootCuff_%s_form" % sfx, bm, ["boot", "boot_band"])
    T.log("forms built")
    measure(args)


def measure(args):
    """World bounds and triangle counts of the evaluated HIGH meshes, grouped by delivery object."""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    lo, hi, per = [1e9] * 3, [-1e9] * 3, {}
    for ob in T.col(C).all_objects:
        if ob.type != "MESH":
            continue
        eo = ob.evaluated_get(dg)
        me = eo.to_mesh()
        pts = [eo.matrix_world @ v.co for v in me.vertices]
        t = sum(len(p.vertices) - 2 for p in me.polygons)
        eo.to_mesh_clear()
        a = [min(p[i] for p in pts) for i in range(3)]
        b = [max(p[i] for p in pts) for i in range(3)]
        per[ob.name] = {"bbox": [round(v, 4) for v in a + b], "tris": t}
        lo, hi = [min(lo[i], a[i]) for i in range(3)], [max(hi[i], b[i]) for i in range(3)]
    groups = {"helmet_goggles": ("Helmet", "Goggle", "Lens"), "boot_L": ("Boot", "_L_"), "boot_R": ("Boot", "_R_")}
    tri = {"body": 0, "helmet_goggles": 0, "boot_L": 0, "boot_R": 0}
    for n, rec in per.items():
        g = "body"
        if any(k in n for k in groups["helmet_goggles"]):
            g = "helmet_goggles"
        elif "Boot" in n:
            g = "boot_L" if "_L_" in n else "boot_R"
        tri[g] += rec["tris"]
    out = {"bbox_min": [round(v, 4) for v in lo], "bbox_max": [round(v, 4) for v in hi],
           "total_height": round(hi[2] - lo[2], 4), "fingertip_span": round(hi[0] - lo[0], 4),
           "depth": round(hi[1] - lo[1], 4), "tris_by_group": tri, "tris_total": sum(tri.values()), "objects": per}
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
