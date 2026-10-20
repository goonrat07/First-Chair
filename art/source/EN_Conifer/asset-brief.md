# EN_Conifer asset brief (tree-kit Phase 0)

```text
ASSET      EN_Conifer (environment, tree). Built with .claude/skills/tree-kit from EN_Conifer_params.json.
KIND       conifer: slender trunk, tiers of narrow drooping branch planes, narrow spike crown.
VARIANT    snowy_spruce (built). Planned: olive_bare, rust_larch (placeholders in the params, status "planned").
REFERENCE  art/reference/style-notes-gma2.md, "Overall look" and "Trees": our notes on the user's Grand Mountain
           Adventure 2 screenshots. The screenshots are copyrighted and not in the repo. Silhouette reference:
           ref/front_matte.png, side_matte.png, top_matte.png and profiles_sheet.png, drawn by script from the
           params (our own drawing, commit). No third-party images or textures are used.
SCALE      12.0 m tall, per the task brief ("about 12 m"). Real Norway spruce in a ski glade are often taller
           (inferred); 12 m is a stylised height and is open to change after the game-camera finding below.
SHAPE      Measured from the layout (ref/refs.json), checked on the mesh by tree_gate.py within 2 percent:
             height 12.00 m; crown width 3.52 m; height to crown width 3.41
             trunk base diameter 0.32 m; height to trunk diameter 37.5 (very slender)
             lowest crown point (lowest drooping tip) 1.47 m = 12 percent of height; tier roots start at 2.04 m
             10 tiers, 6 branches at the bottom to 3 at the top, 45 branches; droop 40 percent of branch length
             spike crown from 10.32 m to 12.00 m = 14 percent of height
SILHOUETTE Trunk visible below the crown and between tiers; sky between sparse drooping tiers; narrow dark spike;
           tiers white on top and dark green underneath.
MATERIALS  Flat colour, no textures, no UVs, one material per role (linear RGB, PROVISIONAL until the look review):
             M_Conifer_Bark   (0.09, 0.07, 0.06)  roughness 0.9   trunk
             M_Conifer_Needle (0.022, 0.06, 0.035) roughness 0.85 tier sides and undersides, spike
             M_Conifer_Snow   (0.80, 0.87, 0.96)  roughness 0.7   tier tops (snow caps); 95 percent of branches
LODS       LOD0 557 tris (budget 300 to 1,500). LOD1 372 tris (budget 100 to 400): LOD0's placement with at
           most 3 branches per tier and wider planes. LOD2 billboard: planned, not built (see below).
CAMERA     Game camera from art/source/CH_Skier/CH_Skier_master.blend REF_game_camera: az 30, el 45, 35 mm,
           18.0 m from the skier's mid-height (0.875 m). This is still the FIRST-CHAIR.md estimate.
INFERRED   Height (from the task, not from a measured screenshot); tier count, droop and widths (from the notes'
           words, not from measured images); all colours.
TARGET     Unity 6.3 LTS URP, FBX, Assets/Art/Environment/EN_Conifer/ (Phase 6, after the look review).
           Thousands instanced: GPU instancing on the materials and a LODGroup (not verified in the editor).
```

## Game-camera finding

At the estimated game camera, the 12 m tree projects 1,228 px tall in a 1,080 px frame even when the camera
aims at its mid-height, so it never fits (review/final/render_info.json). When the follow camera aims at a skier
standing at the trunk, the tree spans 1,741 px and only about 45 percent of it is on screen. Either the
camera ends up farther away than the estimate, or glade trees near the run are shorter, or near trees are meant
to be cut by the frame top. That is a decision for the user and art direction, not for this build.

## Billboard (LOD2) still to build

Render LOD0 orthographic from 8 azimuths (or front and side for crossed quads) under flat light into an alpha
atlas of about 512 x 256, on a 2 to 4 tri quad with an alpha-clip URP material. The billboard is the one
textured part of the tree, so its atlas size and texture memory go in the manifest. Gate: alpha-mask IoU against
the LOD0 silhouette from the same view.
