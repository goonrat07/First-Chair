# Story 001: Deterministic sim core and replay

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-20

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 1`: "Deterministic riding sim (fixed 120 Hz) with input recording and exact replay."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 1; Player goal: "Every run replays exactly from its recording"):*

- [ ] The sim advances only in fixed steps of 1/120 s; nothing in it reads wall-clock time, frame time or `Time.deltaTime`.
- [ ] Each step's input is one packed integer: left, right, forward (tuck/pole), back (brake), jump, grab, plus a quantized analog steering value. The state after a step depends only on the previous state, that integer and the RunSetup.
- [ ] A RunSetup (sim version, sport, assist level, seed) is copied once at run start and written to the recording header ahead of the input stream.
- [ ] Everything that moves the rider uses fixed-point (Q32.32) math and the one random generator seeded from `RunSetup.Seed`.
- [ ] The sim reads height and surface normal from a 16-bit heightmap. A rider placed on an inclined test heightmap with no input slides downhill under gravity.
- [ ] Determinism test: a scripted ride of at least 10 s is recorded, then replayed from its header and inputs. Position, velocity and the event list match bit for bit at every step.
- [ ] Replaying a recording whose header carries a different sim version reports a version mismatch and does not replay.
- [ ] The sim source lives in `Packages/com.firstchair.sim/`, whose asmdef sets `noEngineReferences: true`. `dotnet test sim/` builds the same sources and runs the tests with no Unity installed.
- [ ] GitHub Actions runs `dotnet test sim/` on every push and fails the run on any failing test.

---

## Implementation Notes

*From `.claude/rules/sim-code.md` and `docs/research/engine-and-pipeline.md` §7:*

- The sim library targets `netstandard2.1` so Unity compiles the same files. The test project targets `net10.0`.
- Fixed-point library: FixedMathSharp (MIT) or a small in-house `Fix64`. Use its sqrt and trig, never `System.Math`.
- No `Dictionary`/`HashSet` iteration where order changes a result. No threads inside a step.
- The heightmap loader reads the raw 16-bit file plus its world size and height range, and nothing Unity-specific.
- Riding numbers go in the sim's tuning data from the start, even the gravity slide's friction.

---

## Out of Scope

- Story 002: carving, skidding, tuck, brake, poling, snow types.
- Story 003: the real test slope, Unity view, camera and input devices.

---

## Files

- `.github/workflows/sim-tests.yml`
- `Packages/com.firstchair.sim/Runtime/FirstChair.Sim.asmdef`
- `Packages/com.firstchair.sim/Runtime/Fix64.cs`
- `Packages/com.firstchair.sim/Runtime/Fnv1a64.cs`
- `Packages/com.firstchair.sim/Runtime/Heightmap.cs`
- `Packages/com.firstchair.sim/Runtime/HeightmapSpec.cs`
- `Packages/com.firstchair.sim/Runtime/Recording.cs`
- `Packages/com.firstchair.sim/Runtime/Replayer.cs`
- `Packages/com.firstchair.sim/Runtime/RiderState.cs`
- `Packages/com.firstchair.sim/Runtime/RunSetup.cs`
- `Packages/com.firstchair.sim/Runtime/SimEvent.cs`
- `Packages/com.firstchair.sim/Runtime/SimInput.cs`
- `Packages/com.firstchair.sim/Runtime/SimRandom.cs`
- `Packages/com.firstchair.sim/Runtime/Simulation.cs`
- `Packages/com.firstchair.sim/Runtime/Tuning.cs`
- `Packages/com.firstchair.sim/Runtime/Vec3.cs`
- `Packages/com.firstchair.sim/package.json`
- `sim/FirstChair.Sim.Tests/DeterminismTests.cs`
- `sim/FirstChair.Sim.Tests/FirstChair.Sim.Tests.csproj`
- `sim/FirstChair.Sim.Tests/Fix64Tests.cs`
- `sim/FirstChair.Sim.Tests/GravitySlideTests.cs`
- `sim/FirstChair.Sim.Tests/HeightmapTests.cs`
- `sim/FirstChair.Sim.Tests/PackageLayoutTests.cs`
- `sim/FirstChair.Sim.Tests/RecordingTests.cs`
- `sim/FirstChair.Sim.Tests/ScriptedRide.cs`
- `sim/FirstChair.Sim.Tests/SimAssemblyRulesTests.cs`
- `sim/FirstChair.Sim.Tests/SimInputTests.cs`
- `sim/FirstChair.Sim.Tests/SimRandomTests.cs`
- `sim/FirstChair.Sim.Tests/TestTerrain.cs`
- `sim/FirstChair.Sim.Tests/TuningTests.cs`
- `sim/FirstChair.Sim.sln`
- `sim/FirstChair.Sim/FirstChair.Sim.csproj`

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: unit tests in `sim/FirstChair.Sim.Tests/` (the sim's test root, outside Unity). At `qa.level: minimal` tests are advisory, but `.claude/rules/sim-code.md` still requires the determinism test on every sim change.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: `production/epics/first-chair/story-002-skiing-on-snow.md`, `production/epics/first-chair/story-003-test-slope-unity-view.md`
