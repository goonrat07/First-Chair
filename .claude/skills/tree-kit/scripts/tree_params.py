"""
TREE PARAMS (tree-kit)
Shared layout for First Chair trees: a parameters JSON in, a deterministic list of parts out.
build_tree.py (bpy) turns the parts into meshes, make_profiles.py (Pillow) projects the same parts
into the Phase 1 silhouette mattes, and tree_gate.py measures the built mesh against them.
Pure Python (no bpy, no Pillow), so it runs inside and outside Blender.

Units and axes: metres, Z up, trunk on the Z axis, origin at the trunk base on the ground.
A part is a loft: rings of (x, y, z) points joined ring to ring, optionally closed by an apex,
with one material role per ring edge ("bark", "needle", "snow").
Kinds: "conifer" (tiered, drooping branch planes and a spike crown). Add a kind by writing a
builder that returns parts and registering it in KINDS; the other scripts do not change.
"""
import copy
import json
import math
import random

GOLDEN = math.pi * (3.0 - math.sqrt(5.0))   # 137.5 degrees: successive tiers never line up


def load(path):
    with open(path) as f:
        return json.load(f)


def _set(d, dotted, value):
    keys = dotted.split(".")
    for k in keys[:-1]:
        d = d.setdefault(k, {})
    d[keys[-1]] = value


def variant_of(params, variant=None):
    name = variant or params["variant"]
    return name, params["variants"][name]


def shape_for(params, level=0, variant=None):
    """Base shape, then the variant's shape_overrides, then the LOD's overrides (dotted keys)."""
    shape = copy.deepcopy(params["shape"])
    _, var = variant_of(params, variant)
    for k, v in var.get("shape_overrides", {}).items():
        _set(shape, k, v)
    for k, v in params["lods"][level].get("overrides", {}).items():
        _set(shape, k, v)
    return shape


def ngon(r, z, sides, phase=0.0):
    return [(r * math.cos(phase + 2 * math.pi * k / sides), r * math.sin(phase + 2 * math.pi * k / sides), z)
            for k in range(sides)]


def trunk_radius(shape, z):
    t = shape["trunk"]
    f = min(max(z / (t["top_frac"] * shape["height_m"]), 0.0), 1.0)
    return t["base_radius_m"] + (t["top_radius_m"] - t["base_radius_m"]) * f


def crown_radius(shape, z):
    """Crown envelope radius at height z: a spike that tapers to the top, slightly pulled in at the base."""
    H, c = shape["height_m"], shape["crown"]
    zb = c["base_frac"] * H
    u = min(max((z - zb) / (H - zb), 0.0), 1.0)
    swell = 1.0
    if c.get("base_swell", 0) > 0:
        swell = c["base_radius_frac"] + (1 - c["base_radius_frac"]) * min(1.0, u / c["base_swell"])
    return c["radius_max_m"] * (1 - u) ** c["taper_power"] * swell


def tier_heights(shape):
    """Tier root heights; spacing_power > 1 spaces the lower tiers wider than the upper ones."""
    H, c, ti = shape["height_m"], shape["crown"], shape["tiers"]
    zb, zt, n = c["base_frac"] * H, c["top_tier_frac"] * H, ti["count"]
    return [zb + (zt - zb) * (1 - (1 - i / (n - 1)) ** ti["spacing_power"]) for i in range(n)]


def branch_part(shape, name, z0, az, length, roll, top_role):
    """One branch plane: a thin trapezoid section (green edges below, narrower snow top above) lofted
    from inside the trunk to a mid station, then closed to a drooping tip. 12 triangles."""
    b = shape["branch"]
    tr = trunk_radius(shape, z0)
    r0 = 0.3 * tr                                   # root sits inside the trunk
    droop = b["droop_frac"] * length
    w_root = min(b["root_width_m"], 1.2 * tr)
    w_mid = min(b["max_mid_width_m"], b["mid_width_frac"] * length)
    th, sf = b["thickness_m"], b["snow_width_frac"]
    dx, dy = math.cos(az), math.sin(az)
    lx, ly = -dy, dx
    cr, sr = math.cos(roll), math.sin(roll)

    def centre(s):
        return r0 + s * (length - r0), z0 + b["root_rise_m"] - droop * s ** b["droop_power"]

    rings = []
    for s, w in ((0.0, w_root), (b["mid_frac"], w_mid)):
        r, zc = centre(s)
        hw = w / 2
        ring = []
        for u, v in ((-hw, -0.35 * th), (-hw * sf, 0.65 * th), (hw * sf, 0.65 * th), (hw, -0.35 * th)):
            u2, v2 = u * cr - v * sr, u * sr + v * cr
            ring.append((dx * r + lx * u2, dy * r + ly * u2, zc + v2))
        rings.append(ring)
    r, zc = centre(1.0)
    return {"name": name, "kind": "branch", "rings": rings, "apex": (dx * r, dy * r, zc),
            "roles": ["needle", top_role, "needle", "needle"]}


def keep_index(k, keep):
    """Which of k evenly spread branches survive when a LOD keeps at most `keep` of them."""
    if not keep or keep >= k:
        return set(range(k))
    return {int(math.floor(i * k / keep + 0.5)) % k for i in range(keep)}


def conifer(params, level=0, variant=None):
    # Placement (tier heights, azimuths, lengths, snow) always comes from LOD0, so a lower LOD is a
    # subset of LOD0's branches and nothing jumps at the LOD switch. The LOD's own shape only changes
    # geometry (widths, trunk and spike sides). Its "select" picks the subset.
    place = shape_for(params, 0, variant)
    shape = shape_for(params, level, variant)
    sel = params["lods"][level].get("select", {})
    stride, keep = sel.get("tier_stride", 1), sel.get("max_branches_per_tier")
    _, var = variant_of(params, variant)
    cover = var.get("snow_cover", 0.0)
    rng = random.Random(params["seed"])
    H, ti, t = place["height_m"], place["tiers"], shape["trunk"]
    parts = [{"name": "trunk", "kind": "trunk", "apex": None, "roles": ["bark"] * t["sides"],
              "rings": [ngon(trunk_radius(shape, z), z, t["sides"]) for z in (0.0, t["top_frac"] * H)]}]
    zs = tier_heights(place)
    n = len(zs)
    gap = (zs[-1] - zs[0]) / (n - 1)
    twist = rng.uniform(0, 2 * math.pi)
    for i, z0 in enumerate(zs):
        f = i / (n - 1)
        k = max(2, int(math.floor(ti["branches"] + (ti["branches_top"] - ti["branches"]) * f + 0.5)))
        zj = z0 + rng.uniform(-1, 1) * ti["z_jitter_frac"] * gap
        base = twist + i * GOLDEN
        tier_kept = i % stride == 0 or i == n - 1
        kept = keep_index(k, keep)
        for j in range(k):
            az = base + 2 * math.pi * j / k + math.radians(rng.uniform(-1, 1) * ti["az_jitter_deg"])
            length = crown_radius(place, zj) * (1 + rng.uniform(-1, 1) * ti["length_jitter"])
            roll = math.radians(rng.uniform(-1, 1) * place["branch"]["roll_jitter_deg"])
            snowy = rng.random() < cover                # always drawn, so cover never shifts the layout
            if tier_kept and j in kept:
                parts.append(branch_part(shape, "tier%02d_b%d" % (i, j), zj, az, length, roll,
                                         "snow" if snowy else "needle"))
    sp = shape["spike"]
    parts.append({"name": "spike", "kind": "spike", "rings": [ngon(sp["base_radius_m"], zs[-1], sp["sides"])],
                  "apex": (0.0, 0.0, H), "roles": ["needle"] * sp["sides"]})
    return parts


KINDS = {"conifer": conifer}


def layout(params, level=0, variant=None):
    return KINDS[params["kind"]](params, level, variant)


def part_points(part):
    pts = [p for ring in part["rings"] for p in ring]
    return pts + ([part["apex"]] if part["apex"] else [])


def tri_count(parts):
    n = 0
    for p in parts:
        m = len(p["rings"][0])
        n += (len(p["rings"]) - 1) * 2 * m + (m if p["apex"] else 0)
    return n


def ratios(m):
    return {"height_to_crown_width": m["height_m"] / m["crown_width_m"],
            "clear_trunk_frac": m["lowest_crown_z_m"] / m["height_m"],
            "height_to_trunk_diameter": m["height_m"] / m["trunk_base_diameter_m"]}


def measures(parts):
    """The proportions tree_gate.py also measures on the mesh (same definitions), plus design-only values."""
    crown = [q for p in parts if p["kind"] != "trunk" for q in part_points(p)]
    allp = [q for p in parts for q in part_points(p)]
    base = next(p for p in parts if p["kind"] == "trunk")["rings"][0]
    m = {"height_m": max(q[2] for q in allp),
         "min_z_m": min(q[2] for q in allp),
         "lowest_crown_z_m": min(q[2] for q in crown),
         "crown_width_m": 2 * max(math.hypot(q[0], q[1]) for q in crown),
         "trunk_base_diameter_m": 2 * max(math.hypot(q[0], q[1]) for q in base)}
    m["ratios"] = ratios(m)
    spike = [p for p in parts if p["kind"] == "spike"]
    if spike:
        m["spike_frac"] = (m["height_m"] - spike[0]["rings"][0][0][2]) / m["height_m"]
    m["tris"] = tri_count(parts)
    m["branches"] = sum(1 for p in parts if p["kind"] == "branch")
    return m
