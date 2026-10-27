"""PR_Ski forms: exact sidecut, rocker, tail kick and thickness taper, plus the binding pieces.
Replaces the blockout. Built in HIGH; 04_topology makes the delivery mesh in LOW.
Story 012: the bindings are shaped (toe piece with a sloped nose and jaw wings, heel piece with a step-in lever and
brake arms), built from ref/refs.json "bindings", the same data the mattes are drawn from. Material 0 is the dark
binding plastic, 1 the light accent (the toe's top cap and the heel lever).

  $BLENDER_BIN --background --python build/03_forms.py -- --master PR_Ski_master.blend
"""
import json, math, os, sys, argparse

SKILL_ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "../../../../.claude/skills/blender-image-to-3d/assets")
sys.path.insert(0, os.path.normpath(SKILL_ASSETS))
import build_template as T  # noqa: E402
import bmesh  # noqa: E402

T.PHASE = "03_forms"
T.OWNER_TAG = "phase:" + T.PHASE

# Measured from the dimension drawing (ref/refs.json, ref/top_matte.png, ref/side_matte.png)
L = 1.77                                  # length (top, side)
W_TIP, W_WAIST, W_TAIL = 0.133, 0.092, 0.118   # widths (top)
S_TIP_WIDE, S_BOOT, S_TAIL_WIDE = 0.15, 0.965, 1.69   # positions from the tip end (top)
ROCKER_LEN, ROCKER_H = 0.30, 0.060        # tip rise (side)
KICK_LEN, KICK_H = 0.15, 0.030            # tail kick (side)
T_FOOT, T_END = 0.017, 0.007              # thickness underfoot and at the ends (side)
REFS = json.load(open(os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "../ref/refs.json"))))

# Dense through the rocker and kick, where the side silhouette curves; ends 1 mm in from the tips
STATIONS = [0.001, 0.02, 0.05, 0.09, 0.12, 0.15, 0.185, 0.22, 0.26, 0.30, 0.45, 0.60, 0.78, 0.965,
            1.15, 1.30, 1.45, 1.55, 1.62, 1.66, 1.69, 1.72, 1.745, 1.769]


def half_width(s):
    if s < S_TIP_WIDE:
        u = (S_TIP_WIDE - s) / S_TIP_WIDE
        return W_TIP / 2 * math.sqrt(max(0.0, 1 - u * u))
    if s > S_TAIL_WIDE:
        u = (s - S_TAIL_WIDE) / (L - S_TAIL_WIDE)
        return W_TAIL / 2 * math.sqrt(max(0.0, 1 - u * u))
    x0, x1, x2 = S_TIP_WIDE, S_BOOT, S_TAIL_WIDE
    y0, y1, y2 = W_TIP / 2, W_WAIST / 2, W_TAIL / 2
    return (y0 * (s - x1) * (s - x2) / ((x0 - x1) * (x0 - x2)) +
            y1 * (s - x0) * (s - x2) / ((x1 - x0) * (x1 - x2)) +
            y2 * (s - x0) * (s - x1) / ((x2 - x0) * (x2 - x1)))


def base_z(s):
    if s < ROCKER_LEN:
        return ROCKER_H * ((ROCKER_LEN - s) / ROCKER_LEN) ** 2
    if s > L - KICK_LEN:
        return KICK_H * ((s - (L - KICK_LEN)) / KICK_LEN) ** 2
    return 0.0


def thickness(s):
    a, b, c, d = 0.15, 0.60, 1.30, 1.65
    f = 0.0 if (s <= a or s >= d) else (s - a) / (b - a) if s < b else (d - s) / (d - c) if s > c else 1.0
    return T_END + (T_FOOT - T_END) * f


def y(s):
    return s - S_BOOT        # tip toward -Y, origin under the boot centre


def binding_piece(bm, parts, accent_top):
    """Every part of one binding piece into bm, in ski space (x across, y along with the tip at -y, z up)."""
    def face(vs, role):
        f = bm.faces.new(vs)
        f.material_index = 1 if role == "accent" else 0
        return f

    def cuboid(corners, role):          # 8 corners: bottom 4 then top 4, both counter-clockwise from above
        vs = [bm.verts.new(c) for c in corners]
        for q in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
            face([vs[i] for i in q], role)
    for part in parts:
        kind, role = part[0], part[1]
        if kind == "box":
            s0, s1, x0, x1, z0, z1 = part[2]
            for m in ((1, -1) if part[3] else (1,)):
                a, b = sorted((m * x0, m * x1))
                cuboid([(a, y(s0), z0), (b, y(s0), z0), (b, y(s1), z0), (a, y(s1), z0),
                        (a, y(s0), z1), (b, y(s0), z1), (b, y(s1), z1), (a, y(s1), z1)], role)
        elif kind == "bar":
            (s0, z0), (s1, z1) = part[2]
            hw, th = part[3], part[4]
            n = math.hypot(s1 - s0, z1 - z0)
            ns, nz = -(z1 - z0) / n * th / 2, (s1 - s0) / n * th / 2
            lo = [(s0 - ns, z0 - nz), (s1 - ns, z1 - nz)]
            hi = [(s0 + ns, z0 + nz), (s1 + ns, z1 + nz)]
            cuboid([(-hw, y(lo[0][0]), lo[0][1]), (hw, y(lo[0][0]), lo[0][1]), (hw, y(lo[1][0]), lo[1][1]),
                    (-hw, y(lo[1][0]), lo[1][1]), (-hw, y(hi[0][0]), hi[0][1]), (hw, y(hi[0][0]), hi[0][1]),
                    (hw, y(hi[1][0]), hi[1][1]), (-hw, y(hi[1][0]), hi[1][1])], role)
        else:                           # loft of chamfered sections along the ski
            rings = []
            for st, hw, zb, zt, ch in part[2]:
                rings.append([bm.verts.new(p) for p in ((-hw, y(st), zb), (hw, y(st), zb), (hw, y(st), zt - ch),
                                                        (hw - ch, y(st), zt), (-hw + ch, y(st), zt),
                                                        (-hw, y(st), zt - ch))])
            for i, (a, b) in enumerate(zip(rings, rings[1:])):
                s_mid = (part[2][i][0] + part[2][i + 1][0]) / 2
                for j in range(6):
                    top = j == 3 and accent_top and accent_top[0] <= s_mid <= accent_top[1]
                    face((a[j], a[(j + 1) % 6], b[(j + 1) % 6], b[j]), "accent" if top else role)
            face(rings[0][::-1], role)
            face(rings[-1], role)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])


def build(scene, args):
    T.clear_owned("phase:02_blockout")    # forms replace the blockout
    sections = []
    for s in STATIONS:
        hw, zb = half_width(s), base_z(s)
        zt = zb + thickness(s)
        sections.append([(-hw, y(s), zb), (hw, y(s), zb), (hw, y(s), zt), (-hw, y(s), zt)])
    T.loft("PR_Ski_Body", sections, collection="HIGH", smooth=False)
    for name, key in (("PR_Ski_Toe", "toe"), ("PR_Ski_Heel", "heel")):
        bm = bmesh.new()
        binding_piece(bm, REFS["bindings"][key], REFS["accent_top"].get(key))
        ob = T.new_mesh_object(name, bm, "HIGH")
        ob.data.materials.append(T.material("M_Ski_Binding", (0.12, 0.12, 0.13, 1), roughness=0.5))
        ob.data.materials.append(T.material("M_Ski_BindingAccent", (0.4233, 0.4678, 0.5395, 1), roughness=0.45))
    T.log("forms built,", len(STATIONS), "stations")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("--master", required=True)
    a = p.parse_args(T.argv_after_dashes())
    scene = T.open_master(a.master)
    T.clear_owned()
    build(scene, a)
    T.save_master(a.master)
    T.log("saved", a.master)
