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
