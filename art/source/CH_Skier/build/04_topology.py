"""CH_Skier topology and LODs (Phase 4): the LOW delivery meshes over the approved Phase 3 forms.

  $BLENDER_BIN --background --python-exit-code 1 --python build/04_topology.py -- --master CH_Skier_master.blend

Builds, in collection LOW (HIGH, SOCKETS and the sockets' positions are not touched):
  LOW_LOD0   CH_Skier_Body_LOD0   one skinned body mesh. Source in quads (ngon caps fanned to triangles), with a
                                  Triangulate modifier (quads FIXED, ngons BEAUTY) as the locked triangulation: the
                                  same split Blender used to draw the Phase 3 forms, and what bake and export see.
  LOW_LOD1   CH_Skier_Body_LOD1   skill lod_copy (Decimate, collapse) per part, outlines fixed, triangles.
  LOW_LOD2   CH_Skier_Body_LOD2   same, about 1k triangles.
  LOW_PROPS  CH_Skier_Helmet (shell, top panel split, strap, goggles, lens), CH_Skier_BootShell_L/R (foot bone),
             CH_Skier_BootCuff_L/R (shin bone, hinged at the ankle). Rigid, one LOD, shared by every body LOD.
The Phase 2 blockout moves from LOW to BLOCKOUT (hidden), so LOW holds only delivery meshes.

How the body is made: every lofted part is rebuilt from the Phase 3 ring generators with extra rings inserted
between the approved rings (linear, so they lie on the approved faces and the silhouette does not move), placed at
the joints. Parts made from primitives (head, face pieces, shoulder puffs, mittens, thumbs, knee patches) are the
Phase 3 meshes as they are. Material splits follow the same face columns and rows as Phase 3. Right-side parts
are exact mirrors of the left. Then the cleanup: doubles, zero-area faces, loose geometry, stray islands,
recalculated normals, and removal of faces buried inside other parts (overlapping interior shells).
"""
import os, sys, math, json, argparse, importlib.util

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "../../../../.claude/skills/blender-image-to-3d/assets")))
import build_template as T  # noqa: E402
import bpy, bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

_spec = importlib.util.spec_from_file_location("forms03", os.path.join(HERE, "03_forms.py"))
F = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(F)      # the Phase 3 constants and ring generators: LOW follows the approved forms exactly
T.PHASE = "04_topology"          # (importing 03 set it to 03_forms)
T.OWNER_TAG = "phase:" + T.PHASE
P = F.P

# EDGE LOOPS added for deformation, as {segment between Phase 3 rings i and i+1: [fractions]}. Positions in metres.
INS_JACKET = {F.JACKET_Z.index(1.02): [0.5]}   # waist loop z 1.085 for the spine bend: .93, 1.02, 1.085, 1.15
INS_SEAT = {1: [0.5]}              # z 0.84: seat loops .80, .84, .88 around the hip joints (z 0.88)
INS_HOOD = {0: [0.5], 1: [0.5]}    # neck: 1.39, 1.4075, 1.425, 1.435, 1.445
INS_NECKWARMER = {0: [0.5]}        # 1.385, 1.4075, 1.43
INS_SLEEVE = {0: [0.5], 1: [0.5], 2: [0.5], 3: [0.5], 4: [0.5]}
#   along the arm from the shoulder joint: shoulder 0, .04, .08, .13 | elbow (0.29) .235, .29, .345 | forearm
#   twist .40, .46 | sleeve end .52, under the cuff
INS_CUFF = {0: [1 / 3, 2 / 3]}     # wrist (0.0 from the wrist joint): -0.04, -0.02, 0.0, +0.02
INS_LEG = {0: [0.4], 1: [0.5], 7: [0.5]}   # hip (0.88) .92, .88, .82, .77 | knee (0.50) .55, .50, .45 were
#   already Phase 3 rings | hem .38, .335, .29
HINGE_SKIRT = (0.17, 0.004)        # boot cuff: a skirt down to z 0.17, inset 4 mm, hidden inside the shell, so the
#   cuff can rotate about the ankle (z 0.11) into the ski stance without opening a gap at the hinge
MARGIN_BODY = 0.008   # a face is interior when every sample point sits this deep inside other parts (different bones)
MARGIN_RIGID = 0.003  # parts on one rigid bone (head pieces under the helmet, pieces of the helmet or a boot)
HEAD_PARTS = ("Head", "Jaw", "Chin", "Nose", "Mouth")
# LOD part ratios (Decimate face ratio per part, applied with the skill's lod_copy). Silhouette parts named in the
# brief (head, shoulders, mittens) keep more; long tubes (sleeves, legs) give the most.
LOD_RATIO = {   # story 012: LOD0 grew from 2,895 to about 4,300, so LOD2 keeps a smaller share to stay near 1k, and
    #   drops the pocket flaps and cargo pockets (0: left out), which are under a pixel wherever LOD2 is shown
    1: {"Jacket": .75, "Seat": .85, "Hood": .7, "NeckWarmer": .7, "Head": .85, "Jaw": 1, "Chin": 1, "Nose": 1,
        "Mouth": 1, "Shoulder": .8, "Sleeve": .7, "Cuff": .7, "Mitten": .85, "Thumb": 1, "ThumbTip": .7,
        "Trousers": .7, "KneePatch": .7, "Gauntlet": .7, "PocketFlap": .8, "CargoPocket": .8},
    2: {"Jacket": .35, "Seat": 1, "Hood": .5, "NeckWarmer": .25, "Head": .45, "Jaw": .5, "Chin": .5, "Nose": .7,
        "Mouth": 1, "Shoulder": .3, "Sleeve": .18, "Cuff": .25, "Mitten": .3, "Thumb": .5, "ThumbTip": .2,
        "Trousers": .25, "KneePatch": .25, "Gauntlet": .25, "PocketFlap": 0, "CargoPocket": 0},
}
OUTLINE_SNAP_TOL = 0.002   # a LOD part whose extent along an axis falls this far inside LOD0's is pulled back out
OUTLINE_MAX = 0.006        # larger shrinkage is not patched (it would make a spike); that part's ratio is raised
DIRS = (Vector((0, 0, 1)), Vector((0, 1, 0)), Vector((0, -1, 0)))   # mirror-symmetric ray directions
STATS = {"interior_faces_removed": {}, "stray_islands_removed": {}, "outline_fixes": {}, "loops": {
    "neck": "hood rings z 1.39/1.4075/1.425/1.435/1.445, neck warmer 1.385/1.4075/1.43",
    "shoulder": "sleeve rings 0/.04/.08/.13 m from the joint, shoulder puff 8 latitude rings, jacket 1.30/1.38/1.40",
    "elbow": "sleeve rings .235/.29/.345 m from the shoulder (elbow at .29)",
    "wrist": "sleeve end .52, cuff rings -.04/-.02/0/+.02 m about the wrist",
    "spine": "jacket rings .93/1.02/1.085/1.15",
    "hip": "trouser rings .92/.88/.82/.77, seat .80/.84/.88",
    "knee": "trouser rings .62/.55/.50/.45/.38",
    "ankle": "boot hinge: shell (foot) and cuff (shin) with a hidden skirt; trouser hem rings .38/.335/.29",
    "material_boundaries": "zip column 4 and chest stripe columns 2 and 6 (jacket rings 1.22 and 1.38 bound "
                           "the stripes), sleeve stripe column 15 ending at the elbow ring, cuff ends, boot band "
                           "rings, jacket hem ring 0.82, trouser hem ring 0.29"}}


# PARTS: {"name", "bm" (world space), "roles" (material role per index), "side" C/L/R}

def part(name, bm, roles, side="C"):
    return {"name": name, "bm": bm, "roles": list(roles), "side": side}


def lerp_ring(a, b, t):
    return [tuple(pa[i] + (pb[i] - pa[i]) * t for i in range(3)) for pa, pb in zip(a, b)]


def dense(rings, ins):
    """Insert rings between the approved ones; returns the rings and, per row, the Phase 3 row it lies in."""
    out, src = [rings[0]], []
    for i in range(len(rings) - 1):
        for t in sorted(ins.get(i, [])):
            out.append(lerp_ring(rings[i], rings[i + 1], t))
            src.append(i)
        out.append(rings[i + 1])
        src.append(i)
    return out, src


def from_form(form, side="C", name=None):
    """A Phase 3 form as it is (modifiers evaluated), in world space."""
    ob = bpy.data.objects["CH_Skier_%s_form" % form]
    dg = bpy.context.evaluated_depsgraph_get()
    eo = ob.evaluated_get(dg)
    me = eo.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(me)
    eo.to_mesh_clear()
    bm.transform(ob.matrix_world)
    roles = [m.name.replace("M_CH_Skier_", "") for m in ob.data.materials]
    return part(name or form, bm, roles, side)


def mirrored(p, name=None):
    bm = p["bm"].copy()
    for v in bm.verts:
        v.co.x = -v.co.x
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.normal_update()
    return part(name or p["name"].replace("_L", "_R"), bm, p["roles"], "R")


def islands(bm):
    bm.verts.index_update()
    seen, groups = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, g = [v], set()
        while stack:
            c = stack.pop()
            if c.index in seen:
                continue
            seen.add(c.index)
            g.add(c.index)
            stack.extend(e.other_vert(c) for e in c.link_edges)
        groups.append(g)
    return groups


def drop_loose(bm):
    for e in [e for e in bm.edges if not e.link_faces]:
        bm.edges.remove(e)
    for v in [v for v in bm.verts if not v.link_edges]:
        bm.verts.remove(v)


def clean(p):
    """Doubles, zero-area faces, loose geometry, stray islands (under 4 faces), consistent outward normals."""
    bm = p["bm"]
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-6)
    zero = [f for f in bm.faces if f.calc_area() < 1e-10]
    if zero:
        bmesh.ops.delete(bm, geom=zero, context="FACES")
    drop_loose(bm)
    bm.verts.ensure_lookup_table()
    stray = 0
    for g in islands(bm):
        faces = {f for i in g for f in bm.verts[i].link_faces}
        if len(faces) < 4:
            bmesh.ops.delete(bm, geom=list(faces), context="FACES")
            stray += 1
    drop_loose(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    STATS["stray_islands_removed"][p["name"]] = stray
    return p


def split_islands(p):
    out = []
    for k, g in enumerate(islands(p["bm"])):
        b = p["bm"].copy()
        b.verts.ensure_lookup_table()
        bmesh.ops.delete(b, geom=[b.verts[i] for i in range(len(b.verts)) if i not in g], context="VERTS")
        out.append(part("%s_%d" % (p["name"], k), b, p["roles"], p["side"]))
    return out


class Vol:
    """A closed part as an occluder: BVH plus bounds, built before any face is removed."""
    def __init__(self, p):
        self.name = p["name"]
        self.bvh = BVHTree.FromBMesh(p["bm"])
        cos = [v.co for v in p["bm"].verts]
        self.lo = Vector([min(c[i] for c in cos) for i in range(3)])
        self.hi = Vector([max(c[i] for c in cos) for i in range(3)])


def parity(bvh, p, d):
    n, o = 0, p.copy()
    for _ in range(64):
        hit = bvh.ray_cast(o, d)
        if hit[0] is None:
            break
        n += 1
        o = hit[0] + d * 1e-4
    return n % 2 == 1


def deep_inside(vol, p, margin):
    if any(p[i] < vol.lo[i] + margin or p[i] > vol.hi[i] - margin for i in range(3)):
        return False
    loc, nrm, _idx, dist = vol.bvh.find_nearest(p)
    if loc is None or dist < margin or (p - loc).dot(nrm) > 0:
        return False
    return all(parity(vol.bvh, p, d) for d in DIRS)


def remove_interior(p, occ):
    """Delete faces whose corners, edge midpoints and centre all sit deep inside other parts."""
    bm = p["bm"]
    cos = [v.co for v in bm.verts]
    lo = [min(c[i] for c in cos) for i in range(3)]
    hi = [max(c[i] for c in cos) for i in range(3)]
    occ = [(v, m) for v, m in occ if all(v.lo[i] <= hi[i] and v.hi[i] >= lo[i] for i in range(3))]
    dead = []
    for f in bm.faces:
        pts = [v.co.copy() for v in f.verts] + [f.calc_center_median()] + \
              [(e.verts[0].co + e.verts[1].co) / 2 for e in f.edges]
        if occ and all(any(deep_inside(v, q, m) for v, m in occ) for q in pts):
            dead.append(f)
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES")
    drop_loose(bm)
    STATS["interior_faces_removed"][p["name"]] = len(dead)


def fan_ngons(bm):
    ng = [f for f in bm.faces if len(f.verts) > 4]
    if ng:
        bmesh.ops.triangulate(bm, faces=ng, quad_method="BEAUTY", ngon_method="BEAUTY")


def tri_copy(p):
    bm = p["bm"].copy()
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="FIXED", ngon_method="BEAUTY")
    return part(p["name"], bm, p["roles"], p["side"])


def join(name, parts, collection):
    roles = []
    for p in parts:
        for r in p["roles"]:
            if r not in roles:
                roles.append(r)
    out = bmesh.new()
    for p in parts:
        vm = {v: out.verts.new(v.co) for v in p["bm"].verts}
        for f in p["bm"].faces:
            nf = out.faces.new([vm[v] for v in f.verts])
            nf.material_index = roles.index(p["roles"][f.material_index])
    ob = T.new_mesh_object(name, out, collection)
    for r in roles:
        ob.data.materials.append(F.mat(r))
    return ob


def lock_tris(ob):
    m = ob.modifiers.new("Triangulate", "TRIANGULATE")
    m.quad_method = "FIXED"
    m.ngon_method = "BEAUTY"
    m.min_vertices = 4
    try:
        m.keep_custom_normals = True
    except AttributeError:
        pass


def subcol(name, parent):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if parent.children.get(name) is None:
        parent.children.link(c)
    return c


# BODY

def body_parts():
    C, L = [], []
    rings, src = dense(F.jacket_rings(), INS_JACKET)
    bm = bmesh.new()
    grid = F.loft(bm, rings)
    for k, row in enumerate(grid):          # the Phase 3 rule, applied per approved row
        F.jacket_splits(row, F.JACKET[src[k]][0], F.JACKET[src[k] + 1][0])
    C.append(part("Jacket", bm, ["jacket", "stripe"]))
    bm = bmesh.new()
    F.loft(bm, dense([F.se_ring(z, hw, hd, 0.03) for z, hw, hd in F.SEAT], INS_SEAT)[0])
    C.append(part("Seat", bm, ["trousers"]))
    bm = bmesh.new()
    F.loft(bm, dense([F.hood_ring(z, rs, rb, rf) for z, rs, rb, rf in F.HOOD], INS_HOOD)[0])
    C.append(part("Hood", bm, ["jacket"]))
    nc, ns = P["neck_warmer_centre"], P["neck_warmer_size"]
    bm = bmesh.new()
    F.loft(bm, dense([F.se_ring(z, ns[0] / 2, ns[1] / 2, nc[1], n=F.NECK_N, p=2.4)
                      for z in (nc[2] - ns[2] / 2, nc[2] + ns[2] / 2)], INS_NECKWARMER)[0])
    C.append(part("NeckWarmer", bm, ["neck_warmer"]))
    for f in HEAD_PARTS:
        C.append(from_form(f))
    # LEFT ARM (+X); the right side is its mirror
    a = math.radians(P["arm_angle_deg"])
    sj = P["shoulder_joint"]
    d = Vector((math.cos(a), 0.0, -math.sin(a)))
    n_out = Vector((math.sin(a), 0.0, math.cos(a)))
    S = Vector((sj[0], sj[1], sj[2]))
    W = S + d * (P["upper_arm_len"] + P["forearm_len"])
    L.append(from_form("Shoulder_L", "L"))
    st = F.SLEEVE + [(P["upper_arm_len"] + P["forearm_len"] + P["forearm_end"], P["sleeve_r"][2] + 0.001)]
    rings, src = dense([F.tube_ring(S + d * t, d, r, F.SLEEVE_N, n_out) for t, r in st], INS_SLEEVE)
    bm = bmesh.new()
    grid = F.loft(bm, rings)
    for k, row in enumerate(grid):
        if st[src[k] + 1][0] <= P["upper_arm_len"] + 1e-6:
            row[F.SLEEVE_N - 1].material_index = 1   # stripe down the outside of the upper sleeve, to the elbow ring
    L.append(part("Sleeve_L", bm, ["jacket", "stripe"], "L"))
    c0, c1, cr = P["cuff"]
    bm = bmesh.new()
    F.loft(bm, dense([F.tube_ring(W + d * c0, d, cr, F.CUFF_N, n_out), F.tube_ring(W + d * c1, d, cr, F.CUFF_N, n_out)],
                     INS_CUFF)[0])
    L.append(part("Cuff_L", bm, ["cuff"], "L"))
    for f in ("Gauntlet_L", "Mitten_L", "Thumb_L", "ThumbTip_L", "PocketFlap_L"):
        L.append(from_form(f, "L"))
    # LEFT LEG
    bm = bmesh.new()
    F.loft(bm, dense(F.leg_rings(1), INS_LEG)[0])
    L.append(part("Trousers_L", bm, ["trousers"], "L"))
    for f in ("KneePatch_L", "CargoPocket_L"):
        L.append(from_form(f, "L"))
    return C, L


def helmet_parts():
    return [clean(from_form(f)) for f in ("Helmet", "GoggleStrap", "Goggles", "Lens", "Vents", "StrapBuckle")]


def boot_cuff_L():
    """The Phase 3 cuff rings plus a hidden hinge skirt; the band stays on the same row of faces."""
    bx = P["boot_x"]
    sc, ss = P["boot_shaft_centre"], P["boot_shaft_size"]
    bc, bs = P["cuff_band_centre"], P["cuff_band_size"]
    b0, b1, top = bc[1] - bs[2] / 2, bc[1] + bs[2] / 2, sc[1] + ss[2] / 2
    zs, ins = HINGE_SKIRT
    rings = [F.rr_ring(zs, ss[0] / 2 - ins, ss[1] / 2 - ins, 0.03 - ins, bx, sc[0]),
             F.rr_ring(0.22, ss[0] / 2 - ins, ss[1] / 2 - ins, 0.03 - ins, bx, sc[0]),
             F.rr_ring(0.22, ss[0] / 2, ss[1] / 2, 0.03, bx, sc[0]),
             F.rr_ring(b0, bs[0] / 2, bs[1] / 2, 0.03, bx, bc[0]),
             F.rr_ring(b1, bs[0] / 2, bs[1] / 2, 0.03, bx, bc[0]),
             F.rr_ring(b1 + 0.008, ss[0] / 2, ss[1] / 2, 0.03, bx, sc[0]),
             F.rr_ring(top, ss[0] / 2, ss[1] / 2, 0.03, bx, sc[0])]
    bm = bmesh.new()
    grid = F.loft(bm, rings)
    for f in grid[3]:
        f.material_index = 1
    ps0, ps1 = P["power_strap_z"]          # the Phase 3 power strap round the top of the cuff
    for row in F.loft(bm, [F.rr_ring(z, ss[0] / 2 + 0.004, ss[1] / 2 + 0.004, 0.034, bx, sc[0]) for z in (ps0, ps1)]):
        for f in row:
            f.material_index = 2
    return clean(part("BootCuff_L", bm, ["boot", "boot_band", "goggle_frame"], "L"))


# LODS

def lod_part(p, level, tmp):
    """The skill's lod_copy on one part, with material-boundary vertices protected, then the outline fix."""
    key = p["name"].replace("_L", "")
    ratio = LOD_RATIO[level][key]
    bmc = p["bm"].copy()
    bmc.verts.index_update()
    keep = [v.index for v in bmc.verts if len({f.material_index for f in v.link_faces}) > 1
            or (key == "Jacket" and (v.co.z <= 0.871 or abs(v.co.z - 1.38) < 1e-3))]   # hem flare, shoulder line
    src = T.new_mesh_object("TMP_%s_LOD0" % p["name"], bmc, tmp)
    for r in p["roles"]:
        src.data.materials.append(F.mat(r))
    new = T.lod_copy(src, level, ratio)
    if not new.users_collection:
        T.col(tmp).objects.link(new)
    dec = [m for m in new.modifiers if m.type == "DECIMATE"][-1]
    dec.decimate_type = "COLLAPSE"
    dec.ratio = ratio
    dec.use_collapse_triangulate = True
    if keep:
        vg = new.vertex_groups.new(name="lod_keep")
        vg.add(keep, 1.0, "REPLACE")
        dec.vertex_group = "lod_keep"
        dec.vertex_group_factor = 10.0
        dec.invert_vertex_group = True   # collapse penalises LOW group weights, so invert: boundary verts protected
    if p["side"] == "C":
        dec.use_symmetry = True
        dec.symmetry_axis = "X"
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    eo = new.evaluated_get(dg)
    me = eo.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(me)
    eo.to_mesh_clear()
    STATS.setdefault("lod_part_tris", {})["LOD%d %s" % (level, p["name"])] = [len(p["bm"].faces), len(bm.faces)]
    out = part(p["name"], bm, p["roles"], p["side"])
    STATS["outline_fixes"]["LOD%d %s" % (level, p["name"])] = fix_outline(out, p)
    return out


def fix_outline(lod, base):
    """Outline fix: put every LOD vertex back on the approved surface, then pull any extent that shrank by more
    than OUTLINE_SNAP_TOL back out to LOD0's (fingertips, shoulder tops, the back of the head and so on)."""
    bvh = BVHTree.FromBMesh(base["bm"])
    for v in lod["bm"].verts:
        loc = bvh.find_nearest(v.co)[0]
        if loc is not None:
            v.co = loc
    fixes = []
    bco = [v.co for v in base["bm"].verts]
    for i in range(3):
        for sgn in (1, -1):
            e0 = max(sgn * c[i] for c in bco)
            el = max(sgn * v.co[i] for v in lod["bm"].verts)
            if OUTLINE_SNAP_TOL < e0 - el <= OUTLINE_MAX:
                for v in lod["bm"].verts:
                    if el - sgn * v.co[i] < 0.003:
                        v.co[i] += sgn * (e0 - el)
                fixes.append("%s%s %.1f mm" % ("+" if sgn > 0 else "-", "xyz"[i], 1000 * (e0 - el)))
            elif e0 - el > OUTLINE_MAX:
                fixes.append("NOT FIXED %s%s %.1f mm" % ("+" if sgn > 0 else "-", "xyz"[i], 1000 * (e0 - el)))
    lod["bm"].normal_update()
    return fixes


# BUILD

def build(scene, args):
    low = T.col("LOW")
    blk = T.col("BLOCKOUT")
    for ob in list(low.objects):
        if ob.get("owner") == "phase:02_blockout":
            if blk.objects.get(ob.name) is None:
                blk.objects.link(ob)
            low.objects.unlink(ob)
    blk.hide_render = True
    lc = bpy.context.view_layer.layer_collection.children.get("BLOCKOUT")
    if lc:
        lc.hide_viewport = True
    cols = {k: subcol("LOW_" + k, low) for k in ("LOD0", "LOD1", "LOD2", "PROPS")}
    tmp = "TOPO_TMP"

    # BODY: build, clean, mirror, remove interior faces against the closed parts, mirror the result
    C, L = body_parts()
    C = [clean(p) for p in C]
    L = [clean(p) for p in L]
    R_pre = [mirrored(p) for p in L]
    H = helmet_parts()
    vol = {p["name"]: Vol(p) for p in C + L + R_pre + H}
    body_names = [p["name"] for p in C + L + R_pre]
    for p in C + L:
        occ = [(vol[n], MARGIN_BODY) for n in body_names if n != p["name"]]
        if p["name"] in HEAD_PARTS:
            occ += [(vol[h["name"]], MARGIN_RIGID) for h in H]
        remove_interior(p, occ)
    R = [mirrored(p) for p in L]
    tri_parts = {p["name"]: tri_copy(p) for p in C + L}
    src_parts = []
    for p in C + L + R:
        q = part(p["name"], p["bm"].copy(), p["roles"], p["side"])
        fan_ngons(q["bm"])
        src_parts.append(q)
    lock_tris(join("CH_Skier_Body_LOD0", src_parts, cols["LOD0"].name))

    # LOD1 and LOD2: lod_copy per part (left and centre), outline fix, right side mirrored, joined
    for level in (1, 2):
        lp = [lod_part(tri_parts[p["name"]], level, tmp) for p in C + L
              if LOD_RATIO[level][p["name"].replace("_L", "")] > 0]
        lp += [mirrored(p) for p in lp if p["side"] == "L"]
        ob = join("CH_Skier_Body_LOD%d" % level, lp, cols["LOD%d" % level].name)
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
        bm.to_mesh(ob.data)
        bm.free()
    tc = bpy.data.collections.get(tmp)
    if tc:
        for ob in list(tc.objects):
            me = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if me and me.users == 0:
                bpy.data.meshes.remove(me)
        bpy.data.collections.remove(tc)

    # PROPS: helmet with goggles; boot shell (two lofts) and hinged cuff, left built, right mirrored
    hvol = {p["name"]: Vol(p) for p in H}
    for p in H:
        remove_interior(p, [(hvol[n], MARGIN_RIGID) for n in hvol if n != p["name"]])
    for p in H:
        fan_ngons(p["bm"])
    lock_tris(join("CH_Skier_Helmet", H, cols["PROPS"].name))
    shell = split_islands(clean(from_form("BootShell_L", "L")))
    svol = {p["name"]: Vol(p) for p in shell}
    for p in shell:
        remove_interior(p, [(svol[n], MARGIN_RIGID) for n in svol if n != p["name"]])
        fan_ngons(p["bm"])
    cuff = boot_cuff_L()
    fan_ngons(cuff["bm"])
    for side, conv in (("L", lambda p: p), ("R", mirrored)):
        lock_tris(join("CH_Skier_BootShell_%s" % side, [conv(p) for p in shell], cols["PROPS"].name))
        lock_tris(join("CH_Skier_BootCuff_%s" % side, [conv(cuff)], cols["PROPS"].name))
    T.log("topology built")
    measure(args)


def measure(args):
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    per = {}
    for ob in T.col("LOW").all_objects:
        if ob.type != "MESH":
            continue
        eo = ob.evaluated_get(dg)
        me = eo.to_mesh()
        bm = bmesh.new()
        bm.from_mesh(me)
        eo.to_mesh_clear()
        pts = [v.co for v in bm.verts]
        per[ob.name] = {
            "collection": ob.users_collection[0].name,
            "tris": sum(len(f.verts) - 2 for f in bm.faces),
            "source_faces": len(ob.data.polygons),
            "source_quads": sum(1 for p in ob.data.polygons if len(p.vertices) == 4),
            "verts": len(bm.verts), "islands": len(islands(bm)),
            "open_edges": sum(1 for e in bm.edges if len(e.link_faces) == 1),
            "bbox": [round(min(p[i] for p in pts), 4) for i in range(3)] +
                    [round(max(p[i] for p in pts), 4) for i in range(3)],
            "materials": [m.name for m in ob.data.materials]}
        bm.free()
    props = sum(v["tris"] for v in per.values() if v["collection"] == "LOW_PROPS")
    by = {"body_LOD%d" % i: per["CH_Skier_Body_LOD%d" % i]["tris"] for i in range(3)}
    by["helmet_goggles"] = per["CH_Skier_Helmet"]["tris"]
    for s in "LR":
        by["boot_" + s] = per["CH_Skier_BootShell_" + s]["tris"] + per["CH_Skier_BootCuff_" + s]["tris"]
    by["props_total"] = props
    for i in range(3):
        by["character_LOD%d" % i] = by["body_LOD%d" % i] + props
    out = {"tris": by, "objects": per, **STATS}
    path = os.path.join(os.path.dirname(os.path.abspath(args.master)), "review", T.PHASE, "measure.json")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    json.dump(out, open(path, "w"), indent=1)
    T.log("measure", json.dumps(by))


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--master", required=True)
    a = ap.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    for me in [m for m in bpy.data.meshes if m.users == 0 and m.name.startswith(("CH_Skier_", "TMP_"))]:
        bpy.data.meshes.remove(me)
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
