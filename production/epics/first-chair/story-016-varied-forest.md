# Story 016: A varied forest (procedural tree shapes from the tree kit)

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Presentation
> **Type**: Visual/Feel (tree assets, the forest in a build) + Logic (the variant choice)
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/game-brief.md`, "After a rough gameplay demo": "Procedural generation of trees and props where it
makes sense (`.claude/skills/tree-kit/` already builds trees from parameters)".
**Requirement**: the user, 2026-10-27, starting sleep mode: keep working through the brief's list once the named work
(procedural animation, the snow shader, the mountain tool) is done.

Today every tree is one conifer, `EN_Conifer` (12 m, flat colours, LODs and a billboard), scaled to the branch radius
the layout gives it. From the chase camera a forest's trees share one silhouette: the same tiers at the same heights,
the same spike, so a tree line reads as copies. The tree kit builds a tree from a parameters JSON, so new shapes are
new numbers, not new modelling.

Written by Claude in sleep mode; the design choices are logged under Decisions and are the user's to change.

## Design

1. **Shape variants from the tree kit.** `EN_Conifer`'s parameters gain shape variants beside its colour variants,
   each built, gated and exported by the kit's phases:
   - `spruce`: today's tree;
   - `slim_fir`: taller for its width, with tighter, more regular tiers and a longer spike;
   - `broad_spruce`: wider and lower, with heavier, drooping lower tiers;
   - `young`: a small tree of few tiers (for glade edges and the tree line);
   - `snag`: a dead tree, a bare trunk with a few broken branch stubs (a new kind in `tree_params.py`'s `KINDS`).
   Every variant keeps the kit's budgets (LOD0 300 to 1,500 tris and its LODs), its origin at the trunk base and its
   flat colour roles, and gets a billboard for the far LOD.
2. **The view picks a variant per tree.** The layout's tree records stay as they are: position, trunk and branch
   radii (so the sim's trees, near misses and crashes don't change). The view picks each tree's variant from a hash
   of its position, weighted by a mix (`spruce` 45, `slim_fir` 25, `broad_spruce` 20, `young` 7, `snag` 3, per
   cent), and scales it to the layout's branch radius as now, so the same mountain always grows the same forest.
3. **Unity:** each variant is its own model with GPU instancing on its materials; the mix is a setting on the look
   (the Feel Lab doesn't need it).

## As built (2026-10-27)

- **Four tree assets, not variants inside EN_Conifer** (55ad865): `EN_ConiferSlim` (a slim fir, 14 m), `EN_ConiferBroad`
  (a broad spruce, 11 m, branches nearly to the ground), `EN_ConiferYoung` (4.5 m) and `EN_Snag` (9 m, a grey trunk
  with 18 broken stubs, half with snow), each in its own `art/source/<ASSET>/` with its params, brief, master and
  `review/final/`. The tree kit builds one shape per params file, so a shape variant is a new asset; their params are
  written from EN_Conifer's by `tools/blender/trees/make_variant_params.py`, changing only the shape, the budgets and the
  colour variant. Every LOD0 passes front, side and top IoU at 0.985 to 0.998.
- **The snag is made from conifer parameters**, not a new `snag` kind in `tree_params.py`: the kit's skill files can't
  be edited in sleep mode. Short, stiff, irregular branches in dead-wood grey read as stubs; a kind of its own (a
  broken top, bare twigs) is for when the skill is next edited.
- **Budgets:** the slim fir and the broad spruce keep the spruce's (LOD0 300 to 1,500, LOD1 100 to 400); the young tree
  (LOD0 150 to 900, LOD1 50 to 300) and the snag (80 to 600, 30 to 250) get smaller ones, for a tree a third the
  spruce's height and one of 18 stubs. The slim fir's LOD1 keeps 3 branches a tier (4 came to 444 tris, over 400).
- **Exports** (81380ac): one FBX per tree with both LODs, its manifest and its billboard atlas, in
  `Assets/Art/Environment/<ASSET>/`; export check 30 of 30 and the FBX round trip pass for each.
- **Billboards sized to each tree.** The billboard script's cell was fixed at EN_Conifer's 3.6 x 14.4 m, which cut the
  broad spruce's crown off on both sides (and touched EN_Conifer's own side view), unseen by the gate, which compares
  against a render cut alike. Each cell is now the tree's LOD0 plus a margin at 72 texels a metre, the gate fails any
  cell whose alpha reaches its sides or top, and EN_Conifer's billboard is rebuilt. All five pass, IoU 0.983 to 0.996.
- **The choice and the scale** (`TreeVariantPicker`, 11 EditMode tests): a hash of each layout tree's position picks
  from the mix (45, 25, 20, 7, 3), and the model's crown is fitted to the sim's branch radius as before, but no taller
  than 16.5 m (`TestSlopeLook.MaxTreeHeightMetres`): matched crowns alone would grow 28 m slim firs on the test slope,
  whose tallest spruce is 16.2 m. Above the limit the crown falls short of the sim's branch circle; the slim fir's
  lowest tiers are 3 m up, over a passing rider. The snag is scaled as the spruce is, not blown up to the branch
  radius by its narrow stubs.
- **Unity:** `TestSlopeLook` places a model per tree from the mix with its own billboard LOD; a variant whose files are
  missing drops out with a warning (seen in CI run 40579929851, before the exports), and with none the cone stands in.
  The mix is a table in `TestSlopeLook`. Not verified in the editor.

## Acceptance Criteria

- [ ] Each new variant passes the tree kit's gates: front, side and top IoU at or above 0.90 against its profile,
  triangles per LOD in budget, origin and proportions within tolerance; the evidence in
  `art/source/EN_Conifer/review/final/`.
- [ ] Each variant exports with its LODs and a billboard, and its FBX round trip keeps height, tris and materials.
- [ ] The variant choice is pure C# with EditMode tests: deterministic for a position, every variant reachable, the
  mix's shares within a few percent over a large forest.
- [ ] The Unity EditMode tests pass in CI and the Windows and Linux players build.
- [ ] A cloud ride of the build, from the chase camera, retained in `production/qa/evidence/story-016-varied-forest/`:
  a tree line along the groomer before and after.
- [ ] No change to the sim or the layout format: `dotnet test sim/` passes and the mountains rebuild byte for byte.

## Out of Scope

- Rocks, stumps and logs that riders can hit: they need the sim to know them (a later story).
- Broadleaf trees, bushes and grass.
- Wind animation on the trees.

## Test Evidence

**Story Type**: Visual/Feel + Logic. The kit's gate outputs in `art/source/EN_Conifer/review/final/`; EditMode tests
for the variant choice; retained cloud-build screenshots in `production/qa/evidence/story-016-varied-forest/`; the
look sign-off is the user's, on the PC.

## Dependencies

- Depends on: the tree kit and `EN_Conifer` (story 007's art), Story 010 (the trees in the layout).
- Unlocks: per-mountain tree mixes in the mountain tool's format.

## Decisions (sleep mode, 2026-10-27; the user's to change)

- Variety in the view, not in the layout: the sim's trees stay as they are, and every mountain gets it at once.
- The shares of the mix are a first guess for a sub-alpine spruce forest; the shapes and colours stay provisional
  until the user's look review, as the kit requires.
