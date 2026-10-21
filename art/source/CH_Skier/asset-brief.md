# CH_Skier asset brief

Status: Phase 4 done, waiting for the user's review before Phase 5 (UVs, AO and materials). The proportions,
palette, helmet, stripes and socket names are approved (user, 2026-10-20); Phase 4 keeps the Phase 3 forms as they are.
Story: `production/epics/first-chair/story-007-first-art-pass.md`.

```text
ASSET      CH_Skier
CATEGORY   character, biped: rigged player character. A deforming body (one skinned mesh) plus rigid helmet,
           goggles and boots as separate objects on the same skeleton. Skis and poles are the existing
           props PR_Ski and PR_SkiPole, attached at sockets.
VIEWS      ref/front.png and ref/front_matte.png: front, orthographic (az 0, camera at -Y, image right =
             world +X = the figure's left), 400 px/m, 760 x 780 px, axis column 380, ground line row 740.
             The subject fills 700 px of the 780 (1.75 m); the ground is 5.13 percent up from the bottom.
           ref/side.png and ref/side_matte.png: side, orthographic (az 90, camera at +X, image right = world +Y,
             so the figure faces image left), same scale and ground line, 360 x 780 px, axis column 180.
           ref/sheet.png: both views with a metre ruler and key heights, for review only.
           All drawn by ref/make_refs.py from the proportion table in ref/refs.json: our own reference sheet,
             not a photo or a screenshot. Simple shapes (tapered capsules, flat-ended cylinders, rounded boxes,
             ellipsoids, a tilted helmet rim), projected exactly, so front and side agree. Mattes are drawn at
             4x, box-filtered and thresholded at 50 percent (FIRST-CHAIR.md, Lessons).
           Look source: art/reference/style-notes-gma2.md, our notes on the user's GMA2 screenshots, including
             "Second skier" (face, puffy shoulders, hood bunched at the neck). The screenshots are copyrighted,
             not in the repo, and were used only to judge forms, never traced or measured.
           Game camera (FIRST-CHAIR.md estimate): az 30, el 45, 35 mm, 18 m. Review renders call it the
             "ref" view (--ref-cam 30 45 35), with an 80 px gameplay strip.
SCALE      1.75 m from sole to helmet top, in boots (user brief: an adult about 1.75 m tall in boots).
           1 unit = 1 m, Z up, faces -Y, origin on the ground between the feet. Every other dimension comes
           from our proportion table (see INFERRED). Proportions approved by the user, 2026-10-20.
PROPORTIONS checked at blockout (5 percent) and forms (2 percent); H = 1.75 m
            1 helmet width / H           0.149 (0.26 m; depth 0.31 m), about 15 percent over a real 0.22 m helmet
            2 helmet rim to top / H      0.123 (rim plane z = 1.535 - 0.125 y: 1.554 at the front, 1.514 at
                                          the back, about 7 deg up at the front; top 1.75 m)
            3 goggle line / H            0.914 (1.60 m); goggles 0.22 x 0.085 m
            4 shoulder width / H         0.303 (0.53 m across the sleeve caps)
            5 shoulder top / H           0.826 (1.445 m)
            6 jacket width / H           0.263 (0.46 m; 0.30 m deep; hem flare 0.48 m)
            7 jacket hem / H             0.469 (0.82 m)
            8 crotch / H                 0.440 (0.77 m); seat width 0.38 m
            9 knee / H                   0.286 (0.50 m)
           10 trouser hem / H            0.166 (0.29 m); rust boot band 0.23 to 0.29 m
           11 fingertip span / H         0.875 (1.53 m) with the arms 40 deg below horizontal
           12 mitten length / H          0.109 (0.19 m; 0.12 wide, 0.085 thick), about 15 percent oversized
           13 boot length / H            0.183 (0.32 m; 0.115 wide); boot centres 0.26 m apart (hip width)
           14 depth / H                  0.214 (0.375 m, boot toe -0.19 to hood back +0.185)
           15 visible face / H           0.071 (about 0.125 m at the front, from the band top 1.43 to the
                                          goggle bottom 1.5575)
PARTS      in construction and overlap order; (S) = separate object in the export
           1 body, one skinned mesh, deforming:
             trouser legs and seat (baggy, rounded knee, flat hem over the boot cuff); jacket body (puffy,
             slight hem flare, neckline under the band); puffy round shoulders; sleeves (16-sided, flat-ended)
             with navy cuffs; mittens with a forward thumb; hood and collar as ONE loft (collar inside the
             band, hood bunched at the back of the neck above it); neck warmer lowered to a band around the
             neck and collar (user, 2026-10-20); lower face: head, jaw, chin, nose in skin, mouth as a small
             darker piece, all on the head bone.
           2 helmet (S): shell cut by a rim plane tilted 7 deg up at the front, lighter top panel (material
             split), goggle strap ring. Rigid, one bone (head).
           3 goggles (S): dark frame and pink lens, curved to the helmet front. Rigid, head bone; may merge into
             the helmet object at export (same bone, same budget line).
           4 boot L and R (S): lower shell (foot loft and lower shaft) rigid to the foot bone; cuff rigid to
             the shin bone, hinged at the ankle rivet (how real ski boots are built), red band on the cuff as a
             material split. The trouser hem overlaps the cuff.
           5 not in this asset: PR_Ski and PR_SkiPole, attached at the sockets below.
           Surface detail: centre zip, two short chest stripes and one stripe down the outside of each upper
           sleeve are material splits on the jacket and sleeve faces; knee patches are separate faceted
           pieces on the trousers. Stripes only on the jacket, no pattern (user).
SILHOUETTE 1 big rounded helmet with wide dark goggles and a pink lens, the lower face and jaw showing below
           2 puffy, boxy jacket with round shoulders, the hood bunched behind the neck and a slight hem flare
           3 baggy navy trousers with rounded knees over chunky dark boots with red cuff bands
           4 chunky red mittens
           5 in game: the athletic ski stance from the rig (knees forward, hips back, hands low and forward)
MATERIALS  "First Chair" outfit: the user's palette (user choice 2026-10-20), replacing the GMA2 colours.
           One material per colour role in the master, flat colour, no normal or detail maps; AO bake later
           (FIRST-CHAIR.md). Roles that share a colour merge at export (see Decisions).
           jacket          woven cloth, roughness 0.7 to 0.9    #F2B705 lift yellow
           stripes, zip    tape (material split)                #22304A navy
           cuffs           elastic cloth                         #22304A navy
           mittens         padded cloth                          #D7263D red
           trousers        woven cloth, 0.7 to 0.9               #22304A navy
           knee patches    reinforced cloth (separate facets)    #18223A
           boots           plastic shell, 0.4 to 0.6             #2B2F38
           boot band       plastic or strap (material split)     #D7263D red
           helmet          plastic shell, 0.4 to 0.6             #2C3A57, top panel #4A5B7D (material split)
           goggle frame    plastic, and the strap                #15181E
           goggle lens     glossy lens, 0.1 to 0.2               #E2557A pink
           neck warmer     fleece, 0.8 to 1.0                    #D7263D red
           skin            skin, 0.5 to 0.7                      #D9A07A *
           mouth           skin shadow                           #9A5A4E *
           * Placeholders (my picks, light-medium skin tone and a darker mouth) for the art-director or the
             user to confirm. Every other value is the user's. Equipment colours still need checking
             against snow at the follow camera (story 007).
ARTICULATION humanoid deformation skeleton (references/rigging-animation.md) bound to the body; helmet,
           goggles and boot parts rigid to one bone each.
           Rest pose: relaxed A-pose, arms 40 deg below horizontal, standing straight, feet hip-width apart.
           The ski stance comes from the rig, driven by sim state, never from the mesh (story 007).
           Joint centres in metres, left side; the right side mirrors X:
             shoulder (0.18, 0.02, 1.36)   elbow (0.402, 0.02, 1.174)   wrist (0.601, 0.02, 1.006)
             hip (0.10, 0.03, 0.88)        knee (0.12, 0.0, 0.50)       ankle (0.13, 0.02, 0.11)
           Sockets (empties in SOCKETS, placed in the blockout; +Y = the figure's forward, +Z up):
             SOCKET_ski_L, SOCKET_ski_R    under the sole at the boot centre, (+/-0.13, -0.03, 0.0).
                                           PR_Ski's SOCKET_boot (0.045 above the ski base) aligns here.
             SOCKET_hand_L, SOCKET_hand_R  palm centre inside the mitten, (+/-0.669, 0.02, 0.924) in the rest
                                           pose; PR_SkiPole's grip aligns here. Final axes in Phase 6.
           Animation: pose driven from sim state (lean from edge angle, crouch from tuck and pop load), plus
           authored clips for tuck, grabs and crashes (story 007).
INFERRED   No measured reference exists. Every dimension except the 1.75 m height and the 40 deg arm angle is
           from our proportion table (ref/make_refs.py P, copied into ref/refs.json "proportions_m"):
           - helmet: centre (0, 0.01, 1.60), radii 0.13 / 0.155 / 0.15, rim plane z = 1.535 - 0.125 y (1.554
             front, 1.514 back), top panel above 1.69, about 15 percent oversized from a typical 0.22 x 0.28 m
             adult helmet
           - goggles 0.22 x 0.06 x 0.085 at z 1.60, front at y -0.17; lens 0.19 x 0.06; strap z 1.578 to 1.622
           - neck warmer band 0.26 x 0.25 x 0.045 (z 1.385 to 1.43), wrapping the collar
           - collar 0.22 x 0.21 x 0.05 at z 1.40 (under the band); hood bunched behind the neck: back to
             y 0.19, sides +/-0.14, z 1.42 to 1.505, at least 1 cm below the helmet rim
           - face: head ellipsoid radii 0.088 / 0.095 / 0.10 centred (0, -0.02, 1.54); jaw 0.13 x 0.09 x 0.07
             at z 1.47 (front y -0.105); chin 0.085 x 0.05 x 0.045 at z 1.455 (front y -0.115); nose
             0.034 x 0.05 x 0.05 at z 1.525 (tip y -0.143); mouth 0.045 x 0.009 at z 1.487
           - jacket 0.46 x 0.30 x 0.62 (z 0.82 to 1.44); hem flare 0.48 x 0.32 x 0.12; shoulder caps r 0.085
             (forms: puffy shoulders r 0.095, neckline at 1.42 under the band)
           - shoulder joint (0.18, 0.02, 1.36); upper arm 0.29, forearm 0.26; sleeve radii 0.085 / 0.075 /
             0.066; the sleeve ends 0.03 before the wrist; cuff r 0.06, from 0.04 before to 0.02 past the wrist
           - mitten 0.19 x 0.12 x 0.085, about 15 percent oversized from a 0.19 x 0.09 m hand plus padding;
             thumb r 0.025 pointing forward
           - pelvis 0.38 x 0.28 x 0.18 at z 0.86 (crotch 0.77); hip joint (0.10, 0.03, 0.88); knee (0.12, 0,
             0.50); thigh radii 0.105 / 0.095; shin radii 0.095 / 0.10; trouser hem z 0.29
           - knee patches 0.12 x 0.15 ellipsoids, 1 cm proud of the knee front
           - boots: foot 0.115 x 0.32 x 0.13 (boot centre y -0.03), shaft 0.12 x 0.16 x 0.30 (top 0.34),
             band 0.13 x 0.17 x 0.06 (z 0.23 to 0.29), ankle joint z 0.11; boot centres 0.26 apart
           - socket positions above
           - the back: no back view, so the back is the side view's depth with a symmetric jacket and hood
           - boot construction (shell and cuff on a hinge) and a vertical boot cuff in the rest pose; real
             boots have about 13 deg of built-in forward lean, which comes from the rig
           - colours marked * above
TARGET     Unity 6.3 LTS (URP), FBX (--engine unity; rigged, so Blender's default armature axes are kept per
           FIRST-CHAIR.md). Budget (FIRST-CHAIR.md): body 5k to 10k tris at LOD0, 2k to 4k at LOD1, about 1k
           at LOD2; helmet with goggles and each boot 100 to 600 tris, one LOD. Game camera estimate az 30,
           el 45, 18 m, 35 mm; skier about 80 px tall on screen.
           Phase 4 (review/04_topology/measure.json): body 2,895 tris at LOD0, 2,139 at LOD1, 1,081 at LOD2;
           helmet with goggles 374, each boot 204 (shell 96 + cuff 108); whole character 3,677 at LOD0. The
           body is under the 5k floor on purpose (see Decisions); the 10k ceiling is the hard limit.
```

## Decisions

- **Helmet and boots are separate objects on the same skeleton, not part of the body mesh and not
  runtime-socketed.**
  - FIRST-CHAIR.md's budget table already lists them as separate props with their own 100 to 600 tri lines.
  - They are rigid plastic. One-bone binding keeps them from bending like cloth at the neck or ankle,
    and it is cheaper than skinning them.
  - They are the gear most likely to be recoloured against snow (story 007) or swapped later.
  - The two-piece boot (shell on the foot bone, cuff on the shin bone) lets the rig flex the ankle into
    the ski stance without deforming the boot.
  - Keeping them in the same FBX on the same skeleton means no extra head or foot sockets. The four named
    sockets cover what attaches at runtime: skis and poles.
  - If runtime gear swaps are wanted later, the same objects can become socketed props.
- **Sockets are named `SOCKET_ski_L/R` and `SOCKET_hand_L/R`** (the user's choice, 2026-10-20), matching
  PR_Ski and PR_SkiPole, so `scripts/validate.py` checks them.
- **The reference is a measuring stick, not a design.** The blockout and the reference come from the same
  table. A high IoU therefore proves scale, placement and proportions match the table; it does not prove
  the table is the right look. That is the user's review.
- **Stripes, zip, helmet top panel and boot band are material splits on existing faces**, not decals
  or extra geometry. FIRST-CHAIR.md asks for flat colour with one material per colour role. Splits add no
  triangles and leave nothing to z-fight. The jacket and sleeve sections are laid out so that whole face
  columns line up with the zip and stripes. **Phase 4 must keep those columns**, or the stripes break up
  at LOD.
- **Knee patches, goggle strap, nose, jaw, chin and mouth are separate faceted pieces.** Each is a small
  low-segment shape that reads as a facet under flat shading.
- **The hood and collar are one loft** (second forms pass). The first pass overlapped a hood ellipsoid, a
  flat collar rim and the jacket back at shallow angles. The collar's back face sat about 4 mm outside the
  jacket's, which rendered as grainy, speckled yellow behind the neck. The single loft keeps the collar
  inside the band and keeps the hood at least 1 cm clear of the helmet rim and the shoulder puffs.
- **More face, same proportions** (second forms pass):
  - The neck warmer band is lowered to z 1.385 to 1.43 and wraps the collar.
  - The jaw and chin are pushed forward and widened (jaw 0.13 wide).
  - The helmet rim tilts 7 deg up at the front (1.554 front, 1.514 back), so the face shows from the band
    up to the goggles.
  - The goggles stay at their table height (1.60). Tilting them was considered and left out, because the
    rim tilt alone exposes the face.
- **Merge materials for Phase 5.** The master keeps 15 colour roles, but several share a colour: navy is
  stripe, cuff and trousers; red is mitten, boot band and neck warmer. Each material on a
  SkinnedMeshRenderer is a separate submesh and draw call. At export, either merge same-colour roles into
  one material, or use one small palette texture with UVs packed by colour (one material for the whole
  character, and the AO atlas can share it). Recommended: the palette texture, decided in Phase 5.
- **Sample count for material reviews:** colour renders use `--samples 64`. At 16 samples with no
  denoising, the shadowed crevices around the neck read as noise.
- **Body LOD0 is 2,895 tris, under the 5k floor of FIRST-CHAIR.md** (Phase 4). The floor is a starting
  estimate. Triangles went only where the silhouette or the bending needs them: LOD0's silhouette is
  pixel-identical to the approved forms (IoU 1.0 against the Phase 3 silhouettes, front and side) and every joint
  has its loops. More would only split flat facets that the 80 px game camera cannot show. The whole character,
  props included, is 3,677 at LOD0, well under the 10k ceiling.
- **Deformation loops** (Phase 4), inserted between the approved rings so no face moved: neck (hood rings z 1.39,
  1.4075, 1.425, 1.435, 1.445; neck warmer 1.385, 1.4075, 1.43); shoulder (sleeve rings 0, 0.04, 0.08, 0.13 m
  from the joint, the shoulder puff's 8 latitude rings, jacket rings 1.30, 1.38, 1.40); elbow (0.235, 0.29, 0.345
  m from the shoulder); wrist (sleeve end 0.52, cuff rings -0.04, -0.02, 0, +0.02 m about the wrist); spine
  (jacket 0.93, 1.02, 1.085, 1.15); hip (trousers 0.92, 0.88, 0.82, 0.77; seat 0.80, 0.84, 0.88); knee (0.62, 0.55,
  0.50, 0.45, 0.38); ankle (the boot hinge, plus trouser hem rings 0.38, 0.335, 0.29). The zip, chest stripes and
  sleeve stripe stay on the same face columns, ending on ring edges, so they survive deformation and LOD.
- **Helmet and goggles are one object, `CH_Skier_Helmet`** (shell with the top panel split, strap, goggles and
  lens): one head bone and one budget line, as the parts list allowed.
- **Each boot is two objects:** `CH_Skier_BootShell_L/R` (foot bone) and `CH_Skier_BootCuff_L/R` (shin bone), so
  each stays rigid on one bone. The cuff gained a hidden skirt (down to z 0.17, 4 mm inset, inside the shell), so
  it can tip forward about the ankle into the ski stance without opening a gap at the hinge. 204 tris per boot.
- **Triangulation lock:** the LOD0 meshes keep quads in the source (ngon caps fanned to triangles) and carry a
  Triangulate modifier (quads FIXED, ngons BEAUTY), the same split Blender used to draw the forms. Bake and export
  both evaluate it, so shading cannot change later. Keep it first in the stack when Phase 6 adds the Armature
  modifier. LOD1 and LOD2 are stored as triangles.
- **Interior faces removed:** 237 body faces (87 on centre parts, 75 per side; plus 6 in the helmet: strap
  behind the goggles, back of the lens) whose corners, edge midpoints and centre all sit at least 8 mm inside
  other body parts (3 mm for pieces on one rigid bone, such as the top of the head under the helmet). The body is
  therefore 25 open shells, one per part, each opening hidden inside another part.
- **LODs:** the skill's `lod_copy` per part with a ratio table (head, face, shoulders and mittens keep the most;
  sleeves and legs give the most), with material-boundary vertices, the jacket hem and the shoulder line protected
  through the Decimate vertex group, and the right side mirrored from the left. Then an outline fix: every LOD
  vertex snapped back onto the approved surface, and any part extent that shrank by 2 to 6 mm pulled back out.
  Parts that shrank more got a higher ratio instead (a bigger pull makes spikes). Helmet and boots have one LOD,
  shared by all three body LODs, so their outline never changes.
- **Collections:** LOW holds LOW_LOD0, LOW_LOD1 and LOW_LOD2 (the body per LOD) and LOW_PROPS (helmet and boots).
  The Phase 2 blockout moved to BLOCKOUT (hidden). Review a LOD with `--collections LOW_LOD<n>,LOW_PROPS`.
- **`--require-uv` belongs to Phase 5.** The skill's Phase 4 example passes it, but UVs are Phase 5's work ("UVs,
  baking, materials"). Phase 4 validates without it. A run with it fails only on the 8 missing UV layers
  (review/04_topology/validate_require_uv.json).
- **For Phase 5:** `build/04_topology.py` rebuilds LOD1 and LOD2 from LOD0. Unwrap LOD0, then rebuild the LODs from
  it (Decimate carries UVs over), rather than unwrapping each LOD.

## Gate results

| Phase | View | world_gate IoU | Band widths (max abs) | compose IoU | Notes |
| --- | --- | --- | --- | --- | --- |
| 02 blockout v1 | front | 0.9945 | 0.0057 | 0.987 | tolerance 0.85 / 0.05 |
| 02 blockout v1 | side | 0.9886 | 0.0029 | 0.983 | tolerance 0.85 / 0.05 |
| 02 blockout v2 (face, band, hood) | front | 0.9947 | 0.0029 | 0.988 | tolerance 0.85 / 0.05 |
| 02 blockout v2 | side | 0.9895 | 0.0029 | 0.966 | tolerance 0.85 / 0.05 |
| 02 blockout v3 (lower band, jaw, tilted rim) | front | 0.9942 | 0.0057 | 0.987 | tolerance 0.85 / 0.05 |
| 02 blockout v3 | side | 0.9888 | 0.0029 | 0.986 | tolerance 0.85 / 0.05 |
| 03 forms pass 1 | front | 0.9808 | 0.0129 | 0.977 | tolerance 0.90 / 0.03 |
| 03 forms pass 1 | side | 0.9652 | 0.0100 | 0.952 | tolerance 0.90 / 0.03 |
| 03 forms pass 2 (approved) | front | 0.9806 | 0.0029 | 0.976 | tolerance 0.90 / 0.03 |
| 03 forms pass 2 (approved) | side | 0.9612 | 0.0100 | 0.953 | tolerance 0.90 / 0.03 |
| 04 topology LOD0 (body 2,895) | front | 0.9806 | 0.0029 | 0.976 | floor 0.9756 (Phase 3 minus 0.005), IoU against the Phase 3 silhouette 1.0000 |
| 04 topology LOD0 (body 2,895) | side | 0.9612 | 0.0100 | 0.953 | floor 0.9562 (Phase 3 minus 0.005), IoU against the Phase 3 silhouette 1.0000 |
| 04 topology LOD1 (body 2,139) | front | 0.9804 | 0.0029 | - | IoU against the Phase 3 silhouette 0.9989 |
| 04 topology LOD1 (body 2,139) | side | 0.9596 | 0.0114 | - | IoU against the Phase 3 silhouette 0.9967 |
| 04 topology LOD2 (body 1,081) | front | 0.9799 | 0.0029 | - | IoU against the Phase 3 silhouette 0.9884 |
| 04 topology LOD2 (body 1,081) | side | 0.9575 | 0.0100 | - | IoU against the Phase 3 silhouette 0.9845 |

Measured on the forms (`review/03_forms/measure.json`): height 1.75, fingertip span 1.531 (table 1.532),
depth 0.3745. The blockout's measured proportions are all within 1 percent of the table. One known
deviation: the shin cylinders tilt slightly (knee x 0.12 to hem x 0.13), so the trouser hem's lowest point
is 0.283 against 0.29 (7 mm, 0.4 percent of H).

Phase 4 checks (`review/04_topology/`): `validate.py` on LOW,SOCKETS with `--budget-tris 10000` exits 0: 0 FAIL, 12
warnings, all expected: no UV layer on the 8 LOW meshes (Phase 5) and no parent on the 4 sockets (parented to bones
in Phase 6). No doubles, zero-area faces, loose geometry, non-manifold edges or stray islands remain. validate counts
the unsuffixed props at LOD0 only: 3,677 / 2,139 / 1,081. The greyscale gameplay strip (`gameplay_strip_grey.png`)
shows LOD0, LOD1 and LOD2 reading the same at 80 px.

## Rebuild

```bash
cd art/source/CH_Skier
python3 ref/make_refs.py
S=../../../.claude/skills/blender-image-to-3d/scripts
$BLENDER_BIN -b --python $S/init_master.py -- --out CH_Skier_master.blend --name CH_Skier --height 1.75 \
  --ref-front ref/front.png --ref-right ref/side.png --subject-frac 0.948718 --ground-frac 0.051282 \
  --cell 2.0 --capsule 0.35 1.75 --cam-elev 45 --cam-az 30 --cam-dist 18 --cam-lens 35
$BLENDER_BIN -b --python build/02_blockout.py -- --master CH_Skier_master.blend
$BLENDER_BIN -b --python build/03_forms.py -- --master CH_Skier_master.blend
$BLENDER_BIN -b --python-exit-code 1 --python build/04_topology.py -- --master CH_Skier_master.blend
# per phase: D=review/02_blockout with C=BLOCKOUT (moved out of LOW by Phase 4), or D=review/03_forms with C=HIGH
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out $D \
  --collections $C --views front,side,threequarter --ref-cam 30 45 35 --mode clay --gameplay-px 80
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out $D \
  --collections $C --views front,side --mode silhouette
$BLENDER_BIN -b --python $S/world_gate.py -- --blend CH_Skier_master.blend --out $D --collections $C \
  --matte ref/front_matte.png --az 0 --m-per-px 0.0025 --axis-col 380 --ground-row 740 --name front
$BLENDER_BIN -b --python $S/world_gate.py -- --blend CH_Skier_master.blend --out $D --collections $C \
  --matte ref/side_matte.png --az 90 --m-per-px 0.0025 --axis-col 180 --ground-row 740 --name side
python3 $S/compose_review.py --ref ref/front_matte.png --render $D/silhouette_front.png \
  --out $D/compare_front.png --measure --gameplay-px 80   # and the same for side
# forms only: colour renders, head close-ups, sheets
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out review/03_forms \
  --collections HIGH --views front,side,threequarter --ref-cam 30 45 35 --mode material --gameplay-px 80 --samples 64
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out review/03_forms/head \
  --collections HIGH --views threequarter,threequarter_back --ref-cam 30 45 35 --mode material --samples 64 \
  --fit CH_Skier_Helmet_form,CH_Skier_Hood_form,CH_Skier_Jaw_form,CH_Skier_NeckWarmer_form
python3 build/review_sheet.py --dir review/03_forms --mode material --out review/03_forms/colour_sheet.png
python3 build/review_sheet.py --dir review/03_forms --mode clay --out review/03_forms/clay_sheet.png
# Phase 4 gates: D=review/04_topology; for L in 0 1 2, C=LOW_LOD$L,LOW_PROPS and O=$D/lod$L
$BLENDER_BIN -b --python $S/validate.py -- --blend CH_Skier_master.blend --collections LOW,SOCKETS \
  --out $D/validate.json --budget-tris 10000
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out $O --collections $C \
  --views front,side,threequarter --ref-cam 30 45 35 --mode clay --gameplay-px 80   # and again with --mode wire
# world_gate.py front and side as above with --collections $C --out $O; LOD0 also silhouette renders + compose_review
python3 build/review_sheet.py --dir $O --mode wire --out $D/wire_sheet_lod$L.png
python3 build/lod_review.py --dir $D --forms review/03_forms   # clay_compare, gameplay_strip_grey, lod_gates.json
```
