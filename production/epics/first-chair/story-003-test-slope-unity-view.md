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

Since 2026-10-22, Unity runs on GitHub Actions (`.github/workflows/unity.yml`). The project settings
(`ProjectSettings/`, `Packages/manifest.json` and `packages-lock.json`, the URP assets in `Assets/Settings/`) are
committed, and `Assets/Scripts/Ci/Editor/CiBuild.cs` builds the scene and the players. Every script compiles, the 34
EditMode tests pass, and the Linux and Windows players build and run. Evidence is in
`production/qa/evidence/story-003-test-slope-unity-view/`. The gamepad and the Windows playtest still need the
user's PC.

## Playtest feedback (user, 2026-10-23, Windows build from run 40032472712)

To do, in order (the user: fix the skis and poles first, then move to the next step):
- [x] **Skis and poles point the wrong way.** The skis stand vertical under the boots and the poles stick backward
  and up, with odd shadows. CH_Skier's sockets come in with Blender's axes on the armature (story 007 notes), so
  attaching the props with an identity rotation is wrong. The skier model itself "looks really good".
- [x] **Camera framing.** The view is a little too wide: narrow the lens. Put the rider near the bottom of the
  screen, as GMA2 does, so more of the mountain ahead is visible and the slope reads as a slope.
- [x] **Feel: skiing seems slow.** Partly because the test slope is so wide that the skier looks small. Revisit
  after the camera change, then tune speed (story 002's numbers are placeholders until a playtest).
- [x] **Later: a less static camera.** GMA2 doesn't pin the rider to the centre; give the rider a little room to
  drift side to side (a lag or dead zone) before the camera follows.

Done in commit d455f70 and seen in build 40055981760 on 2026-10-24 (sleep mode; not verified in the editor). The
skis and poles are lined up in the Stance pose: the skis lie flat and point along the line, and the poles hang to
the snow. The camera has a 50 mm lens and puts the rider near the bottom. The speed was retuned in sim version 6
(below). Evidence: `production/qa/evidence/story-003-test-slope-unity-view/2026-10-24-build-40055981760-*.png` and
`evidence.md`. The user's own playtest of the feel is still open.

Decisions (user, 2026-10-24), for the next work and sleep mode:
- [x] **Raise the speed now:** retune the sim toward a GMA2-like pace (roughly 40 to 60 km/h on this groomer, not
  about 25), with tests showing the speeds before and after (story 002's tuning; bump the sim version).
  Done in sim version 6 (2026-10-24, sleep mode). The cause was the keyboard: every key turn is full lock, and a
  full-lock turn out-turned groomed grip above 32 km/h (GroomedGrip 16 / TurnRate 1.8 = 8.9 m/s), so key turns
  skidded the rider down to about 33 km/h however hard they were held. Straight runs already reached 80 km/h
  (105 tucked). Now every snow type has a quarter more grip (groomed 20) and TurnRate is 1.5 rad/s, so key turns
  carve to 48 km/h. Key S-turns down the groomer, held until the skis point this far from the fall line, measured
  at 10 to 20 s: 30° 36-37 → 39-51 km/h, 50° 33-35 → 47-49, 70° 30-33 → 44-48; straight and tucked unchanged.
  `TestSlopeTests.KeyboardSTurnsDownGroomer_FiftyDegreesEachWay_HoldFortyToSixtyKmh` pins it (34 km/h before,
  failing; passes after). Whether it now feels right is the user's call.
- [x] **Real trees along the groomer and park edges:** add tree lines to `tools/terrain/make_test_slope.py`, so they
  are in the sim's layout and can be hit (story 005's rules).
  Done 2026-10-24 (sleep mode): three 24 m strips of crud between the runs, `treesWest` (4 m off the groomer's left
  edge), `treesMiddle` (between the groomer and the park) and `treesEast` (between the park and the bowl). Each is
  filled with trees on an 8 m grid, keeping 95% of the cells in the columns that face a run and 85% inside, so 428
  trees are added to the glade's 51. The strips are crud on crud, so the heightmap and snow map are byte-identical;
  only `TestSlope.txt` changed. Tests: the generator's (every canopy is in a wooded zone and off every run; each side
  of the groomer and park has trees within 10 m of its edge with no gap over 40 m) and the sim's (the same placement
  check, and a rider traversing off the groomer's left edge meets a tree in the line).
- [x] **Old ski tracks:** scatter GMA2-style curving old track pairs over the groomers and powder, display only.
  Done 2026-10-24 (sleep mode), not yet seen in a build or the editor. `OldTracks` (Core, engine-free) generates
  seeded S-turn track pairs that run down the fall line, stay inside their zone and stop at anything blocked.
  `TestSlopeLook.OldTracks` lays them as 9 cm ribbons 2 cm off the snow over the groomer and the bowl (about one
  per 2300 m^2), at least 1 m clear of any canopy, with no shadows. Tests: `OldTracksTests` (6; two mutations of
  the generator were each caught).
- [x] **Camera drift:** the less static camera above; the user moved it into tonight's work.
  Done 2026-10-24 (sleep mode), not yet seen in a build or the editor. The rider may drift 2.5 m left or right of
  the aim (about 6% of the screen width each way) before the camera follows, and the camera eases back to centre over
  1.5 s (`FollowCameraSettings` drift fields; `FollowCameraMath.LateralFollow`, 3 new tests). Set the drift to 0 to
  pin the rider to the middle again.
- Also tonight: story 007's art items, and grab and trick poses for the skier (story 007).

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
