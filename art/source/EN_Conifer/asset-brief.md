# EN_Conifer asset brief (tree-kit Phase 0)

```text
ASSET      EN_Conifer (environment, tree). Built with .claude/skills/tree-kit from EN_Conifer_params.json.
KIND       conifer: slender trunk, tiers of wide drooping branch planes, narrow spike crown.
VARIANT    snowy_spruce v2 (built; approved for use in the test build; exported 2026-10-23). Planned: olive_bare, rust_larch (placeholders in the params, status "planned").
REFERENCE  art/reference/style-notes-gma2.md, "Overall look" and "Trees": our notes on the user's Grand Mountain
           Adventure 2 screenshots. The screenshots are copyrighted and not in the repo. Silhouette reference:
           ref/front_matte.png, side_matte.png, top_matte.png and profiles_sheet.png, drawn by script from the
           params (our own drawing, commit). No third-party images or textures are used.
SCALE      12.0 m base height. In the game, instances are scaled 0.667 to 1.167 to give 8 to 14 m trees (user
           decision, 2026-10-20). Trees taller than the frame are accepted: near trees are cut by the frame top.
HISTORY    v1 (10 tiers, 557 tris) read too sparse, like a fishbone. v2, from the user's review on 2026-10-20:
           16 tiers, 8 to 4 branches, wider, flatter overlapping planes, bigger snow caps, darker needle green.
           Still slim, with sky between the tiers.
SHAPE      Measured from the layout (ref/refs.json), checked on the mesh by tree_gate.py within 2 percent:
             height 12.00 m; crown width 3.70 m; height to crown width 3.25
             trunk base diameter 0.32 m; height to trunk diameter 37.5
             lowest crown point (lowest drooping tip) 1.56 m = 13 percent of height; tier roots start at 2.04 m
             16 tiers, 8 branches per tier at the bottom down to 4 at the top, 96 branches. Each plane is widest
             (46 percent of its length) at mid-length, drops 30 percent of its length at the tip, has a snow
             top 88 percent of its width, and rolls up to 6 degrees.
             spike crown from 10.44 m to 12.00 m = 13 percent of height
SILHOUETTE Trunk visible below the crown; layered snowy skirts with sky between them; narrow dark spike;
           tiers white on top and dark green underneath.
MATERIALS  Flat colour, no textures, no UVs, one material per role (linear RGB, PROVISIONAL until art sign-off):
             M_Conifer_Bark   (0.09, 0.07, 0.06)   roughness 0.9   trunk
             M_Conifer_Needle (0.010, 0.030, 0.018) roughness 0.85 tier rims and undersides, spike (darker in v2)
             M_Conifer_Snow   (0.80, 0.87, 0.96)   roughness 0.7   tier tops (snow caps); 98 percent of branches
LODS       LOD0 1,169 tris (budget 300 to 1,500). LOD1 396 tris (budget 100 to 400): LOD0's placement, all 16
           tiers, at most 4 branches per tier, each a 6-tri double pyramid (a point at the trunk, a tent section
           with a snow ridge, a point at the tip), 55 percent wide. LOD2 billboard: two crossed quads (below).
CAMERA     Game camera from art/source/CH_Skier/CH_Skier_master.blend REF_game_camera: az 30, el 45, 35 mm,
           18.0 m from the skier's mid-height (0.875 m). This is still the FIRST-CHAIR.md estimate. The game
           review render now uses this exact follow camera on a skier proxy beside the trunk.
INFERRED   Base height and the 8 to 14 m range (the user's decision, not measured from screenshots); tier count,
           droop and widths (from the notes and the review, not from measured images); all colours.
TARGET     Unity 6.3 LTS URP, FBX, Assets/Art/Environment/EN_Conifer/ (exported in Phase 6, 2026-10-23).
           Thousands instanced: GPU instancing on the materials and a LODGroup (not verified in the editor).
```

## Status

- 2026-10-23: snowy_spruce v2 is approved for use in the test build and exported (tree-kit Phase 6) to
  `Assets/Art/Environment/EN_Conifer/`: `EN_Conifer.fbx` (Git LFS) with the meshes `EN_Conifer_LOD0`
  (1,169 tris) and `EN_Conifer_LOD1` (396 tris) and the materials `M_Conifer_Bark`, `M_Conifer_Needle`
  and `M_Conifer_Snow` (base colours carried as the FBX DiffuseColor, linear), plus `asset-manifest.json`
  and `animation-contract.json` (no clips: a static asset). Shape and colours are unchanged from v2.
- One FBX holds both LODs (no `--split-lods`), so Unity gets one model and builds its LODGroup from the
  `_LOD0` and `_LOD1` suffixes.
- Export check PASS (30 of 30, `tools/blender/trees/export_check.py`). In the raw FBX: Y up, both meshes at the scene
  root with identity local transforms, 1,169 and 396 tris, height 12.00 m after the unit scale, trunk base
  at the origin, DiffuseColor equal to the params colours. Reimported into a blank Blender scene: the same
  tris, height 12.00 m and base colours. `roundtrip.py`: no errors, height 12.0 m. The evidence is in
  `review/final/` (`export_check.json`, `roundtrip.json`, `export_clay.png`, `export_colour.png`). The
  working copies and logs are in `review/export/`, which git ignores.
- **Not verified in the Unity editor:** the LODGroup, no rotation on import, 1 unit = 1 m, the imported
  Base Colours, and GPU instancing on the materials.
- LOD2 billboard: built 2026-10-24 (below); the crossed quads in Unity are not verified in the editor.
- The colours are approved for the test build only. They stay provisional until the final art sign-off.

## Rebuild

From the repo root, with `$BLENDER_BIN` set (cloud sessions: `cloud-setup.sh` sets it):

```bash
bash art/source/EN_Conifer/build/phases.sh all   # phases 1 to 5, review/final, then the Phase 6 export
bash art/source/EN_Conifer/build/phases.sh 6     # the export only, from the current master .blend
```

Phase 6 runs these steps in order:

1. `export_delivery.py --engine unity --format FBX --collections LOW --no-animation`
2. `tools/blender/trees/manifest_extras.py`, which adds the tree fields to the manifest: height, trunk base radius,
   crown width, the colours of each material, the variant, the source and the licence
3. `roundtrip.py --expect-height 12`
4. `tools/blender/trees/export_check.py`

It then copies the check evidence into `review/final/`. To change the tree, change the params and rerun.
Never edit the mesh or the FBX by hand. Each export rewrites the manifest's `generated` time and
absolute `source` path (export_delivery's convention, the same as PR_Ski).

## Billboard (LOD2)

Built 2026-10-24 (sleep mode) by `tools/blender/trees/billboard.py` (Blender: LOD0 alone, orthographic, Workbench flat light in its
material colours, Standard view transform, front and side at 4x) and `tools/blender/trees/billboard_atlas.py` (Pillow: a
premultiplied box downsample into a 512 x 1024 RGBA atlas, two 256 x 1024 cells of 3.6 x 14.4 m, colour bled into the
clear texels). `phases.sh billboard` runs both. The atlas and its layout are
`Assets/Art/Environment/EN_Conifer/EN_Conifer_Billboard.png` and `.json`. Texture memory: 2 MiB as RGBA8,
about 512 KiB at BC7 or BC3, plus mipmaps.

Gate: each cell's alpha mask against LOD0's silhouette from the same view, rendered at 4x and reduced by
majority: IoU 0.993 front and side (minimum 0.9). Crossed quads (4 tris) rather than a camera-facing quad, which
would need a custom vertex shader. Unity's scene builder makes the quads (`BillboardQuads`), an alpha-clip URP Lit
material with up-facing normals so it lights like the snow, and adds them as each tree's last LOD (switch heights
0.25, 0.08 and 0.0125 of the screen). Not verified in the editor. The scripts live in `tools/blender/trees/`
(shared by every tree asset since story 016) because tree-kit has no billboard script yet; they move into the skill
when it is next edited.
