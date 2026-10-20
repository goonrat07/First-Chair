# Story 005: Trees and Flow

> **Epic**: First Chair MVP (base skiing system)
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: [set by /dev-story when implementation begins]

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

- `Packages/com.firstchair.sim/Runtime/` (trees, near misses, Flow, branch and trunk contact)
- `sim/FirstChair.Sim.Tests/TreesFlowTests.cs`

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

- Depends on: `production/epics/first-chair/story-003-test-slope-unity-view.md` (tree positions), `production/epics/first-chair/story-004-air-landings-crashes.md` (crash and reset)
- Unlocks: `production/epics/first-chair/story-006-rails-and-trick-scoring.md`
