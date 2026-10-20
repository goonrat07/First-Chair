# CH_Skier asset brief

Status: Phase 0 and Phase 2 (blockout) done, waiting for the user's review of the look before forms.
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
             ellipsoids), projected exactly, so front and side agree. Mattes drawn at 4x, box-filtered,
             thresholded at 50 percent (FIRST-CHAIR.md, Lessons).
           Look source: art/reference/style-notes-gma2.md (our notes on the user's GMA2 screenshots). The
             screenshots are copyrighted, not in the repo, and were not used directly.
           Game camera (FIRST-CHAIR.md estimate): az 30, el 45, 35 mm, 18 m. Review renders call it the
             "ref" view (--ref-cam 30 45 35), with an 80 px gameplay strip.
SCALE      1.75 m from sole to helmet top, in boots (user brief: an adult about 1.75 m tall in boots).
           1 unit = 1 m, Z up, faces -Y, origin on the ground between the feet. Every other dimension comes
           from our proportion table (see INFERRED).
PROPORTIONS checked at blockout (5 percent) and forms (2 percent); H = 1.75 m
            1 helmet width / H           0.149 (0.26 m; depth 0.31 m), about 15 percent over a real 0.22 m helmet
            2 helmet rim to top / H      0.126 (rim at 1.53 m, top 1.75 m)
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
           14 depth / H                  0.211 (0.37 m, boot toe -0.19 to helmet back +0.18)
PARTS      in construction and overlap order; (S) = separate object in the export
           1 body, one skinned mesh, deforming:
             trouser legs and seat (baggy, flat hem over the boot cuff); jacket body with a flared hem;
             hood collar; sleeves (baggy, flat-ended) with dark cuffs; mittens with a forward thumb; neck
             warmer. The goggles and neck warmer cover the whole face, so no face is modelled. From Phase 3
             the master keeps a simple head and neck under the gear, per the skill.
           2 helmet (S): shell, lighter top panel, goggle strap. Rigid, one bone (head).
           3 goggles (S): dark frame and yellow lens. Rigid, head bone; may merge into the helmet object at
             export (same bone, same budget line).
           4 boot L and R (S): lower shell rigid to the foot bone, cuff rigid to the shin bone, hinged at
             the ankle rivet (how real ski boots are built), rust band on the cuff. The trouser hem
             overlaps the cuff.
           5 not in this asset: PR_Ski and PR_SkiPole, attached at the sockets below.
           Surface detail for forms and materials, not in the blockout: centre zip, two short chest stripes,
           one stripe down the outside of each upper sleeve, darker olive knee patches (separate faceted
           pieces on the trousers), goggle strap.
SILHOUETTE 1 big rounded helmet with a wide yellow goggle
           2 boxy oversized jacket with a hood collar and a flared hem
           3 baggy straight trousers over chunky dark boots
           4 chunky yellow mittens
           5 in game: the athletic ski stance from the rig (knees forward, hips back, hands low and forward)
MATERIALS  one material per colour role, flat colour, no normal or detail maps; AO bake later (FIRST-CHAIR.md)
           jacket          woven cloth, roughness 0.7 to 0.9    #D4512C (notes)
           stripes, zip    cloth / tape                          #F2F2EE *
           cuffs           elastic cloth                         #2E2F36 *
           mittens         padded cloth                          #E3CF3A (notes)
           trousers        woven cloth, 0.7 to 0.9               #C3B45C (notes)
           knee patches    reinforced cloth                      #8E8440 *
           boots           plastic shell, 0.4 to 0.6             #3C434C *
           boot band       plastic or strap                      #B5562A *
           helmet          plastic shell, 0.4 to 0.6             #8A929C *, top panel #B9C0C8 *
           goggle frame    plastic, strap                        #24262B *
           goggle lens     glossy lens, 0.1 to 0.2               #F0C419 *
           neck warmer     fleece, 0.8 to 1.0                    #26304A *
           * Placeholder hex values for colours the notes name without one, for the art-director to set.
             Hex values marked (notes) come from the notes. Equipment colours still need checking against
             snow (story 007).
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
           - helmet: centre (0, 0.01, 1.60), radii 0.13 / 0.155 / 0.15, rim cut at 1.53, top panel above 1.69,
             about 15 percent oversized from a typical 0.22 x 0.28 m adult helmet
           - goggles 0.22 x 0.06 x 0.085 at z 1.60, front at y -0.165; lens 0.19 x 0.06; strap z 1.578 to 1.622
           - neck warmer 0.17 x 0.19 x 0.14 (z 1.42 to 1.56); hood collar 0.30 x 0.26 x 0.12 (z 1.40 to 1.52)
           - jacket 0.46 x 0.30 x 0.62 (z 0.82 to 1.44); hem flare 0.48 x 0.32 x 0.12; shoulder caps r 0.085
           - shoulder joint (0.18, 0.02, 1.36); upper arm 0.29, forearm 0.26; sleeve radii 0.085 / 0.075 /
             0.066; the sleeve ends 0.03 before the wrist; cuff r 0.06, from 0.04 before to 0.02 past the wrist
           - mitten 0.19 x 0.12 x 0.085, about 15 percent oversized from a 0.19 x 0.09 m hand plus padding;
             thumb r 0.025 pointing forward
           - pelvis 0.38 x 0.28 x 0.18 at z 0.86 (crotch 0.77); hip joint (0.10, 0.03, 0.88); knee (0.12, 0,
             0.50); thigh radii 0.105 / 0.095; shin radii 0.095 / 0.10; trouser hem z 0.29
           - knee patches 0.13 x 0.15 ellipses on the knee front
           - boots: foot 0.115 x 0.32 x 0.13 (boot centre y -0.03), shaft 0.12 x 0.16 x 0.30 (top 0.34),
             rust band 0.13 x 0.17 x 0.06 (z 0.23 to 0.29), ankle joint z 0.11; boot centres 0.26 apart
           - socket positions above
           - the back: no back view, so the back is the side view's depth with a symmetric jacket and hood
             collar; the hood shape is a collar block until forms
           - the face is fully covered (goggles and neck warmer), so no face is modelled
           - boot construction (shell and cuff on a hinge) and a vertical boot cuff in the rest pose; real
             boots have about 13 deg of built-in forward lean, which comes from the rig
           - colours marked * above
TARGET     Unity 6.3 LTS (URP), FBX (--engine unity; rigged, so Blender's default armature axes are kept per
           FIRST-CHAIR.md). Budget (FIRST-CHAIR.md): body 5k to 10k tris at LOD0, 2k to 4k at LOD1, about 1k
           at LOD2; helmet with goggles and each boot 100 to 600 tris, one LOD. Game camera estimate az 30,
           el 45, 18 m, 35 mm; skier about 80 px tall on screen.
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

## Gate results

| Phase | View | world_gate IoU | Band widths (max abs) | compose IoU | Notes |
| --- | --- | --- | --- | --- | --- |
| 02 blockout | front | 0.9945 | 0.0057 | 0.987 | tolerance 0.85 / 0.05 |
| 02 blockout | side | 0.9886 | 0.0029 | 0.983 | tolerance 0.85 / 0.05 |

Measured on the blockout (`review/02_blockout/measure.json`): height 1.75, fingertip span 1.531 (table
1.532), depth 0.37, shoulder top 1.445, shoulder width 0.53, jacket hem 0.82, helmet width 0.26, rim
1.53, seat 0.38, boot length 0.32. All proportions are within 1 percent. One known deviation: the shin
cylinders tilt slightly (knee x 0.12 to hem x 0.13), so the trouser hem's lowest point is 0.283 against
0.29 (7 mm, 0.4 percent of H).

## Rebuild

```bash
cd art/source/CH_Skier
python3 ref/make_refs.py
S=../../../.claude/skills/blender-image-to-3d/scripts
$BLENDER_BIN -b --python $S/init_master.py -- --out CH_Skier_master.blend --name CH_Skier --height 1.75 \
  --ref-front ref/front.png --ref-right ref/side.png --subject-frac 0.948718 --ground-frac 0.051282 \
  --cell 2.0 --capsule 0.35 1.75 --cam-elev 45 --cam-az 30 --cam-dist 18 --cam-lens 35
$BLENDER_BIN -b --python build/02_blockout.py -- --master CH_Skier_master.blend
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out review/02_blockout \
  --collections LOW --views front,side,threequarter --ref-cam 30 45 35 --mode clay --gameplay-px 80
$BLENDER_BIN -b --python $S/review_render.py -- --blend CH_Skier_master.blend --out review/02_blockout \
  --collections LOW --views front,side --mode silhouette
$BLENDER_BIN -b --python $S/world_gate.py -- --blend CH_Skier_master.blend --out review/02_blockout \
  --matte ref/front_matte.png --az 0 --m-per-px 0.0025 --axis-col 380 --ground-row 740 --name front
$BLENDER_BIN -b --python $S/world_gate.py -- --blend CH_Skier_master.blend --out review/02_blockout \
  --matte ref/side_matte.png --az 90 --m-per-px 0.0025 --axis-col 180 --ground-row 740 --name side
python3 $S/compose_review.py --ref ref/front_matte.png --render review/02_blockout/silhouette_front.png \
  --out review/02_blockout/compare_front.png --measure --gameplay-px 80   # and the same for side
```
