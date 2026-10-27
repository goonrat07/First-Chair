# Story 012: A skier detail pass for the chase camera

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Review
> **Layer**: Presentation (art)
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/game-brief.md` ("First real art toward the target look: skier ...") and the skier's asset brief,
`art/source/CH_Skier/asset-brief.md`.
**Requirement**: the user, 2026-10-26: "the character model needs to be a little more detailed (not steep level
realism but just a slightly higher level of detail while maintaining the visual style of the current character to
account for the fact that the character will take up more pixels of the screen)".

Decisions (user, 2026-10-26): no backpack on the base skier (a pack becomes a customizable item later, once the
basics are in); camera first (story 011), then this story, then procedural animation (story 013).
Decisions (user, 2026-10-27, on approving this story): start building straight away, seat fix first; the skis and
poles get detail too (bindings, grips and baskets).

Why now: the skier was made for the old far camera, about 80 px tall on screen. The chase camera (story 011) shows
the back and the right side at 35 to 46 percent of the screen's height in a fast tuck, about 250 to 330 px at 720p
and 500 to 650 px at 1440p. At that size the cloud rides show:
- the navy trouser seat poking through the lower back of the jacket, at rest and in every pose (logged as open in
  the asset brief since the rig);
- faceted shoulders, sleeves and legs (the sleeves are 16-sided);
- a plain yellow box of a jacket back, plain boots, and boots that seem to stand on bare skis (the bindings are two
  grey blocks the boots hide).

The skier today: 3,677 triangles (body 2,895 at LOD0, 2,139 at LOD1, 1,081 at LOD2; helmet with goggles 374; each
boot 204), 20 bones, 4 sockets, 16 pose clips. The project's budget for the player skier is 5k to 10k at LOD0.

## Design

**What stays** (approved by the user, 2026-10-20): the proportions (1.75 m in boots), the palette (yellow jacket
#F2B705, navy #22304A, red #D7263D and the rest of the asset brief's table), the helmet and goggles, the stripes
(stripes only on the jacket, no pattern), flat colours (no textures or normal maps), the 20-bone rig and its socket
names, and the 16 pose clips with their names (the Unity side's pose weights and the animation contract depend on
them). A cleaner, rounder version of the same figure, not realism.

**What changes**, in the order the chase camera sees it:
1. **The seat stays hidden.** The trouser faces under the jacket hem are tucked in (or removed where nothing can
   ever see them), the hem keeps clearance over the seat in every pose, and the seat vertices just under the hem
   follow the jacket's weights.
2. **Rounder forms.** More segments around the torso, shoulders, sleeves, legs and helmet, spent where the outline
   facets at chase-camera size; shoulders, elbows and knees bend more cleanly.
3. **The jacket's back and sides:** a hem drawcord band and a slightly dropped back hem (which also covers the
   seat), a yoke line across the shoulder blades as a navy stripe (still stripes only), and two hand-pocket flaps
   on the sides as raised panels.
4. **The trousers:** a cargo pocket on each outer thigh and a gaiter flare where the hem meets the boot.
5. **Mittens:** a gauntlet cuff over the sleeve cuff.
6. **Helmet:** a few vent grooves on top and a strap buckle at the back of the goggle strap (seen from behind).
7. **Boots:** two buckles and a power strap on each shell.
8. **Skis and poles** (PR_Ski, PR_SkiPole): the two grey binding blocks, hidden under the boots from the chase
   camera, become shaped bindings: a toe piece with a sloped nose and jaw, a heel piece with a lever and brake arms
   that stand out behind the boot, each with a light accent. The pole's grip gets a finger ridge and its basket a
   ringed shape. Same flat colours, one LOD each.

All new detail is geometry with flat material colours (material splits), as now. **Budget:** body 5k to 8k
triangles at LOD0, 2k to 4k at LOD1, about 1k at LOD2; helmet with goggles, each boot, each ski and each pole 100 to
600.

**The review camera.** The skill's First Chair notes (`.claude/skills/blender-image-to-3d/FIRST-CHAIR.md`) still
review assets from the old far camera (az 30, el 45, 18 m, an 80 px strip). They change to the chase camera
(from behind and above, about 3 to 5 m, 26 mm, a strip about 300 px tall), so the gates judge the skier as players
see it. A back reference view joins the front and side ones (`ref/make_refs.py`), so the back is designed, not
inferred.

## As built (2026-10-27)

- **The seat** (fbca6b6): the trouser faces under the hem are tucked in and the hem drops at the back. An in-repo check
  (`build/seat_only_copy.py`, `build/seat_check.py`) renders all 16 clips from behind with and without the seat; it finds
  0 seat pixels in every clip, and 5,386 on the model from before the fix, so it can fail.
- **The detail pass** (0d2953f): rounder sections and every listed detail, all flat colour; the pockets and flaps are
  panels swept along the surface they sit on. Silhouettes pass front, side and back at every LOD (IoU 0.955 to 0.977,
  bands within 0.0172); height 1.7505 and span within 0.1 percent; `validate.py --budget-tris 10000` 0 FAIL; weights
  0 unweighted, at most 4 influences; the FBX round trip passes (20 bones, 16 clips, 4 sockets). Reviews now use the
  chase camera and a back reference view (`FIRST-CHAIR.md`, `ref/make_refs.py`).
- **Skis and poles** (729473d): toe and heel pieces with a lever, brake arms and light accents (PR_Ski 456 tris, was
  212); the pole's finger ridge, rounder knob and ringed basket (PR_SkiPole 400, was 192). Sizes, origins and sockets
  unchanged; gates and round trips pass.
- **Full detail on the player** (310c30e): Unity's automatic LOD group would switch the rider to LOD1 under 25 percent
  of the screen's height, so `SkierView` forces LOD0 on the player's skier and logs the heights Unity picked.
- **In the build** (run 40443808319, commit bf20bd7, ridden 2026-10-27; before: run 40423919050, a5091fb): at rest,
  carving, in a fast tuck and in the air with three grabs, the seat shows in every before frame and in no after frame;
  the stripes, pockets, gauntlets and helmet details read, and each heel piece shows behind its boot
  (`production/qa/evidence/story-012-skier-detail-pass/evidence.md`).
- **Not met as written: the body's budget.** LOD0 is 4,269, under the 5k floor: every section that faceted at the chase
  camera got more sides and every detail is in, and more would mostly smooth the flat facets that are the style. LOD2
  is 1,262, above about 1k, because decimation keeps the stripes' boundaries. Both are in the asset brief; the first
  waits on the user's call (`production/sleep-mode/questions.md`).
- **Found in passing:** in both builds the ski tracks were drawn over the back half of each ski once moving (the
  ribbon sits 15 mm off the snow, the ski's top 17 mm, and the ribbon was laid from under the boot). Not this story's;
  fixed separately so tracks start at the ski's trailing end, seen in story 013's build.

## Acceptance Criteria

- [x] The asset brief records the pass: the new parts, the budget, the back reference view and the decisions.
- [x] Silhouette gates pass front, side and back against the reference views; proportions within 2 percent.
- [x] The seat never shows through the jacket: back three-quarter close-ups of all 16 poses, with a pixel check
  for navy inside the jacket's outline.
- [ ] Budget: body LOD0 5k to 8k triangles, LOD1 2k to 4k, LOD2 about 1k; helmet with goggles, each boot, each ski
  and each pole 100 to 600; `validate.py --budget-tris 10000` passes; no unweighted vertices or more than 4
  influences. *Not met as written: body LOD0 4,269 and LOD2 1,262 (As built); the rest holds (helmet 584, each boot
  256, each ski 456, each pole 400).*
- [x] The skis' bindings and the poles' grips and baskets read from the chase camera; the ski and pole sockets, sizes
  and origins are unchanged, and their round-trip checks pass.
- [x] The rig, sockets and 16 clips keep their names; the FBX round-trip check passes (triangles per LOD,
  20 bones, 16 clips, height 1.75 m); the Unity EditMode tests still pass.
- [x] In a build, from the chase camera, at rest, carving, in a fast tuck and in the air with a grab: the new
  details read, the style and palette match the old skier, the bindings show behind the boots, and the seat stays
  hidden (retained before-and-after screenshots).
- [x] No change to riding or the camera.

## Out of Scope

- Procedural animation (story 013) and new poses.
- Customization: outfits, colours, a backpack.
- Textures, normal maps and an ambient occlusion bake (URP Lit ignores vertex colours; it would need its own shader).
- The snowboarder.

## Test Evidence

**Story Type**: Visual/Feel. Gate renders and JSON in `art/source/CH_Skier/review/`; retained build screenshots in
`production/qa/evidence/story-012-skier-detail-pass/`; sign-off is the user's, on the PC.

## Dependencies

- Depends on: Story 007 (the skier, skis and poles), Story 011 (the chase camera sets the size they are seen at).
- Unlocks: Story 013 (procedural animation, judged on the detailed skier).
