# EN_ConiferYoung asset brief (tree-kit Phase 0)

```text
ASSET      EN_ConiferYoung (environment, tree): a shape variant of EN_Conifer for story 016's varied forest. Built
           with .claude/skills/tree-kit from EN_ConiferYoung_params.json, which tools/blender/trees/make_variant_params.py
           writes from EN_Conifer's params (only the shape, the budgets and the variant differ).
KIND       conifer: a young spruce. Small, branches to the ground, few tiers; for glade edges and the tree line.
VARIANT    snowy_spruce: the spruce's v2 colours and roughness (built 2026-10-27, story 016; provisional until the
           user's look review).
REFERENCE  art/reference/style-notes-gma2.md, "Trees" (our notes on the user's Grand Mountain Adventure 2 screenshots;
           the screenshots are not in the repo). Silhouettes: ref/front_matte.png, side_matte.png, top_matte.png and
           profiles_sheet.png, drawn by script from the params (our own drawing). No third-party images or textures.
SCALE      4.5 m base height. In the game the forest scales each tree so its crown matches the layout's branch radius
           (at most twice its size): about 6 m on the test mountain and Pine Bowl, 5 to 9 m on the test slope, so it
           stands lower than the spruce beside it.
SHAPE      Measured from the layout (ref/refs.json), checked on the mesh by tree_gate.py within 2 percent:
             height 4.50 m; crown width 2.10 m; height to crown width 2.14
             trunk base diameter 0.14 m; height to trunk diameter 32.1
             lowest crown point 0.13 m = 3 percent of height (the crown starts 8 percent up; at 5 percent the lowest
             tips dipped below the ground)
             9 tiers, 6 branches per tier at the bottom down to 3 at the top, 41 branches
             spike 18 percent of height
MATERIALS  Flat colour, no textures, no UVs, the spruce's three roles (linear RGB, PROVISIONAL until art sign-off):
             M_Conifer_Bark (0.09, 0.07, 0.06), M_Conifer_Needle (0.010, 0.030, 0.018), M_Conifer_Snow (0.80, 0.87, 0.96)
LODS       Budgets below the spruce's, for a tree a third its height: LOD0 506 tris (budget 150 to 900). LOD1 216 tris
           (budget 50 to 300): all 9 tiers, at most 4 branches per tier, tent sections 55 percent wide. LOD2
           billboard: two crossed quads.
GATES      PASS. IoU against the profiles: front 0.987, side 0.985, top 0.997 (minimum 0.90); LOD1 front 0.757, top
           0.878 (minimum 0.70). Proportions within 2 percent, base on the ground, trunk on the axis, identity transforms.
INFERRED   All colours, the shape values and the smaller budgets: design choices, provisional until the look review.
TARGET     Unity 6.3 LTS URP, FBX, Assets/Art/Environment/EN_ConiferYoung/. The forest's mix gives it 7 percent of the
           trees (TestSlopeLook.TreeVariants). Not verified in the editor.
```

## Rebuild

From the repo root, with `$BLENDER_BIN` set:

```bash
python3 tools/blender/trees/make_variant_params.py            # rewrites the variants' params from EN_Conifer's
bash art/source/EN_ConiferYoung/build/phases.sh all          # phases 1 to 5, the billboard, review/final, the export
```

Change the tree by changing its entry in `make_variant_params.py` (or the params) and rerunning. Never edit the mesh or
the FBX by hand.
