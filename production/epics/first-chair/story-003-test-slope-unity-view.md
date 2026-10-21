# Story 003: Test slope, Unity view, follow camera and input

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-20

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 6`: "One test slope (groomer, powder bowl, kicker line, short glade) on a heightmap shared by the sim and Unity Terrain, with a high, pulled-back follow camera like GMA2's (a Steep-style chase cam can come later) and keyboard plus gamepad input."

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity 6.3 | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**: N/A (minimal — no control manifest)

---

## Acceptance Criteria

*From `design/game-brief.md` (MVP feature 6; Player goal: "the rider gets from top to bottom of the test slope under full player control"):*

- [ ] A script generates the test slope: a 16-bit RAW heightmap with a groomer, a powder bowl, a kicker line and a short glade. It also writes a snow-type map and feature data (kicker and tree positions) aligned to the heightmap.
- [ ] Unity Terrain is built from that same heightmap file. At sampled points, Unity's terrain height and the sim's height agree within 1 cm.
- [ ] Unity steps the sim at 120 Hz as many times per frame as needed and draws the rider interpolated between the last two sim states. Unity never writes rider state.
- [ ] A placeholder skier (a simple body wearing `PR_Ski` and `PR_SkiPole` on their sockets) follows the sim rider's position and heading.
- [ ] A high, pulled-back follow camera like GMA2's frames the rider and the slope ahead. Its height, distance and angle are tuning values.
- [ ] Keyboard and gamepad both drive the rider. Gamepad steering is quantized into the sim input integer before it reaches the sim. On keyboard, no action needs more than two keys.
- [ ] A ride recorded in Unity and replayed reproduces the live run's positions step for step.
- [ ] A playtest on the user's PC rides the test slope top to bottom in a Windows build, with a retained screenshot from the follow camera.

---

## Implementation Notes

*From `docs/research/engine-and-pipeline.md` §5 and §7, and `README.md` (Unity project setup):*

- Needs the Unity project created on the user's PC first (README, "Set up your PC", step 3). Cloud sessions can do the generator and the sim-side checks, but must report Unity-side work as not verified in the editor.
- The heightmap is the single source of truth. Zones (groomer, powder, glade, park) are data layered on it.
- Use Unity's Input System, not the legacy Input Manager (`docs/engine-reference/unity/VERSION.md`).
- The camera is presentation only. A chase cam can be added later without touching the sim.

---

## Out of Scope

- Story 004: kicker physics and air (this story only places the kickers in the slope data).
- Story 005: tree collisions and Flow (this story only places the trees).
- Story 007: real art, snow shader and lighting.

---

## Files

Sim half, done in the cloud (`5d9a50a`):

- `tools/terrain/make_test_slope.py`
- `tools/terrain/test_make_test_slope.py`
- `Assets/Terrain/TestSlope/TestSlope.raw` (heightmap, Git LFS)
- `Assets/Terrain/TestSlope/TestSlopeSnow.raw` (snow map, Git LFS)
- `Assets/Terrain/TestSlope/TestSlope.txt` (layout: spec, zones, start, kickers, trees)
- `Packages/com.firstchair.sim/Runtime/SlopeLayout.cs`
- `sim/FirstChair.Sim.Tests/SlopeLayoutTests.cs`
- `sim/FirstChair.Sim.Tests/TestSlopeTests.cs`
- `.github/workflows/sim-tests.yml` (checks out LFS)

Unity half, still to build on the PC:

- `Assets/Scripts/Riding/` (sim driver, rider view, input to sim, follow camera)
- `Assets/Scenes/TestSlope.unity`
- `Assets/Tests/EditMode/TerrainParityTests.cs`

Unity half, pre-written in the cloud (2026-10-21), **not verified in the editor**: it has never been
compiled by Unity or run there. The engine-free parts (`Riding/Core/` and the tests that use only them) compile
as netstandard2.1 / C# 9 with the sim sources and pass under `dotnet` in a scratch project, which is not committed.

- `Assets/Scripts/Riding/Core/` (asmdef `FirstChair.Riding.Core`, no engine references): `SimCoordinates`
  (the sim-to-Unity mapping, documented), `FixedStepClock`, `RiderInputQuantizer`, `RiderPose`, `TestSlopeData`,
  `RideSession`, `ReplayCheck`, `TerrainHeights`, `FollowCameraMath`
- `Assets/Scripts/Riding/` (asmdef `FirstChair.Riding`): `SimDriver`, `RiderInputReader` + `RiderInputConfig`,
  `RiderView` + `RiderViewSettings`, `FollowCamera` + `FollowCameraSettings`, `SimCoordinatesUnity`
- `Assets/Scripts/Riding/Editor/` (asmdef `FirstChair.Riding.Editor`): `TestSlopeTerrainBuilder`
  (First Chair > Rebuild Test Slope Terrain), `TestSlopeSceneBuilder` (First Chair > Build Test Slope Scene, which
  writes `Assets/Scenes/TestSlope.unity`), `TestSlopeBuildStep` (copies the slope files into a Windows player)
- `Assets/Scripts/ScreenshotOnArg.cs` (run-and-observe bootstrap, verbatim from `.claude/docs/run-and-observe.md`)
- `Assets/Tests/EditMode/` (asmdef `FirstChair.Riding.Tests.EditMode`): `TerrainParityTests`, `RideReplayTests`,
  `FixedStepClockTests`, `RiderInputQuantizerTests`, `SimCoordinatesTests`, `TestSlopeFiles`

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/TerrainParityTests.cs`, or a playtest doc in `production/qa/evidence/` with the retained follow-camera screenshot.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: `production/epics/first-chair/story-001-sim-core-replay.md`, `production/epics/first-chair/story-002-skiing-on-snow.md`
- Unlocks: `production/epics/first-chair/story-004-air-landings-crashes.md`, `production/epics/first-chair/story-005-trees-and-flow.md`, `production/epics/first-chair/story-007-first-art-pass.md`
