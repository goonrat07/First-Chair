"""
TREE SHAPE VARIANTS (story 016)
Writes the params JSON of each of EN_Conifer's shape variants (a slim fir, a broad spruce, a young tree and a
weathered snag) from EN_Conifer's own params, changing only what makes each one its shape, so every variant keeps the
spruce's LOD rules, gates, cameras and look. Each becomes its own tree-kit asset in art/source/<ASSET>/, built by
tools/blender/trees/phases.sh. The values are design choices for a sub-alpine forest, provisional until the user's look
review, as the kit's colours are.

  python3 tools/blender/trees/make_variant_params.py            # writes the four params files
  python3 tools/blender/trees/make_variant_params.py --check    # exits 1 if a written file differs
"""
import argparse
import copy
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
BASE = os.path.join(ROOT, "art", "source", "EN_Conifer", "EN_Conifer_params.json")

SNOWY = {"status": "built", "snow_cover": 0.98,
         "colours_linear": {"bark": [0.09, 0.07, 0.06], "needle": [0.01, 0.03, 0.018], "snow": [0.8, 0.87, 0.96]},
         "roughness": {"bark": 0.9, "needle": 0.85, "snow": 0.7}}

VARIANTS = {
    "EN_ConiferSlim": {
        "notes": "A slim fir: taller for its width, a longer clear trunk, more and flatter tiers, a longer spike.",
        "seed": 17, "variant": "snowy_fir", "colours": SNOWY,
        "shape": {"height_m": 14.0,
                  "trunk": {"base_radius_m": 0.15, "top_radius_m": 0.025, "top_frac": 0.94},
                  "crown": {"base_frac": 0.22, "top_tier_frac": 0.84, "radius_max_m": 1.3, "taper_power": 0.85,
                            "base_radius_frac": 0.8, "base_swell": 0.08},
                  "tiers": {"count": 18, "spacing_power": 1.05, "branches": 7, "length_jitter": 0.1, "z_jitter_frac": 0.12},
                  "branch": {"droop_frac": 0.2, "droop_power": 1.6, "mid_width_frac": 0.42, "max_mid_width_m": 0.8,
                             "thickness_m": 0.1, "root_width_m": 0.16},
                  "spike": {"base_radius_m": 0.12}},
        # 18 tiers of 4 tent branches came to 444 tris, over LOD1's 400: thin the branches, keep every tier (the kit's rule).
        "lod_select": {1: {"max_branches_per_tier": 3}},
        "cluster_scale": [0.75, 1.15],
    },
    "EN_ConiferBroad": {
        "notes": "A broad spruce: wider and lower, branches nearly to the ground, heavier drooping tiers.",
        "seed": 23, "variant": "snowy_spruce", "colours": SNOWY,
        "shape": {"height_m": 11.0,
                  "trunk": {"base_radius_m": 0.2, "top_radius_m": 0.035, "top_frac": 0.92},
                  "crown": {"base_frac": 0.08, "top_tier_frac": 0.88, "radius_max_m": 2.4, "taper_power": 1.15,
                            "base_radius_frac": 0.9, "base_swell": 0.2},
                  "tiers": {"count": 15, "spacing_power": 1.15, "branches": 9, "length_jitter": 0.14},
                  "branch": {"droop_frac": 0.42, "droop_power": 2.0, "mid_width_frac": 0.5, "max_mid_width_m": 1.2,
                             "thickness_m": 0.14, "root_width_m": 0.2},
                  "spike": {"base_radius_m": 0.16}},
        "cluster_scale": [0.7, 1.1],
    },
    "EN_ConiferYoung": {
        "notes": "A young spruce: small, branches to the ground, few tiers; for glade edges and the tree line.",
        "seed": 29, "variant": "snowy_spruce", "colours": SNOWY,
        "shape": {"height_m": 4.5,
                  "trunk": {"base_radius_m": 0.07, "top_radius_m": 0.015, "top_frac": 0.9, "sides": 5},
                  "crown": {"base_frac": 0.08, "top_tier_frac": 0.82, "radius_max_m": 1.05, "base_radius_frac": 0.9,
                            "base_swell": 0.1},
                  "tiers": {"count": 9, "branches": 6, "branches_top": 3},
                  "branch": {"droop_frac": 0.25, "mid_width_frac": 0.48, "max_mid_width_m": 0.5, "thickness_m": 0.07,
                             "root_width_m": 0.1, "root_rise_m": 0.05},
                  "spike": {"base_radius_m": 0.07, "sides": 4}},
        "budgets": {0: [150, 900], 1: [50, 300]},
        "cluster_scale": [0.8, 1.2], "cluster_spacing_m": 2.0, "cluster_radius_m": 5.0,
    },
    "EN_Snag": {
        "notes": "A weathered snag: a dead tree, a grey trunk with a few short, broken branch stubs and a little snow.",
        "seed": 31, "variant": "weathered",
        "colours": {"status": "built", "snow_cover": 0.5,
                    "colours_linear": {"bark": [0.2, 0.18, 0.16], "needle": [0.17, 0.15, 0.13], "snow": [0.8, 0.87, 0.96]},
                    "roughness": {"bark": 0.95, "needle": 0.95, "snow": 0.7}},
        "shape": {"height_m": 9.0,
                  "trunk": {"base_radius_m": 0.2, "top_radius_m": 0.05, "top_frac": 0.97},
                  "crown": {"base_frac": 0.35, "top_tier_frac": 0.8, "radius_max_m": 0.75, "taper_power": 0.7,
                            "base_radius_frac": 0.7, "base_swell": 0.0},
                  "tiers": {"count": 7, "spacing_power": 1.0, "branches": 3, "branches_top": 2, "az_jitter_deg": 35,
                            "length_jitter": 0.5, "z_jitter_frac": 0.3},
                  "branch": {"droop_frac": 0.05, "droop_power": 1.2, "mid_width_frac": 0.3, "max_mid_width_m": 0.25,
                             "thickness_m": 0.08, "root_width_m": 0.08, "snow_width_frac": 0.6},
                  "spike": {"base_radius_m": 0.05, "sides": 4}},
        "budgets": {0: [80, 600], 1: [30, 250]},
        "cluster_scale": [0.8, 1.2],
    },
}


def merge(into, changes):
    for key, value in changes.items():
        if isinstance(value, dict):
            merge(into.setdefault(key, {}), value)
        else:
            into[key] = value


def params_for(asset, spec, base):
    p = copy.deepcopy(base)
    p["asset"] = asset
    p["seed"] = spec["seed"]
    p["variant"] = spec["variant"]
    p["notes"] = (spec["notes"] + " Made from EN_Conifer's params by tools/blender/trees/make_variant_params.py (story 016): "
                  "only the shape, budgets and variant differ; values are provisional until the user's look review.")
    merge(p["shape"], spec["shape"])
    p["variants"] = {spec["variant"]: copy.deepcopy(spec["colours"])}
    for level, budget in spec.get("budgets", {}).items():
        p["lods"][level]["budget_tris"] = budget
    for level, select in spec.get("lod_select", {}).items():
        p["lods"][level]["select"].update(select)
    p["lods"][2]["notes"] = "Flat far version: two crossed quads on an alpha atlas from tools/blender/trees/billboard.py and billboard_atlas.py."
    p["cluster"]["scale"] = spec["cluster_scale"]
    p["cluster"]["notes"] = ("the kit's cluster renders scale it %s of the %s m base; in the game the forest fits its crown to "
                             "the layout's branch radius, no taller than 16.5 m (TestSlopeLook)"
                             % (spec["cluster_scale"], spec["shape"]["height_m"]))
    p["cluster"]["min_spacing_m"] = spec.get("cluster_spacing_m", p["cluster"]["min_spacing_m"])
    p["cluster"]["radius_m"] = spec.get("cluster_radius_m", p["cluster"]["radius_m"])
    return p


def main(check):
    base = json.load(open(BASE))
    differ = False
    for asset, spec in VARIANTS.items():
        path = os.path.join(ROOT, "art", "source", asset, asset + "_params.json")
        text = json.dumps(params_for(asset, spec, base), indent=2) + "\n"
        if check:
            same = os.path.exists(path) and open(path).read() == text
            print(("SAME " if same else "DIFFERS ") + path)
            differ |= not same
            continue
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w") as f:
            f.write(text)
        print("WROTE " + os.path.relpath(path, ROOT))
    return 1 if differ else 0


if __name__ == "__main__":
    a = argparse.ArgumentParser(description=__doc__.strip().splitlines()[0])
    a.add_argument("--check", action="store_true", help="compare instead of writing")
    sys.exit(main(a.parse_args().check))
