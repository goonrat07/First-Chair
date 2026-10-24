# Sleep mode report

Started 2026-10-24T02:38:00Z · Ended (running) · Reason (running) · Commits 0 (`0f146e0..`)

## Summary
- Finished: (running)
- Skipped: (running)
- Waiting on you: 0 questions in production/sleep-mode/questions.md
- Needs the PC: the gamepad check and your playtest of each new build; story 007 sign-off
- Next: (running)

## 1. The playtest fixes, seen in a build
- Build 40055981760 (commit 999c2a5) is green: EditMode tests, Linux and Windows. I rode the Linux player here
  under Xvfb, at 4K for detail.
  - Skis: flat under the boots, pointing along the line. Before, they stood vertical.
  - Poles: hanging to the snow. Before, they pointed backward and up.
  - Carving: in a left carve the skis follow the tracks on edge and the body leans in.
  - Camera: narrower, with the rider near the bottom of the screen.
  - Tree lines: they show, with their shadows.
- Changed:
  - `.github/workflows/unity.yml`: a build in progress now finishes instead of being cancelled by the next push.
    Two runs, 40054775510 and 40055604010, were lost to that before the change.
  - Evidence: `production/qa/evidence/story-003-test-slope-unity-view/2026-10-24-build-40055981760-*.png`, plus
    a new section in its `evidence.md`.
  - Story 003's to-do list: the skis and poles, the camera and the speed are ticked.
- Not verified:
  - The speed in the build. Software rendering runs the game slower than real time, so the sim test is the
    measure.
  - The editor, and the feel.
  - The Windows build is in the run's artifacts for your playtest.

## 2. Speed retune (sim version 6)
- Why it felt slow: on a keyboard every turn is full lock, and a full-lock turn out-turned groomed grip above
  32 km/h (GroomedGrip 16 / TurnRate 1.8 rad/s = 8.9 m/s). So key turns always skidded, and the skid drag held the
  rider near 33 km/h. Straight runs already reached 80 km/h, and 105 tucked.
- Changed:
  - `Packages/com.firstchair.sim/Runtime/Tuning.cs`: every snow type's grip is up a quarter (groomed 16 → 20,
    powder 13 → 16.25, crud 10 → 12.5, ice 6 → 7.5, slush 12 → 15), and TurnRate is 1.8 → 1.5 rad/s. Key turns
    now carve up to 48 km/h.
  - `Packages/com.firstchair.sim/Runtime/Simulation.cs`: Version 5 → 6, so old recordings report a mismatch.
  - `sim/FirstChair.Sim.Tests/TestSlopeTests.cs`: a new test of key S-turns down the real groomer. It averages
    40 to 60 km/h; it was 34 before the change, so it failed first.
  - `sim/FirstChair.Sim.Tests/SnowRidingTests.cs`: the moderate skid now steers halfway between the grip limit and
    full lock, instead of 1.3 times the limit, which went past full lock with the new grip.
  - `sim/FirstChair.Sim.Tests/ScriptedRide.cs`: the box moved 2 m, to stay on the scripted line after the reset.
  - `sim/FirstChair.Sim.Tests/DeterminismTests.cs`: the golden trace is re-pinned for version 6.
  - Stories 003 ("Decisions") and 002 (Completion Notes) record it.
- Speeds before → after, for key S-turns held until the skis point this far from the fall line, at 10 to 20 s:

  | Turn | Before | After |
  |---|---|---|
  | 30° | 36-37 km/h | 39-51 km/h |
  | 50° | 33-35 km/h | 47-49 km/h |
  | 70° | 30-33 km/h | 44-48 km/h |

  Straight (80 km/h) and tucked (105 km/h) runs are unchanged.
- Tests: `dotnet test sim/`: 340 passed, 0 failed.
- Decisions made for you:
  - I tuned the grip and turn rate, not the drag. Straight runs were already fast, and only turning was slow.
  - Grip rose by the same factor on every snow type, so ice still skids first and powder still grips less than
    groomed.
- Not verified: the feel. Turning is a sixth slower at every speed, so check whether dodging trees still feels
  quick enough on your next playtest.

## 3. Tree lines along the groomer and park
- Changed:
  - `tools/terrain/make_test_slope.py`: three new zones of crud between the runs, `treesWest`, `treesMiddle` and
    `treesEast`, each 24 m wide. `TREE_STANDS` fills them on an 8 m grid, keeping 95% of the cells in the columns
    that face a run and 85% inside, with 1.6-2.5 m canopies (about 10-16 m tall in Unity). The glade's trees are
    unchanged.
  - `Assets/Terrain/TestSlope/TestSlope.txt` was regenerated with 479 trees, up from 51. The heightmap and snow map
    are byte-identical, since the strips are crud on crud.
  - `tools/terrain/test_make_test_slope.py`: every canopy is in a wooded zone and off every run, and each side of
    the groomer and the park has trees within 10 m of its edge with no gap over 40 m.
  - `sim/FirstChair.Sim.Tests/TestSlopeTests.cs`: the same placement check in C#, plus a ride that traverses off the
    groomer's left edge and meets a tree in the line. Both failed on the old layout.
- Tests:
  - `python3 -m pytest tools/terrain -q`: 15 passed.
  - `dotnet test sim/`: 341 passed.
- Decisions made for you:
  - The strips are crud, the snow already between the runs, so riding into the trees feels like it did before
    they were there.
  - The groomer stays 100 m wide. The trees frame it; they don't narrow it.
- Not verified:
  - How 479 trees look and run in Unity (the next CI build shows it).
  - GPU instancing on the tree is item 8.

## 4. Spins and flips in the air
- Spins already showed: the sim turns the heading with the spin, and the views turn the rider by the heading.
  Flips never did. Now they do.
- Changed:
  - `Assets/Scripts/Riding/Core/RiderPose.cs`: a new `FlipDegrees`, the sim's flip interpolated while in the air.
    It is zero on the snow, so a landing never shows the flip unwinding, and it starts from zero at takeoff.
  - `Assets/Scripts/Riding/Core/SimCoordinates.cs`: `FlipAngleToPitchDegrees`.
  - `Assets/Scripts/Riding/RiderPlacement.cs` (new): tilt, then yaw, then flip about a pivot 0.9 m above the feet.
    `SkierView` and `RiderView` both use it.
  - `Assets/Scripts/Riding/RiderViewSettings.cs`: `flipPivotHeight` (0.9 m).
  - `Assets/Scripts/Riding/FollowCamera.cs`: it aims at the sim's position instead of the view's root, which a flip
    swings about.
  - Tests: `Assets/Tests/EditMode/RiderPoseTests.cs` (4) and `RiderPlacementTests.cs` (5).
- Tests:
  - The engine-free EditMode check here: 57 passed. The new pose tests failed (did not compile) before the change.
  - The runtime scripts and the new EditMode tests compile against Unity 6000.3.27f1's player DLLs.
  - `RiderPlacementTests` uses Unity's math, so it runs only in CI's EditMode job.
- Decisions made for you: the flip turns about the middle of the body (0.9 m up), not the boots, so the rider turns
  over in place like GMA2's skier.
- Not verified: how flips look in a build, and in the editor.

## 5. Grab poses
- Changed:
  - `art/source/CH_Skier/build/08_poses.py`: a `grab()` spec builder and five grabs, one per sim `GrabKind`:
    - GrabMute: the left hand across onto the right ski.
    - GrabTip: a pike, the right hand near the right tip.
    - GrabTail: the right hand back on the right tail.
    - GrabJapan: the left hand behind the left boot, trunk turned.
    - GrabSafety: the right hand on the right ski's outside edge.

    The knees come up and spread, so the trunk folds between them. The trunk angles came from a grid search
    (thousands of solves) for zero reach error with the knees clear of the chest and head. All five hands reach
    (`arm_reach_err` 0).
  - `pose_sheet.py` and `compose_pose_sheet.py` render the grabs as their own sheet. `09_export.py` gained clip
    descriptions.
  - `CH_Skier_master.blend`, `CH_Skier.fbx`, `asset-manifest.json` and `animation-contract.json` now have 14
    takes. The round-trip check found no errors; the triangle counts are unchanged.
  - `Assets/Scripts/Riding/Core/SkierPoseWeights.cs`: five `SkierPose` grab entries, `GrabPose(GrabKind)`, and
    the held grab shown in the air instead of Air. Count is 14, and the scene builder picks up the new clips by
    name.
  - Tests: `SkierPoseWeightsTests` gained 7, including that every pose has a clip in CH_Skier's
    `animation-contract.json`. `TestSlopeFiles.RepoRoot()` was added for it.
  - Evidence: `production/qa/evidence/story-007-first-art-pass/2026-10-24-grab-poses-side.png` and
    `-front-three-quarter.png`.
- Tests:
  - The engine-free EditMode check: 64 passed. The new tests failed (did not compile) before the change.
  - The runtime scripts and EditMode tests compile against Unity's DLLs.
- Decisions made for you:
  - Each grab is named after the sim's `GrabKind` (Mute, Tip, Tail, Japan, Safety). The shapes are my reading
    of those names for skis, not a reference you gave.
- Not verified: how the grabs look in a build and in the editor, and their feel. They are a first pass for you
  to judge.

## 6. Old ski tracks
- Changed:
  - `Assets/Scripts/Riding/Core/OldTracks.cs` (new, engine-free): S-turn track pairs from a seed, using its own
    SplitMix64 so they come out the same on every runtime. Each pair is two lines 0.24 m apart, running down the
    fall line. A track stops at its zone's edge or at a blocked point.
  - `Assets/Scripts/Riding/Editor/TestSlopeLook.cs`: `OldTracks()` lays them over the groomer and the bowl, about
    20 and 22 tracks. Each line is a 9 cm ribbon 2 cm off the snow, a little bluer than the snow, with no shadow,
    1 m clear of the trees. There is one mesh per zone, saved in the generated Look folder.
    `TestSlopeSceneBuilder` calls it.
  - `Assets/Tests/EditMode/OldTracksTests.cs`: 6 tests.
- Tests:
  - The engine-free EditMode check: 70 passed.
  - A test found a real bug before the commit: one ski's line could poke 2.5 cm past the zone, because only the
    centre line was checked. Both lines are checked now.
  - Mutations caught: ignoring blocked points, and a zero swing.
  - The new editor methods compile against Unity's DLLs, with stubs for the two AssetDatabase calls.
- Decisions made for you:
  - Old tracks on the groomer and in the bowl, not the park (its kickers and rails) or the glade (its trees).
  - Their colour is lighter than your own live track, so they read as old.
- Not verified: the look, in a build and in the editor.

## 7. Camera drift
- Changed:
  - `Assets/Scripts/Riding/Core/FollowCameraMath.cs`: `LateralFollow`, a dead zone with slow recentring.
  - `Assets/Scripts/Riding/FollowCameraSettings.cs`: `driftMetres` 2.5 (about 6% of the screen width each way at
    45 m) and `driftReturnTime` 1.5 s.
  - `Assets/Scripts/Riding/FollowCamera.cs`: the aim follows smoothly along the view and up. Sideways, the rider
    has the drift zone's room, and the camera eases back to centre.
  - `Assets/Tests/EditMode/FollowCameraMathTests.cs`: 3 tests.
- Tests:
  - The engine-free EditMode check: 73 passed. The new tests failed (did not compile) first.
  - The runtime scripts compile against Unity's DLLs.
- Decisions made for you: 2.5 m of room and a 1.5 s return, a "tiny bit of room" as you described. Both are
  settings: 0 m puts the old pinned camera back.
- Not verified: the feel, in a build and in the editor.
