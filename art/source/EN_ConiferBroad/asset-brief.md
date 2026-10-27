# EN_ConiferBroad asset brief (tree-kit Phase 0)

```text
ASSET      EN_ConiferBroad (environment, tree): a shape variant of EN_Conifer for story 016's varied forest. Built
           with .claude/skills/tree-kit from EN_ConiferBroad_params.json, which tools/blender/trees/make_variant_params.py
           writes from EN_Conifer's params (only the shape, the budgets and the variant differ).
KIND       conifer: a broad spruce. Wider and lower than the spruce, branches nearly to the ground, heavier drooping
           tiers, a short spike.
VARIANT    snowy_spruce: the spruce's v2 colours and roughness (built 2026-10-27, story 016; provisional until the
           user's look review).
REFERENCE  art/reference/style-notes-gma2.md, "Trees" (our notes on the user's Grand Mountain Adventure 2 screenshots;
           the screenshots are not in the repo). Silhouettes: ref/front_matte.png, side_matte.png, top_matte.png and
           profiles_sheet.png, drawn by script from the params (our own drawing). No third-party images or textures.
SCALE      11.0 m base height. In the game the forest scales each tree so its crown matches the layout's branch radius:
           about 7 m on the test mountain and Pine Bowl, 6 to 12 m on the test slope.
SHAPE      Measured from the layout (ref/refs.json), checked on the mesh by tree_gate.py within 2 percent:
             height 11.00 m; crown width 4.51 m; height to crown width 2.44 (the spruce: 3.25)
             trunk base diameter 0.40 m; height to trunk diameter 27.5
             lowest crown point 0.13 m = 1 percent of height (the drooping lowest tips nearly touch the snow)
             15 tiers, 9 branches per tier at the bottom down to 4 at the top, 98 branches; each plane drops 42 percent
             of its length at the tip (the spruce: 30)
             spike 12 percent of height
MATERIALS  Flat colour, no textures, no UVs, the spruce's three roles (linear RGB, PROVISIONAL until art sign-off):
             M_Conifer_Bark (0.09, 0.07, 0.06), M_Conifer_Needle (0.010, 0.030, 0.018), M_Conifer_Snow (0.80, 0.87, 0.96)
LODS       LOD0 1,193 tris (budget 300 to 1,500). LOD1 372 tris (budget 100 to 400): all 15 tiers, at most 4 branches
           per tier, tent sections 55 percent wide. LOD2 billboard: two crossed quads.
GATES      PASS. IoU against the profiles: front 0.995, side 0.995, top 0.998 (minimum 0.90); LOD1 front 0.723, top
           0.811 (minimum 0.70). Proportions within 2 percent, base on the ground, trunk on the axis, identity transforms.
BILLBOARD  Two 352 x 832 px cells of 4.89 x 11.56 m (72 texels a metre), a 704 x 832 atlas; IoU 0.996 front, 0.995 side
           against LOD0 (minimum 0.90), nothing reaching a cell edge.
INFERRED   All colours and the shape values: design choices, provisional until the look review.
TARGET     Unity 6.3 LTS URP, FBX, Assets/Art/Environment/EN_ConiferBroad/. The forest's mix gives it 20 percent of the
           trees (TestSlopeLook.TreeVariants). Not verified in the editor.
```

## Rebuild

From the repo root, with `$BLENDER_BIN` set:

```bash
python3 tools/blender/trees/make_variant_params.py            # rewrites the variants' params from EN_Conifer's
bash art/source/EN_ConiferBroad/build/phases.sh all          # phases 1 to 5, the billboard, review/final, the export
```

Change the tree by changing its entry in `make_variant_params.py` (or the params) and rerunning. Never edit the mesh or
the FBX by hand.

## Status

- 2026-10-27: exported (tree-kit Phase 6) to `Assets/Art/Environment/EN_ConiferBroad/`: `EN_ConiferBroad.fbx` (Git LFS)
  with `EN_ConiferBroad_LOD0` and `EN_ConiferBroad_LOD1`, `asset-manifest.json`, `animation-contract.json` and the billboard atlas
  and layout. Export check PASS (30 of 30), round trip height 11.0 m. The evidence is in `review/final/`. Not verified in the Unity
  editor.
