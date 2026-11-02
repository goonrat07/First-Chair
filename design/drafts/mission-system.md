# Draft: how missions read the riding

> **Status:** Draft for the user to choose from (sleep mode, 2026-11-02), the next step after the mission tables in
> `story-outline.md`: what a mission is made of, using only what the sim already reports, so the first story-mode
> work could start from it. Nothing here is decided or built.

## Ground rules (from the project, not new)

- **Missions read the ride; they never change it.** The sim (`Packages/com.firstchair.sim/`) knows nothing of
  missions. A game layer reads the sim's events and state after each step and decides what a mission has seen.
- **The same discipline as the sim:** the game layer is plain C# with no UnityEngine references, in its own package
  (say `Packages/com.firstchair.game/`), so it builds and tests under `dotnet test` like the sim does.
- **Deterministic, so testable from recordings.** The sim replays a recorded run exactly. A mission that reads only
  the sim's output therefore gives the same verdict on every replay: each mission can be proven with a recorded run
  that completes it, replayed in a unit test, with no Unity and no person riding.

## What the sim already reports

| The sim gives | Fields | A mission can ask |
|---|---|---|
| `Takeoff` event | speed | "jump", "get air at speed" |
| `Land` event | grade (clean, solid, sketchy), air time, height, spin, flip, grab and how long, landing angle, switch, Flow | "land N jumps", "land a 360", "grab", "land clean", "land switch" |
| `Crash` event | cause (over- or under-rotation, a tree trunk, a fall off a rail) | "no crash", "a crash restarts the objective" |
| `NearMiss` event | which tree, the gap | "near misses in a glade" |
| `BranchHit` event | which tree | "a clean line through the trees" |
| `GrindStart`, `GrindEnd` events | rail or box, how long, balance, fell off | "grind end to end", "a box then a rail" |
| `Reset`, `LeftHeightmap` events | | an objective restarts |
| `RunScore` | trick points, named tricks, repeats | "score N in one run" |
| `RiderState` each step | position, speed, heading, edge angle, skidding, snow type, stance, Flow, airborne, grinding | "reach a place", "under a time", "carve without skidding", "Flow above a half", "ride the powder" |

Everything act 1 asks for is in that table. What it lacks: a way to say *where* on the mountain (below), and a
turn counter (a carve is an edge change while not skidding: a few lines in the game layer, from `EdgeAngle` and
`Skidding`, not a sim change).

## Objectives: small checkers

Each objective is one small type with one job: given the sim's events and state after a step, has it been met,
and has it been spoiled (a miss restarts it; nothing fails a mission).

| Objective | Met when | Spoiled by |
|---|---|---|
| `Reach(spot)` | the rider is inside a spot | |
| `Cross(gate)` | the rider crosses a gate line, in its direction | |
| `RunClean(from, to)` | crosses `to` after `from` | a crash or reset between them |
| `Under(from, to, seconds)` | crosses `to` within the time | a crash, a reset, or the time running out |
| `CarveTurns(n)` | n edge changes without skidding | |
| `Land(n, grade, filter)` | n landings at or above the grade that pass the filter (a spin of at least k half turns, a flip, a grab, switch) | |
| `Grind(n, kind, whole)` | n grinds on rails or boxes, end to end if `whole` | falling off, if `whole` |
| `NearMisses(n, zone)` | n near misses inside a zone | a trunk hit |
| `FlowAbove(x, at gate)` | Flow above x as the rider crosses the gate | |
| `Score(n)` | the run's score reaches n | |
| `Follow(ghost, metres)` | the rider stays within the distance of a recorded run, at the same tick, to its end | falling back too far |

Missions are lists of objectives in order, written as data rather than code, so a new mission is a new entry.

## Where: spots and gates on the mountain

The slope layout already has records such as `start`, `zone`, `kicker`, `tree` and `rail`. Missions need two
more, which the mountain tool (story 015) would lay out like the rest:

- `spot <name> <x> <z> <radius>`: a place: a cast member's hangout, a mission's start, a lost ball.
- `gate <name> <x0> <z0> <x1> <z1>`: a line to cross, in one direction: a race's start and finish, a timing split,
  the base.

The sim doesn't read either. They live in the same file so a mountain and its missions are made together.

## The prologue and act 1 as data

What the missions in `story-outline.md` become (each also has its cast lines, which are not shown here):

| # | Starts at | Objectives |
|---|---|---|
| P1 | `spot express-base` | (the lift ride: see "The chairlift" below) |
| P2 | `spot express-top` | `RunClean(gate opening-top, gate base)`, then `CarveTurns(3)` on the way (both in one run) |
| P3 | `spot lift-shack` | `Under(gate runout-top, gate base, 40)` |
| 1-1 | `spot park-top` | `Land(2, solid)`, then `Land(1, solid, grab)` |
| 1-2 | `spot rail-line` | `Grind(1, box, whole)`, then `Grind(1, rail, whole)` |
| 1-3 | `spot fire-pit` | `Follow(wren-green-glade, 6)`, and `FlowAbove(0.5, gate glade-exit)` |
| 1-4 | `spot timing-hut` | `RunClean(gate race-start, gate race-finish)` through every gate, then `Under(..., ilse-record)` |
| 1-5 | `spot patrol-room` | `Reach(spot bowies-ball)` (in the powder off the groomer), then `Reach(spot patrol-room)` |
| 1-6 | `spot base` | `CarveTurns(1)` inside `zone tavi-1`, `Land(1, solid)` inside `zone tavi-2`, `NearMisses(3, zone tavi-3)` |
| 1-7 | `spot lodge` | the Pulse reaching its first mark (the save's count of everything above) |

## The chairlift

Riding the lift is view-side today: the view pauses the sim and carries the rider up, and the next run starts at
the top. To the game layer a lift ride is the end of one run and the start of the next, at a known lift's top
station. So P1 needs only "a run started at `express-top`", which the game layer can see from the run's setup.
Nothing about the lift has to move into the sim.

## How it would be tested

- Each objective type: unit tests on hand-made event and state streams (a landing at each grade; a crash between
  two gates; a grind that falls off).
- Each mission: a recorded run that completes it, replayed through the sim and the game layer in `dotnet test`. The
  recording is the proof: if a sim change ever breaks a mission's line (a jump that no longer clears, a time that
  can't be met), the test fails at once. It works the way the sim's own replay tests already do.
- The Unity side (markers, cast lines, the phone texts) only shows what the game layer says, so it needs a build and
  a screenshot like any other view story, and no mission logic.

## Decisions for the user

1. A game-layer package beside the sim, engine-free and tested like it (recommended), or mission logic in Unity
   scripts (quicker to start, but not testable from recordings)?
2. Spots and gates in the mountain file (recommended: a mountain and its missions are made together), or in a
   separate missions file per mountain?
3. Can a mission's objective ever fail (a race lost, a timer run out), or only restart (Bluebird's forgiving
   rule, and the story draft's)?
4. The first story-mode story: P1 to P3 on the test mountain (the lift, a clean run, a timed run-out), which proves
   the whole chain from sim events to a mission done on screen.
