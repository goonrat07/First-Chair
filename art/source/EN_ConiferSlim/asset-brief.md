# EN_ConiferSlim asset brief (tree-kit Phase 0)

```text
ASSET      EN_ConiferSlim (environment, tree): a shape variant of EN_Conifer for story 016's varied forest. Built
           with .claude/skills/tree-kit from EN_ConiferSlim_params.json, which tools/blender/trees/make_variant_params.py
           writes from EN_Conifer's params (only the shape, the budgets and the variant differ).
KIND       conifer: a slim fir. Taller for its width than the spruce, a longer clear trunk, more and flatter tiers,
           a longer spike.
VARIANT    snowy_fir: the spruce's snowy_spruce v2 colours and roughness (built 2026-10-27, story 016; provisional
           until the user's look review).
REFERENCE  art/reference/style-notes-gma2.md, "Trees" (our notes on the user's Grand Mountain Adventure 2 screenshots;
           the screenshots are not in the repo). Silhouettes: ref/front_matte.png, side_matte.png, top_matte.png and
           profiles_sheet.png, drawn by script from the params (our own drawing). No third-party images or textures.
SCALE      14.0 m base height. In the game the forest scales each tree so its crown matches the layout's branch radius,
           but no taller than 16.5 m (TestSlopeLook.MaxTreeHeightMetres): about 16 m on the test mountain and Pine Bowl,
           16.5 m on most of the test slope.
SHAPE      Measured from the layout (ref/refs.json), checked on the mesh by tree_gate.py within 2 percent:
             height 14.00 m; crown width 2.49 m; height to crown width 5.63 (the spruce: 3.25)
             trunk base diameter 0.30 m; height to trunk diameter 46.7
             lowest crown point 2.99 m = 21 percent of height (the spruce: 13)
             18 tiers, 7 branches per tier at the bottom down to 4 at the top, 99 branches; each plane drops 20 percent
             of its length at the tip (the spruce: 30)
             spike 16 percent of height
MATERIALS  Flat colour, no textures, no UVs, the spruce's three roles (linear RGB, PROVISIONAL until art sign-off):
             M_Conifer_Bark (0.09, 0.07, 0.06), M_Conifer_Needle (0.010, 0.030, 0.018), M_Conifer_Snow (0.80, 0.87, 0.96)
LODS       LOD0 1,205 tris (budget 300 to 1,500). LOD1 336 tris (budget 100 to 400): all 18 tiers, at most 3 branches
           per tier (4 came to 444 tris, over budget), tent sections 55 percent wide. LOD2 billboard: two crossed quads.
GATES      PASS. IoU against the profiles: front 0.990, side 0.990, top 0.996 (minimum 0.90); LOD1 front 0.749, top
           0.858 (minimum 0.70). Proportions within 2 percent, base on the ground, trunk on the axis, identity transforms.
BILLBOARD  Two 224 x 1056 px cells of 3.11 x 14.67 m (72 texels a metre), a 448 x 1056 atlas; IoU 0.987 front, 0.985 side
           against LOD0 (minimum 0.90), nothing reaching a cell edge.
INFERRED   All colours and the shape values: design choices, provisional until the look review.
TARGET     Unity 6.3 LTS URP, FBX, Assets/Art/Environment/EN_ConiferSlim/. The forest's mix gives it 25 percent of the
           trees (TestSlopeLook.TreeVariants). Not verified in the editor.
```

## Rebuild

From the repo root, with `$BLENDER_BIN` set:

```bash
python3 tools/blender/trees/make_variant_params.py            # rewrites the variants' params from EN_Conifer's
bash art/source/EN_ConiferSlim/build/phases.sh all           # phases 1 to 5, the billboard, review/final, the export
```

Change the tree by changing its entry in `make_variant_params.py` (or the params) and rerunning. Never edit the mesh or
the FBX by hand.

## Status

- 2026-10-27: exported (tree-kit Phase 6) to `Assets/Art/Environment/EN_ConiferSlim/`: `EN_ConiferSlim.fbx` (Git LFS)
  with `EN_ConiferSlim_LOD0` and `EN_ConiferSlim_LOD1`, `asset-manifest.json`, `animation-contract.json` and the billboard atlas
  and layout. Export check PASS (30 of 30), round trip height 14.0 m. The evidence is in `review/final/`. Not verified in the Unity
  editor.
