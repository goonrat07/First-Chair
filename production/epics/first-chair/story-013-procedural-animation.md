# Story 013: Procedural animation on top of the key poses

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Presentation
> **Type**: Logic + Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/game-brief.md` (MVP feature 7, "First real art toward the target look: skier ..."; story 007's "The
skier's pose is driven from sim state").
**Requirement**: the user, 2026-10-26: after the chase camera (story 011) and the skier detail pass (story 012),
procedural animation layered on top of the key poses: the legs on the snow (IK), lean and angulation, springs, and
pole plants. Restated 2026-10-27 when starting sleep mode ("do the animation with the procedural generation
aspects").

Today the skier blends 16 authored clips by weights from sim state (`SkierPoseWeights`), placed as one rigid body on
the snow under the sim position (`RiderPlacement`). At the chase camera's distance that shows as: skis that float or
sink on rollers and kicker lips because the body follows one snow normal; the same carve lean at 20 and at 60 km/h;
a body that never reacts to landings, bumps or edge changes; and arms that hold still except in the Pole clip.

Written by Claude in sleep mode (the user asked for this work); the design choices are logged under Decisions and
are the user's to change.

## Design

Presentation only, like every view: the sim is untouched and nothing goes back to it. Each frame, after the clip
blend, four layers adjust the skier's bones, in this order. All of their math is pure C# in `Riding/Core`
(`noEngineReferences`), tested in EditMode; the Unity side reads bones and writes rotations. Every value is a field
on `RiderViewSettings`, so the Feel Lab's Effects tab tunes it live, and each layer has a weight (0 turns it off).

1. **Lean with the turn.** Physics leans a rider into a turn until gravity balances the turn's pull: tan(lean) =
   speed x turn rate / g. The carve clips hold one lean (their hips roll about 22 degrees). The layer rolls the whole
   skier about the line where the skis meet the snow by the difference between the physical lean and the lean the
   blended clips already show, clamped, through a spring, so a fast carve leans further than a slow one and the
   transition swings through upright. **Angulation:** the chest and head roll back by part of that extra lean, so
   the upper body stays quieter than the legs, as real skiers do.
2. **Springs.** The body's own acceleration (from frame to frame, in the skier's frame) drives damped springs: a
   landing or a bump compresses the hips (the leg layer keeps the feet down, so the knees bend), braking pitches the
   upper body forward and speeding up pitches it back a little, and an edge change sways it across. Each spring has
   a frequency, a damping ratio, a gain and a limit. The integration is exact for any frame time (frame-rate
   independent, tested like story 011's blur).
3. **Legs on the snow.** Each foot looks at the snow under its boot: the terrain height and normal there (the same
   heights the sim rides, `TerrainHeights`/the heightmap). A two-bone IK solve (thigh, shin; the knee stays in the
   plane the clip bent it in) puts the sole on the snow, and the ski pitches to the snow along its length. When a
   foot cannot reach, the hips come down by what it lacks, up to a limit, and the other leg bends. Off the snow (air,
   crash, the chairlift) the layer fades out.
4. **Pole plants.** At the start of each turn on the snow (an edge change at riding speed, not in a tuck, the air, a
   crash or on the lift), the new turn's inside pole is planted: the hand swings forward and down, the tip touches
   the snow beside and ahead of the boot, stays there while the skier passes it, then the arm swings back into the
   clip. Two-bone IK on the arm (upper arm, forearm) places the hand so the pole, held rigidly in it, reaches the
   tip point. The timing (reach, planted, release) and reach distances are settings.

## Acceptance Criteria

- [ ] Core math with EditMode tests: lean from speed and turn rate (zero when straight, sign follows the turn,
  clamped); the extra lean over the clips' own; a damped spring that converges, does not overshoot when critically
  damped and gives the same result at 30, 60 and 144 frames a second; a two-bone IK that keeps the bone lengths,
  reaches a reachable target, straightens toward an unreachable one and bends in the hinted plane; the foot
  correction and the hip drop; the pole plant timing (starts on an edge change at speed, not while tucked, airborne,
  crashed or paused; one plant per turn; the new turn's inside side).
- [ ] Unity: the four layers run after the clip blend in `SkierView`, each with a weight in `RiderViewSettings`
  (tooltips, ranges, shown in the Feel Lab's Effects tab); with every weight at 0 the skier is exactly as before.
- [ ] The Unity EditMode tests pass in CI and the Windows and Linux players build.
- [ ] A cloud ride of the build, from the chase camera, retained in `production/qa/evidence/story-013-procedural-animation/`:
  a slow and a fast carve (the fast one leans further), an edge change with a pole plant, a roller or kicker lip
  with both skis on the snow, and a landing with the hips compressing; before-and-after where a layer changes a shot.
- [ ] No change to the sim, the camera or the clips; replays still match step for step.

## Out of Scope

- New authored clips or changes to the 16 existing ones (Blender side).
- Cloth or hair simulation; the jacket stays rigid on its bones.
- The snowboard.
- Feet on rails (grinds keep the clip pose).

## Test Evidence

**Story Type**: Logic + Visual/Feel. Logic: EditMode tests in `Assets/Tests/EditMode/` (CI). Visual/Feel: retained
cloud-ride screenshots in `production/qa/evidence/story-013-procedural-animation/`; the feel sign-off is the
user's, on the PC.

## Dependencies

- Depends on: Story 012 (the detailed skier the layers move), Story 011 (the camera it is judged from), Story 009 (the
  Feel Lab).
- Unlocks: nothing blocks on it.

## Decisions (sleep mode, 2026-10-27; the user's to change)

- Custom IK in C# rather than Unity's Animation Rigging package: the package is not in the project, its Unity 6.3
  API is not in `docs/engine-reference/`, and the analytic two-bone solve is small and testable without the engine.
- The layers' settings live on `RiderViewSettings` (not a new asset), so the Feel Lab picks them up with no change.
- The view's own frame clock times the layers (presentation only); replays reproduce the sim exactly, and the
  layers follow from it frame by frame.
