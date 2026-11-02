# Draft: a skill tree

> **Status:** Draft for the user to choose from (sleep mode, 2026-11-02, at the user's request: "brainstorming ...
> skill tree"). Nothing here is decided. Companion drafts: `story-outline.md`, `gear-progression.md`.

## Ground rules (from the project, not new)

- **Anything that changes a ride goes through the sim.** A rider's ranks and unlocked moves are copied into the run's
  `RunSetup` at its start and stored in the recording, so replays and ghosts ride with the skills they were recorded
  with (Bluebird's principle 2).
- **The tuned feel is sacred.** The riding was tuned by feel over many playtests (stories 008 and 009); a skill tree
  must never make a new player ride a worse version of it for hours.
- **Freeride has everything.**

## Where it starts from

- **Bluebird's abilities** (`design/reference/bluebird-spec.md`, Abilities): seven abilities (Pop, Spin, Balance, Tree
  Sense, Float, Edge, Stoke) ranked 0 to 5, each capped by a crew's tier; one point a rider level, 29 points for 35
  ranks so nobody maxes everything; respec for cash. Its key idea: **rank 5 is exactly today's tuned feel**, and a new
  rider starts at 90 percent.
- **The brief's "next riding layer"**, out of the MVP's scope: combos, presses and butters, rewind, Stoke, boost and
  signature tricks. Those are moves the game doesn't have yet, and they make better unlocks than percentages.
- **What the sim already has** (all of it stays available from the start): carve, skid, quick turns, drift, hockey
  stop, brake, skate, pole, tuck, pop, spins, flips, five grabs, rails and boxes, trees and Flow, near misses.

## Three directions

### A. Bluebird's ranks

Seven abilities, five ranks each, capped by crew tiers. **For:** written already; small; maps straight onto tuning.
**Against:** every node is a percentage, so nothing new to do opens up; starting at 90 percent means early Career
rides worse than Freeride.

### B. A tree of moves (recommended, with C's earning)

Four branches. Most nodes unlock a move from the next riding layer; a few are small ranks. Points come from riding in
that branch's style (C). **For:** each unlock is something new to try on the next run; the riding itself never gets
worse; the next riding layer arrives in an order that teaches it. **Against:** each move is real sim work, built one
by one.

### C. Mastery by use

No points: carving levels Carving, airs level Air, and so on, unlocking that branch's nodes in order. **For:**
nothing to spend, no grind in styles you don't enjoy. **Against:** no choices to make.

**Recommended: B's tree, earned C's way.** Each branch has its own level, raised by riding that way (the game layer
counts sim events: carve time, clean landings, grind time, near misses). Each branch level gives one point for that
branch's nodes. A park rider grows the park branch without grinding races.

## The recommended tree (skis)

**Ranks** (marked R) are small: three steps of about 2 percent above today's tuning, tuned in the Feel Lab so the top
step still feels right. **Moves** (marked M) are new; the sim gates each one on a bit in `RunSetup`.

> **Measured since (2026-11-02):** each rank family at its top rank, applied to today's tuning, with the same scripted
> rides in the real sim as the gear draft's skis (`gear-progression.md`, "Measured in the sim"):
>
> | Top rank | 2% a step (as drafted) | 5% a step | 8% a step |
> |---|---|---|---|
> | Edge III (ice grip up, edge ramp down) | ice carve skids 76% of the time (78% with no ranks), leaves the ice 1% faster | 72%, 2% faster | 67%, 3% faster |
> | Race Tuck II (tuck air drag down) | top speed 127 km/h (126) | 129 | 132 |
> | Float III (powder drag and skid drag down) | 1% faster through powder | 2% | 3% |
> | Pop III (pop speed up, load time down) | ollie 1.57 m (1.40) | 1.87 m | 2.17 m |
> | Swing III (spin and flip rates up) | 497° in a one-second ollie (469°) | 539° | 581° |
>
> So the **air ranks are right at about 2% a step**: rank III is felt (12% higher, 6% more rotation) without
> breaking the park (at 5%, Pop III alone lands a 540 off a one-second ollie). The **snow ranks are not felt at 2%**
> and barely at 8% (3 to 5%): today's pace is set mostly by gravity and air drag. Either give them 8% steps and
> accept a small effect, or drop Edge and Float as ranks and leave them to the skis (the gear draft's bars move the
> same values further), keeping Race Tuck at 8% (rank II: top speed +5%). Decision 6 below.

### Carving (from carving, racing and speed)

| Node | Kind | What it does | Sim values or rule |
|---|---|---|---|
| Edge I to III | R | A carve bites sooner and holds on ice | `EdgeRampTime` down, `IceCarveGrip` up |
| Race Tuck I to II | R | Less drag in a tuck | `TuckAirDragFactor` down |
| Stoke | M | A meter filled by clean carves, near misses and clean landings | new state in `RiderState`, no drain |
| Boost | M (needs Stoke) | Spend Stoke for a short burst of speed | new input; a timed drag cut |
| Trench Carve | M | A deep carve in powder scores, and throws a wall of snow | scoring rule from edge angle and snow depth |

### Air (from jumps and tricks)

| Node | Kind | What it does | Sim values or rule |
|---|---|---|---|
| Pop I to III | R | Higher ollies and take-offs | `PopSpeedMax` up, `PopLoadTime` down |
| Swing I to III | R | Spins and flips come round quicker | `SpinRate`, `FlipRate` up |
| Tweaks | M | Hold a grab past a beat to tweak it for more points | scoring rule on grab time |
| Corks | M | Off-axis spins (Bluebird's Q and E) | new input and air state |
| Signature Trick | M (end of branch) | One named trick of your own, worth the most | a scored trick from a short list |

### Park (from rails, boxes and combos)

| Node | Kind | What it does | Sim values or rule |
|---|---|---|---|
| Balance I to III | R | Steadier on rails | rail wobble down |
| Combos | M | Tricks chained within a window multiply | Bluebird's combo window, 1.5 s |
| Presses and Butters | M | Nose and tail presses on the snow and on boxes, which keep a combo alive | new ground state |
| Switch | M | Riding backwards scores, and switch take-offs count | scoring rule |

### Trees and Powder (from glades, powder and Flow)

| Node | Kind | What it does | Sim values or rule |
|---|---|---|---|
| Float I to III | R | Keep speed through deep snow, straight and turning | `PowderDrag`, `PowderSkidDrag` down (not the turn factor: raised, it made a powder ski slower through turns; `gear-progression.md`) |
| Flow Keeper I to II | R | Flow lasts longer before draining | `FlowDrainDelay` up |
| Tree Sense | M | Gaps between trees glow faintly at speed; tree wells show | view hint from sim gap data (an assist, off on the hardest setting) |
| Pillow Lines | M | Drops off snow pillows score, and land softer | scoring and landing rule |

### Basics (one shared root, any branch's points)

| Node | Kind | What it does | Sim values or rule |
|---|---|---|---|
| Forgiveness I to II | R | Landings a little further off still count as clean or solid | landing angles up 2 degrees a step |
| Quick Recovery | R | A crash resets sooner | `CrashResetDelay` down |

### Counts

33 points of nodes in all, each rank one point: Carving 8, Air 9, Park 6, Trees and Powder 7, Basics 3. Each branch
levels to 10, so 40 points in all; a branch's points buy its own nodes or Basics, so every node opens eventually, with
points to spare. Decision 3 asks whether it should (Bluebird ran points short on purpose).

## What it needs in the code

- `RunSetup` gains the rank bytes and a move bitmask; the recording header stores them. The sim applies the ranks
  to its tuning once at the run's start, in fixed point, and gates each new move's input on its bit.
- Each move is its own story, tests first, as the riding stories were: Stoke and Boost, Combos, Presses and Butters,
  Corks, Tweaks, Switch, Signature tricks.
- The game layer counts each branch's riding from sim events and keeps the levels and points in the save.
- The Feel Lab gets a "Career ranks" view: each rank as a slider, so the top of each rank is tuned by feel.

## Decisions for the user

1. A, B or C? (Recommended: B's tree earned C's way.)
2. Start at today's feel and grow past it a little (recommended), or start at 90 percent and grow back to it
   (Bluebird)?
3. Can one rider unlock everything eventually, or should points run short so builds differ (Bluebird D2)?
4. Which next-layer moves matter most to you, and in what order should they arrive?
5. Respec: free at the lodge (warm and forgiving), or a cost?
6. The snow ranks (Edge, Float, Race Tuck): 8% steps for a small but real effect, or drop Edge and Float as ranks and
   leave grip and float to the skis (recommended: a ski choice already moves them by 15% a step; ranks on top would
   blur what a ski is for)? Race Tuck stays either way, at 8% a step.
