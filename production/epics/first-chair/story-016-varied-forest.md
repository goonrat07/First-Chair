# Story 016: A varied forest (procedural tree shapes from the tree kit)

> **Epic**: First Chair MVP (base skiing system)
> **Status**: Ready
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
