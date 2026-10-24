# Sleep mode report

Started 2026-10-24T02:38:00Z · Ended (running) · Reason (running) · Commits 0 (`0f146e0..`)

## Summary
- Finished: (running)
- Skipped: (running)
- Waiting on you: 0 questions in production/sleep-mode/questions.md
- Needs the PC: the gamepad check and your playtest of each new build; story 007 sign-off
- Next: (running)

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
