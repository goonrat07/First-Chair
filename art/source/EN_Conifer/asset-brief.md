# EN_Conifer asset brief (tree-kit Phase 0)

```text
ASSET      EN_Conifer (environment, tree). Built with .claude/skills/tree-kit from EN_Conifer_params.json.
KIND       conifer: slender trunk, tiers of wide drooping branch planes, narrow spike crown.
VARIANT    snowy_spruce v2 (built). Planned: olive_bare, rust_larch (placeholders in the params, status "planned").
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
           with a snow ridge, a point at the tip), 55 percent wide. LOD2 billboard: planned, not built (below).
CAMERA     Game camera from art/source/CH_Skier/CH_Skier_master.blend REF_game_camera: az 30, el 45, 35 mm,
           18.0 m from the skier's mid-height (0.875 m). This is still the FIRST-CHAIR.md estimate. The game
           review render now uses this exact follow camera on a skier proxy beside the trunk.
INFERRED   Base height and the 8 to 14 m range (the user's decision, not measured from screenshots); tier count,
           droop and widths (from the notes and the review, not from measured images); all colours.
TARGET     Unity 6.3 LTS URP, FBX, Assets/Art/Environment/EN_Conifer/ (Phase 6, after the look review).
           Thousands instanced: GPU instancing on the materials and a LODGroup (not verified in the editor).
```

## Billboard (LOD2) still to build

Render LOD0 orthographic from 8 azimuths (or front and side for crossed quads) under flat light into an alpha
atlas of about 512 x 256, on a 2 to 4 tri quad with an alpha-clip URP material. A camera-facing quad needs a
custom vertex shader (Shader Graph) in Unity: not verified in the editor. The billboard is the one textured part
of the tree, so its atlas size and texture memory go in the manifest (512 x 256 RGBA8 is 512 KiB uncompressed,
about 128 KiB at BC7 or BC3). Gate: alpha-mask IoU against the LOD0 silhouette from the same view.
