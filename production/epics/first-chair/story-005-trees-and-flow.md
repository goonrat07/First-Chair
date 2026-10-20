# Story 005: Trees and Flow

> **Epic**: First Chair MVP (base skiing system)
> **Status**: Complete
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-20

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 4`: "Trees: near misses fill Flow, branch clips cost speed, trunk hits crash."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 4; Core loop: "close calls build score and the Flow meter, which makes you faster"):*

- [ ] The glade's trees are sim data: position, trunk radius and branch radius, read from the test slope's feature data.
- [ ] Passing a trunk inside the near-miss distance without touching it pushes one nearMiss event per tree per pass (tree, gap, speed). It fills the Flow meter (0 to 1) by more the closer the pass.
- [ ] Flow drains after a delay with no near misses. Full Flow cuts drag and raises trick points, by amounts in tuning data.
- [ ] Clipping branches costs speed and Flow and pushes a branchHit event.
- [ ] Hitting a trunk is a crash (cause: tree) and resets to the last safe spot.
- [ ] Scripted rides: a line threading close gaps fills Flow; a line into a trunk crashes.
- [ ] The Story 001 determinism test still passes with trees and Flow.

---

## Implementation Notes

*Starting values from `design/reference/bluebird-spec.md` (Flow and the trees), all in tuning data:*

- Near-miss distance 2.4 m. A branch clip costs 25% of Flow. Drain is 0.22 per second after 1.4 s without a near miss. Full Flow cuts drag 30% and adds 50% to trick points.
- Tree-well crashes, gap sense and glade ratings belong to the later design, not this MVP.

---

## Out of Scope

- Story 006: applying Flow to trick scores (this story exposes the Flow value).
- Story 007: the tree models.

---

## Files

- `Packages/com.firstchair.sim/Runtime/Forest.cs`
- `Packages/com.firstchair.sim/Runtime/Recording.cs`
- `Packages/com.firstchair.sim/Runtime/Replayer.cs`
- `Packages/com.firstchair.sim/Runtime/RiderState.cs`
- `Packages/com.firstchair.sim/Runtime/SimEvent.cs`
- `Packages/com.firstchair.sim/Runtime/Simulation.cs`
- `Packages/com.firstchair.sim/Runtime/SlopeLayout.cs`
- `Packages/com.firstchair.sim/Runtime/Tuning.cs`
- `sim/FirstChair.Sim.Tests/DeterminismTests.cs`
- `sim/FirstChair.Sim.Tests/RecordingTests.cs`
- `sim/FirstChair.Sim.Tests/ScriptedRide.cs`
- `sim/FirstChair.Sim.Tests/TreesFlowTests.cs`

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: unit tests in `sim/FirstChair.Sim.Tests/` (advisory at `qa.level: minimal`; the determinism test is still required by `.claude/rules/sim-code.md`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/first-chair/story-003-test-slope-unity-view.md` (tree positions), `production/epics/first-chair/story-004-air-landings-crashes.md` (crash and reset)
- Unlocks: `production/epics/first-chair/story-006-rails-and-trick-scoring.md`

---

## Completion Notes
**Completed**: 2026-10-20
**Criteria**: 7/7 passing (none deferred). Each is covered by a test that ran, in `TreesFlowTests` and `DeterminismTests`; 277 tests pass. Input-free lines across the committed glade both thread gaps and hit trunks.
**Deviations**: ADVISORY:
- Built while Story 003 was still In Progress; it used only 003's finished tree data.
- Open design question: trees have no height, so a rider in the air hits them at any height. See `production/sleep-mode/questions.md` #1.
- The engine review's follow-ups are fixed (`1c880ae`): a start inside a trunk is rejected, `TreePassHash` is checked in the replay tests, and the Flow bonus is capped.
- Feel is untuned, and nothing is verified in Unity. The Unity driver must pass `Forest.FromLayout(layout)`; the old constructors ride with no trees.
**Test Evidence**: waived at qa.level minimal; unit tests in `sim/FirstChair.Sim.Tests/TreesFlowTests.cs` exist anyway, as `.claude/rules/sim-code.md` requires
**Reviewed by**: self, no independent review (solo mode; a unity-specialist engine review ran during `/dev-story`)
