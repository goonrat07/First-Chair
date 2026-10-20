# Story 006: Rails, boxes and single-trick scoring

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-20

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 5`: "Rails and boxes with grinds, and single-trick scoring and naming."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 5; Core loop: "clean landings, tricks and close calls build score"):*

- [ ] Rails and boxes are sim data on the test slope's kicker line. Landing on or riding onto one starts a grind (grindStart event); leaving it ends the grind (grindEnd event).
- [ ] During a grind the rider balances with left/right input or falls off. The grind scores by time held and balance.
- [ ] Each landed jump gets a name: optional "Switch", then flips, then spin, then grab. Examples: "Backflip", "Left 720 Japan".
- [ ] Each landed jump is scored from spin, flips, grab, air time and landing grade, multiplied up by Flow, with the formula's coefficients in tuning data.
- [ ] Repeating the same trick name in a run scores half as much each time.
- [ ] The run total is the sum of scored tricks and grinds. A crash keeps everything already scored, since there are no combos in the MVP.
- [ ] Unit tests check the name and score of fixed inputs, including a repeated trick.

---

## Implementation Notes

*Starting formula from `design/reference/bluebird-spec.md` (Trick scoring, Single tricks):*

- air = (100·h + 450·f + g + 60·t) × q × (1 + 0.5·Flow). h = half turns of spin, f = full flips, t = air time in seconds.
- g = grab bonus: 150 + 300 per second held, capped at 600, counted only if held 0.15 s or more.
- q = 1.5 clean, 1.0 solid, 0.6 sketchy.
- Grinds score per second, times balance and quality. A trick ID is the name in kebab-case, e.g. `left-720-japan`.

---

## Out of Scope

- Combos, presses, butters and their multipliers: out of the MVP (brief).
- A scoring HUD beyond a debug readout.

---

## Files

- `Packages/com.firstchair.sim/Runtime/` (rails and boxes, grinds, trick naming, trick scoring, run total)
- `sim/FirstChair.Sim.Tests/TrickScoringTests.cs`

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

- Depends on: `production/epics/first-chair/story-004-air-landings-crashes.md`, `production/epics/first-chair/story-005-trees-and-flow.md` (Flow value)
- Unlocks: None
