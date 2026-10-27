# Sleep mode report

Started 2026-10-27T02:58:57Z · Ended (running) · Reason (running) · Commits 0 (`0d2953f..`)

## Summary
- Finished: (running)
- Skipped: (running)
- Waiting on you: 2 questions in production/sleep-mode/questions.md
- Needs the PC: stories 008 to 011 (In Review), story 003's PC playtest, and the look sign-off of everything below
- Next: (running)

## Usage
- At the start the seven-day limit was at its warning level (`allowed_warning`), resetting 2026-11-01 16:00 UTC. The
  user said not to stop for it and to carry on after resets, so besides the 5-hour resets a wake-up is scheduled for
  just after the weekly reset (the skill's default is to schedule nothing for a weekly limit).

## story-012-skier-detail-pass (In Review)
- Changed:
  - `art/source/CH_Skier/` and `Assets/Art/Characters/CH_Skier/`: the rounder, more detailed skier (0d2953f; the seat
    fix fbca6b6 came just before this run), with an in-repo seat check (`build/seat_only_copy.py`, `build/seat_check.py`)
  - `art/source/PR_Ski/`, `art/source/PR_SkiPole/` and their exports: toe and heel pieces with a lever, brake arms and
    accents; the pole's finger ridge, knob and ringed basket (729473d)
  - `Assets/Scripts/Riding/SkierView.cs`: the player's skier always draws LOD0 (310c30e)
  - `production/qa/evidence/story-012-skier-detail-pass/`: before-and-after rides, evidence and stamp
- Tests: Blender gates (silhouettes front, side and back at every LOD; `validate.py` 0 FAIL; FBX round trips; the seat
  check, 0 seat pixels in all 16 clips and 5,386 on the old model). CI run 40443808319 (bf20bd7): EditMode 224/224,
  Linux and Windows players build. Cloud rides before (a5091fb) and after: at rest, carving, a fast tuck and three
  grabs in the air; the seat shows in every before frame and in no after frame.
- Decisions made for you:
  - Body LOD0 kept at 4,269, under the story's 5k floor (more would smooth the flat facets that are the style); the
    question is in questions.md.
  - LOD2 at 1,262, above about 1k: decimation keeps the stripes' boundaries straight.
  - The player's skier always draws LOD0 (Unity's automatic LOD heights would switch to LOD1 under 25 percent of the
    screen); other skiers, when there are any, keep Unity's LODs.
  - Bindings are generic alpine toe and heel pieces with brakes (no specific product).
  - The in-air grabs were shot from jumps off flat snow: the park ride's line depends on the cloud's frame rate and
    neither build's run left the kicker cleanly.
- Found in passing: in both builds the ski tracks were drawn over the back half of each ski (the ribbon sits 15 mm
  off the snow, the ski's top 17 mm). Fixed as its own change (below).
- Not verified: the Unity editor; the look sign-off is yours, on the PC.

## Fix: ski tracks drawn over the ski tails (found in story 012's rides)
- Changed: `Assets/Scripts/Riding/Core/SkiTrailingEnd.cs` (new), `SkiTrack.cs`, `ViewHelpers.cs`, `SkierView.cs`,
  `RiderView.cs`; `Assets/Tests/EditMode/SkiTrailingEndTests.cs` (new, 7 tests).
- What: tracks start at the ski's trailing end (the tail riding forward, the tip riding switch) instead of under the
  boot, so the translucent ribbon never lies under the ski. The end follows the rider's velocity along the ski's line
  and keeps its end in a skid across the ski or below 0.5 m/s; a change of end breaks the track.
- Tests: local NUnit run (.NET 10) 219 passed; compiles against Unity 6.3's player DLLs. Seen in story 013's build.
- Decisions made for you: a short gap (up to the 0.3 m point spacing) can show between the tail and the track at low
  speed, in place of the ribbon over the ski.
- Not verified: the Unity editor.

## story-013-procedural-animation (In Review)
- Changed:
  - `Assets/Scripts/Riding/Core/`: `TurnLean`, `DampedSpring`, `TwoBoneIk`, `LegReach`, `PolePlantTimer` and
    `PoseVector` (bf20bd7), the lean that counts the frame's tilt (ee60523) and the lean that waits for speed
    (b90eccf), each with EditMode tests
  - `Assets/Scripts/Riding/SkierProceduralPose.cs` (new) and `SkierView.cs`: the four layers after the clip blend
    (c875a57); `RiderViewSettings.cs`: every number, on the Feel Lab's Effects tab
  - `production/qa/evidence/story-013-procedural-animation/`: rides with the layers on and off, evidence and stamp
- Tests: CI run 40510043401 (a399708): EditMode 238/238, Linux and Windows players build. Cloud rides, layers on and
  off: the first edge change (the bug and its fix), a slow and a fast carve, a fast edge change with the plant's
  reach.
- Found and fixed on the way: the first cloud ride (c875a57) laid a slow skier almost flat on the snow at every edge
  change. Two causes: the lean didn't count the frame's tilt to the slope (ee60523), and the sim's slow GMA-style
  pivots read as a 50 degree lean, so the extra lean now waits for speed (b90eccf). Ridden with one layer at a time
  before the fix, the lean layer alone laid the skier flat.
- Found about the cloud: the player draws about 0.75 frames a second there (1280x720 on four CPU cores, Mesa's
  software renderer), and a player Xvfb never focuses idles. Rides run in slow motion and the springs settle between
  frames, so motion can't be judged from cloud shots.
- Decisions made for you:
  - IK written in C# (`TwoBoneIk`) rather than Unity's Animation Rigging package, which the project doesn't have.
  - The layers' numbers live on `RiderViewSettings`, so the Feel Lab tunes them with no new asset.
  - The extra lean fades in from 6 m/s (about 22 km/h) to 14 m/s (about 50 km/h) and is capped at 15 degrees; the
    carve clips are taken to lean 30 degrees and the race clips 34.
- Not verified: the landing's compression, a roller with both skis on the snow, and every layer's motion (the cloud's
  frame rate); the Unity editor; the feel sign-off is yours, on the PC.

## story-014-procedural-snow-shader (running: the five snow types view is next)
- Changed:
  - `Assets/Shaders/SnowTerrain/` (new): `SH_Env_SnowTerrain.shader` and its add pass, forks of URP 17.3's terrain
    shader whose forward pass includes `SH_Env_SnowTerrainForward.hlsl`; the snow terms in `SH_Env_SnowEffects.hlsl`
    (938e312; the glints reworked in cadd45d)
  - `LightingSettings.cs`: 18 snow fields, on the Feel Lab's Light tab; `LightingRig.cs`: sets them as shader globals
    each frame; the scene builder's terrain material uses the new shader
  - `production/qa/evidence/story-014-procedural-snow-shader/`: pinned before and after views, the glint, wrap and
    ripple crops, the snow types, two frame-time logs, evidence and stamp
- Tests: CI run 40522231518 (cadd45d): EditMode 238/238, both shaders compile 64 ForwardLit variants for glcore and
  vulkan with no shader error, Linux and Windows players build. Cloud rides, pinned, against the stock shader
  (ee60523): every weight at 0 draws the stock snow (all but 4 differing pixels are on the skier); glints in 0.05
  percent of lit snow and none in the skier's shadow; shade bluer (blue minus red 26 to 32); the corduroy's
  terminators softer; ripples in powder, fading by 30 m; frame time 2.3 percent above the stock build's.
- Found and fixed on the way: the first build's glints (938e312) changed no pixel from the chase camera. Its 2 cm
  cells were under a pixel by 10 m, and its facets leaned too little to mirror a sun low behind the camera; the cells
  now grow with distance to stay 2.5 pixels wide, and the facets spread wider (cadd45d). Its default wrap (0.35) cut
  the corduroy's contrast by a quarter; now 0.15.
- Found about the cloud: the player runs at about 7 percent of real time on the test slope and 4.7 percent on the test
  mountain, and the chase camera needs a few seconds after a pinned restart before its frame holds still. A scratch
  console program on the sim (the planner) now picks where and when to pin a ride.
- Decisions made for you:
  - A fork of URP's own terrain shader in HLSL (Shader Graph has no terrain target in URP), the snow terms added after
    URP's lighting in the forward pass, so shadows, fog and the ambient stay URP's.
  - The values are shader globals set each frame, not material properties: every terrain gets them.
  - Defaults: glints 6 (powder 1, groomed 0.6, crud 0.35, slush 0.15, ice 0), shade 0.6, wrap 0.15, ripples 0.25 at
    0.5 m. The ripples are faint at 0.25: if you want them seen, raise "Snow ripple strength" in the Light tab.
- Not verified: the glints' twinkle in motion and the look at a real GPU's frame rate; the Unity editor; the look
  sign-off is yours, on the PC.

## story-015-mountain-design-tool (In Review)
- Changed:
  - `tools/mountain/` (new): `mountain.py` (`build`, `preview`, `check`, `new`), `README.md` (the format, field by
    field), `test_mountain.py` (31 tests)
  - `design/mountains/`: the test mountain in the format (`test-mountain.mountain.toml`) and Pine Bowl
    (`pine-bowl.mountain.toml`), built into `Assets/Terrain/PineBowl/`
  - `tools/terrain/make_test_mountain.py`: a thin wrapper over the tool, so story 010's tests still run
  - Unity: `Editor/MountainCatalog.cs` (new), the scene builder makes a scene per mountain folder, the CI build carries
    them all and copies their files, the Feel Lab's places are the build's scenes
  - `sim/FirstChair.Sim.Tests/`: `MountainRider.cs` (shared), `EveryMountainTests.cs` (new): every mountain loads and
    every run is ridden to the base area
  - `production/qa/evidence/story-015-mountain-design-tool/`: the previews, `check` output, the Feel Lab's places, the
    Pine Bowl ride, evidence and stamp
- Tests: pytest `tools/mountain` and `tools/terrain`, 66 passed; `dotnet test sim/`, 435 passed; CI run 40510043401
  (a399708): EditMode 238/238, both players build, Pine Bowl in StreamingAssets, listed in the Feel Lab and ridden.
- Found and fixed on the way: the new sim test rode Pine Bowl's green to a stop at the base area's edge; its runs
  ended 1 to 3 m inside it, so they now end 25 m in, and `check` reports how far into the base area every run ends.
- Decisions made for you: TOML for the format (comments, Python reads it with no extra package); the test mountain's
  algorithm kept and checked byte for byte rather than rewritten; mountains found by folder, so a new mountain is a
  data change; Pine Bowl's layout (runs, features, lift) is a first design for the user to change.
- Not verified: the Unity editor; riding Pine Bowl's runs top to bottom (the cloud's slow motion covers a few
  metres); the look sign-off is yours, on the PC.
