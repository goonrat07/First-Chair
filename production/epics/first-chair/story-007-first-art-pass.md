# Story 007: First art pass toward the target look

> **Epic**: First Chair MVP (base skiing system)
> **Status**: Ready
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: [set by /dev-story when implementation begins]

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 7`: "First real art toward the target look: skier, skis, poles, trees, snow trails and lighting."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 7; Art direction: "realistic-leaning light and snow with clean, low-detail models and flat colors, after Grand Mountain Adventure 2"):*

- [ ] A rigged skier (`CH_Skier`) is built with the blender-image-to-3d skill within the First Chair budget (5k to 10k tris at LOD0). It wears `PR_Ski` and `PR_SkiPole` on its foot and hand sockets.
- [ ] The skier's pose is driven from sim state (lean from edge angle, crouch from tuck and pop load), with authored clips for tuck, grabs and crashes. The pose never feeds back into the sim.
- [ ] Glade conifers come from a new tree skill in the same gated format: 300 to 1,500 tris, a flat far version (billboard), and GPU instancing in Unity.
- [ ] Snow trails: a render texture is stamped where skis touch, and a URP snow shader reads it as bump and detail. The trails are display only.
- [ ] A bluebird-day lighting preset gives soft shadows, blue-tinted shadows on snow and snow sparkle.
- [ ] Equipment colours are chosen against snow, so the poles read at the follow camera. They currently almost vanish (`.claude/skills/blender-image-to-3d/FIRST-CHAIR.md`, Lessons).
- [ ] Retained screenshots from the follow camera, plus sign-off, are in `production/qa/evidence/`.

---

## Implementation Notes

*From `docs/research/engine-and-pipeline.md` §4 and §6, `.claude/rules/blender-assets.md`, and `.claude/skills/blender-image-to-3d/FIRST-CHAIR.md`:*

- Skis and poles already exist (`Assets/Art/Equipment/`). This story recolours them if needed; it doesn't rebuild them.
- The skier is the first rigged asset. Its FBX keeps Blender's default axes on the armature (the documented Unity export rule). Check the first import in the Unity editor.
- An art bible (`/art-bible`) before this story would settle the palette. It isn't required at `minimal`.
- **Open (2026-10-24):** CH_Skier's trouser seat shows through the lower jacket back in close back views
  (`art/source/CH_Skier/asset-brief.md`); not visible at the follow camera. The skis' and poles' socket orientation
  in Unity is tracked in story 003's playtest feedback.
- **Next (user, 2026-10-24):** grab and trick poses for CH_Skier (grabs, spins and flips in the air), and this
  story's open criteria: stamped snow trails with a URP snow shader, blue shadows and sparkle, the tree's billboard
  with GPU instancing, and equipment colours that read on snow.
  - Spins and flips, done 2026-10-24 (sleep mode), not verified in the editor. Spins already showed, because the
    sim's heading turns with the spin. Flips now show too: `RiderPose.FlipDegrees` carries the sim's flip while in
    the air (zero on the snow, so a landing never unwinds one), and `RiderPlacement` pitches the rider by it about
    a point 0.9 m above the feet (`RiderViewSettings.flipPivotHeight`). The follow camera aims at the sim's position,
    not the view's root, so a flip doesn't bob it. Tests: `RiderPoseTests` (engine-free, run here) and
    `RiderPlacementTests` (Unity math, run in CI's EditMode job).
  - Grab poses, done 2026-10-24 (sleep mode), not verified in the editor. CH_Skier has five more clips, one per
    sim grab: GrabMute (left hand across onto the right ski), GrabTip (a pike, right hand near the right tip),
    GrabTail (right hand back on the right tail), GrabJapan (left hand behind the left boot, trunk turned) and
    GrabSafety (right hand on the right ski's outside edge). `08_poses.py` solves them like the other poses. Their
    trunk angles came from a grid search for zero reach error with the knees clear of the chest and head. In the
    air `SkierPoseWeights` shows the held grab's pose instead of Air. Evidence:
    `production/qa/evidence/story-007-first-art-pass/2026-10-24-grab-poses-*.png`. The skier's grab names follow
    the sim's `GrabKind` names, not real ski-trick definitions; the user can rename or reshape any of them.
    Flips, spins and grabs were seen in build 40056930596 (evidence `2026-10-24-build-40056930596-*.png`).
  - Equipment colours and tree instancing, done 2026-10-24 (sleep mode), not yet seen in a build or the editor.
    PR_SkiPole's shaft is graphite now (8.5:1 luminance contrast on sunlit snow; the old light grey was 1.1:1), and
    its grip and basket are red like the gloves. Re-exported from `05_materials.py` through `export_delivery.py`;
    the round trip is clean. The skis' orange topsheet already reads on snow in the 4K build crops, so it stays.
    `EnvironmentModelImport` turns GPU instancing on for every material imported under `Assets/Art/Environment/`,
    and `EnvironmentImportTests` checks the conifer's in CI. The URP asset has the SRP Batcher on, which takes those
    materials first. Unity 6's GPU Resident Drawer (URP asset plus Forward+) would instance the trees through the
    BatchRendererGroup, but it is a renderer change to try on the PC with the profiler.
  - Tree billboard, done 2026-10-24 (sleep mode), not verified in the editor. LOD0 is rendered front and side into
    a 512 x 1024 alpha atlas. Its gate is IoU 0.993 against LOD0's silhouette, with 0.9 the minimum. Two crossed
    quads (4 tris) use an alpha-clip URP material and are added by the scene builder as each tree's last LOD.
    Details are in `art/source/EN_Conifer/asset-brief.md` ("Billboard (LOD2)"). Evidence:
    `production/qa/evidence/story-007-first-art-pass/2026-10-24-tree-billboard-atlas-on-snow-blue.png`.
  - Sparkle, done 2026-10-24 (sleep mode), not yet seen in a build or the editor. Each snow type's texture has a few
    2 x 2 texel crystals: 0.6% of groomed, 1% of powder, 1.2% of ice, none in slush. They are very smooth (0.95)
    and tilted 25 to 45 degrees in random directions, so each glints only from some angles as the rider moves. The
    terrain layers read smoothness from the albedo's alpha (`TerrainLayerSmoothnessSource.DiffuseAlphaChannel`),
    still through URP's own Terrain Lit shader. Tests: 4 more in `SnowSurfaceTests`; a mutation with untilted
    crystals was caught.
  - Still open: the stamped snow trails (a render texture stamped where the skis touch) and a URP snow shader that
    reads it as bump and detail. They need a custom terrain shader, or Shader Graph, written and checked in the editor:
    a mistake there turns the whole slope magenta in the next build. That is PC work (sleep mode, 2026-10-24).
- **Playtest notes (user, 2026-10-24, build 40060424205):**
  - [x] Fix the Japan and Tail grabs: the knees look bent backward and the torso doesn't follow the legs. The
    cause: their specs put the feet 10 cm under an upright trunk, so each leg folded flat (170 degrees at the knee)
    with the shin pointing straight back, which reads as a backward knee from the game camera. Searched again
    (`08_poses.py`): the knee now bends about 127 degrees, the shin points down, and the trunk leans 40 degrees forward
    with the legs, as in Mute and Tip. The hand still reaches the ski with zero error, and the round trip passes.
    Evidence: `production/qa/evidence/story-007-first-art-pass/2026-10-24-grab-tail-japan-refolded.png`.
  - [x] The slope doesn't read as a slope: give it contours and better lighting. Contours done in the terrain
    (`tools/terrain/make_test_slope.py`, `ROLLERS`). Four rollers cross the whole slope, a bench then a rollover or a
    steep step then a bench, about 2 m deep over 40 to 50 m. Each swaps gradient between its halves (8 to 24 degrees),
    so the slope never rises and drops no further overall. They keep off the start, the pitch, the kickers' ramps and
    landings and the roll-out. Even a full tuck at 120 km/h stays on the snow over them. Lighting is still to do.
    Seen in build 40119190896, the bands are faint: about 4 levels of 255 down the screen. Neutral tonemapping
    compresses the bright snow, and the sun, from downhill and to the right, lights steep and flat bands nearly alike.
    Making the slope's shape read needs the lighting pass. Options: a sun nearer the fall line or lower, less
    ambient, the snow out of the tonemapper's shoulder, or relief in the snow itself as GMA2 has. Cross-slope bumps
    off the groomer would also catch the side light.
    Lighting pass done (2026-10-26, sleep mode): the light is data now (`LightingSettings`, applied by `LightingRig`,
    live in the Feel Lab's Light tab), and the defaults moved the sun behind and to the right of the camera, 35 degrees
    up (heading -30), so steeper snow faces further from it and reads darker; sun 2.2, sky light 0.8, exposure +0.3,
    contrast 10. Variants were compared in build 40281825872 from one held view over the first rollers (the run
    restarted in place with gravity near zero, so every shot matched): the brightness range down the screen rose from
    12 to 26 levels of 255, the snow as bright (mean 203 against 207). The sun ahead lit steep and flat snow nearly
    alike, and exposure alone barely helped (12 to 13). Lower ambient (0.6 to 0.7) reads more (26 to 33) but greys the
    snow. Evidence: `production/qa/evidence/story-007-first-art-pass/2026-10-26-build-40281825872-lighting-before-after.png`.
    Not verified in the editor. On a PC that built the scene since the Light tab landed (86bc33a), reset or delete
    `Assets/Data/Riding/LightingSettings.asset` to pick up the new defaults: the scene builder keeps an existing asset.
  - [x] Trees maybe a little sparse. Dense forest beyond the runs: `forestWest` (between the map's west edge and the
    western tree line) and `forestEast` (east of the bowl and glade), 459 more trees (938 in all). No line through the
    tree lines or the glade is closed. Seen in build 40120266835: a left traverse from the start runs into the western
    tree line, with the forest strip behind it up to the map's edge, all snow-laden conifers with long blue shadows.
    Evidence: `production/qa/evidence/story-007-first-art-pass/2026-10-24-build-40120266835-west-tree-line-and-forest.png`.
  - [ ] Tracks should build up where skiers pass, as in GMA2, not sit as streaks on a static texture. This belongs
    to the custom snow shader.
    Step one, sleep mode 2026-10-26 (commit 716391a): each ski lays a ribbon on the snow that stays (`TrackRibbon`,
    `SkiTrack`), kept up to 12 km of track a ski, across runs and resets, and the live and old track materials are
    translucent so crossings show darker. Seen in build 40278892710: after a restart at the top, the first run's
    track is still on the snow and the second run rides over it. Evidence:
    `production/qa/evidence/story-007-first-art-pass/2026-10-26-build-40278892710-tracks-stay-across-runs.png`.
    Still open: grooves in the snow itself (a render texture stamped where the skis touch, read by a snow shader as
    relief), which need the editor to write and check.
- **Snow shaders (user, 2026-10-23):** GMA2's semi-realistic look comes largely from its own snow and terrain
  shaders. Build them here, after a rough gameplay demo exists: the snow trail and sparkle criteria above, plus
  whatever else gets the snow to read like GMA2's (soft blue shading, sparkle, deformable tracks).

---

## Out of Scope

- Snowboard and its rider: out of the MVP (brief).
- Crowds, buildings and lifts: later design.

---

## Files

- `art/source/CH_Skier/` (brief, build scripts, master `.blend`)
- `.claude/skills/tree-kit/` (new skill) and `art/source/EN_Conifer/`
- `Assets/Art/Characters/CH_Skier/`, `Assets/Art/Environment/EN_Conifer/`
- `Assets/Shaders/Snow/` (trail render texture, snow shader)
- `Assets/Settings/Lighting/` (bluebird preset)
- `production/qa/evidence/story-007-first-art-pass-evidence.md`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**: a retained screenshot in `production/qa/evidence/` and sign-off in `production/qa/evidence/story-007-first-art-pass-evidence.md`. These are not waived at `qa.level: minimal`.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/first-chair/story-003-test-slope-unity-view.md` (scene, camera, rider view)
- Unlocks: None
