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
Story 012 (detail pass for the chase camera, which shows the rider three to five times larger): rounder sections
(the RING COUNTS below), a navy drawcord line above the jacket hem and a hem that drops at the back, a navy yoke
stripe across the upper back, hand-pocket flaps on the side seams, cargo pockets on the thighs, a gaiter flare at the
trouser hem, gauntlets over the cuffs, helmet vents and a strap buckle, boot buckles and power straps. All from the
proportion table, drawn in ref/ (front, side and back).
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

# RING COUNTS (story 012: rounder for the chase camera). Phase 4 builds from the same constants.
JACKET_BACK_N = 9            # points over the back half of each jacket ring, side to side (was 5)
SLEEVE_N = 20                # sleeve sides (was 16); the stripe is the last column, centred on the outside
CUFF_N = 16                  # (was 12)
SEAT_N = 16                  # (was 12)
LEG_N = 16                   # trouser sides (was 12)
HOOD_N = 16                  # (was 12)
NECK_N = 18                  # neck warmer (was 14)
STRAP_N = 20                 # goggle strap ring points (was 16)
THUMB_N = 10                 # (was 8)
MITTEN_SEGS = 3              # mitten bevel segments (was 2)
UV = {"head": (16, 10), "helmet": (20, 12), "shoulder": (16, 10), "thumbtip": (10, 8), "knee": (10, 8)}  # was
#   12x8, 16x10, 12x8, 8x6, 8x6
# FORMS CHOICES (metres). Jacket sections (z, half width, half depth): a puffy chest and a slight hem flare. Story
# 012 adds rings for the drawcord line (0.845 to 0.87) and the yoke stripe (1.285 to 1.315, replacing 1.30 and
# keeping its profile).
JACKET = [(0.82, .245, .162), (0.845, .245, .162), (0.87, .245, .162), (0.93, .232, .152), (1.02, .232, .152),
          (1.15, .238, .157), (1.22, .238, .157), (1.285, .2364, .1554), (1.315, .2334, .1533), (1.38, .222, .146),
          (1.40, .15, .11), (1.42, .11, .08)]   # neckline under the band
JACKET_Z = [r[0] for r in JACKET]
HEM_DROP_TOP = 0.8701        # rings at or below this z (the hem and the drawcord line) drop at the back
JACKET_YC, PUFF_P = 0.02, 2.6                 # section centre y; superellipse exponent (2 = ellipse)
SHOULDER_PUFF = 0.095                         # front: shoulder top 1.45, width 0.55 (table 1.445, 0.53)
SLEEVE = [(0.0, .088), (0.08, .088), (0.18, .082), (0.29, .076), (0.40, .072)]   # (from shoulder, radius)
GAUNTLET_N = 16
PANEL_INSET = 0.010          # raised panels (pocket flaps, cargo pockets) reach this far under the surface they sit on
CARGO_FLAP_LIP = 0.004       # the cargo pocket's flap stands this much prouder than the pocket
# Seat rings above the jacket hem (z 0.82) sit well inside the jacket: the jacket's back ring is coarse, and its flat
# chords cut inside a seat drawn to the jacket's own size, so the seat showed through the lower back as two navy
# patches either side of the spine, even at rest (story 012; was .19/.14 and .18/.13, 3.4 and 1.0 mm outside).
# Now 22 and 29.5 mm inside (measured against the jacket ring at the same height).
SEAT = [(0.77, .13, .09), (0.80, .185, .13), (0.88, .17, .115), (0.95, .155, .10)]
LEG = [(0.92, .105, 0.0), (0.82, .108, 0.0), (0.72, .106, 0.0), (0.62, .102, 0.0), (0.55, .101, -.006),
       (0.50, .100, -.010), (0.45, .098, -.006), (0.38, .096, 0.0), (0.29, P["gaiter_r"], 0.0)]   # (z, radius,
#   knee push); the hem flares over the boot cuff (story 012 gaiter; was .100)
FOOT = [(0.13, .10, .12), (0.10, .115, .13), (0.0, .118, .135), (-0.10, .115, .12), (-0.16, .105, .10),
        (-0.19, .09, .075)]                   # side: (y, width, height), heel to toe; sole on z 0
FOOT_BUCKLE_Y = -0.015                       # the boot buckles' y, a little forward of the boot centre (ref: fy / 2)
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


def se_ring(z, hw, hd, yc, n=SEAT_N, p=PUFF_P, xc=0.0):
    out = []
    for k in range(n):
        th = 2 * math.pi * (k + 0.5) / n
        c, s = math.cos(th), math.sin(th)
        out.append((xc + hw * math.copysign(abs(c) ** (2 / p), c), yc + hd * math.copysign(abs(s) ** (2 / p), s), z))
    return out


def torso_ring(z, hw, hd, yc=JACKET_YC, p=PUFF_P, drop=0.0):
    """Front points at fixed x so face columns 4 (zip) and 2 and 6 (chest stripes) line up on every section; then
    JACKET_BACK_N points round the back from the left side (+X) to the right. drop lowers the back (most at the
    centre, none at the sides): the hem's drop tail."""
    zh, sx, sh = P["zip_size"][0] / 2, P["chest_stripe_x"], P["chest_stripe_size"][0] / 2
    o1 = max(0.7 * hw, sx + sh + 0.004)
    o2 = max(0.9 * hw, o1 + 0.004)
    xs = [-o2, -o1, -sx - sh, -sx + sh, -zh, zh, sx - sh, sx + sh, o1, o2]
    pts = [(x, yc - hd * max(0.0, 1 - abs(x / hw) ** p) ** (1 / p), z) for x in xs]
    for k in range(JACKET_BACK_N):
        th = math.pi * k / (JACKET_BACK_N - 1)
        c, s = math.cos(th), math.sin(th)
        pts.append((hw * math.copysign(abs(c) ** (2 / p), c), yc + hd * math.copysign(abs(s) ** (2 / p), s),
                    z - drop * max(0.0, s) ** 2))
    return pts


def jacket_rings():
    """The jacket's approved rings, the hem and drawcord rings dropping at the back."""
    return [torso_ring(z, hw, hd, drop=P["hem_drop_back"] if z <= HEM_DROP_TOP else 0.0) for z, hw, hd in JACKET]


def jacket_splits(row, z0, z1):
    """Material splits on one row of jacket faces lying between approved rings z0 and z1 (Phase 3 and 4 alike):
    zip (column 4), chest stripes (columns 2 and 6), the drawcord line (the whole row) and the yoke stripe (back
    faces near the centre)."""
    s0 = P["chest_stripe_z"] - P["chest_stripe_size"][2] / 2
    s1 = P["chest_stripe_z"] + P["chest_stripe_size"][2] / 2
    if z1 <= 1.40 + 1e-6:
        row[4].material_index = 1
    if z0 >= s0 - 1e-6 and z1 <= s1 + 1e-6:
        row[2].material_index = row[6].material_index = 1
    b0, b1 = P["hem_band_z"]
    if z0 >= b0 - 1e-6 and z1 <= b1 + 1e-6:
        for f in row:
            f.material_index = 1
    y0, y1 = P["yoke_stripe_z"]
    if z0 >= y0 - 1e-6 and z1 <= y1 + 1e-6:
        for f in row:
            c = f.calc_center_median()
            if c.y > JACKET_YC + 0.03 and abs(c.x) < P["yoke_stripe_half_w"]:
                f.material_index = 1


def rr_ring(z, a, b, c, xc=0.0, yc=0.0):
    pts = [(a, -b + c), (a, b - c), (a - c, b), (-a + c, b), (-a, b - c), (-a, -b + c), (-a + c, -b), (a - c, -b)]
    return [(xc + x, yc + y, z) for x, y in pts]


def hood_ring(z, rs, rb, rf, n=HOOD_N):
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


def leg_rings(s):
    """The trouser leg's rings for side s (+1 left, -1 right): centred on the hip, knee and hem line."""
    hip, knee, hem = [Vector((s * P[k][0], P[k][1], P[k][2])) for k in ("hip_joint", "knee", "trouser_hem")]
    rings = []
    for z, r, push in LEG:
        if z >= hip.z:
            c = hip
        elif z >= knee.z:
            c = hip.lerp(knee, (hip.z - z) / (hip.z - knee.z))
        else:
            c = knee.lerp(hem, (knee.z - z) / (knee.z - hem.z))
        rings.append(se_ring(z, r, r, c.y + push, n=LEG_N, p=2.0, xc=c.x))
    return rings


def section(rings, z):
    """The outline [(x, y)] at height z of a loft of level rings (linear between the two rings around z)."""
    for a, b in zip(rings, rings[1:]):
        za, zb = a[0][2], b[0][2]
        if min(za, zb) - 1e-9 <= z <= max(za, zb) + 1e-9:
            t = (z - za) / (zb - za)
            return [(pa[0] + (pb[0] - pa[0]) * t, pa[1] + (pb[1] - pa[1]) * t) for pa, pb in zip(a, b)]
    raise ValueError("z %.3f is outside the loft" % z)


def ray_hit(poly, c, phi):
    """Where the ray from c at angle phi leaves the outline poly: (point, outward normal); the mean normal at a
    corner."""
    dx, dy = math.cos(phi), math.sin(phi)
    n, best = len(poly), None
    for k in range(n):
        (x0, y0), (x1, y1) = poly[k], poly[(k + 1) % n]
        ex, ey = x1 - x0, y1 - y0
        den = dx * ey - dy * ex
        if abs(den) < 1e-12:
            continue
        wx, wy = x0 - c[0], y0 - c[1]
        s, t = (wx * ey - wy * ex) / den, (wx * dy - wy * dx) / den
        if s > 0 and -1e-9 <= t <= 1 + 1e-9 and (best is None or s > best[0]):
            best = (s, k, t)

    def normal(k):
        (x0, y0), (x1, y1) = poly[k % n], poly[(k + 1) % n]
        v = Vector((y1 - y0, x0 - x1))
        if v.dot(Vector(((x0 + x1) / 2 - c[0], (y0 + y1) / 2 - c[1]))) < 0:
            v = -v
        return v.normalized()
    s, k, t = best
    nv = normal(k)
    if t < 1e-6:
        nv = (nv + normal(k - 1)).normalized()
    elif t > 1 - 1e-6:
        nv = (nv + normal(k + 1)).normalized()
    return (c[0] + s * dx, c[1] + s * dy), (nv.x, nv.y)


def panel(bm, rings, centre, phi0, phi1, prof):
    """A raised panel lying on a loft of level rings: between angles phi0 and phi1 about centre(z), the closed
    profile prof [(offset along the outward normal, z)] swept along the surface, through every corner of the
    outline on the way, so it follows the faces. Returns faces[segment][profile edge]."""
    zm = sum(z for _, z in prof) / len(prof)
    cm = centre(zm)
    corners = sorted(math.atan2(y - cm[1], x - cm[0]) for x, y in section(rings, zm))
    phis = [phi0] + [p for p in corners if phi0 + 1e-4 < p < phi1 - 1e-4] + [phi1]
    out = []
    for phi in phis:
        ring = []
        for off, z in prof:
            (x, y), (nx, ny) = ray_hit(section(rings, z), centre(z), phi)
            ring.append((x + nx * off, y + ny * off, z))
        out.append(ring)
    return loft(bm, out)


def ring_centre(rings):
    """centre(z) for a loft of round rings: the outline's centroid."""
    def c(z):
        pts = section(rings, z)
        return (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
    return c


def mirror_x(bm):
    for v in bm.verts:
        v.co.x = -v.co.x
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])


def pocket_flap(bm):
    """Hand-pocket flap on the left side seam (+X), a panel on the jacket with its zip along the top edge (material
    index 1)."""
    fc, fs, zh = P["pocket_flap_centre"], P["pocket_flap_size"], P["pocket_zip_h"]
    rings = [r for r in jacket_rings() if r[0][2] > HEM_DROP_TOP]          # level rings above the hem
    phc = math.atan2(fc[1] - JACKET_YC, fc[0])
    half = fs[1] / 2 / math.hypot(fc[0], fc[1] - JACKET_YC)
    z0, z1 = fc[2] - fs[2] / 2, fc[2] + fs[2] / 2
    grid = panel(bm, rings, lambda z: (0.0, JACKET_YC), phc - half, phc + half,
                 [(-PANEL_INSET, z0), (fs[0], z0), (fs[0], z1 - zh), (fs[0], z1), (-PANEL_INSET, z1)])
    for row in grid:
        row[2].material_index = row[3].material_index = 1


def cargo_pocket(bm):
    """Cargo pocket on the outside of the left thigh, a panel on the trousers (material index 0) with a flap over its
    top that stands a little prouder (index 1)."""
    cc, cs, fh = P["cargo_pocket_centre"], P["cargo_pocket_size"], P["cargo_flap_h"]
    rings = leg_rings(1)
    cen = ring_centre(rings)
    c = cen(cc[2])
    phc = math.atan2(cc[1] - c[1], cc[0] - c[0])
    half = cs[1] / 2 / math.hypot(cc[0] - c[0], cc[1] - c[1])
    z0, z1 = cc[2] - cs[2] / 2, cc[2] + cs[2] / 2
    lip = cs[0] + CARGO_FLAP_LIP
    grid = panel(bm, rings, cen, phc - half, phc + half,
                 [(-PANEL_INSET, z0), (cs[0], z0), (cs[0], z1 - fh), (lip, z1 - fh), (lip, z1), (-PANEL_INSET, z1)])
    for row in grid:
        for j in (2, 3, 4, 5):
            row[j].material_index = 1


def build(scene, args):
    # JACKET: body with zip, chest stripes, drawcord line and yoke stripe as material splits
    bm = bmesh.new()
    grid = loft(bm, jacket_rings())
    for i, row in enumerate(grid):
        jacket_splits(row, JACKET[i][0], JACKET[i + 1][0])
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
    loft(bm, [se_ring(z, ns[0] / 2, ns[1] / 2, nc[1], n=NECK_N, p=2.4) for z in (nc[2] - ns[2] / 2, nc[2] + ns[2] / 2)])
    finish("CH_Skier_NeckWarmer_form", bm, ["neck_warmer"])
    # FACE
    ball("CH_Skier_Head_form", P["head_radii"], P["head_centre"], "skin", u=UV["head"][0], v=UV["head"][1])
    boxed("CH_Skier_Jaw_form", P["jaw_size"], P["jaw_centre"], "skin", bevel=P["jaw_r"])
    boxed("CH_Skier_Chin_form", P["chin_size"], P["chin_centre"], "skin", bevel=P["chin_r"])
    boxed("CH_Skier_Nose_form", P["nose_size"], P["nose_centre"], "skin", bevel=P["nose_r"])
    boxed("CH_Skier_Mouth_form", P["mouth_size"], P["mouth_centre"], "mouth")
    # HELMET, top panel split, strap, goggles curved to the helmet front
    hc, hr = P["helmet_centre"], P["helmet_radii"]
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=UV["helmet"][0], v_segments=UV["helmet"][1], radius=1.0)
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
        rings.append([(hc[0] + (hr[0] * e + 0.008) * math.cos(2 * math.pi * k / STRAP_N),
                       hc[1] + (hr[1] * e + 0.008) * math.sin(2 * math.pi * k / STRAP_N), z) for k in range(STRAP_N)])
    bm = bmesh.new()
    loft(bm, rings)
    finish("CH_Skier_GoggleStrap_form", bm, ["goggle_frame"])
    # VENTS: four dark grooves along the top, front to back, each a strip lying 1 mm off the round shell (its
    # faceted faces lie inside that)
    bm = bmesh.new()
    top_k = [j / 8 for j in range(9)]
    for vx in P["vent_x"]:
        strip = []
        for t in top_k:
            y = P["vent_y"][0] + (P["vent_y"][1] - P["vent_y"][0]) * t
            row = []
            for x in (vx - P["vent_w"] / 2, vx + P["vent_w"] / 2):
                q = 1 - (x / hr[0]) ** 2 - ((y - hc[1]) / hr[1]) ** 2
                row.append(bm.verts.new((x, y, hc[2] + hr[2] * math.sqrt(max(q, 0.0)) + 0.001)))
            strip.append(row)
        for a, b in zip(strip, strip[1:]):
            bm.faces.new((a[0], a[1], b[1], b[0]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    finish("CH_Skier_Vents_form", bm, ["goggle_frame"])
    # STRAP BUCKLE at the back of the strap
    bs = P["strap_buckle_size"]
    boxed("CH_Skier_StrapBuckle_form", bs, (0.0, hc[1] + hr[1] + 0.008 + bs[1] / 2 - 0.002, sum(P["strap_z"]) / 2),
          "buckle", bevel=0.003)
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
        ball("CH_Skier_Shoulder_%s_form" % sfx, (SHOULDER_PUFF,) * 3, S - Vector((0, 0, 0.005)), "jacket",
             u=UV["shoulder"][0], v=UV["shoulder"][1])
        st = SLEEVE + [(P["upper_arm_len"] + P["forearm_len"] + P["forearm_end"], P["sleeve_r"][2] + 0.001)]
        bm = bmesh.new()
        grid = loft(bm, [tube_ring(S + d * t, d, r, SLEEVE_N, n_out) for t, r in st])
        for i in range(len(st) - 1):
            if st[i + 1][0] <= P["upper_arm_len"] + 1e-6:
                grid[i][SLEEVE_N - 1].material_index = 1       # stripe down the outside of the upper sleeve
        finish("CH_Skier_Sleeve_%s_form" % sfx, bm, ["jacket", "stripe"])
        c0, c1, cr = P["cuff"]
        bm = bmesh.new()
        loft(bm, [tube_ring(W + d * c0, d, cr, CUFF_N, n_out), tube_ring(W + d * c1, d, cr, CUFF_N, n_out)])
        finish("CH_Skier_Cuff_%s_form" % sfx, bm, ["cuff"])
        g0, g1, gr0, gr1 = P["gauntlet"]                 # over the cuff and the sleeve end, flaring to the hand
        bm = bmesh.new()
        loft(bm, [tube_ring(W + d * g1, d, gr1, GAUNTLET_N, n_out), tube_ring(W + d * (g1 + g0) / 2, d, (gr0 + gr1) / 2,
                                                                               GAUNTLET_N, n_out),
                  tube_ring(W + d * g0, d, gr0, GAUNTLET_N, n_out)])
        finish("CH_Skier_Gauntlet_%s_form" % sfx, bm, ["mitten"])
        M = W + d * P["mitten_offset"]
        boxed("CH_Skier_Mitten_%s_form" % sfx, P["mitten_size"], M, "mitten", bevel=P["mitten_r"], segs=MITTEN_SEGS,
              rot=(0.0, s * a, 0.0))
        t0v, t1v, ty, tr = P["thumb"]
        p0 = M + d * t0v
        p1 = M + d * t1v + Vector((0.0, ty, 0.0))
        bm = bmesh.new()
        loft(bm, [tube_ring(p0, p1 - p0, tr, THUMB_N, (0, 0, 1)), tube_ring(p1, p1 - p0, tr, THUMB_N, (0, 0, 1))])
        finish("CH_Skier_Thumb_%s_form" % sfx, bm, ["mitten"])
        ball("CH_Skier_ThumbTip_%s_form" % sfx, (tr,) * 3, p1, "mitten", u=UV["thumbtip"][0], v=UV["thumbtip"][1])
        # HAND-POCKET FLAP on the side seam, a panel following the jacket, its zip along the top edge
        bm = bmesh.new()
        pocket_flap(bm)
        if s < 0:
            mirror_x(bm)
        finish("CH_Skier_PocketFlap_%s_form" % sfx, bm, ["jacket", "stripe"])
        bm = bmesh.new()
        loft(bm, leg_rings(s))
        finish("CH_Skier_Trousers_%s_form" % sfx, bm, ["trousers"])
        kp = P["knee_patch_centre"]
        ball("CH_Skier_KneePatch_%s_form" % sfx, KNEE_PATCH[0], (s * kp[0], KNEE_PATCH[1], kp[2]), "knee_patch",
             u=UV["knee"][0], v=UV["knee"][1])
        # CARGO POCKET on the outside of the thigh, a panel following the leg, with a navy flap over its top
        bm = bmesh.new()
        cargo_pocket(bm)
        if s < 0:
            mirror_x(bm)
        finish("CH_Skier_CargoPocket_%s_form" % sfx, bm, ["knee_patch", "trousers"])
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
        kb = P["boot_buckle_size"]                       # two buckles on the outside of the shell
        for z in P["boot_buckle_z"]:
            vs = bmesh.ops.create_cube(bm, size=1.0)["verts"]
            bmesh.ops.scale(bm, vec=kb, verts=vs)
            bmesh.ops.translate(bm, vec=(bx + s * (FOOT[2][1] / 2 + kb[0] / 2 - 0.004), FOOT_BUCKLE_Y, z), verts=vs)
            for f in {f for v in vs for f in v.link_faces}:
                f.material_index = 1
        finish("CH_Skier_BootShell_%s_form" % sfx, bm, ["boot", "buckle"])
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
        ps0, ps1 = P["power_strap_z"]                    # power strap round the top of the cuff, 4 mm proud
        strap = loft(bm, [rr_ring(z, ss[0] / 2 + 0.004, ss[1] / 2 + 0.004, 0.034, bx, sc[0]) for z in (ps0, ps1)])
        for row in strap:
            for f in row:
                f.material_index = 2
        finish("CH_Skier_BootCuff_%s_form" % sfx, bm, ["boot", "boot_band", "goggle_frame"])
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
    groups = {"helmet_goggles": ("Helmet", "Goggle", "Lens", "Vents", "StrapBuckle"), "boot_L": ("Boot", "_L_"),
              "boot_R": ("Boot", "_R_")}
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
