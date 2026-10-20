# Story 004: Air: jumps, spins, flips, grabs, landings, crashes, reset

> **Epic**: First Chair MVP (base skiing system)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-20

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 3`: "Air: hold-to-load jumps, spins, flips and grabs, landings graded clean, solid or sketchy, crashes, and tap-to-reset."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 3; Fail state: "a bad landing or over-rotation ... resets to the last safe spot in about a second"):*

- [ ] Holding jump on snow loads a pop. Releasing it pops the rider upward with a strength that grows with hold time, up to a cap set by tuning data.
- [ ] Leaving the snow, off a kicker or from a pop, puts the rider in the air.
- [ ] In the air, left/right spins, forward/back flips, and grab plus a direction grabs: none = mute, forward = tip, back = tail, left = Japan, right = safety.
- [ ] On touchdown the landing is graded from the angle between the skis and the slope and any unfinished rotation: clean, solid or sketchy, with thresholds in tuning data.
- [ ] Past the sketchy limit the landing is a crash, with cause over-rotation or under-rotation.
- [ ] A crash stops riding and resets the rider to the last safe spot about one second later (duration in tuning data). Tapping reset does the same at once.
- [ ] The sim pushes events: takeoff (speed), land (grade, air time, max height) and crash (cause).
- [ ] Scripted-input tests cover: a straight jump lands clean; a timed 360 lands clean; an unfinished rotation crashes; tap-reset returns to the last safe spot.
- [ ] The Story 001 determinism test still passes with air, landings and crashes.

---

## Implementation Notes

*Design intent from `design/reference/bluebird-spec.md` ("The baseline: what must already work", Riding):*

- Controls: hold Space to load, release to pop. In the air, A/D spin, W/S flip, Shift plus a direction grabs. Keyboard only needs two keys per action. Gamepad mapping comes from Story 003.
- The last safe spot is the most recent grounded, uncrashed state on snow, sampled by the sim and stored in its state, so a reset replays exactly.

---

## Out of Scope

- Story 006: trick names and scores (this story produces the land event data they use).
- Combos, rewind and signature tricks: out of the MVP (brief).

---

## Files

- `Packages/com.firstchair.sim/Runtime/RiderState.cs`
- `Packages/com.firstchair.sim/Runtime/SimEvent.cs`
- `Packages/com.firstchair.sim/Runtime/SimInput.cs`
- `Packages/com.firstchair.sim/Runtime/Simulation.cs`
- `Packages/com.firstchair.sim/Runtime/Tuning.cs`
- `sim/FirstChair.Sim.Tests/AirTests.cs`
- `sim/FirstChair.Sim.Tests/DeterminismTests.cs`
- `sim/FirstChair.Sim.Tests/ScriptedRide.cs`
- `sim/FirstChair.Sim.Tests/SimInputTests.cs`

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: unit tests in `sim/FirstChair.Sim.Tests/` (advisory at `qa.level: minimal`; the determinism test is still required by `.claude/rules/sim-code.md`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/first-chair/story-002-skiing-on-snow.md`, `production/epics/first-chair/story-003-test-slope-unity-view.md` (kicker positions in the slope data)
- Unlocks: `production/epics/first-chair/story-006-rails-and-trick-scoring.md`

---

## Completion Notes
**Completed**: 2026-10-20
**Criteria**: 9/9 passing (none deferred). Each is covered by a test that ran, in `AirTests` and `DeterminismTests`; 243 tests pass. All three committed test-slope kickers launch at their lips and land clean.
**Deviations**: ADVISORY:
- This story was built while Story 003 was still In Progress. It used only 003's finished slope data; 003's Unity half is open.
- Feel is untuned. The playtest flags: sharp crests at high speed can give very short hops (Story 006 may want a minimum air time), and the safe spot is often a kicker lip.
- The engine review's tuning notes are fixed (`19e292a`).
- Not verified in Unity or IL2CPP.
**Test Evidence**: waived at qa.level minimal; unit tests in `sim/FirstChair.Sim.Tests/AirTests.cs` exist anyway, as `.claude/rules/sim-code.md` requires
**Reviewed by**: self, no independent review (solo mode; a unity-specialist engine review ran during `/dev-story`)
