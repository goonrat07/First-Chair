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

## Item 3: story-002-skiing-on-snow, /story-done
- Verdict: COMPLETE WITH NOTES. Status set to Complete (`919ce3a`).
- Tests: 201 passed; CI green.
- Decisions made for you: closed it (the recommended option).
- Not verified: feel and Unity (see item 2).

## Item 5: story-004-air-landings-crashes, /dev-story
- Result: implemented (`9223bb3`). Sim version 2 to 3.
  - Pop: loaded by holding jump, with a strength cap.
  - Takeoff: detected from the terrain itself, so all three test-slope kickers launch at their lips.
  - In the air: spins, flips, and five grabs (mute, tip, tail, Japan, safety).
  - Landing: graded clean, solid or sketchy, or a crash from over- or under-rotation.
  - Reset: after a crash, the rider goes back to the last safe spot after 1 s; a new Reset button does it at once.
  - Events: takeoff, land (with a summary of the flight for Story 006's trick scoring), crash and reset.
- Changed: `Simulation.cs`, `RiderState.cs`, `SimEvent.cs`, `SimInput.cs` (Reset is bit 6) and `Tuning.cs` (11 new numbers), plus the new `AirTests.cs` and updates to the determinism, scripted-ride and input tests.
- Tests: `dotnet test sim/`, 228 passed. Seven breaks were watched failing:
  - takeoff only from a pop;
  - crash cause inverted;
  - safe spot never sampled;
  - pop ignores hold time;
  - wrong grab mapping;
  - fixed crash delay;
  - landing ignores the spin.
- Decisions made for you:
  - Takeoff happens when the ground falls away faster than a tuned speed. A fixed 4 cm gap missed the lips, which are smooth.
  - The landing angle is the unfinished flip or spin, whichever is larger. Landing switch is fine.
  - The pop pushes along the slope's normal.
  - Spins and flips turn only while their key is held; holding grab stops rotation.
  - Reset puts you back at rest, facing the safe heading.
  - A `Reset` event was added so the view can snap the rider back.
- Playtest flags from the programmer:
  - Sharp crests at high speed can give very short hops; Story 006 may want a minimum air time.
  - The safe spot is often a kicker lip, so a reset can leave you at rest on the lip.
- Not verified: feel, and anything in Unity.

## Item 6: story-004-air-landings-crashes, /story-done
- Verdict: COMPLETE WITH NOTES. Status set to Complete (`6636874`).
- Engine review (unity-specialist): nothing blocking. Its RISK (gravity of 0 or less kept a flight going forever) and its tuning notes are fixed in `19e292a`. The sim now checks the whole `Tuning` when a ride starts. 243 tests pass, and rides are unchanged.
- Decisions made for you: closed it (the recommended option), although Story 003 is still In Progress. Story 004 needed only 003's finished slope data.
