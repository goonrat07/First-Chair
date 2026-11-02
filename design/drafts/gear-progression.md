# Draft: gear progression

> **Status:** Draft for the user to choose from (sleep mode, 2026-11-02, at the user's request: "brainstorming ...
> gear progression"). Nothing here is decided. Companion drafts: `story-outline.md`, `skill-tree.md`.

> **Changed 2026-11-02 (`season-loop.md`):** the user chose a fuller loadout: skis, boot flex, the bindings'
> release setting and mount point, wax and edge tune, picked each morning, with clothes and goggles cosmetic only.
> Gear is gated by crew level and won through challenges, events and clashing sponsors. The five bars and the
> measurements below carry over as the skis' stats, with bigger differences, since gear should change how riding
> feels.

## Ground rules (from the project, not new)

- **Anything that changes a ride goes through the sim** (Bluebird's principle 2; `.claude/rules/sim-code.md`). A
  ski's stats are copied into the run's `RunSetup` when the run starts, and the recording stores them, so a replay or
  a ghost rides with the gear it was recorded with. Menus, shops and rewards never touch the sim.
- **Freeride owns everything** (Bluebird's Freeride overrides): gear is a Career reward, never a wall in front of the
  riding.
- **The tuned feel is the reference.** Today's `Tuning.Default` is what the rental ski rides like; gear moves the
  numbers either way around it, by enough to feel (measured: about 5% on the snow, 15% in the air; see "Measured in
  the sim").

## What other snow games do with gear

| Game | Gear | Lesson |
|---|---|---|
| Grand Mountain Adventure 2 | Credits from challenges and exploring buy gear and clothing; each ski or board performs differently; some clothing only for the hardest medals ([App Store](https://apps.apple.com/app/id6475040075), [TapTap](https://www.taptap.io/app/33778115)) | A few distinct skis plus prestige cosmetics, paid for by just riding |
| Lonely Mountains: Snow Riders | Apparel to unlock, and extra skis with varying stats ([Way Too Many Games](https://waytoomany.games/2025/01/21/review-lonely-mountains-snow-riders/)) | Small and readable: a handful of skis, not a loot table |
| SSX (2012) | Boards tuned on speed, boost and tricks, bought with credits; functional gear (ice axes, wingsuits, armour) that changes how you ride its Deadly Descents ([Wikipedia](https://en.wikipedia.org/wiki/SSX_(2012_video_game)), [Gematsu](https://www.gematsu.com/2011/04/ssx-detailed-game-modes-gear-story-and-more)) | Gear that does something (a wingsuit, a headlamp) beats a bigger number |
| Riders Republic | Every bike, ski and board has stats, a rarity and a gear score; one reviewer admits rarely looking at the stats ([Twinfinite](https://twinfinite.net/guides/riders-republic-sponsors-work-unlock-earn/), [The Xbox Hub](https://www.thexboxhub.com/?p=117499)) | A gear score that only climbs turns gear into a chore; trade-offs are what make a choice |
| Bluebird (`design/reference/bluebird-spec.md`, Shops) | Six stats from 1 to 5, a 3 is neutral, each point 4% on one tuning value; gear trades off rather than upgrades; outfits cosmetic; beacon, headlamp, snowmobile and wing open places | Already fits this sim; the starting point below |

## Three directions

### A. Bluebird's stat gear

Every ski has six stats (1 to 5) mapped to tuning values; a dozen skis across the crews, bought with cash once a
crew's tier allows. **For:** written already; trade-offs built in. **Against:** six numbers per ski is a lot to read
for a warm, casual audience.

### B. Archetype skis (recommended)

About eight skis in five archetypes (all-mountain, carver, park, powder, charger). Each shows its stats as five short
bars, never a total or a score; each is a trade-off, not an upgrade. Functional gear opens places; cosmetics are the
prestige rewards. **For:** a choice the player reads at a glance ("this one floats, that one holds an edge");
small enough to tune by feel in the Feel Lab. **Against:** fewer items to chase than a loot game (on purpose).

### C. Cosmetic skis, functional gear only

All skis ride the same; only outfits and functional gear. **For:** nothing to balance, every ghost fair.
**Against:** loses GMA2's "each ski performs differently", which players like.

## The recommended direction in detail

### Ski stats: five bars, each a real sim value

A bar runs 1 to 5; 3 is today's tuning. Each step changes the values below by the share shown, on top of any skill
ranks (`skill-tree.md`). The tuning names are the sim's own (`Packages/com.firstchair.sim/Runtime/Tuning.cs`).

| Bar | What the player feels | Tuning values it scales | Per step from 3 |
|---|---|---|---|
| Edge | Holds a carve on hard snow; bites sooner | `GroomedCarveGrip`, `IceCarveGrip` up; `EdgeRampTime` down | 15% |
| Glide | Faster on the groomer, on ice and in a tuck | `GroomedDrag`, `IceDrag`, `SlushDrag` and `AirDrag` down | 8% |
| Float | Keeps its speed through powder and crud, straight or turning | `PowderDrag`, `CrudDrag`, `PowderSkidDrag`, `CrudSkidDrag` down | 15% |
| Pop | Higher ollies and kicker take-offs | `PopSpeedMax` up; `PopLoadTime` down | 4% |
| Swing | Spins and flips come round quicker | `SpinRate`, `FlipRate` up | 4% |

The first version of this table had 3 to 5% a step for every bar, Float raising the powder and crud turn factors, Glide
on all five snows and Edge on crud too. Measured in the sim (below), that moved the pace by under 2%, too little to
feel; the turn factors made a powder ski *slower* through powder turns (it pivots harder and scrubs more); Glide on
powder let the race ski beat the rental there; and crud grip under Edge made the powder ski worse in crud. The table
above is the measured fix.

A sixth, **Forgiveness** (landing angles and branch hits: `CleanLandingAngle`, `SolidLandingAngle`,
`SketchyLandingAngle` up 2 degrees a step; `BranchSpeedLoss` down 5%), is better as a skill than a ski stat: it is
the one bar every player would always max.

### The skis

Early skis sum to 15 (the rental's five 3s): pure trade-offs. Later skis sum to 17 or 18: a little stronger, as a
reward, but never best at everything.

| Ski | Archetype | Edge | Glide | Float | Pop | Swing | Sum | How it's earned |
|---|---|---|---|---|---|---|---|---|
| Hill Rental | All-mountain | 3 | 3 | 3 | 3 | 3 | 15 | Start |
| Corduroy | Carver | 5 | 4 | 1 | 3 | 2 | 15 | First race medal |
| Birch Twin | Park | 2 | 2 | 3 | 4 | 4 | 15 | First park medal |
| Pillow | Powder | 2 | 3 | 5 | 3 | 2 | 15 | First powder line in a storm |
| Ridge All-Mountain | All-mountain | 4 | 3 | 3 | 4 | 3 | 17 | Act 2 |
| Gate GS | Race | 5 | 5 | 1 | 3 | 3 | 17 | Gold in every race |
| Birch Pro | Park | 3 | 2 | 3 | 5 | 5 | 18 | Gold in every park event |
| Charger | Big mountain | 4 | 5 | 5 | 2 | 2 | 18 | The couloir, act 3 |

Each ski also gets a topsheet colourway or two (the PR_Ski model already exists; a recolour is cheap).

### Measured in the sim (2026-11-02)

Each ski's bars scaled `Tuning.Default` as the table above says, and the same scripted rides ran in the real sim on
the test slope, the same inputs for every ski (a throwaway probe in the sleep session; nothing in the repo changed).
The rides: a tuck straight down the groomer for 20 s (average and top speed, km/h); carved and quick S-turns on the
groomer for 20 s (average speed, and the share of the time skidding); powder straight and in quick turns for 16 s;
quick turns down the crud strip; carved turns through the ice patch (skidding share on the ice, and the speed leaving
it); and a full-load ollie on the groomer with spin held (air time, height, degrees turned).

**One bar at a time** (the others at 3):

| Bars | Tuck avg / top | Groomer carve, quick | Powder straight / turns | Crud turns | Ice carve: skid, exit | Ollie: air, height, spin |
|---|---|---|---|---|---|---|
| All 3 (the rental) | 74 / 126 | 70, 59 (skid 15%) | 69 / 65 | 56 | 78%, 78 | 1.04 s, 1.40 m, 469° |
| Edge 1 | 74 / 126 | 66, 52 (skid 57%) | 69 / 65 | 56 | 86%, 68 | same |
| Edge 5 | 74 / 126 | 71, 60 (skid 14%) | 69 / 65 | 56 | 63%, 81 | same |
| Glide 1 | 71 / 119 | 66, 58 | 67 / 63 | 55 | 81%, 70 | same |
| Glide 5 | 77 / 133 | 71, 64 | 72 / 68 | 58 | 86%, 73 | same |
| Float 1 | same | same | 66 / 62 | 54 | same | same |
| Float 5 | same | same | 72 / 68 | 58 | same | same |
| Pop 1 | same | same | same | same | same | 0.97 s, 1.20 m, 435° |
| Pop 5 | same | same | same | same | same | 1.12 s, 1.63 m, 502° |
| Swing 1 | same | same | same | same | same | 1.04 s, 1.40 m, 431° |
| Swing 5 | same | same | same | same | same | 1.04 s, 1.40 m, 506° |

**The eight skis:**

| Ski | Tuck top | Groomer quick | Powder turns | Crud turns | Ice: skid, exit | Ollie height, spin | Reads as |
|---|---|---|---|---|---|---|---|
| Hill Rental | 126 | 59 | 65 | 56 | 78%, 78 | 1.40 m, 469° | the reference |
| Corduroy | 129 | 64 | 63 | 55 | 62%, 82 | 1.42 m, 450° | holds ice, worse in powder |
| Birch Twin | 122 | 59 | 64 | 56 | 83%, 76 | 1.51 m, 507° | bigger, rounder air; slower |
| Pillow | 126 | 60 | 68 | 58 | 83%, 75 | 1.40 m, 450° | best in powder and crud of the early four |
| Ridge AM | 126 | 60 | 65 | 56 | 72%, 80 | 1.51 m, 484° | the rental, a little better |
| Gate GS | 133 | 65 | 65 | 56 | 60%, 82 | 1.42 m, 469° | fastest and grippiest on the groomer |
| Birch Pro | 122 | 58 | 64 | 56 | 78%, 77 | 1.63 m, 547° | the only 540 off a one-second ollie |
| Charger | 133 | 65 | 71 | 60 | 71%, 79 | 1.31 m, 432° | fast everywhere off-piste, poor in the air |

What the numbers say:

- **Edge is lopsided.** Today's grip sits just above where turns start to wash out, so a step down costs far more than
  a step up gains: Edge 1 skids 57% of its quick turns on the groomer and leaves the ice 13% slower, Edge 5 leaves it
  4% faster. No ski goes below Edge 2 above; if one should, its down steps want to be smaller (8%).
- **Glide trades against ice.** A faster ski reaches the ice faster and holds it worse (Glide 5: 86% skidding, slower
  out). That is a real trade-off and worth keeping.
- **Float is capped by today's powder.** Even at 15% a step, Float 5 is only 4% quicker through powder, because on the
  bowl gravity outweighs powder's drag by far. If powder skis should matter the way they do in GMA2, powder itself has
  to bog a rider down more (a sim tuning change, decision 7 below).
- **Pop and Swing are already felt at 4%** (ollie height 1.20 to 1.63 m; spin 431 to 506°), and they add up: the
  Birch Pro's 547° is a 540 off a one-second ollie, which no other ski reaches.

### Functional gear: opens places, not numbers

| Item | What it opens | When |
|---|---|---|
| Avalanche beacon, probe, shovel | Backcountry gates; the act 3 rescue | Hank's training, act 3 |
| Skins | Hike-to ridges (a slow climb on foot, skis on) | Act 3 |
| Headlamp | Night runs after the lifts close | Act 2 |
| Helmet camera | Clips and the filmer track | Act 1, from Tavi |
| Speed-riding wing | Fly the north face; the post-game | After the finale |

### Cosmetics: the prestige rewards

Outfits in five slots (jacket, pants, helmet, goggles, gloves), three colourways each. The best ones are not sold:
they are for the hardest medals (all diamonds on a mountain, GMA2's model) and the crews' top tiers. Costumes for the
spring festival.

### Paying for it

One currency (credits) from events, missions and exploring, plus the medals themselves as unlock keys. No
microtransactions. Prices sized so a player who rides a bit of everything can buy the early skis in act 1 and the
later ones by act 3.

## What it needs in the code

- `RunSetup` gains the ski's five bars (five bytes), and the recording header stores them; the sim multiplies its
  tuning by them once at the run's start (deterministic, fixed-point). Ghosts and replays carry their own.
- The Feel Lab can preview a ski: five sliders that set the bars, for tuning each ski by feel.
- A save with owned gear and the equipped ski; a shop or rack screen (UI).
- Snowboards later get their own list on the same five bars.

## Decisions for the user

1. A, B or C? (Recommended: B.)
2. Five bars, or Bluebird's six (with Stability) on skis?
3. Are later skis allowed to be slightly stronger overall (sum 17 to 18), or every ski a pure trade-off (sum 15)?
4. How gear is earned: medals and milestones (above), a shop with credits, or crew tiers (Bluebird)?
5. Real ski brands someday, or fictional ones only (Bluebird's: Birchline, Hollow Pine, Vantage, Cairn)?
6. How big should the differences be? The measured shares give about 5% in the ski's best place (top speed, ice exit,
   powder pace) and 15% in the air. Smaller and nobody feels gear; much bigger and the rental feels broken.
7. Should powder slow a rider more, so a powder ski's float matters (GMA2's deep snow)? Today the rental keeps about 95% of the
   Pillow's pace through the bowl. This is a change to the riding, so it would be its own story, ridden on the PC.
