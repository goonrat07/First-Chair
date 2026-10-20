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
