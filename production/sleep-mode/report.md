# Sleep mode report

Started 2026-10-26T01:32:33Z · Ended (running) · Reason (running) · Commits 0 (`6ad33d0..`)

## Summary
- Finished: story 009 (GMA-style riding, the Feel Lab, speed cues: In Review); ski tracks that stay (story 007);
  story 010, a small test mountain with a chairlift, five runs, a park and forest (In Review); the lighting pass
  (story 007).
- Skipped: nothing.
- Waiting on you: 1 question in production/sleep-mode/questions.md
- Needs the PC: story 003's Windows playtest; the feel of story 009 and the look of stories 007 and 010 (yours to
  judge); GMA2-matched numbers (your clips).
- Next: play the newest build (CI run 40287291873): press ` for the Feel Lab, Controls tab, "Ride the test
  mountain", then "Go to the park" or "Go to the lift".
- Usage: at 11:05 UTC the binding limit was the seven-day one (resets 2026-11-01 16:00 UTC, status
  "allowed_warning"). No after-reset wake-up is scheduled for it: if it is hit, the run stops there with no
  notification, so this report is kept current after every step.

## story-009-gma-style-riding-and-feel-lab: done, In Review
- Changed: sim version 8 (`Simulation.cs`, `Tuning.cs`, `RiderState.cs`, `SimInput.cs`), keys and poses
  (`RiderInputReader.cs`, `RiderInputQuantizer.cs`, `SkierPoseWeights.cs`), carve spray (`SnowSpray.cs`,
  `SnowSprayMath.cs`), the Feel Lab (`FeelLab.cs`, `FeelLabValues.cs`, `SimDriver.cs`), camera speed effects
  (`FollowCamera.cs`, `FollowCameraSettings.cs`, `FollowCameraMath.cs`), snow contrast and old tracks
  (`SnowSurface.cs`, `TestSlopeLook.cs`); commits 2c55dee, b28a2b1, 0863fed, c838bb8, dad1dba.
- Tests: `dotnet test sim/` 400/400; Core EditMode tests 116/116 locally; CI run 40256892551 EditMode tests and both
  players pass. The S-turn test now asserts version 7's carve pace (63 km/h): plain carves hold 69.3, quick turns 32.9.
- Seen in the build (Linux player, cloud): carves, a drift, a quick turn and a hockey stop with spray; the Feel Lab's
  four tabs, the chase preset, a riding number nudged, applied and copied out (clipboard checked), an effects number
  nudged live; at 53 km/h the snow moves a median 0.76 px between frames 0.25 s apart (before: 0.09). Evidence:
  `production/qa/evidence/story-009-gma-style-riding-and-feel-lab/`.
- Decisions made for you: Shift (either) with a turn drifts on snow and grabs in the air; Ctrl or Alt with a turn
  quick-turns (gamepad: left shoulder); W tucks. Version 8 replaced version 7 outright (no switch back; the previous
  build is the comparison). Added `HockeyMinSpeed`; kept the drift scrub low (0.3). Left the story In Review: its
  feel sign-off is yours.
- Not verified: the feel (your playtest), the gamepad, anything in the Unity Editor.

## story-007-first-art-pass, tracks that build up and stay: done (step one)
- Changed: `TrackRibbon.cs` (new, Core), `SkiTrack.cs` (now a ribbon laid on the snow, kept in stretches),
  `SkierView.cs`, `RiderView.cs`, `RiderViewSettings.cs` (`trackKeptMetres` 12 000 replaces the 120 s lifetime),
  `TestSlopeLook.cs` (translucent live and old track materials); commit 716391a.
- Tests: 9 new TrackRibbonTests, 8 seen failing against a stub, four mutations caught; Core 116/116 at the time; CI
  run 40258047589 compiled and built it.
- Seen in build 40278892710: tracks stay after a restart at the top, and the next run rides over them.
- Decisions made for you: ribbons now, not the render-texture snow shader (that needs the editor to write and check;
  the ribbons can make way for it later).
- Not verified: the editor; how the darker crossings read at full speed on your PC.

## story-007-first-art-pass, the lighting pass: done (the look is yours to judge)
- Changed: `LightingSettings.cs` and `LightingRig.cs` (86bc33a: the scene's light as data, live in the Feel Lab's new
  Light tab, carried by Copy values and Load saved); new defaults (this commit): the sun behind and to the right of the
  camera (heading -30) at 35 degrees, intensity 2.2; sky light 0.8; exposure +0.3; contrast 10.
- Tests: none automated (a look). Compared in build 40281825872 from one held view over the test slope's first
  rollers (each variant loaded through Load saved with gravity near zero, so the rider stayed put and every shot
  matched): the brightness range down the screen rose from 12 to 26 levels of 255, the snow as bright (mean 203
  against 207). Evidence: `production/qa/evidence/story-007-first-art-pass/2026-10-26-build-40281825872-lighting-before-after.png`.
- Decisions made for you: the sun moved from ahead of the camera to behind it, so steeper snow reads darker and the
  shadows fall ahead and to the left of the rider. Exposure alone barely helped (12 to 13 levels); a lower sky light
  (0.6 to 0.7) read more (up to 33) but greyed the snow, so 0.8.
- Not verified: the editor; your eye. On a PC that built the scene since 86bc33a, reset or delete
  `Assets/Data/Riding/LightingSettings.asset` to pick up the new defaults (the scene builder keeps an existing asset).

## story-010-test-mountain (new): done, In Review
- Changed: the story (new); the generator `tools/terrain/make_test_mountain.py` and its tests; the mountain's files in
  `Assets/Terrain/TestMountain/`; the sim's `run`, `lift` and `tower` records (`SlopeRunsAndLifts.cs`,
  `SlopeLayout.cs`) and `TestMountainTests` (each run ridden); Core `CableLoop`, `RunLine`, `RunTracks`, `SlopeSpots`;
  `ChairLift`, `LiftRide`, `SimDriver` (a slope by name, `Paused`, `RestartRunAt`), `FollowCamera.RideTarget`, the Feel
  Lab's places and "Go to the lift / park", `RiderHud` (hidden on the lift); the scene builder, terrain builder and
  build step for a second scene (`TestSlopeLook.Mountain.cs`); CI's second screenshot. Commits a22387e 1f60167
  832ada8 76d14ad 03b8b04 17ef94b bb48b3b 2d3eafc 5e1d06b dbd2114 75eeeb4.
- Tests: `dotnet test sim/` 431/431; `python3 -m pytest tools/terrain` 35 passed; Core EditMode tests 154/154 locally;
  CI run 40287291873: EditMode tests and both players pass.
- Seen in build 40287291873 (cloud, Linux player): the start beside the lift, facing down the fall line; chairs
  running round the rope; markers and signs; old tracks on the runs; "Go to the park" rolls down the kicker lane and
  over the first kicker; "Go to the lift": a short push into the station, the chair, the ride up with the camera on the
  chair and the HUD hidden, and a new run at the top. Evidence: `production/qa/evidence/story-010-test-mountain/`.
- Found in builds and fixed: the runs' start levels had cut a trough into the summit, so riders started 51 degrees off
  the fall line (2d3eafc); "Go to the lift" left a 25 m push across the station's flat hollow, with no hint (dbd2114);
  the HUD showed the last speed all the way up the lift (75eeeb4).
- Decisions made for you: the whole layout (names, sizes, where things go); an earlier run keeps its surface where
  runs meet; the runs leave the summit's surface alone; the start sits 10 m beside the lift line; old tracks on every
  run, shorter turns on narrow ones; "Go to the lift" starts 12 m out; W hurries the lift five times over.
- Not verified: the editor; the mountain's look and the lift ride on your PC; the gamepad button that hurries the lift.
