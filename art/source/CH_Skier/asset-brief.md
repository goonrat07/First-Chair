# CH_Skier asset brief

Status: Phases 5 (light), 6, 8 (16 pose clips) and 9 done, 2026-10-23. The user asked for the real skier with
animation in the next test build, so the work went past the Phase 4 review stop. Exported to
`Assets/Art/Characters/CH_Skier/` (FBX, manifest, animation contract); **not verified in the Unity editor**. The
seat showing through the lower jacket back is fixed (story 012, 2026-10-27; see Gate results). The proportions,
palette, helmet, stripes and socket names are approved (user, 2026-10-20).
**Detail pass for the chase camera (story 012, 2026-10-27):** phases 3 to 9 rebuilt with rounder sections and new
detail (see PARTS, "Story 012 detail pass" under Decisions, and the story 012 rows of Gate results), reviewed from
the chase camera with a back reference view added. Same proportions, palette, rig, sockets and clip names.
Stories: `production/epics/first-chair/story-007-first-art-pass.md`; the detail pass for the chase camera,
`production/epics/first-chair/story-012-skier-detail-pass.md`.

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
           ref/back.png and ref/back_matte.png (story 012): back, orthographic (az 180, camera at +Y, image right
             = world -X), same scale and ground line, 760 x 780 px, axis column 380. Drawn so the back is designed,
             not inferred: yoke stripe, hem band, the strap buckle; front-only decals (zip, chest stripes, mouth)
             left out.
           ref/sheet.png: the three views with a metre ruler and key heights, for review only.
           All drawn by ref/make_refs.py from the proportion table in ref/refs.json: our own reference sheet,
             not a photo or a screenshot. Simple shapes (tapered capsules, flat-ended cylinders, rounded boxes,
             ellipsoids, a tilted helmet rim), projected exactly, so front and side agree. Mattes are drawn at
             4x, box-filtered and thresholded at 50 percent (FIRST-CHAIR.md, Lessons).
           Look source: art/reference/style-notes-gma2.md, our notes on the user's GMA2 screenshots, including
             "Second skier" (face, puffy shoulders, hood bunched at the neck). The screenshots are copyrighted,
             not in the repo, and were used only to judge forms, never traced or measured.
           Game camera (story 011, FIRST-CHAIR.md): the chase camera, behind the rider and a little over the
             right shoulder, about 3 to 5 m back, 26 mm. Review renders call it the "ref" view
             (--ref-cam 185 33 26), with a 300 px gameplay strip. Before story 012 it was the GMA2-style far
             camera (az 30, el 45, 35 mm, 18 m, an 80 px strip); the story 007 gate rows below used that.
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
           Story 012 detail (the user approved the list, 2026-10-27; seen from behind and the right):
             jacket: a navy drawcord line above the hem (z 0.845 to 0.87) and a hem that drops 3.5 cm at the
               back centre; a navy yoke stripe across the upper back (z 1.285 to 1.315, |x| < 0.19); both
               material splits. A hand-pocket flap on each side seam: a raised panel following the jacket,
               6 mm proud, 15 x 5 cm, its zip along the top edge.
             trousers: a cargo pocket on each outer thigh, a raised panel following the leg (12 x 13 cm, 7 mm
               proud) with a navy flap 4 mm prouder over its top; the hem flares to r 0.108 over the boot cuff.
             mittens: a red gauntlet over the cuff and sleeve end, flaring from r 0.072 at the wrist to 0.078.
             helmet: four dark vent grooves on the crown, front to back; a light buckle on the strap at the back.
             boots: two light buckles on the outside of each shell; a dark power strap round the top of each cuff.
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
           - the back: drawn as ref/back.png from the same table since story 012 (before that, no back view: the
             side view's depth with a symmetric jacket and hood)
           - story 012 detail sizes and places (PARTS), all design choices
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
           Phase 9 (Assets/Art/Characters/CH_Skier/CH_Skier.fbx): body 2,895 / 2,139 / 1,081; helmet 374, boot shells
           96, boot cuffs 108 each, shared by every LOD (with props: 3,677 / 2,913 / 1,855). 20 bones, 4 sockets, 9 clips.
           Story 012: the game camera is now the chase camera (VIEWS), the skier 250 to 330 px tall at 720p and twice
           that at 1440p. Budget: body 5k to 8k at LOD0, 2k to 4k at LOD1, about 1k at LOD2; helmet with goggles and
           each boot 100 to 600. Built (Phase 9, 2026-10-27): body 4,269 / 3,171 / 1,262; helmet 584; each boot 256 (shell
           120 + cuff 136); with props 5,365 / 4,267 / 2,358. 20 bones, 4 sockets, 16 clips. Body LOD0 is under the
           story's 5k floor and LOD2 above about 1k (see Decisions).
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
- **Phase 5 is light: 11 flat materials, no UVs, no bake** (2026-10-23). Merged same-colour roles rather than the
  palette texture, because merged materials need no UVs and URP Lit takes the FBX diffuse colour:
  `M_CH_Skier_Yellow` #F2B705 (jacket), `_Navy` #22304A (stripes, zip, cuffs, trousers), `_Red` #D7263D (mittens,
  boot band, neck warmer), `_KneePatch` #18223A, `_Boot` #2B2F38, `_Helmet` #2C3A57, `_HelmetPanel` #4A5B7D,
  `_GoggleFrame` #15181E, `_Lens` #E2557A, `_Skin` #D9A07A, `_Mouth` #9A5A4E; roughness from the MATERIALS table
  (cloth 0.8, knee patch 0.85, plastic 0.5, lens 0.15, skin 0.6). The body has 6 submeshes, the helmet 4, each cuff 2.
  **AO bake deferred:** it needs UVs (unwrap LOD0, rebuild LODs, per the Phase 4 note) or vertex colours. The HIGH and
  BLOCKOUT pieces keep the 15 role materials.
- **Skeleton (Phase 6):** `CH_Skier_Rig` with `Root` (ground, between the feet), `Hips`, `Spine`, `Chest`, `Neck`,
  `Head` and per side `Shoulder`, `UpperArm`, `LowerArm`, `Hand`, `UpperLeg`, `LowerLeg`, `Foot` (`_L`/`_R`), at the
  ARTICULATION joint centres. Inferred: clavicle root (0.04, 0.02, 1.34), hand bone 0.15 m along the forearm, foot
  tail (0.13, -0.15, 0.06). Every bone deforms (FBX deform-only export); Root has no weights. Rest = the A-pose.
- **Weights from one analytic field, not heat weighting.** Heat weighting weights each of the 25 open shells on its
  own, so overlapping shells (puff over sleeve, hem over seat) would separate. Every deforming shell samples the
  same field of position (`build/06_rig.py`): trunk blends along z, limb chains blend across each joint with
  smoothstep falloffs along the bone axes, limbs mix into the trunk around the shoulder and hip, less behind the hip
  joint. The falloffs replace mesh smoothing, which would differ per shell. 4 influences, under 0.01 dropped,
  normalised. Face parts are rigid on Head, mittens and thumbs on Hand.
- **Rigid props are skinned to one bone at weight 1.0**, not bone-parented: helmet to Head, boot shells to Foot,
  boot cuffs to LowerLeg (the ankle hinge). In Unity each is a one-bone SkinnedMeshRenderer.
- **Socket axes (Phase 6):** `SOCKET_ski_L/R` on `Foot_L/R`, at the sole under the boot centre, +Y the figure's
  forward, +Z up (PR_Ski's SOCKET_boot aligns). `SOCKET_hand_L/R` on `Hand_L/R`, at the palm centre, +Z up the pole
  (thumb side), -Z down the shaft, +Y along the hand to the fingertips (PR_SkiPole's SOCKET_grip aligns).
- **Snow plane:** PR_Ski's base is 0.045 m below its SOCKET_boot. Flat poses put the soles at z 0, so with skis the
  snow is 0.045 m below the root; Unity places the root 0.045 m above the snow.
- **Poses are solved from specs, not hand-keyed** (`build/08_poses.py`): hips, trunk bends, wrist targets, elbow and
  knee directions, pole directions and ski placement; two-bone IK for arms and legs; the hand turns so its socket's
  -Z runs down the pole; a foot with no height drops until its ski's lowest edge touches the snow. CarveRight is the
  mirror of CarveLeft's spec on the mirrored skeleton. Brake turns the pelvis -25 deg and counter-turns the chest
  so the upper body faces forward. All poses are authored from the user's descriptions (no motion reference).
- **Take names:** each clip is also one NLA strip of the same name, and `build/09_export.py` exports strips instead of
  "all actions" (Blender names those `CH_Skier_Rig|Stance`), so the FBX takes, and Unity's clips, are named exactly
  like the clips. Everything else is `export_delivery.py` with `--engine unity`. Static clips are not looped.
- **Manifest at rest:** `09_export.py` sets the rig to its rest pose after the FBX is written, so the manifest's
  bounds and socket positions are rest values (otherwise the top NLA track was evaluated).
- **`roundtrip.py --rest-pose`** (skill change, recorded in FIRST-CHAIR.md): Blender's FBX importer leaves the first
  take active, so the rigged import was measured in Stance (1.579 m). The flag measures at rest.
- **Story 012 detail pass (2026-10-27).** The chase camera shows the rider three to five times larger than the far
  camera did, so:
  - **Rounder where the outline facets** (`03_forms.py` RING COUNTS; Phase 4 builds from the same constants): jacket
    back 5 to 9 points per ring; sleeves 16 to 20 sides; cuffs, seat, legs and hood 12 to 16; neck warmer 14 to 18;
    goggle strap 16 to 20; thumb 8 to 10; mitten bevel 2 to 3 segments; spheres: head 12x8 to 16x10, helmet 16x10 to
    20x12, shoulders 12x8 to 16x10, thumb tips and knee patches 8x6 to 10x8. Still flat-shaded facets: rounder, not
    smooth, to keep the look.
  - **The new stripes are material splits too:** the drawcord line is the whole row of jacket faces between two new
    rings (z 0.845 and 0.87); the yoke stripe is the back faces near the centre between rings 1.285 and 1.315 (which
    replace the ring at 1.30, keeping its profile). The hem drop lowers the back points of the rings at and below
    0.87 by up to 3.5 cm (drop times sin squared of the angle round the back), which also covers the seat.
  - **Raised panels follow the surface they sit on:** the pocket flaps and cargo pockets are swept along the jacket's
    or leg's actual faces, through every corner of its outline, 6 to 7 mm proud and reaching 10 mm under. A first
    pass used boxes, which stood up to 1.6 cm off the curving surface and read as tabs.
  - **The gauntlet flares** from r 0.072 at the wrist to 0.078 at its open end, so it stays outside the 20-sided
    sleeve. At r 0.068 the sleeve's corners poked through its flat faces as a saw-tooth edge.
  - **Vents** are strips 1 mm off the round shell (its flat faces lie inside that), so they cannot z-fight. From
    straight behind they show as small notches on the crown, as the reference draws them.
  - **New material `M_CH_Skier_Buckle`** #AEB6C2 (a placeholder light grey) for the boot and strap buckles. The
    helmet has 5 submeshes (was 4), each boot cuff 3 (the power strap is goggle-frame dark; was 2), each boot shell 2.
  - **Body LOD0 is 4,269, under the story's 5k floor.** The floor was an estimate of what rounder sections would cost.
    Every section that facets at the chase camera was made rounder and every listed detail was added; more triangles
    would mostly smooth the flat facets that are the style. Waiting on the user's call (story 012): a rounder pass
    (shoulders, sleeves, jacket back) is a constants change and a rebuild.
  - **LOD2 is 1,262, above the about 1k asked.** Decimation protects material boundaries so the stripes stay straight,
    and the new drawcord rows and yoke hold the jacket at 233. LOD2 leaves out the pocket flaps and cargo pockets
    (under a pixel wherever LOD2 is shown); Seat, Hood, Nose, Trousers and ThumbTip keep higher ratios so no part
    shrinks more than 6 mm.
  - **Seat check, seat only:** a copy of the master with just the seat shell removed, rendered from behind in all 16
    poses; any pixel that is navy in the normal render and jacket in the copy is seat in front of the jacket. The
    story 007 version removed every trouser face, so it also counted the thighs that rightly pass in front of the
    jacket in the grabs.

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
| 012 forms (detail pass) | front | 0.9769 | 0.0057 | - | tolerance 0.90 / 0.03; new references drawn from the same table |
| 012 forms | side | 0.9571 | 0.0172 | - | |
| 012 forms | back | 0.9769 | 0.0057 | - | the new back view |
| 012 LOD0 (body 4,269) | front | 0.9769 | 0.0057 | - | at rest (rig in its rest pose) |
| 012 LOD0 | side | 0.9569 | 0.0172 | - | |
| 012 LOD0 | back | 0.9769 | 0.0057 | - | |
| 012 LOD1 (body 3,171) | front | 0.9759 | 0.0057 | - | |
| 012 LOD1 | side | 0.9573 | 0.0172 | - | |
| 012 LOD1 | back | 0.9759 | 0.0057 | - | |
| 012 LOD2 (body 1,262) | front | 0.9705 | 0.0086 | - | |
| 012 LOD2 | side | 0.9550 | 0.0172 | - | |
| 012 LOD2 | back | 0.9705 | 0.0086 | - | |

Story 012 checks (2026-10-27). Forms measured (`review/03_forms/measure.json`): height 1.7505, fingertip span 1.5306
(table 1.532), depth 0.3769, all within 0.1 percent. `validate.py` on LOW, SOCKETS, RIG_DEF with `--budget-tris 10000`
exits 0: 0 FAIL, 8 WARN (no UV layers, expected); triangles by LOD 5,365 / 3,171 / 1,262. Weights on all three body
LODs: 0 unweighted, at most 4 influences; the gauntlets classify with the cuffs (`06_rig.py`), so they bend across the
wrist with the sleeve end under them. Seat check, seat only (see Decisions), back three-quarter close-ups of all 16
clips: 0 pixels in every clip; the same check on the model before the seat fix finds the seat in all 16 (5,386 px,
275 in Stance), so the check can fail. With every trouser face removed instead, the only pixels left are the thighs
passing in front of the jacket's side in the five grabs (correct occlusion). Export: 16 takes, frames 0 to 30.
`roundtrip.py --rest-pose` exits 0 at height 1.7505; `09_roundtrip_check.py` exits 0: triangles 5,365 / 3,171 / 1,262
match the manifest, 20 bones, 16 clips, the 4 sockets on Foot_L/R and Hand_L/R.

Measured on the forms (`review/03_forms/measure.json`): height 1.75, fingertip span 1.531 (table 1.532),
depth 0.3745. The blockout's measured proportions are all within 1 percent of the table. One known
deviation: the shin cylinders tilt slightly (knee x 0.12 to hem x 0.13), so the trouser hem's lowest point
is 0.283 against 0.29 (7 mm, 0.4 percent of H).

Phase 4 checks (`review/04_topology/`): `validate.py` on LOW,SOCKETS with `--budget-tris 10000` exits 0: 0 FAIL, 12
warnings, all expected: no UV layer on the 8 LOW meshes (Phase 5) and no parent on the 4 sockets (parented to bones
in Phase 6). No doubles, zero-area faces, loose geometry, non-manifold edges or stray islands remain. validate counts
the unsuffixed props at LOD0 only: 3,677 / 2,139 / 1,081. The greyscale gameplay strip (`gameplay_strip_grey.png`)
shows LOD0, LOD1 and LOD2 reading the same at 80 px.

Phase 5 (`review/05_materials/`): material renders front, side, three-quarter, game camera and 80 px (plus a greyscale
80 px copy); every colour role reads, flat colours only.

Phase 6 (`review/06_rig/`): `validate.py` on LOW, SOCKETS, RIG_DEF with `--budget-tris 10000` exits 0: 0 FAIL, 8 WARN
(no UV layers, expected). On all three body LODs: 0 unweighted, 0 over 4 influences, 0 not normalised, 0 tiny
weights (`validate.json`, `weights.json`). Pose sheets `pose_sheet.png` and `pose_sheet_front/side/game.png` show all
nine clips with PR_Ski and PR_SkiPole attached at the sockets; `joints_sheet.png` has front and back three-quarter
close-ups of Tuck, Crouch, Brake, CarveLeft and Crash. Shoulders, elbows and knees hold their volume. **Fixed
(story 012, 2026-10-27):** navy patches of the seat showed through the lower jacket back. The rest pose from behind
showed them too, so the overlap predated rigging: the jacket's back ring has five points, and its flat chords cut
inside a seat drawn to the jacket's own size (the seat rings at z 0.88 and 0.95 were 3.4 and 1.0 mm outside the
jacket). Those two rings now sit 22 and 29.5 mm inside (`03_forms.py` SEAT). The analytic weight field moves
overlapping shells together, so no weight change was needed. Checked on back three-quarter close-ups of all 16
clips, rendered again with the trouser faces removed: no pixel that is trousers in front of jacket remains, except
the near thigh in front of the jacket's side in the five grabs (correct occlusion) and a 29 px sliver at the hem
edge in Brake. Body LOD0 2,865 tris (was 2,895: more of the seat is now buried and removed as interior faces).

Phase 8 lowest points per clip (m; the snow is -0.045; `review/06_rig/poses/lowest_points.json`):

| Clip | Body | Boot | Ski | Pole |
| --- | --- | --- | --- | --- |
| Stance | 0.2369 | -0.0000 | -0.0450 | 0.0044 |
| CarveLeft | 0.1322 | -0.0103 | -0.0405 | 0.0797 |
| CarveRight | 0.1322 | -0.0103 | -0.0405 | 0.0797 |
| Tuck | 0.2422 | -0.0000 | -0.0450 | 0.6418 |
| Crouch | 0.2210 | -0.0000 | -0.0450 | -0.0302 |
| Air | 0.4053 | 0.1777 | 0.0608 | 0.2649 |
| Brake | 0.1686 | -0.0010 | -0.0422 | -0.0076 |
| Pole | 0.2337 | -0.0000 | -0.0450 | -0.0743 |
| Crash | -0.0175 | -0.0226 | -0.0405 | 0.0499 |

No limb needed more reach than it has. Pole tips up to 3 cm into the snow in Crouch and Pole read as planted
poles. Air lifts the skis to 0.06.

Phase 9 (`review/09_export/`): the FBX holds exactly 9 takes, `Stance`, `CarveLeft`, `CarveRight`, `Tuck`, `Crouch`,
`Air`, `Brake`, `Pole`, `Crash`, each frames 0 to 30 (read back from the file). `roundtrip.py --rest-pose` exits 0 at
height 1.75. `09_roundtrip_check.py` exits 0: tris 3,677 / 2,139 / 1,081 match the manifest, 20 bones match, 9 clips at
1.0 s, sockets on Foot_L/R and Hand_L/R at their manifest positions, height 1.7500 at rest. Reimport renders:
`reimport_clay_rest.png`, `reimport_clay_CarveLeft.png`, `reimport_clay_Tuck.png`.

## Rebuild

```bash
cd art/source/CH_Skier
python3 ref/make_refs.py
S=../../../.claude/skills/blender-image-to-3d/scripts
$BLENDER_BIN -b --python $S/init_master.py -- --out CH_Skier_master.blend --name CH_Skier --height 1.75 \
  --ref-front ref/front.png --ref-right ref/side.png --subject-frac 0.948718 --ground-frac 0.051282 \
  --cell 2.0 --capsule 0.35 1.75 --cam-elev 33 --cam-az 185 --cam-dist 5.7 --cam-lens 26
$BLENDER_BIN -b --python build/02_blockout.py -- --master CH_Skier_master.blend
$BLENDER_BIN -b --python build/03_forms.py -- --master CH_Skier_master.blend
$BLENDER_BIN -b --python-exit-code 1 --python build/04_topology.py -- --master CH_Skier_master.blend
# per phase: D=review/02_blockout with C=BLOCKOUT (moved out of LOW by Phase 4), or D=review/03_forms with C=HIGH
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out $D \
  --collections $C --views front,side,threequarter --ref-cam 185 33 26 --mode clay --gameplay-px 300
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out $D \
  --collections $C --views front,side --mode silhouette
$BLENDER_BIN -b --python $S/world_gate.py -- --blend CH_Skier_master.blend --out $D --collections $C \
  --matte ref/front_matte.png --az 0 --m-per-px 0.0025 --axis-col 380 --ground-row 740 --name front
$BLENDER_BIN -b --python $S/world_gate.py -- --blend CH_Skier_master.blend --out $D --collections $C \
  --matte ref/side_matte.png --az 90 --m-per-px 0.0025 --axis-col 180 --ground-row 740 --name side
$BLENDER_BIN -b --python $S/world_gate.py -- --blend CH_Skier_master.blend --out $D --collections $C \
  --matte ref/back_matte.png --az 180 --m-per-px 0.0025 --axis-col 380 --ground-row 740 --name back
python3 $S/compose_review.py --ref ref/front_matte.png --render $D/silhouette_front.png \
  --out $D/compare_front.png --measure --gameplay-px 300   # and the same for side
# forms only: colour renders, head close-ups, sheets
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out review/03_forms \
  --collections HIGH --views front,side,threequarter --ref-cam 185 33 26 --mode material --gameplay-px 300 --samples 64
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out review/03_forms/head \
  --collections HIGH --views threequarter,threequarter_back --ref-cam 185 33 26 --mode material --samples 64 \
  --fit CH_Skier_Helmet_form,CH_Skier_Hood_form,CH_Skier_Jaw_form,CH_Skier_NeckWarmer_form
python3 build/review_sheet.py --dir review/03_forms --mode material --out review/03_forms/colour_sheet.png
python3 build/review_sheet.py --dir review/03_forms --mode clay --out review/03_forms/clay_sheet.png
# Phase 4 gates: D=review/04_topology; for L in 0 1 2, C=LOW_LOD$L,LOW_PROPS and O=$D/lod$L
$BLENDER_BIN -b --python $S/validate.py -- --blend CH_Skier_master.blend --collections LOW,SOCKETS \
  --out $D/validate.json --budget-tris 10000
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out $O --collections $C \
  --views front,side,threequarter --ref-cam 185 33 26 --mode clay --gameplay-px 300   # and again with --mode wire
# world_gate.py front, side and back as above with --collections $C --out $O (after Phase 6, on a copy with
#   the rig at rest); LOD0 also silhouette renders + compose_review
python3 build/review_sheet.py --dir $O --mode wire --out $D/wire_sheet_lod$L.png
python3 build/lod_review.py --dir $D --forms review/03_forms   # clay_compare, gameplay_strip_grey, lod_gates.json
# Phase 5, 6, 8, 9 (run in this order; 06 rebuilds the rig, so 08 must follow it)
$BLENDER_BIN -b --python-exit-code 1 --python build/05_materials.py -- --master CH_Skier_master.blend
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out review/05_materials \
  --collections LOW_LOD0,LOW_PROPS --views front,side,threequarter --ref-cam 185 33 26 --mode material \
  --gameplay-px 300 --samples 64 --res 768
$BLENDER_BIN -b --python-exit-code 1 --python build/06_rig.py -- --master CH_Skier_master.blend
$BLENDER_BIN -b --python $S/validate.py -- --blend CH_Skier_master.blend --collections LOW,SOCKETS,RIG_DEF \
  --out review/06_rig/validate.json --budget-tris 10000
$BLENDER_BIN -b --python-exit-code 1 --python build/08_poses.py -- --master CH_Skier_master.blend
ALL=Air,Brake,CarveLeft,CarveRight,Crash,Crouch,GrabJapan,GrabMute,GrabSafety,GrabTail,GrabTip,Pole,RaceCarveLeft,RaceCarveRight,Stance,Tuck
$BLENDER_BIN -b --python build/pose_sheet.py -- --master CH_Skier_master.blend --out review/06_rig/poses --closeup $ALL
python3 build/compose_pose_sheet.py --dir review/06_rig/poses --out review/06_rig
# seat check (story 012): the same close-ups of a copy with only the seat removed; exits 0 on PASS only
$BLENDER_BIN -b --python-exit-code 1 --python build/seat_only_copy.py -- CH_Skier_master.blend review/06_rig/no_seat.blend
$BLENDER_BIN -b --python build/pose_sheet.py -- --master review/06_rig/no_seat.blend --out review/06_rig/poses_no_seat \
  --closeup $ALL
python3 build/seat_check.py --with review/06_rig/poses --without review/06_rig/poses_no_seat --out review/06_rig/seat_check.json
$BLENDER_BIN -b --python-exit-code 1 --python build/09_export.py
E=../../../Assets/Art/Characters/CH_Skier
$BLENDER_BIN -b --python $S/roundtrip.py -- --file $E/CH_Skier.fbx --out review/09_export --expect-height 1.75 --rest-pose
$BLENDER_BIN -b --python build/09_roundtrip_check.py -- --dir $E --out review/09_export
```
