# Story 002: Skiing on snow: carve, skid, tuck, brake, pole, snow types

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-20

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 2`: "Skiing on snow: carve, skid, tuck, brake, pole on flats, and five snow types (groomed, powder, crud, ice, slush), each with its own grip and drag."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 2; Player goal: "every speed change ... traces to the player's inputs and the snow"):*

- [ ] Steering edges the skis. Below the grip limit for the current snow and speed the skis carve: velocity follows the ski heading with little speed loss.
- [ ] Past the grip limit the skis skid, losing speed in proportion to the slip angle.
- [ ] Holding tuck lowers drag, so a straight run down the groomer reaches a higher terminal speed than standing. Tuck also slows turning.
- [ ] Holding brake slows the rider to a stop on the groomer within a stopping distance set by tuning data.
- [ ] On flats (slope below a tuning threshold) forward input poles, adding speed up to a pole-speed cap. Poling does nothing on steeper slopes.
- [ ] Five snow types (groomed, powder, crud, ice, slush) each have their own grip and drag in tuning data. The sim reads the type at the rider's position from a snow-type map aligned with the heightmap.
- [ ] Scripted-input tests show the types differ as intended: ice skids at a lower edge angle than groomed, and powder slows a straight run more than groomed.
- [ ] No riding number (grip, drag, pole force, tuck factor, brake force) is inline in logic; all are in the sim's tuning data.
- [ ] The Story 001 determinism test still passes with all of this enabled.

---

## Implementation Notes

*From `.claude/rules/sim-code.md` and the brief's target feel:*

- Target feel is "between realism and fun" (GMA's lesson, `docs/research/engine-and-pipeline.md` §2): grip that rewards clean edging, skids that forgive.
- The snow-type map is data the sim reads, at the same resolution and alignment as the heightmap.
- Tune by playtest once Story 003 makes the rider visible. These tests pin behaviour, not final numbers.

---

## Out of Scope

- Story 003: the test slope's real terrain and snow map, and the Unity view.
- Story 004: jumps, air and crashes.

---

## Files

- `Packages/com.firstchair.sim/Runtime/` (rider snow contact, carving and skidding, tuck, brake, pole, snow types, tuning)
- `sim/FirstChair.Sim.Tests/SnowRidingTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: unit tests in `sim/FirstChair.Sim.Tests/` (advisory at `qa.level: minimal`; the determinism test is still required by `.claude/rules/sim-code.md`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/first-chair/story-001-sim-core-replay.md`
- Unlocks: `production/epics/first-chair/story-003-test-slope-unity-view.md`, `production/epics/first-chair/story-004-air-landings-crashes.md`
