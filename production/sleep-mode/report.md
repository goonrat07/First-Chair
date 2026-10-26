# Sleep mode report

Started 2026-10-26T01:32:33Z · Ended (running) · Reason (running) · Commits 0 (`6ad33d0..`)

## Summary
- Finished: (running)
- Skipped: (running)
- Waiting on you: 0 questions in production/sleep-mode/questions.md
- Needs the PC: story 003's Windows playtest; the feel of story 009 (yours to judge); GMA2-matched numbers (your clips)
- Next: (running)

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

