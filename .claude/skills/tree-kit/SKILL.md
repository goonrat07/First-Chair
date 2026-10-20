---
name: tree-kit
description: Build First Chair's procedural trees in Blender from a parameters JSON, through gated phases with measured checks. Covers silhouette profiles drawn by script from the parameters, tri budgets per LOD, trunk and crown proportions, origin at the trunk base, flat colour variants, LODs and a billboard, and an FBX export with a manifest and a round-trip reimport check. Use it for any tree or glade asset (conifers now; other kinds plug into the same generator). Everything else modelled goes through blender-image-to-3d.
user-invocable: true
argument-hint: "[asset, e.g. EN_Conifer] [variant, e.g. snowy_spruce] [phase 0-6 or all]"
---

# Tree kit

Read first: `.claude/rules/blender-assets.md`, `.claude/skills/blender-image-to-3d/FIRST-CHAIR.md`
(budgets, Unity export rule, file locations, lessons) and the "Overall look" and "Trees" sections of
`art/reference/style-notes-gma2.md`.

Trees are generated, not modelled. One parameters JSON per asset goes through `build_tree.py` into the
master `.blend`, and every phase reruns from the JSON. Change numbers, never the mesh by hand. Each
phase ends at a gate: run the measurement, keep the numbers in `review/`, show the user, continue on
approval. A gate's verdict is PASS, FAIL or NOT ASSESSED (an input was missing, so a check did not
run). NOT ASSESSED never counts as a pass.

## Runtime

`$BLENDER_BIN` (5.2 LTS) headless; Pillow for `make_profiles.py`; Mesa EGL for Workbench renders
(installed by `cloud-setup.sh`). Every script takes `--help`.

| Script (`scripts/`) | Phase | Purpose |
| --- | --- | --- |
| `tree_params.py` | all | pure-Python layout: params in, parts (lofted rings with material roles) out; kinds register in `KINDS` |
| `make_profiles.py` | 1 | front, side and top silhouette mattes plus a profile sheet, drawn from the layout; `refs.json` registration |
| `build_tree.py` | 2, 4, 5 | parameters in, master `.blend` out: one mesh per LOD in `LOW/LOD<n>`, one flat material per role |
| `tree_gate.py` | 2, 3, 5 | tris per LOD, origin and transforms, proportions, top IoU; folds in world_gate's front and side IoU |
| `look_render.py` | 4 | Cycles colour renders on snow under a low sun: three-quarter, front, top, game camera, instanced cluster |

Reused from `.claude/skills/blender-image-to-3d/scripts/` (call them; do not copy them):
`world_gate.py` (front and side IoU), `validate.py` (topology, tri budget), `review_render.py` (clay
views; `look_render.py` imports its camera fitting), `export_delivery.py` and `roundtrip.py` (Phase 6).

## Files per asset

```text
art/source/<ASSET>/
  asset-brief.md              Phase 0 brief
  <ASSET>_params.json         the only source of the shape, LODs, variants, gates, cameras
  ref/                        *_matte.png, profiles_sheet.png, refs.json: drawn by script, ours, commit
  build/phases.sh             this asset's command line for every phase
  <ASSET>_master.blend        rebuilt by build_tree.py (Git LFS)
  review/NN_*/                per-phase evidence (git-ignored)
  review/final/               the evidence that is committed
```

Exports go to `Assets/Art/Environment/<ASSET>/` in Phase 6 only.

## Phase 0: brief and parameters

Write `asset-brief.md` with ASSET, KIND, VARIANT, REFERENCE (sources and licences; GMA2 screenshots are
described in our notes, never committed), SCALE (height and the evidence), SHAPE (the ratios the gates
check), MATERIALS (roles and colours, marked provisional until art direction approves them), LODS
(budgets from FIRST-CHAIR.md), CAMERA (the game camera used for review and where it came from), and
INFERRED. Then write `<ASSET>_params.json`. Its keys are `shape` (trunk, crown, tiers, branch, spike),
`lods` (budget_tris, select, overrides), `variants` (colours_linear, roughness, snow_cover,
shape_overrides, status), `gates`, `camera.game`, `look`, `cluster` and `profile`. Ask the user only
for what the notes and budgets do not settle: height, variant, budget tier. **Gate:** the user approves
the brief.

## Phase 1: silhouette reference

`make_profiles.py` draws front (az 0), side (az 90) and top mattes from the layout at `profile.m_per_px`.
It supersamples 4x, box-downsamples and thresholds at 50 percent, and writes the registration that
world_gate needs. **Gate:** the user approves `profiles_sheet.png` (shape, tier rhythm, spike, clear
trunk) and the ratios printed on it before anything is built.

## Phase 2: build

`build_tree.py --lods 0`. It builds one mesh with the origin at the trunk base, flat shaded, with
outward normals and no UVs. **Gate:** world_gate front and side, and tree_gate top IoU against the
profile at or above `gates.iou_min` (0.90, the pack's forms tolerance). The profile and the build come
from one layout, so this gate proves the build realises the parameters (scale, axes, droop, origin).
It does not judge the look; that happens in Phase 4.

## Phase 3: measured checks

Run `tree_gate.py` and `validate.py --collections LOW --budget-tris <LOD0 max>`. Pass when:

- triangles per LOD are inside the params budget and equal to the layout's own count;
- each LOD object is at the origin with rotation 0, scale 1 and no parent;
- the trunk base is on z = 0 and on the Z axis within `gates.origin_tol_m`;
- height, lowest crown point, crown width, trunk base diameter, height to crown width, clear trunk
  fraction and height to trunk diameter are each within `gates.proportion_tol_frac` of the layout;
- height is within the same tolerance of `shape.height_m`.

Watch a gate fail before trusting it (`.claude/rules/skill-authoring.md`). For example, run tree_gate
against a scratch copy of the params with another height: it must report FAIL.

## Phase 4: materials and colour variants

Three roles: bark, needle, and snow (the light top faces of each tier). There is one material per
role, flat colour, no textures, and each material carries a `role` property. A variant is a set of
colours plus `snow_cover` and optional `shape_overrides`. Snowless variants put the needle material on
the snow faces. Variants share one mesh layout per LOD where they can, so Unity can instance them with a
material swap. Colours are art direction's call; values in the JSON stay "provisional" until the user's
look review. Run `look_render.py`. **Gate:** the user's look review of the three-quarter, game camera
and cluster renders. Check that lit snow reads near-white, shadows read blue, sky shows between tiers,
the trunk is visible, and `render_info.json` reports the tree's on-screen size at the game camera.

## Phase 5: LODs and billboard

A lower LOD is a subset of LOD0: placement always comes from LOD0, and `select` keeps tiers
(`tier_stride`) and at most `max_branches_per_tier` evenly spread branches. Overrides only change
geometry, never placement, so nothing jumps at the switch: wider planes, fewer trunk sides, and
cheaper branches. `branch.section: "tent"` (a snow ridge, 9 tris) and `branch.stations: "mid_only"`
(a point at the trunk) together make a 6-tri branch. Keep every tier and thin the branches before you
drop tiers. **Gate:** budget and proportions as in
Phase 3, and front and top IoU against the LOD0 profile at or above `gates.lod_iou_min`.

The billboard (last LOD, about 2 to 4 tris) is a separate step and its script is not written yet. It
needs LOD0 rendered orthographic from N azimuths into a small alpha-cutout atlas, a quad that faces
the camera (or crossed quads), and its own material. That is the one textured part of a tree, so the
brief records the atlas size. The billboard gate is its alpha mask's IoU against the LOD0 silhouette
from the same view.

## Phase 6: export and round trip

Run `export_delivery.py --engine unity --format FBX --collections LOW --split-lods --name <ASSET>
--out Assets/Art/Environment/<ASSET>/`, then `roundtrip.py --file <fbx> --expect-height <H>` for each
file. **Gate:** each LOD arrives with the Phase 3 tri count and the role materials, height within
1 percent, origin at the trunk base, and no rotation. Unity setup (LODGroup, GPU instancing on the
materials) is reported **not verified in the editor** until someone checks it there.

## Lessons (EN_Conifer, first build)

- A LOD regenerated with fewer tiers put its tiers at new heights. Its front IoU against LOD0 was
  0.47, and it would pop visibly. Lower LODs now subset LOD0's placement.
- Dropping every second tier also fails: front IoU was 0.51 on the 16-tier v2. Keeping all tiers with
  6-tri branches (4 per tier, 396 tris) scored front 0.75 and top 0.86.
- v1 (10 tiers, 3 to 6 branches) read as a fishbone. v2 (16 tiers, 8 to 4 wide planes, 1,169 tris) is
  what the user asked for. Sparse still has to mean layered.
- At the estimated game camera (18 m, el 45, 35 mm), a 12 m tree projects taller than a 1080 px frame
  wherever it is aimed. The user accepts near trees cut by the frame top (8 to 14 m trees), so the
  game render uses the true follow camera on a skier proxy.
- Review lighting: a sun at 16 degrees needs about 10 W/m2 for lit snow to read near-white under the
  blue sky fill. At 6 it reads lavender-grey.
- Cycles look renders on 4 CPU cores take about 5.5 minutes for the six shots at 48 samples. Use
  `--shots` to rerender only what changed.
