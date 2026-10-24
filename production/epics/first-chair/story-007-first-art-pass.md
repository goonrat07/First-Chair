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
  - Equipment colours and tree instancing, done 2026-10-24 (sleep mode), not yet seen in a build or the editor.
    PR_SkiPole's shaft is graphite now (8.5:1 luminance contrast on sunlit snow; the old light grey was 1.1:1), and
    its grip and basket are red like the gloves. Re-exported from `05_materials.py` through `export_delivery.py`;
    the round trip is clean. The skis' orange topsheet already reads on snow in the 4K build crops, so it stays.
    `EnvironmentModelImport` turns GPU instancing on for every material imported under `Assets/Art/Environment/`,
    and `EnvironmentImportTests` checks the conifer's in CI. The URP asset has the SRP Batcher on, which takes those
    materials first. Unity 6's GPU Resident Drawer (URP asset plus Forward+) would instance the trees through the
    BatchRendererGroup, but it is a renderer change to try on the PC with the profiler.
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
