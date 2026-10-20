# Sleep mode report

Started 2026-10-20T11:05:45Z · Ended (running) · Reason (running) · Commits (running) (`5268986..`)

## Summary
- Finished: (running)
- Skipped: (running)
- Waiting on you: see production/sleep-mode/questions.md
- Needs the PC: story 003's Unity half, story 007, feel tuning (production/sleep-mode/plan.md, "Left for the PC")
- Next: (running)

## Item 1: story-001-sim-core-replay, /story-done
- Verdict: COMPLETE WITH NOTES. Status set to Complete (`bb0e1c0`).
- Changed: `production/epics/first-chair/story-001-sim-core-replay.md` (Status, Completion Notes)
- Tests: `dotnet test sim/`, 80 passed; `sim-tests` CI green. A failing test makes `dotnet test` exit 1, which fails CI (checked with a deliberate break, then restored).
- Decisions made for you: closed the story (the recommended option) rather than logging the advisory notes as tech debt or leaving it open.
- Not verified: anything in Unity. Commit the `.meta` files on first open, then run the golden-trace check in the Editor and an IL2CPP build.

## Item 2: story-002-skiing-on-snow, /dev-story
- Result: implemented (`77371d7`). Carve and skid, tuck, brake, poling on flats, and five snow types read from a snow-type map. Sim version 1 to 2, recording format 1 to 2.
- Changed: 10 sim files (new `SnowMap.cs`; riding in `Simulation.cs`; 21 riding numbers in `Tuning.cs`; integer `Sin`/`Cos`/`Atan2` in `Fix64.cs`) and 9 test files (new `SnowRidingTests.cs`, `SnowMapTests.cs`, `Fix64TrigTests.cs`). Full list in the story's `## Files`.
- Tests: `dotnet test sim/`, 148 passed (was 80). Key tests were seen failing on deliberate breaks:
  - grip limit ignored
  - skid drag removed
  - grip read as groomed (this exposed a weak ice test, now fixed)
  - drag read as groomed
  - tuck without less air drag
  - brake removed
  - every slope treated as flat
- Engine review (unity-specialist): nothing blocking, and nothing above C# 8.
  - RISK: a `Tuning` with `MaxEdgeAngle` 0, or a negative drag, brake or gravity, throws divide-by-zero during a ride. Fix queued: validate in the `Simulation` constructor.
  - NOTE: `WrapAngle` can return up to 3 raw steps past pi. Still deterministic; left as is, because changing it changes ride bits.
  - NOTE: a few `int` hash codes overflow by design and are not on the step path.
- Decisions made for you (the story left these open):
  - Steering edges the skis toward the input at a set rate. The heading turns with the edge angle, not with speed, so a slow rider can still pivot.
  - Grip cancels sideways speed up to the snow's limit; past it, the skis skid.
  - Snow drag acts as friction, so a rider at rest on groomed snow gentler than about 2.9 degrees stays put. The old speed-proportional slide drag is gone.
  - Back (brake) wins over Forward. Forward tucks on slopes, and poles on flats until the pole-speed cap, then tucks.
  - The snow map is one byte per heightmap sample, with its own hash. A replay on a different snow map reports `SnowMapMismatch`.
  - Heading 0 faces +Z.
  - Braking is equally strong on every snow type.
  - Default numbers are placeholders: grip groomed 16, powder 13, crud 10, ice 6, slush 12; brake 8 m/s²; poling up to 4 m/s.
- Not verified: feel, which needs Story 003's Unity view and a playtest on the PC. Nothing was compiled in Unity.

## Item 4: story-003-test-slope-unity-view, /dev-story (sim half only)
- Result: sim half done (`5d9a50a`). The story stays In Progress; its Unity half needs the PC.
- Changed:
  - `tools/terrain/make_test_slope.py` and its pytest file;
  - `Assets/Terrain/TestSlope/`: `TestSlope.raw` heightmap and `TestSlopeSnow.raw` snow map (both Git LFS), plus the `TestSlope.txt` layout;
  - the sim's `SlopeLayout.cs` loader and two test files;
  - `sim-tests.yml` now checks out LFS.
- The slope:
  - 512 m square at 0.5 m per sample, heights 0 to 160 m;
  - a groomer with a steeper pitch, an 18 m-deep powder bowl, and three kickers (0.8, 1.2 and 1.6 m) in a park;
  - a 51-tree glade and a level run-out;
  - every snow type present, including ice on the pitch and slush at its foot.
- Tests:
  - pytest: 9 passed. Two runs are byte-identical, and the committed files match a fresh run.
  - `dotnet test sim/`: 198 passed (50 new), and CI is green with LFS.
  - Six breaks were watched failing.
- Decisions made for you:
  - 1025 samples at 0.5 m; 2049 would be 12 MB, and 513 too coarse for a kicker.
  - The snow map is a `.raw` in LFS rather than a `.bytes` in plain git.
  - The layout is one text file in whole millimetres and millidegrees. JSON was out: the sim has no parser for it.
  - Zones are rectangles painted in order.
  - A `start` record was added, so Unity and the tests share the run start.
  - Kicker profile: h = H·(s/L)². The lips stand at 21.8°, 25.6° and 28.1° to the slope.
  - Trees sit on a jittered 12 m grid with a fixed seed.
- For the PC (Unity half), none of it verified:
  - import the heightmap at 1025, 16-bit, Windows byte order, 512 x 160 x 512 m, and check that row 0 is z = 0;
  - get the `.raw` bytes to the sim in a build (StreamingAssets or an importer); the `.txt` loads as a TextAsset;
  - then the stepping, skier, camera, input, Unity replay and playtest.

Engine follow-up from item 2 (`41e4a12`): the sim now rejects a `Tuning` with `MaxEdgeAngle` 0 or below, and a rider at rest no longer divides by zero under bad tuning. Rides are unchanged (golden trace matches). 201 tests pass.
