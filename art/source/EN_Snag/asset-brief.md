# EN_Snag asset brief (tree-kit Phase 0)

```text
ASSET      EN_Snag (environment, tree): a dead tree for story 016's varied forest. Built with .claude/skills/tree-kit
           from EN_Snag_params.json, which tools/blender/trees/make_variant_params.py writes from EN_Conifer's params
           (only the shape, the budgets and the variant differ).
KIND       conifer parameters shaped into a snag: a grey trunk with a few short, broken branch stubs and a little snow.
           Story 016 planned a "snag" kind in tree_params.py; the kit's skill files can't be edited in sleep mode, so
           the conifer generator makes it from short, stiff, irregular branches instead. A kind of its own (a broken
           top, bare twigs) is for when the skill is next edited.
VARIANT    weathered (built 2026-10-27, story 016; provisional until the user's look review).
REFERENCE  art/reference/style-notes-gma2.md, "Trees" (our notes on the user's Grand Mountain Adventure 2 screenshots;
           the screenshots are not in the repo). Silhouettes: ref/front_matte.png, side_matte.png, top_matte.png and
           profiles_sheet.png, drawn by script from the params (our own drawing). No third-party images or textures.
SCALE      9.0 m base height. In the game the forest scales it as it scales the spruce (by the spruce's 1.848 m crown
           radius, TestSlopeLook.TreeVariants), not by its own narrow crown, which would blow it up to the layout's
           branch radius: about 7 m on the test mountain and Pine Bowl, 6 to 12 m on the test slope.
SHAPE      Measured from the layout (ref/refs.json), checked on the mesh by tree_gate.py within 2 percent:
             height 9.00 m; crown width 1.89 m; height to crown width 4.77
             trunk base diameter 0.40 m; height to trunk diameter 22.5
             lowest stub 3.04 m = 34 percent of height
             7 tiers of 3 stubs at the bottom down to 2 at the top, 18 stubs, lengths varying by up to half,
             up to 35 degrees off an even spacing around the trunk; half carry snow (the spruce: 98 percent)
             bare top 20 percent of height
MATERIALS  Flat colour, no textures, no UVs, the spruce's three roles in weathered colours (linear RGB, PROVISIONAL until
           art sign-off):
             M_Conifer_Bark   (0.20, 0.18, 0.16)   roughness 0.95   trunk
             M_Conifer_Needle (0.17, 0.15, 0.13)   roughness 0.95   the stubs (dead wood, no needles)
             M_Conifer_Snow   (0.80, 0.87, 0.96)   roughness 0.7    snow on the stubs
LODS       Budgets below the spruce's, for a tree of 18 stubs: LOD0 232 tris (budget 80 to 600). LOD1 120 tris (budget
           30 to 250). LOD2 billboard: two crossed quads.
GATES      PASS. IoU against the profiles: front 0.990, side 0.992, top 0.994 (minimum 0.90); LOD1 front 0.970, top
           0.910 (minimum 0.70). Proportions within 2 percent, base on the ground, trunk on the axis, identity transforms.
INFERRED   All colours, the shape values and the smaller budgets: design choices, provisional until the look review.
TARGET     Unity 6.3 LTS URP, FBX, Assets/Art/Environment/EN_Snag/. The forest's mix gives it 3 percent of the trees
           (TestSlopeLook.TreeVariants). Not verified in the editor.
```

## Rebuild

From the repo root, with `$BLENDER_BIN` set:

```bash
python3 tools/blender/trees/make_variant_params.py            # rewrites the variants' params from EN_Conifer's
bash art/source/EN_Snag/build/phases.sh all                  # phases 1 to 5, the billboard, review/final, the export
```

Change the tree by changing its entry in `make_variant_params.py` (or the params) and rerunning. Never edit the mesh or
the FBX by hand.
