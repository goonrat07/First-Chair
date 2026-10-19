<!--
LEGACY REFERENCE. This is the gameplay and story spec written for Bluebird, an
earlier browser prototype, converted from the original .docx on 2026-10-19.

How to use it for First Chair:
- Design intent only. Its tech (TypeScript, src/ paths, Vitest, Playwright,
  window.__game, three.js-era tooling) does not apply to the Unity project.
- Where it contradicts the user or design/game-brief.md, they win.
- It says "Bluebird" and assumes riding already works. In First Chair, the base
  skiing system is being rebuilt from scratch first (see design/game-brief.md).
- Formulas and numbers are starting values, not commitments.
-->

# Bluebird: Gameplay and Story Build Spec

Oct 19, 2026 · @Miguel Lucas

This spec is everything needed to turn Bluebird's riding into the full game: every mode, system, number, mission and line of story, written so someone who has never seen the project can build it. It assumes the basic riding already works (the Baseline below says exactly what that means).

## How to use this spec

Read this tab first, then build in the order the last tab gives. Each tab stands alone once you know the terms defined here.

| Tab                         | What it holds                                                                                                                          | Read it when                      |
|-----------------------------|----------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------|
| Read me first               | The baseline you start from, the architecture, the loops, terms and decisions                                                          | Before anything                   |
| Core systems                | Rules shared by every mode: sim events, trick scoring, combos, Stoke, Flow, Heat and Mountain Ops, clips, discovery, the clock, travel | Before building any mode          |
| Modes and activities        | Freeride, every event type, Patrol Chase, the Career day, daily challenges, Rope Drop, After Hours, photo mode, custom challenges      | When building a mode              |
| Progression and economy     | Every number for XP, levels, abilities, crews, Followers, cash, the shop catalogue, Mountain Pulse and act gates                       | When building rewards and unlocks |
| Story and missions          | The cast, how story is shown, and every mission in all four acts with triggers, objectives, rewards and lines                          | When building Career content      |
| Data, saves and build order | TypeScript shapes, where files live, the save format, and a step-by-step build order with a test for each step                         | When planning the work            |

Three rules hold everywhere:

1.  **Every number is a starting value.** Each one lives in a tuning or content file, never inline in code, so play tests can change it.

2.  **Anything that changes a ride goes through the simulation.** Abilities, gear stats and pursuers change how a run plays out, so they are part of the deterministic sim and recorded in the replay header. Menus, rewards and story never touch the sim.

3.  **Decisions marked D1 to D8** follow Miguel's answers, listed at the end of this tab.

## The baseline: what must already work

This spec starts from a game where one rider can ride a mountain well. Every item below exists in Bluebird today unless it says **new**; the new ones are specified in Core systems before anything uses them.

### Riding (src/sim/rider.ts)

- [x] A fixed 120 Hz simulation step that is deterministic: the same RideInput sequence from the same start gives the same ride, to the bit.

- [x] RideInput is six held keys: left, right, up (W), down (S), jump (Space), grab (Shift). It packs into one number per step for replays.

- [x] Carving and skidding, tucking, braking, poling on flats, snow types (groomed, powder, crud, ice, slush) with their own grip and drag.

- [x] Jumps: hold Space to load, release to pop. In the air A/D spin, W/S flip, Shift plus a direction grabs.

- [x] Landings graded clean, solid or sketchy, or a crash; scoreTrick() names and scores each jump.

- [x] Rails and boxes with grinds scored by time and balance.

- [x] Tree near misses build the Flow meter (0 to 1); branch clips cost speed and Flow; trunk hits crash.

- [x] Crash and tap-R reset to the last safe spot.

- [x] Skis and snowboard, swapped with F.

- [ ] **new** Butters and presses on the ground (Shift plus a direction while on snow).

- [ ] **new** Hold R to rewind 3 seconds.

- [ ] **new** Speed boost and signature tricks from the Stoke meter.

### World and travel

- [x] Kettle Peak terrain with runs (RUNS), runAt(), lateral(), Summit Road and the base village.

- [x] Chairlifts (Kettle Express, Summit Chair): board with E, hold W to hurry, unload at the top.

- [x] Walking on foot (B), entering buildings, the dogs Bowie and Pepper, instanced crowds that cheer.

- [x] Time-of-day presets (T) and weather (Y), purely visual.

- [ ] **new** Snowmobile, skins and splitboard, speed-riding wing (Core systems, Travel).

- [ ] **new** A game clock that drives light and weather in Career (Core systems, Clock).

### Events and replays

- [x] EventDef with a start, a finish, points or time scoring and four medal targets; Kettle Slopestyle and Glade Dash exist.

- [x] Repeat decay: the same trick scores half each time it's repeated in a run.

- [x] Recordings are an input mask per step plus a header (physics version, sport, assist). Ghosts replay them.

- [x] Bots (src/sim/bots.ts) ride events to set medal targets.

### What the sim must expose

The game layer reads these from RiderState after each step and never writes them:

| Field                    | Meaning                                                          |
|--------------------------|------------------------------------------------------------------|
| pos, vel, heading        | Position (m), velocity (m/s), facing (rad)                       |
| grounded, crashed, rail  | Contact state; rail is the grind in progress, or null            |
| flow                     | Flow meter, 0 to 1                                               |
| lastTrick, landings      | Last scored trick and a counter that ticks on each landing       |
| snow                     | Snow type under the rider                                        |
| **new** events           | The list of sim events from this step (Core systems, Sim events) |
| **new** stoke, boostTime | Stoke meter 0 to 100 and seconds of boost left                   |

## Architecture

The game is two layers. The **simulation** (src/sim/) is deterministic and is the only thing that moves the rider or the pursuers. The **game layer** (new: src/game/) runs activities, rewards and story by reading what the sim reports. Rendering reads both and changes neither.

*(Diagram not included in this copy; see the original .docx.)*  
*architecture · input, sim, game systems, save*

What goes in the sim and what doesn't:

| In the sim (recorded, replayable)                           | In the game layer (not in replays)            |
|-------------------------------------------------------------|-----------------------------------------------|
| Rider physics, tricks, Flow, Stoke, boost, signature tricks | Combo banking, event scoring, medals          |
| Mountain Ops pursuers and Heat (seeded random)              | Missions, daily challenges, phone, scenes     |
| Ability ranks and gear stats, fixed at run start            | XP, Rep, cash, Followers, unlocks             |
| Closed-run ropes and zones (as data the sim reads)          | Clips, discovery grid, Mountain Pulse, crowds |

### One frame, in order

1.  Read the keyboard into RideInput; append its mask to the recorder.

2.  Step the sim at 120 Hz as many times as the frame needs (src/game.ts already does this). Each step fills rider.events and ops.events.

3.  After each step, the game layer drains the events into, in this order: the **combo tracker**, the **activity runner** (whatever event, mission, chase or challenge is live), the **clip recorder** and **discovery grid**, then the **story director** (mission objectives listen here too).

4.  Rewards never apply mid-run. The activity runner returns a Payout when it ends; the **progression ledger** applies it, checks unlocks and queues phone messages.

5.  Render interpolates between the last two sim states; the HUD reads the game layer.

6.  Autosave after any payout and at day end.

### Run setup

Every ride segment (an event, a chase, a free run between resets) starts from a RunSetup that the sim copies once: sport, assist level, ability ranks, gear stats, Heat start level and the random seed. The recording header stores the same RunSetup, so a ghost or leaderboard check rebuilds the exact run.

## The four gameplay loops

Four loops nest inside each other, and each pays into the next: tricks fill meters, runs pay Rep and Followers, days bank them, and the season spends them on new mountain. Freeride uses only the first two loops plus leaderboards.

*(Diagram not included in this copy; see the original .docx.)*  
*gameplay loops · four timescales*

| Loop       | Length                | What the player decides                                                 | What it pays into                    | Specified in         |
|------------|-----------------------|-------------------------------------------------------------------------|--------------------------------------|----------------------|
| Moment     | Seconds               | Which trick, which gap in the trees, when to spend Stoke                | Combo score, Stoke, Flow             | Core systems         |
| Run        | 1 to 3 minutes        | Which line, event or challenge; whether to film it                      | Medals, cash, Rep, XP, a clip        | Modes and activities |
| Career day | About 20 real minutes | Which dailies, missions and Rope Drop; whether to push into After Hours | Banked rewards, Pulse, the day recap | Modes and activities |
| Season     | 12 to 15 hours        | Which crews to raise, which abilities and gear                          | Acts, areas, the Kettle Classic      | Progression, Story   |

Failing is cheap at every level: a crash resets in a second, a failed event restarts at the gate, a lost After Hours pot only costs that night, and no mission can be failed for good.

## Conventions and glossary

### Conventions

- **Units:** metres, seconds, metres per second, radians in code and degrees in text. Points, cash, Rep, XP and Followers are whole numbers.

- **Time:** the sim step is 1/120 s. "Game time" means the in-game clock (08:30 to 22:00); "real time" means seconds the player sits through.

- **IDs:** lowercase kebab-case strings that never change once shipped, such as kettle-slopestyle, a2-05-lights-out, ski-birch-twin. Saves store IDs, never array positions.

- **Randomness:** the sim uses one seeded generator (mulberry32 is fine) seeded from RunSetup.seed. The game layer uses its own generator seeded from save.seed plus the day number, so the same save gets the same dailies.

- **Tuning:** riding numbers live in src/sim/tuning.ts; progression and content numbers live in src/content/ (Data tab). Nothing here is hard-coded in logic.

- **Keyboard only, two keys at most** for any trick or action.

### Glossary

| Term              | Meaning                                                                                                                               |
|-------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| Activity          | Anything with a start and an end that can pay out: an event, a mission, a daily challenge, a Rope Drop, a chase, an After Hours night |
| After Hours       | The Career night session, 16:30 to 22:00 game time, where Mountain Ops patrols and rewards multiply until banked                      |
| Banked / unbanked | Unbanked rewards are held in a pot during After Hours and lost if caught; banked ones are safe                                        |
| Clip              | A saved stretch of up to 60 s of riding, stored as inputs plus a starting snapshot, scored and posted for Followers                   |
| Combo             | A chain of tricks linked within the combo window; its total is multiplied by how varied it is                                         |
| Crew              | One of four rider groups (Park Rats, Glade Folk, Gatekeepers, Backcountry). The Filmer track works like a fifth crew                  |
| Discovery         | How rarely ridden the cells of a clip's path are; the main input to clip originality                                                  |
| Flow              | 0 to 1 meter built by near misses with trees; raises speed and trick points                                                           |
| Followers         | The Filmer track's Rep, earned from posted clips                                                                                      |
| Heat              | 0 to 5 level of how hard Mountain Ops is chasing you                                                                                  |
| Mountain Ops      | Summit Corp's private rules crew who chase you; never the real ski patrol                                                             |
| Mountain Pulse    | 0 to 100 measure of how alive Kettle Peak is; drives crowds and the ending                                                            |
| Rep               | Respect earned with one crew; raises that crew's tier                                                                                 |
| Rope Drop         | The once-a-day Career run down a roped-off run without getting caught                                                                 |
| Signature trick   | A big unique trick spent from a full Stoke meter                                                                                      |
| Stoke             | 0 to 100 meter filled by tricks and close calls; spent on boost or a signature trick                                                  |
| Tier              | A crew's level, 1 (Newcomer) to 5 (Legend)                                                                                            |
| Tier-up           | One step up one crew's tiers; acts open on the total of tier-ups across all five tracks                                               |

## Decisions

Miguel answered all eight on Oct 19, 2026, and the spec is written to these answers.

| \#  | Question                              | Answer                                                                          | Where it shows up        |
|-----|---------------------------------------|---------------------------------------------------------------------------------|--------------------------|
| D1  | How do acts open?                     | On total tier-ups across any crews                                              | Progression, Act gates   |
| D2  | Can one rider max every ability?      | No: 29 points for 35 ranks, respec for cash                                     | Progression, Abilities   |
| D3  | Story tone                            | Warm and funny                                                                  | Story, every scene       |
| D4  | Does Cass stay a rival?               | Yes, Cass is your rival to the end and your last opponent at the Kettle Classic | Story, Cast and Act 4    |
| D5  | Can Career end with Kettle Peak sold? | No; the Pulse sets how big the win is                                           | Story, Ending            |
| D6  | Who is the player?                    | Your own silent rider                                                           | Story, Cast and delivery |
| D7  | When do dailies refresh?              | Each in-game day, plus one bonus challenge each real day                        | Modes, Daily challenges  |
| D8  | Later mountains in Freeride?          | Open as soon as they're built                                                   | Modes, Freeride          |

# Core systems

The shared rules every mode uses: the events the riding sim reports, scoring, the meters, Heat, clips and the clock.

## Sim events

The sim reports what happened each step as a list of small records, and every game system listens to that list instead of polling rider state. Add events: SimEvent\[\] to RiderState, clear it at the start of stepRider(), and push records as things happen. Pursuers push to ops.events the same way.

| Event                   | Fields                                                               | Pushed when                                                                    |
|-------------------------|----------------------------------------------------------------------|--------------------------------------------------------------------------------|
| takeoff                 | speed, fromRail                                                      | The rider leaves the snow (or a rail) with upward speed above 1 m/s            |
| land                    | trick (or null), quality, airTime, height (max metres above takeoff) | Back on snow after a takeoff, without crashing                                 |
| grindStart / grindEnd   | railId, trick on end                                                 | Getting on and off a rail, box or log                                          |
| press                   | kind (nose, tail, butter), time, trick                               | A ground press or butter ends after at least 0.3 s                             |
| nearMiss                | treeId, gap (m), speed                                               | Passing a trunk inside FLOW.nearDist without touching (once per tree per pass) |
| branchHit               | treeId                                                               | Clipping branches (existing stumble)                                           |
| crash                   | cause: tree, overRotate, underRotate, cliff, wall, treeWell, pursuer | The rider goes down                                                            |
| reset                   | kind: tap or rewind                                                  | R is used                                                                      |
| zone                    | zoneId, entered (true/false)                                         | Crossing a zone edge (Core systems, zones list below)                          |
| freshTrack              | cell                                                                 | Riding a powder cell nobody has ridden this game day                           |
| boost                   | stokeSpent                                                           | A boost starts                                                                 |
| signature               | id, landed                                                           | A signature trick starts, and again when it lands or fails                     |
| spotted / lost / caught | pursuerId                                                            | From Mountain Ops: first sight, losing sight, catching you                     |

### Zones

A zone is a named area the sim can test cheaply: a polygon in x/z, or a run corridor (runAt() plus a lateral range). The content file lists them. Kinds used by this spec:

- **run** (each run in RUNS), **glade** (with a rating), **park**, **pipe**

- **closed** (a roped-off run; entering it raises Heat)

- **hut** (warming huts where After Hours pots are banked)

- **crowd** (lodge terraces and lift maze; stopping here hides you from Ops)

- **backcountry** (needs a beacon; a gate at the boundary rope)

- **village**, **pond**, **stage** (story locations)

Zones are data the sim reads, so closing a run in Career changes the sim only through the RunSetup the run started with.

## Trick scoring and combos

Single tricks are already scored by scoreTrick() and scoreGrind() in src/sim/tricks.ts; this section adds ground presses, the combo chain on top, and the rules every mode uses to total a run.

### Single tricks (existing, restated)

```math
\text{air} = \big(100\,h + 450\,f + g + 60\,t\big) \times q \times (1 + 0.5\,\text{Flow})
```

where h is half turns of spin, f is full flips, g is the grab bonus (150 + 300 per second held, capped at 600, only if held at least 0.15 s), t is air time in seconds, and q is landing quality: 1.5 clean, 1.0 solid, 0.6 sketchy. The Flow factor uses FLOW.trickBonus (currently 0.5). Grinds score RAIL.pointsPerSecond per second, times the slide bonus and quality. A grind into a jump joins as "Boardslide to Backside 180".

### Ground presses and butters (new)

On snow, holding Shift with a direction key presses: W is a nose press, S a tail press, A or D a butter that rotates the rider. Turning still works with the other direction key, so it stays two keys.

| Press              | Score                                         | Name                   |
|--------------------|-----------------------------------------------|------------------------|
| Nose or tail press | 50 per second held, from 0.3 s, capped at 3 s | Nose Press, Tail Press |
| Butter             | 120 per 180 degrees turned on the snow        | Butter 180, Butter 360 |

A press shorter than 0.3 s scores nothing and doesn't link a combo. Pressing above 9 m/s on groomed snow slowly bleeds speed (0.5 m/s per second) so it isn't free.

### Combos

A combo is a chain of scored tricks (air, grind or press) where each next trick **starts** within 1.5 s of the previous one ending (COMBO.window). Holding a press keeps the window open, which is how skilled riders chain a whole run.

```math
\text{combo} = \Big(\sum_i s_i \cdot 0.5^{\,r_i}\Big) \times \min\!\big(4,\ 1 + 0.25\,(d - 1)\big)
```

where s is each trick's score, r is how many times that exact trick name already appeared in the current scoring window (below), and d is the number of distinct trick names in the chain. So a 5-trick chain of all different tricks scores double.

- **Banking:** when the window runs out, the combo banks: its points are added to the run total and a comboBanked record goes to the game layer (Stoke and XP read it).

- **Losing it:** a crash before banking loses the whole unbanked combo. The HUD shows the live combo total and multiplier under the trick callout so the risk is visible.

- **Scoring window for repeats:** inside an event, repeats count across the whole event run (as now). In free riding, a trick's repeat count decays by one every 20 s, so landing the same 360 every jump pays less but never zero for long.

### Trick names

Names follow scoreTrick(): optional "Switch", then flips ("Backflip", "Double Frontflip"), then spin ("Backside 720" on a board, "Left 720" on skis), then the grab. Grabs by direction:

| Direction with Shift in the air | Snowboard | Skis      |
|---------------------------------|-----------|-----------|
| None                            | Mute      | Mute      |
| W (up)                          | Nose Grab | Tip Grab  |
| S (down)                        | Tail Grab | Tail Grab |
| A (left)                        | Melon     | Japan     |
| D (right)                       | Indy      | Safety    |

The trick ID used for repeat counting, challenges and clips is the name in lowercase kebab-case, such as switch-backside-540-indy.

### Rewind (new)

Tap R resets to the last safe spot (existing). Holding R for 0.4 s rewinds 3 s instead: the sim keeps a ring of 4 full RiderState snapshots, one per second, and restores the one 3 s back. Rewind is off in events, Rope Drops, chases and After Hours (it would break pursuit), and every rewind pushes a reset event so clips and challenges can ignore the undone stretch.

## Stoke meter and signature tricks

Stoke is a 0 to 100 meter in the sim that riding well fills and the rider spends on a speed boost or, when full, a signature trick. Both spends are read from RideInput patterns, so they replay exactly.

### Filling and losing Stoke (STOKE in tuning.ts)

| Source                             | Stoke                                       |
|------------------------------------|---------------------------------------------|
| A combo banks                      | \+ combo points / 25, at most +40 per combo |
| Clean landing                      | +4                                          |
| Near miss with a tree              | +1.5 x closeness (0 to 1, as Flow uses it)  |
| Riding a fresh-track powder cell   | +0.5 per cell                               |
| Losing Mountain Ops (a lost event) | +15                                         |
| Crash                              | lose half your Stoke                        |

Stoke never drains on its own. The numbers above are at Stoke ability rank 5; lower ranks scale gains down (Progression tab, Abilities). Stoke caps at 100.

### Boost

- **Input:** double-tap W (two presses within 0.25 s) while on snow with at least 25 Stoke.

- **Effect:** spends 25 Stoke and pushes the rider along their heading at 4 m/s squared for 3 s at Stoke rank 5 (2 s plus 0.2 s per rank). The push stops adding above 30 m/s. Boosting in the air does nothing.

- **Feel:** wind roar, snow spray from the tails, the music layer swells. A boost event goes out for clips and challenges.

### Signature tricks

- **Ready:** at 100 Stoke the HUD shows "Signature ready" and the rider glows faintly.

- **Input:** in the air, hold Shift and tap Space. No other move uses Shift and Space together in the air, so the pair is free.

- **Allowed when:** the predicted time to landing (ballistic estimate from height and vertical speed) is at least 1.1 s. Otherwise the input is ignored and nothing is spent.

- **What happens:** the rider plays a fixed 1.1 s move. Spin, flip and grab keys are ignored for that time; the sim holds rotation so the rider lands straight. Landing after it finishes is always clean. Touching snow before 1.1 s is a crash.

- **Score:** 2,500 points, times the clean landing 1.5, times Flow, and it counts as its own trick in the combo. Stoke drops to 0.

Each sport has one equipped signature, chosen at the rental shop. They unlock as follows:

| Signature       | Sport | Move                                          | Unlock                                  |
|-----------------|-------|-----------------------------------------------|-----------------------------------------|
| Method Moonwalk | Board | Deep method grab, back leg running in the air | Park Rats tier 3                        |
| Helicopter Hero | Skis  | Flat 720 with both poles windmilling          | Park Rats tier 3                        |
| Bluebird Spin   | Board | Slow laid-out 1440, arms wide                 | Park Rats tier 5                        |
| Telemark Tea    | Skis  | Telemark stance, pretend teacup, pinky out    | Park Rats tier 5                        |
| Lazy Boy        | Both  | Flat spin lying back, hands behind head       | Birchline Boards sponsor contract       |
| Cocoa Break     | Both  | Sips from a thermos mid-air, then tosses it   | Mountain Pulse 80 at the Kettle Classic |

Freeride has every signature unlocked.

## Flow and the trees

Flow already works (FLOW in tuning.ts: near misses inside 2.4 m fill it, branch clips cost 25%, it drains at 0.22 per second after 1.4 s, and full Flow cuts drag 30% and adds 50% to trick points). This section adds what the glades need around it.

### Gap sense and Tree Sense

Gap sense (ASSIST\[level\].gapSense, GAP) nudges the rider toward clear snow beside the trunk ahead. Miguel's play test found it sometimes steers away from a tight gap he was aiming at, so the rule is: **pick the gap nearest the rider's current heading, never the widest gap.** Only nudge when the rider holds no turn key or holds the key that already points at that gap.

The Tree Sense ability scales gap sense and Flow gain (FLOW.gainRate) as the Progression tab's Abilities table sets; rank 5 is today's tuning. From rank 3, tree wells show as a faint darker ring. The Real assist setting keeps gap sense off at every rank.

### Glade ratings

Every glade is a zone with a rating set by tree density. Tiers of the Glade Folk unlock them in Career.

| Rating       | Trees per 100 m²              | Extras                                 | Career unlock                |
|--------------|-------------------------------|----------------------------------------|------------------------------|
| Green        | under 1.5                     | Wide gaps, a few logs                  | Glade Folk tier 1 (Fernwood) |
| Blue         | 1.5 to 3                      | Pillows, stumps                        | Glade Folk tier 2            |
| Black        | 3 to 5                        | Tree wells, small cliffs, tight chutes | Glade Folk tier 3            |
| Double black | over 5, or any cliff over 4 m | Everything, plus steep fall lines      | Glade Folk tier 4            |

A locked glade has a rope with a Glade Folk sign at each entry. Ducking it doesn't raise Heat; instead Wren texts "Not yet, kid. Come find me." and the rider is reset to the rope. Freeride has no locked glades.

### Glade features

- **Logs** are rails of kind log in rails.ts; they grind like boxes.

- **Stumps** are small kickers (0.6 m) and **pillows** are rounded powder kickers (0.8 to 1.5 m); both are placed per glade in content.

- **Tree wells:** around trees with scale 1.3 or more, on powder, a ring from the trunk out 1.0 m. Entering it below 5 m/s is a crash (treeWell) with a 2 s dig-out animation; above 5 m/s it's a stumble that keeps 70% of speed.

- **Snow bombs:** purely visual, from branches above when you pass within 1 m.

### Stashes and fresh tracks

- **Stashes:** 20 hidden powder pockets on Kettle Peak, each 30 to 60 m across, refilled every morning. First visit pays \$100, counts for discovery, and adds the stash to your map. Glade Folk tier 4 shows every stash's location.

- **Fresh tracks:** powder cells (8 m, the same grid as Discovery) are fresh until someone rides them that game day. After a storm night, every powder cell is fresh again. Fresh cells pay +0.5 Stoke each, and double during the first game hour after the lifts open (08:30 to 09:30).

## Heat and Mountain Ops

Mountain Ops are Summit Corp's private rules crew. They chase you in Patrol Chase, Rope Drops and After Hours, and nowhere else. They live in the sim (new src/sim/ops.ts), seeded from RunSetup.seed, so a chase replays exactly. Two rules never bend: there is no fighting, only evading, and avalanche-closed terrain is a separate thing that is always deadly and never part of a chase.

### Heat

Heat is points from 0 to 500; the level is points / 100, rounded down, from 0 to 5.

| Raises Heat                                                   | Points                            |
|---------------------------------------------------------------|-----------------------------------|
| Ducking a rope into a closed run                              | +60                               |
| Each second inside a closed run while any pursuer can see you | +8                                |
| A trick landed in a pursuer's sight                           | \+ trick points / 50, at most +40 |
| Buzzing a pursuer (passing within 3 m above 10 m/s)           | +30                               |
| Boarding a lift in sight during After Hours                   | +30                               |
| Patrol Chase only: time                                       | +1 level every 60 s               |

While no pursuer has seen you for the **cool-down** (6 s + 2 s per level), Heat drains at 20 points per second down to the floor for the mode (Rope Drop 100, After Hours 100, Patrol Chase never drains).

### Pursuers

| Pursuer    | Appears from level | Speed on groomers                               | In powder | In trees                                | Sight                     | Notes                                                     |
|------------|--------------------|-------------------------------------------------|-----------|-----------------------------------------|---------------------------|-----------------------------------------------------------|
| Ops skier  | 1                  | up to 22 m/s, accel 3.5                         | 11 m/s    | 9 m/s, enters any glade                 | 60 m, 120 degree cone     | Can't drop cliffs over 3 m                                |
| Snowmobile | 2                  | 25 m/s on flats and cat tracks, 18 m/s downhill | 14 m/s    | Can't enter glades rated blue or harder | 80 m, 140 degree cone     | Can't drop over 2 m; loud, so the HUD shows its direction |
| Drone      | 3                  | 20 m/s, flies 25 m above you                    | same      | Loses sight under blue or harder canopy | 30 m circle straight down | Can't catch you, only spots for the others                |

How many are out at each level:

| Level | Skiers | Snowmobiles | Drones |
|-------|--------|-------------|--------|
| 1     | 1      | 0           | 0      |
| 2     | 2      | 1           | 0      |
| 3     | 3      | 1           | 1      |
| 4     | 4      | 2           | 1      |
| 5     | 5      | 2           | 2      |

New pursuers spawn out of your sight, 150 to 250 m away: skiers uphill on the run you're on, snowmobiles on the nearest cat track, drones from the nearest lift top. When the level drops, the farthest pursuer leaves.

### Sight

A pursuer sees you when you're inside its range and cone and the line between you is clear. Test it every 4 sim steps (30 Hz): sample the line every 4 m against the terrain height (blocked if the line is under the ground) and against glade canopy (blocked when the line runs more than 8 m through a blue or harder glade). Inside a **crowd** zone, a rider moving under 2 m/s is invisible.

### Behaviour

- **Hunting** (has sight, or saw you within the cool-down): steer at where you'll be in 1 s (your position plus velocity), ride its fastest legal line, and spread: the second skier aims 10 m left of you, the third 10 m right.

- **Searching** (lost you): go to your last seen spot, then sweep downhill along the run centerline for 20 s.

- **Patrolling** (Heat 0 in After Hours): ride set loops along runs and cat tracks from content.

- Pursuers are kinematic: they follow the snow surface with simple speed limits and turn rates (skier 1.6 rad/s, snowmobile 0.9 rad/s). They never crash, and they slow to their tree speed inside glades.

### Caught and escaped

- **Caught:** a skier or snowmobile within 1.5 m of you for 0.5 s while you're slower than it plus 2 m/s, or you crash within 8 m of one. The sim pushes caught, freezes for 1 s, and the activity ends (see each mode for what that costs).

- **Escaped:** every pursuer goes from hunting to searching. The sim pushes lost (+15 Stoke), and the game layer counts an escape for scoring and challenges.

- **Ways out:** break sight behind a ridge, dive into a blue or harder glade (snowmobiles can't follow, drones go blind), drop a cliff over 3 m, or stop in a crowd.

A chase never hurts the rider. Getting caught shows Ops "clipping your pass" with a hole punch, played as a joke.

## Clips and filming

The game is always recording, so the player never has to plan a shot: press V after something good and the last stretch becomes a clip. Clips are scored like a real audience would and posted for Followers, which is how creative riding outside events pays.

### The buffer

- Keep the last 60 s of input masks plus a full RiderState and ops snapshot every 1 s (60 snapshots). That's a few kilobytes and replays exactly.

- A reset, rewind, teleport or lift ride starts the buffer fresh, so clips never jump.

- **V** saves a clip: by default the last 15 s, or since the last reset if shorter. The album keeps 50 clips in the save; older unposted ones are dropped first.

- While saving, the game also logs the sim events inside the clip, which the score reads.

### Clip score

Six parts, each 0 to 100, measured over the trimmed clip:

| Part        | How it's measured                                                                                                                                                                                                                                 |
|-------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Air         | 20 per second of the longest air, plus banked trick points / 50; cap 100                                                                                                                                                                          |
| Speed       | (top speed in m/s - 10) x 5; cap 100                                                                                                                                                                                                              |
| Variety     | 15 per distinct trick name; cap 100                                                                                                                                                                                                               |
| Close calls | 8 per near miss, + 40 x peak Flow, + 25 per Ops escape; cap 100                                                                                                                                                                                   |
| Scenery     | Light (golden hour 40, blue hour 35, storm with falling snow 30, bluebird 25, flat light 10) + 30 if a landmark is on screen at least 3 s (summit, lodge, glove tree, sunset bench, festival stage) + 30 if the clip drops 100 m or more; cap 100 |
| Originality | From the Discovery map (next section)                                                                                                                                                                                                             |

```math
\text{score} = \big(0.20\,\text{Air} + 0.15\,\text{Speed} + 0.15\,\text{Variety} + 0.15\,\text{Close} + 0.10\,\text{Scenery} + 0.25\,\text{Original}\big) \times c
```

c is the camera multiplier (below), and the result is capped at 100. A clip with a crash in it is scored as a **blooper** instead: 30 + 10 per second of tumbling + 20 if a crowd or pursuer was within 30 m, capped at 70. Bloopers earn half Followers but never count as repeats.

### Posting and Followers

```math
\text{Followers} = \Big\lfloor \frac{\text{score}^2}{10} \times 0.5^{\,n} \times p \Big\rfloor
```

- n is how many already-posted clips share at least 60% of this clip's path cells (posting the same line again pays less).

- p is 1 for the first three posts of a game day and 0.25 after that, so filming stays a part of the day, not a grind.

- Views and likes in the in-game feed are cosmetic, drawn from the save's random stream: views = Followers total x (0.2 to 0.6) x score / 50.

- Followers are the Filmer track's Rep (tiers in Progression).

### Cameras and the replay editor

The replay editor opens a clip by rebuilding it from its snapshot and inputs, so any camera can film it after the fact.

| Camera       | Unlock        | Multiplier c | How it works                                                                               |
|--------------|---------------|--------------|--------------------------------------------------------------------------------------------|
| Chase cam    | Start         | 1.00         | The normal game camera                                                                     |
| Helmet cam   | Filmer tier 1 | 1.05         | First person, slight fisheye                                                               |
| Follow drone | Filmer tier 2 | 1.15         | Orbits and follows automatically; picks a side from the slope                              |
| Tripod       | Filmer tier 3 | 1.20         | Placed in the world from the phone before you drop; fixed, pans to follow you within 150 m |
| Fisheye lens | Filmer tier 3 | +0.05        | Toggle on any camera                                                                       |
| FPV drone    | Filmer tier 4 | 1.30         | Flown by hand in the editor: W/S speed, A/D yaw, Q/E height, Shift slow                    |
| Slow motion  | Filmer tier 5 | +0.05        | Up to 3 s at quarter speed per clip                                                        |

The editor has a one-button auto-edit (picks the best 10 to 15 s by the score above, cuts between owned cameras every 3 to 5 s, adds slow motion on the biggest landing) and a manual mode to trim, cut between cameras and mark slow motion. The best multiplier among cameras used for at least a third of the clip applies.

Photo mode (Modes tab) uses the same light and landmark scoring for still photos.

## Discovery map

The game remembers where you've ridden on an 8 m grid, and lines through rarely ridden snow score as original. This is what rewards finding fun lines nobody else rides.

### The grids

- **Visits:** one byte per 8 m cell over the mountain's bounds (Kettle Peak is about 3 km square, so about 375 x 375 = 140,000 cells). Each step on snow, the rider's cell counts one visit per run through it (count a cell again only after leaving it for 5 s), saturating at 255.

- **Trick spots:** one bit per cell, set when a scored trick lands there.

- **Fresh today:** one bit per cell, cleared each morning (all cells after a storm night), set when ridden. Feeds fresh tracks in Flow and the trees.

- Saved compressed (run-length, then base64); a mostly empty map is a few kilobytes.

### Originality score (0 to 100)

```math
\text{Original} = \min\!\Big(100,\ 100 \cdot \overline{\big(1 - \min(v,10)/10\big)} + 20\,k\Big)
```

- The bar is the average over the clip's path cells, where v is that cell's visit count before this clip.

- k is the number of tricks landed on cells whose trick-spot bit was clear (new tricks in new spots), up to 3.

- Groomed run cells inside a run's middle 60% count their visits double, so riding the obvious line down the middle never looks original.

### Later: everyone's map

With online play, the server keeps a shared visit map. Originality then blends your map (40%) with everyone's (60%), so a line nobody in the world rides scores highest. Until then, only your own map counts.

## Clock, days and weather

Career runs on a game clock that sets the light, the weather and what's open; Freeride keeps the clock still unless the player turns it on.

### The clock (src/game/clock.ts)

| Period                   | Game time      | Real length | Rate                              |
|--------------------------|----------------|-------------|-----------------------------------|
| Day                      | 08:30 to 16:00 | 20 minutes  | 22.5 game seconds per real second |
| Last chair warning       | 15:45          |             | Phone buzz and lift-line callout  |
| After Hours (from act 2) | 16:30 to 22:00 | 10 minutes  | 33 game seconds per real second   |

- The clock pauses in menus, the phone, scenes, photo mode and the replay editor. It keeps running in events.

- The light preset follows the clock (the existing presets eased between: morning, midday, golden hour from 15:00, blue hour from 16:30, night from 18:00). The sim never reads the clock; anything ride-changing (snow types) is fixed into RunSetup at the start of each run.

- Lifts open 08:30 and close 16:00. A rider on a lift at 16:00 still arrives.

### The season calendar

Days count up from 1. Nothing forces an act by date: an act starts the morning after its gate is met (Progression). The day number drives weather and a few story triggers.

### Weather

Each day's weather is rolled at dawn from the save's random stream, using the current act's table:

| Act | Bluebird                                                                 | Flat light | Light snow | Storm |
|-----|--------------------------------------------------------------------------|------------|------------|-------|
| 1   | 60%                                                                      | 25%        | 15%        | 0%    |
| 2   | 40%                                                                      | 30%        | 20%        | 10%   |
| 3   | Fixed 3-day cycle: storm, storm clearing into a powder morning, bluebird |            |            |       |
| 4   | 70%                                                                      | 30%        | 0%         | 0%    |

What weather does to the snow (fixed into RunSetup, so it is part of the ride):

- **After a storm night:** every off-piste cell is fresh powder, and a "Powder day" phone alert goes out at dawn.

- **Groomed runs** are corduroy each morning (the snowcats groom at night) and turn to crud after 12:00 where ridden heavily.

- **Act 2 and 3 bluebird mornings** before 10:00: north-facing groomers are ice.

- **Act 4 afternoons** after 13:00: groomers below the mid-mountain station turn to slush.

- **Storms** cut visibility to 120 m, and **flat light** hides small bumps (render only).

## Travel and vehicles

Everything here is freeride and Career only; events start at their gates.

### Lifts

| Lift           | Kind                                            | Status | Opens in Career       |
|----------------|-------------------------------------------------|--------|-----------------------|
| Bunny carpet   | Magic carpet at the village                     | new    | Act 1, from the start |
| Kettle Express | Chairlift, base to mid-mountain                 | built  | Act 1, mission A1-01  |
| Kettle Gondola | Base to the mid-mountain restaurant             | new    | Act 2, mission A2-01  |
| Summit Chair   | Chairlift to the summit                         | built  | Act 2, mission A2-01  |
| Ridge T-bar    | Surface lift to the north ridge hike-to terrain | new    | Backcountry tier 2    |

On any lift, hold E for 1 s to skip to the top (the clock jumps by the ride time). Lifts are never in events or replays. The Tab map fast-travels to the top or base of any opened lift; in Career that costs 30 game minutes.

### Snowmobile (Backcountry tier 3)

- E within 3 m to get on or off. W throttle, S brake and reverse, A/D steer. No tricks.

- Up to 25 m/s on flats and cat tracks, climbs slopes up to 30 degrees at 8 m/s, and can't enter blue or harder glades (a soft bounce at the edge).

- It parks where you leave it; from the phone, "Call the sled" brings it to the nearest cat track after 30 real seconds.

- It's in the sim (Ops can chase you on it). In After Hours, being seen on it adds double Heat because it's loud.

### Skins and splitboard (Backcountry tier 2)

With touring gear equipped (Cairn Tour skis or Cairn Split board), press B on snow to switch to touring: W climbs at 1.2 m/s on slopes up to 35 degrees, A/D steer. B again at the top switches back to riding. Hike-to ridges are zones only reachable this way.

### Speed-riding wing (Backcountry tier 5, skis only)

- On a slope steeper than 30 degrees, hold Space in the air for 0.5 s to open the wing. Space again on the ground stows it.

- Flying: A/D bank, W dives (2:1 glide, up to 25 m/s), S flares (4:1 glide, down to 12 m/s), neutral is 3:1. Touching snow keeps the wing up, so you can ski and lift off again.

- Allowed on Kettle Couloir's face and everywhere on Halvard Glacier; elsewhere the phone says the wind is wrong.

### Gear that opens places

- **Avalanche beacon:** needed to pass a backcountry gate. Without one, the gate stays shut and Hank texts you.

- **Headlamp:** lights 30 m ahead at night. Without it, only the lamp-lit runs are visible after 18:00.

# Modes and activities

Every way to play, as rules a programmer can build straight from: how each starts, runs, scores and ends.

## Mode map

The title screen leads to two places: Freeride, a sandbox where you move freely between riding and activities, and Career, a loop of game days. Patrol Chase lives inside Freeride; Rope Drops, dailies and missions live inside a Career day.

*(Diagram not included in this copy; see the original .docx.)*  
*mode map · Freeride and Career*

Esc always opens the menu for the mode you're in. Leaving Career mid-day saves at the last payout; the day resumes from the bunkhouse door at the same clock time.

## Freeride

Freeride is the sandbox: everything unlocked, nothing earned, every event playable for score. It is also how most players will first meet the game, so it loads straight onto the mountain.

### Rules

- Every lift, area, glade, event, piece of gear, camera and signature trick is available. Halvard Glacier and Yukimura appear in the mountain picker as soon as they're built (D8).

- Abilities default to rank 5. A Freeride settings panel has a slider per ability (0 to 5) for players who want it harder.

- Mountain Ops only appear in Patrol Chase. Closed-run ropes, locked glades and backcountry gates are all open.

- No XP, Rep, cash or Followers. Clips still score, and posting them adds to a separate Freeride feed counter that never touches Career.

- Medals and best scores per event are kept, with a ghost of the best run, and shown on the event's start gate.

### The Freeride menu (Esc)

| Option                        | Choices                                                                   |
|-------------------------------|---------------------------------------------------------------------------|
| Mountain                      | Kettle Peak, plus any later mountains                                     |
| Drop in                       | Any lift top or base, any event gate, or any point clicked on the Tab map |
| Time of day                   | The light presets, or "Let time run" at the Career day rate               |
| Weather                       | Bluebird, flat light, light snow, storm                                   |
| Snow                          | Season default, all powder, all groomed, spring (slush afternoons)        |
| Sport                         | Skis or snowboard (F also swaps)                                          |
| Assist                        | Relaxed, Normal, Real                                                     |
| Abilities                     | One slider per ability, all seven                                         |
| Gear                          | Any skis or board from the catalogue, any outfit                          |
| Signature                     | Any unlocked signature per sport                                          |
| Events                        | A list of every event with best score, medal and a Ride button            |
| Patrol Chase                  | Starts the arcade mode                                                    |
| Photo mode, custom challenges | See below                                                                 |

### What's saved

The Freeride profile in the save holds: settings above, best result and ghost per event, Patrol Chase top 10 with ghosts, clip album (shared with Career, tagged by mode), photos, and custom challenges made or loaded.

## Events

Every event is one EventDef in content, run by one shared event runner; the type decides only how it scores. The runner already exists for points and time (src/sim/events.ts); this section generalizes it.

### The shared flow

1.  **Find it:** each event has a start gate in the world (a banner arch that glows when available) and a pin on the Tab map. In Career, locked events show a padlock and the crew tier that opens them.

2.  **Start:** press E at the gate (or Ride from the Freeride list). The rider is placed at the start with the event's speed, the camera settles, a 3 s countdown runs (existing COUNTDOWN).

3.  **Run:** rewind is off; tap R still resets to a safe spot but costs 3 s on time events and voids the current combo on points events. Backspace restarts at the gate instantly.

4.  **End:** crossing the finish, running out of time (timeLimit), leaving the course (inBounds), or the type's own end rule.

5.  **Result screen:** score or time, medal, personal best, the rival's result, and three buttons: Retry, Watch replay, Leave. The run's recording is kept as the new best ghost if it beats the old one.

6.  **Payout:** in Career the runner returns a Payout (Progression tab has the amounts). First-time medals and improved medals pay in full; repeat runs pay 10%.

Ghosts: the player's best and the rival's run ride along as translucent riders that never collide. In Career the rival ghost is Cass on Summit events and the crew leader on crew events; in Freeride it's the bot run that set gold.

### Medal targets

Targets live in each EventDef. To set them for a new event, run its bot (src/sim/bots.ts) for a clean, skilled line and call that result **gold**. Then:

| Medal    | Points events | Time events   |
|----------|---------------|---------------|
| Bronze   | 25% of gold   | gold x 1.35   |
| Silver   | 55% of gold   | gold x 1.15   |
| Gold     | the bot's run | the bot's run |
| Platinum | gold x 1.5    | gold x 0.9    |

Kettle Slopestyle already follows this roughly (400 / 900 / 1,600 / 2,500).

### Event types

| Type                        | Scoring          | Rules                                                                                                                                                                                                      |
|-----------------------------|------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Slopestyle                  | Points           | Ride the park line start to finish within 90 s. Every banked combo counts; repeats halve across the run. A combo still open at the finish banks if the last landing was clean or solid                     |
| Big air                     | Points           | Three attempts off one kicker. Each attempt scores its single best air trick times height: 1 + 0.05 x (max height in m - 3), between 1 and 1.5. Final score = best two attempts with different trick names |
| Halfpipe                    | Points           | Two runs, up to 8 wall hits each. Each hit's trick x (1 + 0.1 x metres above the lip). A run's hits sum, times the combo variety multiplier. Best run counts                                               |
| Tree run: Dash              | Time             | Fastest through a glade, staying in its bounds (existing Glade Dash)                                                                                                                                       |
| Tree run: Flow line         | Points           | Stay in the glade bounds to the finish. Score = 100 x seconds of Flow summed (Flow 0.5 for 2 s = 100) + banked trick points                                                                                |
| Slalom                      | Time             | Single poles, alternating sides, 10 to 13 m apart and 4 m offset. Pass each pole on its turning side. A missed gate adds 3 s; three misses ends the run. Touching a pole costs 5% speed, never a crash     |
| Giant slalom                | Time             | Same as slalom with gates 25 to 30 m apart and 8 m offset                                                                                                                                                  |
| Downhill                    | Time             | Long, fast course with speed gates every 80 to 120 m; a missed gate adds 5 s                                                                                                                               |
| Boardercross and skiercross | Place            | Race three ghosts from a four-wide start down a course of berms, rollers and jumps. Gold for 1st, silver 2nd, bronze 3rd, platinum for 1st under the course record. Ghosts don't collide                   |
| Big mountain line           | Judged, 0 to 100 | See below                                                                                                                                                                                                  |
| Speed riding                | Time             | Fly and ski through ring gates with the wing; a missed ring adds 5 s                                                                                                                                       |
| Pond skim                   | Points           | Cross the 30 m village pond. 500 for making it across, plus tricks, plus 200 for wearing a costume. Water slows you 3 m/s per second; under 6 m/s you sink (a splash, not a crash)                         |
| Photo challenge             | Photo score      | Take the asked-for shot in photo mode (below). Bronze for a valid shot, silver at photo score 50, gold 70, platinum 85                                                                                     |
| Hidden stash                | Found            | Reach the stash zone. No medal; pays on first find                                                                                                                                                         |

### Big mountain line judging

Five parts, each 0 to 100, averaged to the final score:

- **Line:** average slope of the cells ridden, in degrees, x 2, plus 10 per cliff of 3 m or more dropped.

- **Control:** 100, minus 40 per crash and 10 per sketchy landing.

- **Fluidity:** 100, minus 4 for every time speed drops under 5 m/s.

- **Air:** biggest drop height in metres x 5.

- **Technique:** banked trick points / 30.

All capped to 0 to 100. Judges show their five numbers on the result screen, so players learn what scores.

### Events on Kettle Peak

| ID                  | Name              | Type                | Where                            | Crew and tier               | Status |
|---------------------|-------------------|---------------------|----------------------------------|-----------------------------|--------|
| kettle-slopestyle   | Kettle Slopestyle | Slopestyle          | Park on Kettle Run               | Park Rats 1                 | built  |
| glade-dash          | Glade Dash        | Tree run: Dash      | Kettle Run glade                 | Glade Folk 1                | built  |
| kettle-big-air      | Kettle Big Air    | Big air             | Bottom of the park               | Park Rats 2                 | new    |
| kettle-pipe         | Kettle Pipe       | Halfpipe            | Mid-mountain, beside the park    | Park Rats 3                 | new    |
| pro-line            | Pro Line          | Slopestyle          | Upper park line, bigger features | Park Rats 4                 | new    |
| fernwood-flow       | Fernwood Flow     | Tree run: Flow line | Fernwood                         | Glade Folk 2                | new    |
| ravens-dash         | Raven's Dash      | Tree run: Dash      | Raven's Drop glades              | Glade Folk 3                | new    |
| deep-woods          | Deep Woods        | Tree run: Flow line | Double-black glade               | Glade Folk 4                | new    |
| kettle-slalom       | Kettle Slalom     | Slalom              | Lower Kettle Run                 | Gatekeepers 1               | new    |
| kettle-gs           | Kettle GS         | Giant slalom        | Upper Kettle Run                 | Gatekeepers 2               | new    |
| summit-cross        | Summit Cross      | Boardercross        | Beside Summit Road               | Gatekeepers 3               | new    |
| summit-downhill     | Summit Downhill   | Downhill            | Summit to base                   | Gatekeepers 4               | new    |
| cornice-line        | Cornice Line      | Big mountain line   | Cornice Chute                    | Backcountry 3               | new    |
| couloir-line        | Couloir Line      | Big mountain line   | Kettle Couloir                   | Backcountry 4               | new    |
| couloir-wing        | Couloir Wing      | Speed riding        | Kettle Couloir face              | Backcountry 5               | new    |
| photo-1 to photo-10 | Photo challenges  | Photo challenge     | Around the mountain              | Filmer 1 to 5, two per tier | new    |
| pond-skim           | Kettle Pond Skim  | Pond skim           | Village pond                     | Act 4                       | new    |

Story missions add one-off events on these same courses (for example the Summit Invitational on the park), listed in the Story tab.

## Patrol Chase

Patrol Chase is the endless arcade mode in Freeride: ride as long as you can while Mountain Ops close in, scoring for distance, escapes and tricks done under pressure. It uses Heat and pursuers exactly as Core systems describes.

### A run

1.  Pick a start: summit (default), mid-mountain or the top of any run. The rider starts at 12 m/s, Heat level 1, one Ops skier 120 m uphill.

2.  Heat rises one level every 60 s on its own and never drains. Everything that raises Heat elsewhere raises it here too.

3.  **Endless mountain:** when the rider reaches the base village, a snowcat tow (E, or automatic after 3 s standing in the tow zone) pulls them back to the top of the Summit Chair in 5 real seconds. Pursuers lose them during the tow, so each lap is a fresh escape. Heat stays.

4.  The run ends when caught. Crashes alone don't end it; crashing near a pursuer usually does.

### Score

```math
\text{score} = \text{metres ridden} + \sum_{\text{escapes}} 500\,L + \sum_{\text{combos under pursuit}} \text{combo} \times (1 + 0.25\,L)
```

L is the Heat level at that moment. "Under pursuit" means at least one pursuer was hunting when the combo banked. Metres count only while moving downhill faster than 5 m/s, so standing still earns nothing.

### Results and leaderboard

- The result screen shows score, time survived, highest Heat, escapes, best combo, and the pursuer who caught you ("Clipped by Ops skier at Heat 4").

- Top 10 local scores are saved with their ghosts. Because pursuers are seeded sim, a recorded chase replays exactly; later the online leaderboard checks a run by replaying its inputs and seed.

- Each run uses a fresh seed. A "Daily chase" button uses the real date as the seed, so everyone online gets the same pursuer spawns that day.

## Career day loop

A Career session is a string of game days. Each day starts in the bunkhouse, runs about 20 real minutes, and ends with a recap that banks everything and saves.

### New Career

1.  **Save slot:** three slots. Each has its own seed (random at creation), shown on the slot card with the day number, act and play time.

2.  **Make your rider (D6):** pick skis or board (swappable later), body type from the rider model's presets, skin tone, hair, and a starter outfit in three colours. The rider never speaks; the cast calls them by a nickname the player types (default "Rookie").

3.  **Opening scene** and mission A1-01 (Story tab) start straight away on day 1.

### Morning

- The rider wakes in the staff bunkhouse in the village at 08:15 game time. The clock holds until the player steps outside.

- The phone opens on the Today screen once per morning (see below). Esc closes it.

- The day's weather, snow changes and fresh powder are already rolled (Core systems, Clock).

### The phone (P opens it anywhere; the clock pauses)

| App      | What it shows                                                                                                                |
|----------|------------------------------------------------------------------------------------------------------------------------------|
| Today    | Weather, the five daily challenges with progress, today's Rope Drop run, story missions available now with who's giving them |
| Messages | Threads with each cast member; story texts arrive here                                                                       |
| Map      | The trail map (same as Tab), with mission pins, event gates, stashes found, Ops sightings tonight                            |
| Clips    | The album, the replay editor and the in-game feed with Followers                                                             |
| Crews    | Each crew's tier, Rep to the next tier, and what that tier unlocks                                                           |
| Rider    | Level, XP, ability points and ranks, equipped gear and signature                                                             |
| Settings | Assist level, audio, controls help, "Call it a day"                                                                          |

### During the day

- **Missions** start by walking or riding up to the giver's marker (a small floating icon over them) and pressing E, or from the Today app (which places the rider at the start after a confirm). Only one mission runs at a time; dailies keep tracking in the background unless the mission says otherwise.

- **Events** start at their gates. **Shops** open with E at the counter (Progression tab, Shops).

- **Riding the lift** passes game time at the ride's real speed; skipping (hold E) jumps the clock by the ride time.

- At 15:45 the phone buzzes "Last chair". At 16:00 lifts stop; riders on the snow can keep riding down.

### Ending the day

The day ends when the player sleeps (E on the bunkhouse bed, any time after 12:00), picks "Call it a day" on the phone, or After Hours finishes. If the clock passes 16:30 with no After Hours, the rider is placed at the bunkhouse door at dusk and the day ends there.

### The recap screen

One screen, shown over a slow pan of the village at dusk, then Continue:

1.  Rewards earned today: XP, cash, Rep per crew, Followers, each with a bar showing progress to the next level or tier.

2.  Anything that unlocked, with one line each ("Glade Folk tier 2: blue glades are open").

3.  Mountain Pulse: today's change and the new value, with the crowd count at the lodge.

4.  Tomorrow's weather forecast.

5.  Autosave, then the next morning starts. Acts start here: if an act's gate was met today, its opening scene plays before the next morning.

## Daily challenges

Each game day offers five short challenges, one per crew plus one Filmer challenge, and refreshes the next game day (D7). One extra bonus challenge changes with the real-world date. Challenges track in the background while you ride anything.

### The generator

At dawn, for each of the five tracks:

1.  Seed the generator with hash(save.seed, day, trackId).

2.  List that track's templates whose required areas and tier are unlocked, and drop any used in the last 2 days.

3.  Pick one at random, then fill its parameters from the tier column (T is the player's current tier with that track).

The **real-day bonus** uses hash("bluebird", YYYY-MM-DD in UTC) and picks from all templates the player has unlocked, at their highest tier. It refreshes at midnight UTC whether the player is in Career or not, and the phone shows its timer.

### Templates

| ID            | Track       | Text shown                                                        | Parameter by tier 1 / 2 / 3 / 4 / 5   |
|---------------|-------------|-------------------------------------------------------------------|---------------------------------------|
| park-combo    | Park Rats   | Bank a combo worth N points                                       | 800 / 1,500 / 2,500 / 4,000 / 6,000   |
| park-spin     | Park Rats   | Land a N or bigger spin                                           | 360 / 540 / 720 / 900 / 1080          |
| park-grind    | Park Rats   | Grind N seconds in one combo                                      | 2 / 3 / 4 / 6 / 8                     |
| park-variety  | Park Rats   | Land N different tricks in one run of the park                    | 3 / 4 / 5 / 6 / 8                     |
| park-medal    | Park Rats   | Get silver or better at a Park Rats event                         | any unlocked event                    |
| glade-flow    | Glade Folk  | Hold Flow above 0.6 for N seconds                                 | 3 / 5 / 7 / 10 / 14                   |
| glade-misses  | Glade Folk  | N near misses without a branch hit                                | 10 / 15 / 20 / 30 / 40                |
| glade-fresh   | Glade Folk  | Ride N fresh-track cells                                          | 20 / 35 / 50 / 70 / 100               |
| glade-pillow  | Glade Folk  | Land a trick off a pillow, stump or log N times                   | 2 / 3 / 4 / 5 / 6                     |
| glade-stash   | Glade Folk  | Visit a stash you haven't been to today                           | 1 / 1 / 2 / 2 / 3                     |
| gate-speed    | Gatekeepers | Reach N m/s                                                       | 18 / 21 / 24 / 27 / 30                |
| gate-clean    | Gatekeepers | Finish a race event with no missed gates                          | any unlocked race                     |
| gate-time     | Gatekeepers | Ride from the top of a named run to the base in under N x its par | 1.3 / 1.2 / 1.1 / 1.0 / 0.95          |
| gate-tuck     | Gatekeepers | Tuck for N seconds without stopping                               | 8 / 12 / 16 / 20 / 25                 |
| bc-drop       | Backcountry | Drop a cliff of N m or more and land it                           | 2 / 3 / 4 / 6 / 8                     |
| bc-steep      | Backcountry | Ride N m of vertical on slopes over 35 degrees                    | 50 / 100 / 150 / 250 / 400            |
| bc-hike       | Backcountry | Climb N m on skins and ride it down                               | needs tier 2: — / 50 / 80 / 120 / 160 |
| bc-zone       | Backcountry | Ride a full line in a backcountry zone                            | needs tier 3                          |
| film-score    | Filmer      | Post a clip scoring N or more                                     | 35 / 45 / 55 / 65 / 75                |
| film-camera   | Filmer      | Post a clip filmed mostly with your newest camera                 | any                                   |
| film-original | Filmer      | Post a clip with Originality N or more                            | 40 / 55 / 65 / 75 / 85                |
| film-photo    | Filmer      | Take a photo scoring N or more                                    | 40 / 50 / 60 / 70 / 80                |

A template's check function reads sim events and run state (Data tab). Progress shows on the HUD for 3 s whenever it moves.

### Rewards

| Completing           | Pays                                                      |
|----------------------|-----------------------------------------------------------|
| A crew challenge     | 60 + 20 x T Rep with that crew, \$150 + 50 x T, 200 XP    |
| The Filmer challenge | 100 + 50 x T Followers, \$150 + 50 x T, 200 XP            |
| All five in one day  | \$500 and 500 XP more                                     |
| The real-day bonus   | \$300, 300 XP, and a guaranteed rare cosmetic             |
| Any challenge        | 10% chance of a rare cosmetic, rolled from the day's seed |

## Rope Drop

Once a game day from act 2 (mission A2-04 teaches it), Mountain Ops ropes off one run as "premium terrain". Duck the rope, ride it to the bottom without being caught, and hit a bonus objective on the way.

### Which run

Rolled at dawn from the day seed among runs the player has unlocked, never the same run two days running: Kettle Run upper half, Fernwood, Cornice Chute, Raven's Drop, Kettle Couloir (act 3 on). The phone's Today app and the Tab map show it with a red rope icon; in the world, an orange rope and a sign ("Premium Terrain: Summit Pass Holders Only") cross its top.

### Rules

- **Start:** ride through the rope at the top. That's +60 Heat, and Heat starts at level 2 (two Ops skiers waiting 80 to 120 m down, one snowmobile on the nearest cat track).

- **Halfway** down (by distance along the run), Heat jumps one level.

- **Finish:** cross the run's bottom line without being caught. You can leave the run's corridor, but the finish is still the run's bottom line.

- **Caught:** the attempt fails and the rider is placed at the rope. Three attempts a day; after a success, the run stays roped for show but pays nothing more.

- No rewind; tap-R reset is allowed but drops you at your last safe spot with pursuers where they are.

### Bonus objectives (one per day, from the same seed)

- Land N backflips (2 to 3)

- Stay in the trees for N seconds (15 to 25)

- Bank a combo worth N points while hunted (1,000 to 3,000)

- Lose your pursuers N times before the bottom (1 to 2)

- Finish in under the run's par time

- Never drop below 10 m/s after the halfway line

### Rewards

Success pays 100 Rep to the crew that owns the run's style (park run: Park Rats; glade: Glade Folk; groomer: Gatekeepers; steep or couloir: Backcountry), \$400 and 400 XP. The bonus objective adds \$300, 200 XP and a 25% chance of a rare cosmetic. Each successful Rope Drop also adds 1 to Mountain Pulse, up to 10 in a season (people like seeing Summit's ropes ignored).

## After Hours

After Hours is the push-your-luck end of a Career day, unlocked by mission A2-05. Once the lifts close, Mountain Ops patrols the whole mountain. Everything you earn goes into an unbanked pot that grows with Heat; reach a warming hut to bank it, or get caught and lose it.

### Starting and ending

- From 16:00 the phone offers "Stay out tonight". Accepting places the rider at the base at 16:30 under blue-hour light; declining ends the day as usual.

- The night runs 16:30 to 22:00 game time (10 real minutes). At 22:00, whatever is still unbanked is lost and the day ends.

- The player can also end early by banking and then choosing "Head home" at a hut.

### The mountain at night

- Kettle Express keeps running (June leaves it on for staff). Boarding it while a pursuer can see you adds +30 Heat.

- Snowcats groom the runs with their lights on. Press E beside one to hitch a ride to the top of its route, hidden from Ops.

- Lamps light Kettle Run and the village (src/render/lamps.ts); elsewhere you need the headlamp to see far.

- Heat starts at level 1 with pursuers patrolling, rises 1 level every 90 game minutes on its own, and drains to a floor of level 1, never to 0.

### What you can do

- **Night events:** every unlocked event gate is lit and runnable. Payout is the normal first-time or repeat amount.

- **Night challenges:** three short challenges appear on the phone at 16:30 (from the same templates as dailies, at your tier) and pay like dailies.

- **Tricks and escapes:** each banked combo puts combo / 10 cash into the pot; each escape puts \$100 x level in.

- **Story beats** in acts 2 to 4 that happen at night (Story tab).

### The pot and banking

Everything earned at night (cash, XP, Rep) goes into the pot, multiplied by the Heat level at the moment it was earned:

| Heat level     | 1    | 2    | 3    | 4    | 5    |
|----------------|------|------|------|------|------|
| Pot multiplier | x1.0 | x1.5 | x2.0 | x2.5 | x3.0 |

- **Banking:** stop inside a hut zone (the mid-mountain warming hut or the patrol hut at the summit) for 3 s while no pursuer can see you. The pot moves to your totals, and Heat drops to level 1.

- **Caught:** the pot is lost, Heat resets to level 1, and the rider is placed at the base. The night goes on.

- The HUD shows the pot, the multiplier and an arrow to the nearest hut whenever the pot is above zero.

## Photo mode and custom challenges

### Photo mode (O)

Press O any time outside events, chases, Rope Drops and After Hours pursuit to freeze the sim and frame a still. It also opens from any frame of a clip in the replay editor, which is the easiest way to catch a mid-air grab.

- **Camera:** W/S move forward and back, A/D sideways, Q/E up and down, all within 30 m of the rider; hold Shift with A/D to orbit the rider. Arrow keys roll and zoom (field of view 20 to 100 degrees).

- **Look:** exposure, depth of field (focus on the rider or the landmark), the time-of-day palette (any preset seen today in Career, any in Freeride), film grain, letterbox.

- **Take:** Enter saves to the album and offers a PNG download.

A photo scores like a clip's Scenery part plus subject: 0 to 100 = Scenery (Core systems) x 0.5 + 25 if the rider fills 8% to 40% of the frame + 15 if mid-air + 10 if grabbing. Photo challenges add their own rule:

| ID       | Ask              | Valid when                                                 |
|----------|------------------|------------------------------------------------------------|
| photo-1  | First chair      | Rider on the Kettle Express, lodge in frame                |
| photo-2  | Glove tree       | Glove tree in frame, rider within 15 m                     |
| photo-3  | Golden hour grab | Rider grabbing mid-air between 15:00 and 16:30             |
| photo-4  | Bowie            | Bowie in frame, rider within 5 m                           |
| photo-5  | Deep day         | Rider in powder with snow spray, storm-clearing morning    |
| photo-6  | Sunset bench     | From the sunset bench at golden hour, sun in frame         |
| photo-7  | Raven            | A raven on a lift tower in frame                           |
| photo-8  | Ops in the wild  | A Mountain Ops pursuer in frame, at Heat 1 or more         |
| photo-9  | Above the clouds | Summit, flat-light day, valley fog in frame                |
| photo-10 | Night lights     | Village lamps and a snowcat's lights in frame, after 18:00 |

### Custom challenges (Freeride)

A simple editor for making and sharing courses, built in Phase 5.

1.  From the Freeride menu, "Make a challenge" drops the rider at the chosen spot with an editor HUD.

2.  Ride the course you want. Press G to drop a gate where you are (a pair of flags 6 m apart across your heading), up to 20 gates. Press Enter at the finish.

3.  Pick scoring (time, or points from tricks), a name and a par (your own run, used as gold).

4.  Share: the challenge encodes to a URL hash (gate positions to 0.5 m, start, finish, scoring, name, then base64). Opening the link loads it in Freeride. Medal targets follow the Events rule from the creator's par.

Custom challenges never pay Career rewards.

## Online hooks

Online play is Phase 6 and needs its own server, but single player must already do four things so it slots in without a rewrite.

1.  **Ghost and live rider share one format.** A RiderFrame is what a ghost plays back and what a live player will send: time, position, heading, pitch, roll, edge, spin and flip angles, grab, sport, outfit ID, crashed flag. Ghosts in events should already render from RiderFrames, not from a full sim.

2.  **Everything ride-changing is in RunSetup.** A server can replay any leaderboard run from its header, seed and input masks and get the same score. Never let unrecorded state (the clock, the Career save) change a ride.

3.  **Rewards are computed from results, not sent as numbers.** Payouts come from a run's result through the content tables, so a server can recompute them.

4.  **Names and IDs are stable.** Event, trick, item and mission IDs never change once shipped, since leaderboards and shared clips refer to them.

What online adds later (from the main plan): rooms of up to 32 riders per mountain, name tags that fade with distance, trick callouts, a live feed, text chat, global leaderboards, shared clips and the shared Discovery map. Career stays single player; Freeride and Patrol Chase go online first.

# Progression and economy

Every number for what you earn, what it unlocks and what it costs, written as tables to copy into data files.

## Currencies and payouts

Career has four things to earn. Every payout in the game is one row of the table below, so the whole economy can be tuned from one content file (src/content/economy.ts).

| Currency       | What it is                                                     | Spent on                                                  |
|----------------|----------------------------------------------------------------|-----------------------------------------------------------|
| Rep (per crew) | Respect with Park Rats, Glade Folk, Gatekeepers or Backcountry | Nothing; it raises that crew's tier                       |
| Followers      | The Filmer track's Rep                                         | Nothing; it raises the Filmer tier and Mountain Pulse     |
| Cash           | Dollars                                                        | Gear, outfits, respecs, cosmetics                         |
| XP             | Rider experience                                               | Nothing; it raises your level, which gives ability points |

### Payout table

| Source                              | Cash                                | XP                                   | Rep or Followers                        |
|-------------------------------------|-------------------------------------|--------------------------------------|-----------------------------------------|
| Event, first bronze                 | \$150                               | 100                                  | 100 Rep, event's crew                   |
| Event, first silver                 | \$300                               | 200                                  | 200 Rep                                 |
| Event, first gold                   | \$500                               | 400                                  | 350 Rep                                 |
| Event, first platinum               | \$800                               | 600                                  | 500 Rep                                 |
| Event, improving a medal            | the difference between the two rows |                                      |                                         |
| Event, any repeat run that finishes | 10% of your best medal's row        |                                      |                                         |
| Banked combo (any time in Career)   |                                     | combo points / 20                    |                                         |
| Crew daily challenge                | \$150 + 50 x tier                   | 200                                  | 60 + 20 x tier Rep                      |
| Filmer daily challenge              | \$150 + 50 x tier                   | 200                                  | 100 + 50 x tier Followers               |
| All five dailies                    | \$500                               | 500                                  |                                         |
| Real-day bonus challenge            | \$300                               | 300                                  | rare cosmetic                           |
| Rope Drop                           | \$400 (+\$300 bonus)                | 400 (+200)                           | 100 Rep, run's crew                     |
| Escaping Mountain Ops               |                                     | 50 x Heat level                      |                                         |
| After Hours                         | the pot (Modes tab)                 | in the pot                           | in the pot                              |
| Posting a clip                      |                                     | 2 x clip score (first 3 posts a day) | Followers formula (Core systems)        |
| Photo challenge, first medal        | as an event                         | as an event                          | as an event, to Filmer as Followers x 2 |
| Glove found (15 on the mountain)    | \$50                                | 50                                   | adds a glove to the glove tree          |
| Shrine found (10, in glades)        | \$200                               | 100                                  | 50 Glade Folk Rep                       |
| Stash found (20)                    | \$100                               | 100                                  |                                         |
| Sponsor mission                     | \$600 to \$1,200                    | 300                                  | 50 Rep with the sponsor's crew          |
| Story mission                       | listed per mission (Story tab)      |                                      |                                         |

Every payout to a crew also checks that crew's tier thresholds at once, so an unlock can pop in the middle of the day. The recap screen lists them again.

## XP and rider levels

Rider level runs 1 to 30. Each level after 1 gives one ability point, so level 30 has 29 points (D2). The XP to go from level L to L + 1 is:

```math
\text{XP}_{L \to L+1} = 1000 + 250\,(L - 1)
```

| Level | Total XP to reach it | XP to the next level |
|-------|----------------------|----------------------|
| 1     | 0                    | 1,000                |
| 2     | 1,000                | 1,250                |
| 3     | 2,250                | 1,500                |
| 5     | 5,500                | 2,000                |
| 10    | 18,000               | 3,250                |
| 15    | 36,750               | 4,500                |
| 20    | 61,750               | 5,750                |
| 25    | 93,000               | 7,000                |
| 29    | 122,500              | 8,000                |
| 30    | 130,500              | cap                  |

At a rough 4,500 XP per game day (one good day of combos, five dailies, a few events and a mission), level 30 lands around day 29, inside a 40-day season. XP past the cap still counts toward a "Season XP" number on the recap for bragging only.

Levelling up shows a toast ("Level 7: 1 ability point") and plays a short sting. Points are spent on the phone's Rider app any time outside a run.

## Abilities

Seven abilities, ranks 0 to 5. **Rank 5 is exactly today's tuned feel** in tuning.ts, the feel Miguel has been play-testing, and lower ranks are a little weaker. So a new Career rider starts at 90% and earns their way back to full strength, and Freeride (all rank 5) plays exactly as the game does now.

### Rules

- Each ability belongs to a crew. **Its highest rank is that crew's tier** (tier 1 allows rank 1, tier 5 allows rank 5).

- One point buys one rank. Level 30 gives 29 points against 35 ranks, so no Career rider maxes everything (D2).

- Respec at the lodge front desk: all points back for \$1,000.

- Ranks are copied into RunSetup when a run starts, so replays and ghosts ride with the ranks they were recorded with.

- The Real assist setting turns gap sense off at every Tree Sense rank; the Flow part still applies.

### Effects (r is the rank, 0 to 5)

| Ability    | Crew        | What it changes in tuning.ts                           | At rank r                                              |
|------------|-------------|--------------------------------------------------------|--------------------------------------------------------|
| Pop        | Park Rats   | RIDE.popMax, RIDE.lipBonus; RIDE.lipWindow             | both x (0.90 + 0.02 r); window x (0.5 + 0.1 r)         |
| Spin       | Park Rats   | AIR.spinRate, AIR.flipRate                             | x (0.90 + 0.02 r)                                      |
| Balance    | Park Rats   | rail wobble (ASSIST.railWobble); longest scoring press | wobble x (1.25 - 0.05 r); press cap 2 + 0.2 r seconds  |
| Tree Sense | Glade Folk  | ASSIST.gapSense; FLOW.gainRate                         | gap sense x (0.5 + 0.1 r); Flow gain x (0.75 + 0.05 r) |
| Float      | Backcountry | SPORTS.powderFloat; SNOW.powder.sinkDrag               | float - 0.05 (5 - r); sink drag x (1 + 0.08 (5 - r))   |
| Edge       | Gatekeepers | SNOW.groomed.grip, SNOW.ice.grip                       | x (0.90 + 0.02 r)                                      |
| Stoke      | Gatekeepers | Stoke gains; boost length                              | gains x (0.75 + 0.05 r); boost 2 + 0.2 r seconds       |

The Core systems tab's Stoke, boost and Tree Sense numbers are the rank 5 values of this table.

## Crews

Four crews ride Kettle Peak, and the Filmer track works the same way with Followers instead of Rep. Each has five tiers; reaching a tier unlocks everything in its row at once, though a few unlocks also wait for their act.

| Crew        | Leader                      | Hangout                           | Cares about                    | Abilities it caps  |
|-------------|-----------------------------|-----------------------------------|--------------------------------|--------------------|
| Park Rats   | Dex Moreau                  | Park shack at the top of the park | Tricks, rails, jumps, the pipe | Pop, Spin, Balance |
| Glade Folk  | Wren Ostrova                | Fire pit at the edge of Fernwood  | Trees, powder, natural lines   | Tree Sense         |
| Gatekeepers | Ilse Brandt                 | Timing hut at the race start      | Racing, speed, clean lines     | Edge, Stoke        |
| Backcountry | Hank Lindqvist (with Bowie) | Patrol hut at the summit          | Big mountain, hiking, safety   | Float              |
| Filmer      | Tavi Reyes                  | Edit bay above the café           | Clips, photos, originality     | none               |

### Thresholds

| Tier | Name     | Crew Rep needed (total)                | Followers needed (total) |
|------|----------|----------------------------------------|--------------------------|
| 1    | Newcomer | 0, set when you meet the crew in act 1 | 0, set by mission A1-07  |
| 2    | Regular  | 500                                    | 1,000                    |
| 3    | Local    | 1,500                                  | 5,000                    |
| 4    | Crew     | 3,500                                  | 15,000                   |
| 5    | Legend   | 7,000                                  | 40,000                   |

A crew you haven't met is tier 0: its events, glades and gear are locked and its dailies don't appear.

### Unlocks by tier

| Tier | Park Rats                                                                         | Glade Folk                                                           | Gatekeepers                                                     | Backcountry                                                                      | Filmer                                                       |
|------|-----------------------------------------------------------------------------------|----------------------------------------------------------------------|-----------------------------------------------------------------|----------------------------------------------------------------------------------|--------------------------------------------------------------|
| 1    | Kettle Slopestyle; small park; Birch Twin skis and Birch Wiggle board in the shop | Fernwood (green glades); Glade Dash                                  | Kettle Slalom; Vantage Carver board in the shop                 | Avalanche beacon; Cairn shell outfit in the shop                                 | Helmet cam; Clips app and feed; photo-1 and photo-2          |
| 2    | Kettle Big Air; rail line                                                         | Blue glades; Fernwood Flow                                           | Kettle GS; timing lights on the race course (splits on the HUD) | Cairn Tour skis and Cairn Split board (touring); Ridge T-bar; hike-to ridges     | Follow drone; photo-3, photo-4                               |
| 3    | Kettle Pipe; first signature trick                                                | Black glades; Raven's Dash; Pillow 112 skis and Fishtail Float board | Summit Cross; Corduroy GS skis                                  | Backcountry zones; Cornice Line (the snowmobile itself comes from mission A3-02) | Tripod and fisheye; first sponsor offers; photo-5, photo-6   |
| 4    | Pro Line; Q/E corks enabled in Career                                             | Double-black glades; Deep Woods; every stash shown on the map        | Summit Downhill; Vantage race suit                              | Couloir Line; Halvard Charger skis and Halvard Gun board                         | FPV drone; manual replay editor; photo-7, photo-8            |
| 5    | Second signature; Park Rats outfit                                                | Yukimura invitation (road trip); Glade Folk outfit                   | Gatekeepers outfit; seeded first at the Classic downhill        | Speed-riding wing; Couloir Wing; Halvard invitation                              | Slow motion; premiere slot at the Classic; photo-9, photo-10 |

When a tier is reached, the crew leader sends a phone message in their voice (Story tab has the lines) and the unlock list shows on the HUD.

## Filmer track: sponsors and camera gear

Filming is a full progression track any rider can level just by exploring: posted clips earn Followers (Core systems), Followers raise the Filmer tier (Crews), and from tier 3 sponsors offer contracts that pay cash and gear. Camera unlocks per tier are in the Crews table and the camera rules in Core systems.

### Sponsors

Four fictional brands, one per crew style. A sponsor's offer arrives by phone once **Filmer tier 3 and that crew's tier 2** are both reached. Accepting is free and you can hold all four. Each contract is four missions done in any order, tracked like dailies but they never expire.

| Sponsor               | Style       | Contract missions                                                                                                                                                  | Contract reward                                                  |
|-----------------------|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------|
| Birchline Boards      | Park        | Post a clip with a 720 or bigger scoring 60+; bank a 4,000 combo in the park; land a trick off the Kettle Big Air at 6 m+; post a clip from the tripod at the park | \$1,200, 300 XP, the Lazy Boy signature, Birchline outfit        |
| Hollow Pine Outerwear | Glades      | Post a clip with Close calls 80+; ride 100 fresh-track cells in one game day; find 5 stashes; post a storm-day clip with Scenery 60+                               | \$1,200, Hollow Pine outfit, a free Pillow 112 or Fishtail Float |
| Vantage Race Co.      | Racing      | Gold at Kettle GS; reach 28 m/s; post a clip scoring 50+ with Speed 90+; win Summit Cross                                                                          | \$1,200, Vantage race suit, Corduroy GS discounted 50%           |
| Cairn Mountain Gear   | Backcountry | Drop a 6 m cliff and post it; climb 300 m on skins in one day; ride a full backcountry zone line under 2 minutes; take photo-9                                     | \$1,200, Cairn shell upgrade, the Halvard Charger discounted 50% |

Each mission pays \$600 to \$1,200 and 300 XP on its own (economy table). Finishing all four also adds 5 to Mountain Pulse, since sponsors bring visitors.

### The in-game feed

The Clips app shows posted clips as a scrolling feed with the clip's thumbnail, score, views and likes, and three generated comments picked from a pool by the clip's best part ("that tree line was disgusting in the best way", "how is this a liftie", "Summit Corp could never"). Comments are flavour only; keep the pool in src/content/feed.ts with at least 20 lines per part and 20 for bloopers.

## Shops

The village has four counters, each opened with E (they already exist as shop counters in src/sim/village.ts). Gear trades off rather than strictly upgrading, so the best pick depends on the run.

| Counter          | Sells or does                                                                            |
|------------------|------------------------------------------------------------------------------------------|
| Rental shop      | Skis, boards, touring gear; equip any owned gear; choose each sport's signature trick    |
| Outfit store     | Jackets, pants, headwear, goggles, gloves; colour variants                               |
| Café             | Hot cocoa (\$5) and fries (\$8): sit at a table and the rider holds them. Purely for fun |
| Lodge front desk | Ability respec (\$1,000), season pass photo retake (free), sleep in the bunkhouse        |

### Gear stats

Each ski or board has six stats from 1 to 5. A stat of 3 is neutral; every point above or below changes the tuning value it maps to by 4%, on top of abilities, and is copied into RunSetup.

| Stat      | Changes                                                                              |
|-----------|--------------------------------------------------------------------------------------|
| Speed     | airDrag and frictionScale x (1 - 0.04 (s - 3))                                       |
| Grip      | snow grip x (1 + 0.04 (g - 3))                                                       |
| Float     | powderFloat + 0.05 (f - 3)                                                           |
| Pop       | popMax x (1 + 0.04 (p - 3))                                                          |
| Stability | landing tolerance surviveDeg + 2 (st - 3) degrees; rail wobble x (1 - 0.04 (st - 3)) |
| Spin      | spinRate x (1 + 0.04 (sp - 3))                                                       |

### Skis and boards

| ID                   | Name                  | Sport | Speed | Grip | Float | Pop | Stability | Spin | Price   | Shop unlock   |
|----------------------|-----------------------|-------|-------|------|-------|-----|-----------|------|---------|---------------|
| ski-kettle-rental    | Kettle Rental         | Skis  | 3     | 3    | 3     | 3   | 3         | 3    | owned   | start         |
| board-kettle-rental  | Kettle Rental         | Board | 3     | 3    | 3     | 3   | 3         | 3    | owned   | start         |
| ski-birch-twin       | Birch Twin            | Skis  | 2     | 3    | 2     | 4   | 3         | 5    | \$1,400 | Park Rats 1   |
| board-birch-wiggle   | Birch Wiggle          | Board | 2     | 3    | 3     | 4   | 3         | 5    | \$1,400 | Park Rats 1   |
| board-vantage-carver | Vantage Carver        | Board | 4     | 5    | 2     | 2   | 4         | 2    | \$1,600 | Gatekeepers 1 |
| ski-corduroy-gs      | Corduroy GS           | Skis  | 5     | 5    | 1     | 2   | 4         | 1    | \$2,200 | Gatekeepers 3 |
| ski-pillow-112       | Pillow 112            | Skis  | 3     | 2    | 5     | 4   | 3         | 3    | \$2,000 | Glade Folk 3  |
| board-fishtail-float | Fishtail Float        | Board | 3     | 2    | 5     | 3   | 3         | 3    | \$2,000 | Glade Folk 3  |
| ski-cairn-tour       | Cairn Tour (touring)  | Skis  | 3     | 3    | 4     | 3   | 4         | 2    | \$1,800 | Backcountry 2 |
| board-cairn-split    | Cairn Split (touring) | Board | 3     | 3    | 4     | 3   | 4         | 2    | \$1,800 | Backcountry 2 |
| ski-halvard-charger  | Halvard Charger       | Skis  | 5     | 4    | 4     | 3   | 5         | 2    | \$3,500 | Backcountry 4 |
| board-halvard-gun    | Halvard Gun           | Board | 5     | 4    | 5     | 2   | 5         | 1    | \$3,500 | Backcountry 4 |

Other gear: avalanche beacon (given in A1-06), headlamp (\$250, from act 2), snowmobile (given in act 3), speed-riding wing (given at Backcountry 5). Freeride owns everything.

### Outfits

Outfits are cosmetic only. Five slots: jacket, pants, headwear, goggles, gloves; each item has three colourways.

| Set                                                                                             | Pieces        | Price per piece | Unlock                         |
|-------------------------------------------------------------------------------------------------|---------------|-----------------|--------------------------------|
| Starter                                                                                         | 5             | owned           | start                          |
| Kettle Staff (liftie jacket, beanie)                                                            | 2             | free            | mission A1-01                  |
| Cairn Shell                                                                                     | 5             | \$250 to \$600  | Backcountry 1                  |
| Thrift Rack (random vintage pieces, 10 total)                                                   | 10            | \$100 to \$300  | start                          |
| Park Rats, Glade Folk, Gatekeepers sets                                                         | 5 each        | not sold        | crew tier 5                    |
| Vantage race suit                                                                               | 1 (full suit) | \$1,500         | Gatekeepers 4                  |
| Birchline, Hollow Pine sets                                                                     | 5 each        | not sold        | sponsor contracts              |
| Costumes (banana, yeti, 1980s one-piece, cow)                                                   | 1 each        | \$400           | act 4, for the pond skim       |
| Rare cosmetics (gold goggles, tie-dye mitts, moose-antler helmet, glove-tree beanie and 8 more) | 12            | not sold        | daily and Rope Drop rare rolls |

## Mountain Pulse

Mountain Pulse is one number from 0 to 100 for how alive Kettle Peak feels. It is the story's scoreboard: it fills the lodge, changes the posters and the radio, and sets how big the win is at the end (D5: the mountain is never sold, whatever the Pulse). It only ever goes up, so nothing the player does can make the mountain worse.

### The formula

Pulse is recomputed from the save whenever the ledger applies a payout, as the sum of seven parts, each with its own cap, then capped at 100. Because it is computed rather than stored, tuning a part later fixes old saves too.

| Part       | Points                                              | Cap | Notes                                           |
|------------|-----------------------------------------------------|-----|-------------------------------------------------|
| Story      | as listed per mission (Story tab)                   | 15  | Main missions only                              |
| Followers  | Followers / 2,000, rounded down                     | 20  | 40,000 Followers (Filmer tier 5) fills it       |
| Crews      | 1 per tier-up with the four crews (tiers above 1)   | 16  | The Filmer track counts under Followers instead |
| Medals     | 1 per Kettle Peak event with gold or platinum       | 15  | Photo challenges and story one-offs don't count |
| Rope Drops | 1 per successful Rope Drop                          | 10  |                                                 |
| Sponsors   | 5 per finished sponsor contract                     | 20  |                                                 |
| Discovery  | 0.25 per stash, 0.3 per shrine, 0.1 per glove found | 6   | Rounded down at the end of the sum              |

The caps add up to 102, so a player who skips one whole part (say, never films) can still reach about 80, and 100 needs a little of everything.

The ledger keeps pulseYesterday in the save so the recap can show today's change ("Mountain Pulse 47, up 4 today").

### What reads the Pulse

| Reader                              | Rule                                                                                                              |
|-------------------------------------|-------------------------------------------------------------------------------------------------------------------|
| Lodge, terrace and lift-line crowds | People at each crowd spot = spot capacity x (0.15 + 0.85 x Pulse / 100), rounded; the recap shows the lodge count |
| Event bleachers                     | Filled to the same share, so a Pulse 90 Classic is packed                                                         |
| Lifties dancing                     | On bluebird days when Pulse is 40 or more                                                                         |
| Village posters                     | Kettle posters replace Summit ones one by one: one Kettle poster per 10 Pulse                                     |
| Radio DJ                            | Picks Pulse lines by band (Story tab, Ambient)                                                                    |
| Phone feed                          | Fake views scale with Pulse x Followers (Core systems)                                                            |
| Cocoa Break signature               | Unlocks if Pulse is 80 or more when the Kettle Classic starts                                                     |
| The ending                          | One of four variants by the Pulse at the end of the Classic (Story tab, Ending)                                   |

Pulse bands, used by the radio, the posters and the ending:

| Band | Pulse     | Name             |
|------|-----------|------------------|
| 1    | 0 to 39   | Hanging on       |
| 2    | 40 to 69  | Back on its feet |
| 3    | 70 to 89  | Packed           |
| 4    | 90 to 100 | Legend season    |

In Freeride the Pulse is fixed at 75 for crowds and posters, and a Freeride setting lets the player pick any band.

## Act gates and Freeride overrides

### How acts open (D1)

An act opens the morning after both of its conditions are true: its main missions from the act before are done, and the player has enough **tier-ups**. A tier-up is one tier gained above tier 1 on any of the five tracks (four crews plus Filmer), so there are 20 in all, and it doesn't matter which tracks they come from. A park-only player and a powder-only player reach act 2 at about the same time.

| Act   | Name                 | Opens when                                                   | What opens with it                                                                                                                                                      |
|-------|----------------------|--------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1     | Opening Day          | New Career                                                   | Village, bunny carpet, Kettle Express (after A1-01), Kettle Run, Fernwood, the small park                                                                               |
| 2     | Summit Comes Calling | Act 1 main missions done (ending with A1-08) and 4 tier-ups  | Summit Chair and Kettle Gondola (A2-01), the upper runs Cornice Chute and Raven's Drop, the headlamp in the shop, Rope Drop (A2-04), After Hours (A2-05), act 2 weather |
| 3     | Storm Cycle          | Act 2 main missions done (ending with A2-09) and 10 tier-ups | Kettle Couloir, backcountry gates, the snowmobile, the three-day storm cycle, act 3 Rope Drops                                                                          |
| 4     | The Kettle Classic   | Act 3 main missions done (ending with A3-09) and 15 tier-ups | Spring snow, the pond skim, costumes, the Classic missions                                                                                                              |
| After | The road trip        | A4-09 (the Kettle Classic) done                              | Free days that carry on forever, Halvard Glacier and Yukimura once built                                                                                                |

On the act's first morning its opening scene plays before the bunkhouse (Story tab). If the missions are done but the tier-ups are short, the Today app says how many are left and points at the two tracks closest to their next tier.

### Unlocks that wait for their act

A crew tier can be reached before the act that opens its place. When that happens, the unlock shows on the Crews app with "Opens in act N" and arrives with that act's opening scene.

| Waits for                      | Unlocks                                                                                                                      |
|--------------------------------|------------------------------------------------------------------------------------------------------------------------------|
| Act 2 (needs the Summit Chair) | Kettle GS, Summit Cross, Summit Downhill, Raven's Dash, Pro Line, black and double-black glades, Ridge T-bar                 |
| Act 3 (the backcountry)        | Snowmobile, backcountry zones, Cornice Line, Couloir Line, Couloir Wing, Rope Drops on Kettle Couloir, the speed-riding wing |
| Act 4 (spring)                 | Pond skim, costumes                                                                                                          |

Everything else unlocks the moment its tier is reached.

### After the Classic

Career doesn't end. The day after the Classic the season carries on as free days: dailies, Rope Drops and After Hours keep running, unfinished side missions and sponsor contracts stay open, and the Pulse keeps counting (the ending already played, but crowds keep growing). The road-trip missions start from here.

### Freeride overrides

Freeride uses the same content and code with these settings forced, so there is one game, not two:

| System                                  | Career                             | Freeride                                                                           |
|-----------------------------------------|------------------------------------|------------------------------------------------------------------------------------|
| Lifts, areas, glades, backcountry gates | Opened by acts, missions and tiers | All open; closed-run ropes are gone                                                |
| Abilities                               | Ranks bought with points           | Rank 5, or the player's sliders                                                    |
| Gear and outfits                        | Owned items                        | Everything                                                                         |
| Signatures                              | Unlocked ones                      | All                                                                                |
| Cameras                                 | By Filmer tier                     | All                                                                                |
| Mountain Ops                            | Rope Drops and After Hours         | Patrol Chase only                                                                  |
| Clock                                   | Runs                               | Still, unless "Let time run"                                                       |
| Weather and snow                        | Rolled each day                    | Picked in the menu                                                                 |
| Pulse                                   | Computed                           | Fixed at 75, or picked                                                             |
| Cast                                    | Hangouts, mission markers, phone   | Standing at their hangouts, with one idle line each; no missions or phone messages |
| Rewards                                 | XP, cash, Rep, Followers           | None; best scores, ghosts and the Freeride feed only                               |
| Later mountains                         | After the road trip                | As soon as they're built (D8)                                                      |

# Story and missions

The Last Season at Kettle Peak, written out mission by mission with triggers, objectives, rewards and the lines characters say.

## Premise and tone

**The Last Season at Kettle Peak.** Kettle Peak is a small, family-run mountain one bad winter away from being sold to Summit Corp, a mega-resort company that wants to turn it into "Summit Kettle™". You arrive as a new rider with a job running the Kettle Express. June Alder, the third-generation owner, has a signed offer from Summit on her desk that expires on the day of the Kettle Classic, the spring festival. She makes you a deal: help bring the mountain back to life this season, and she'll keep the lifts spinning.

Across four acts you win over the four crews and the local filmer, Summit sends a sponsored rider (Cass Vale) and a rules crew (Mountain Ops) to take over the fun, and the season ends at the Kettle Classic, where you ride against Cass in front of everyone the season brought back. June never sells (D5). How alive the mountain is when the Classic ends (the Mountain Pulse) decides how big the win is.

### Tone (D3: warm and funny)

- **Warm first.** The cast like each other and like you from the start. Nobody is cruel. Even Summit's people are ridiculous rather than evil, and they get small human moments.

- **Funny in the details.** Jokes come from character (Dex's terrible puns, Rourke's love of laminated rules, Hank's deadpan), from Mountain Ops being silly, and from the posters. Never mean jokes, never jokes at the player's riding.

- **Real where it matters.** Avalanche safety and the real ski patrol are never jokes. Act 3's burial is played straight, and the patrol are always the good guys.

- **Short.** No line over about 20 words, no scene over 30 seconds, and the player is back on snow fast.

- **The player is a silent rider (D6).** The cast talk to the player by the nickname typed at the start ({nick} in lines, default "Rookie"). The rider answers only with body language the scene picks (a nod, a shrug, a thumbs up), so no line ever waits for a reply.

## Cast bible

Everyone is modelled in the same Blender pipeline as the villagers (tools/blender/characters.py, natural proportions, plain faces, Tiny Bookshop light). Each has a home spot where they stand when no scene needs them, and an idle line for when the player presses E there with nothing to give.

| ID     | Name           | Role                                                                   | Look                                                                          | Voice                                                                         | Wants                                                           | Home spot                                                               |
|--------|----------------|------------------------------------------------------------------------|-------------------------------------------------------------------------------|-------------------------------------------------------------------------------|-----------------------------------------------------------------|-------------------------------------------------------------------------|
| june   | June Alder     | Owner of Kettle Peak, 60s                                              | Grey braid, her grandfather's wool coat, reading glasses on a cord            | Dry, practical, hides how much she cares                                      | To keep the mountain without lying to herself about the numbers | Lodge office, upstairs                                                  |
| dex    | Dex Moreau     | Park Rats leader, builds every feature, 20s                            | Oversized hoodie, shovel always on his shoulder, bleached hair under a beanie | Fast, punny, hypes everything                                                 | A park people travel for                                        | Park shack                                                              |
| wren   | Wren Ostrova   | Glade Folk elder, 70s                                                  | Patched anorak, knitted hat with ear flaps, telemark skis                     | Slow, few words, calls everyone "kid"                                         | The trees left alone, and someone to hand the stashes to        | Fire pit at the edge of Fernwood                                        |
| ilse   | Ilse Brandt    | Gatekeepers coach, ex-World Cup racer, 40s                             | Race suit under a long puffer, stopwatch on a lanyard                         | Precise, blunt, secretly soft                                                 | One more good racer before she retires the timing hut           | Timing hut at the race start                                            |
| hank   | Hank Lindqvist | Kettle patroller, Backcountry crew lead, 50s                           | Red patrol jacket with the white cross, big beard, radio on his chest         | Deadpan, safety first, slips in dad jokes                                     | Everyone home safe, every day                                   | Patrol hut at the summit (act 2 on), patrol room in the village before  |
| tavi   | Tavi Reyes     | Local filmer, 20s                                                      | Round glasses, yellow beanie, a 2000s camcorder on a strap                    | Excited, talks in shot lists                                                  | To make one great film, and for you to be in it                 | Edit bay above the café                                                 |
| cass   | Cass Vale      | Summit Corp's sponsored rider, 20s, rides the same sport as the player | Spotless Summit-branded kit, gold goggles, logo on everything                 | Confident, competitive, quick with a jab, never cruel; takes riding seriously | To win, and to prove they're not just a logo                    | Wherever Summit has a banner up; the Summit tent at the base from act 2 |
| pell   | Graham Pell    | Summit Corp's acquisitions VP                                          | Navy overcoat, loafers on snow, a tablet he holds like a shield               | Corporate cheer, says "synergy" unironically                                  | To close the deal by the Classic                                | The mid-mountain restaurant, ordering things not on the menu            |
| rourke | Rourke         | Mountain Ops chief                                                     | Black Ops jacket, laminated rule cards, a hole punch on a lanyard             | Officious, loves rules, oddly polite                                          | Order, and one rider who respects the rules (it won't be you)   | The Ops booth at the Summit Chair (act 2 on)                            |
| lou    | Lou Okafor     | Radio DJ, Kettle FM 98.1 (Phase 5)                                     | Never seen until the Classic: headphones, wool sweater                        | Smooth late-night voice, gentle teasing                                       | Something good to talk about                                    | The radio booth above the lodge bar                                     |
| mo     | Mo Patel       | Lift operator, your shift partner                                      | Liftie jacket, hand warmers everywhere                                        | Chatty, gossipy, fond of you                                                  | Someone to talk to in the lift maze                             | Kettle Express lift maze                                                |
| bowie  | Bowie          | Patrol avalanche dog (built)                                           | Border collie, patrol vest                                                    | Barks once, leans on your leg                                                 | Snacks; finding people                                          | Patrol hut                                                              |
| pepper | Pepper         | Village dog (built)                                                    | Scruffy terrier mix                                                           | Zoomies                                                                       | Following whoever pets her                                      | Village                                                                 |

Summit's act 3 guest, **Brayden Fosse** (brayden), is a Summit Platinum member in very new gear. He appears only in A3-04 and its follow-ups.

### How Cass works (D4)

Cass is the rival from their first scene to the last race and is never redeemed into a friend or teammate. The arc is respect, not conversion: in act 2 Cass dismisses you, in act 3 Cass starts taking you seriously, and at the Classic Cass rides their best against you for real. Cass's lines get a little warmer each act, but Cass rides for Summit to the end. Cass is always the same sport as the player (switched with the player's sport), and all of Cass's runs are bot runs from src/sim/bots.ts, tuned per mission as listed.

### Idle lines (E at home, nothing to give)

| Who    | Line                                                                                              |
|--------|---------------------------------------------------------------------------------------------------|
| June   | "Lifts don't run on good intentions, {nick}. Mostly diesel."                                      |
| Dex    | "I'm building a rail shaped like a rail. Revolutionary."                                          |
| Wren   | "Snow's talking today. Listen with your knees."                                                   |
| Ilse   | "Your line was fine. Fine is slow."                                                               |
| Hank   | "Beacon on? Good. Now the important question: snacks?"                                            |
| Tavi   | "That light at four o'clock. Don't waste it."                                                     |
| Cass   | "Oh, it's you. The liftie." (act 2), "Nice run earlier. Don't let it go to your head." (act 3 on) |
| Pell   | "Have you considered the Summit Pass? Unlimited synergy."                                         |
| Rourke | "Rule 14: no fun beyond this sign. I didn't write it. I laminated it."                            |
| Mo     | "Chair 37 creaks if you sit on the left. Don't tell June I told you."                             |

## How the story is delivered

There are no cutscenes. The story arrives four ways, and each is a small piece of content data run by the **story director** (src/game/story.ts), which listens to sim events and game state after each step.

| Channel        | When it's used                                    | Rules                                                                                                                                                                                                                |
|----------------|---------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Phone messages | Most story: setups, tier-ups, banter, reactions   | Arrive with a buzz and a one-line toast; read in the Messages app (P). Never more than 2 pushed in a row; extras queue until the next lift ride, hut or day start                                                    |
| Scenes         | Mission starts and ends, act openings, the finale | Up to 30 seconds. The sim pauses, the cast are posed at a named spot, lines appear one at a time as plain text over the scene (the quiet HUD style). Space or E advances, Esc skips the rest and logs it to Messages |
| Markers        | Where a mission can start                         | A small floating icon over the giver at their home spot, visible within 150 m and on the map. E starts the mission                                                                                                   |
| Radio          | Ambient news and jokes (Phase 5)                  | Lou between songs; never carries a fact the player needs, so it can stay muted                                                                                                                                       |

### Scene rules

- A scene is a list of **shots**: a camera anchor (a named spot plus an offset and look-at), who is in frame and in which pose (from the rider model's pose library), and the lines spoken in that shot.

- The player's rider is always in the scene, standing (on foot) or in their bindings, and gets an emote per shot (nod, shrug, thumbs-up, laugh, look-away).

- Scenes never start while the rider is moving faster than 2 m/s or in an activity. The director waits for a stop, a lift ride or the bunkhouse.

- At most one scene every 3 real minutes, except a mission's start and end.

- Every line shows the speaker's name in their colour. {nick} becomes the player's nickname, {sport} becomes "skis" or "board", {pulse} the current Pulse.

### Data shapes

These live in src/content/story/ (full interfaces in the Data tab).

interface Line {  
who: CastId; // 'june', 'dex', ... or 'rider' for an emote-only beat  
text: string; // supports {nick}, {sport}, {pulse}  
emote?: Emote; // the rider's reaction to this line  
}  
  
interface PhoneMessage {  
id: string; // 'msg-a1-tavi-hello'  
from: CastId;  
lines: string\[\]; // each shows as its own bubble  
when: Trigger; // see Data tab: act, mission done, tier reached, time, event...  
attach?: { kind: 'map-pin' \| 'photo' \| 'clip'; ref: string };  
}  
  
interface Scene {  
id: string; // 'scn-a1-01-start'  
spot: SpotId; // named place in village.ts or terrain.ts  
shots: { camera: CameraAnchor; cast: { who: CastId; pose: PoseId; at: SpotId }\[\]; lines: Line\[\] }\[\];  
timeOfDay?: string; // forces a light preset for the scene only  
}

### How to read the mission tables below

Each act has a table of missions, then each mission is written out. A mission is a giver, a trigger, an ordered list of objectives and a reward. Objectives use the kinds defined in the Data tab (goto, ride, medal, challenge, trick, clip, photo, follow, collect, escape, bank, beacon, talk, pet, wait). A mission can't be failed: a failed objective restarts from its checkpoint, and the player can leave and come back. Main missions (M) gate the next act; side missions (S) don't.

Rewards list cash, XP, Rep or Followers, Pulse (main missions only) and any unlock. Lines are given as **start** (the scene or message that opens it), **during** (callouts as objectives complete) and **end**.

## Act 1: Opening Day

Early season. One chairlift, the bunny hill, Kettle Run, Fernwood and a small park. The act teaches riding, the village and the phone, introduces every crew, and ends with June showing you Summit's offer. Weather uses the act 1 table (no storms). About 3 to 5 game days.

### Opening scene (scn-act1-open)

Plays once, right after character creation. The bus drops the rider at the village at dusk the night before day 1.

- **June:** "You must be {nick}. You're late. Good, so's the snow."

- **June:** "Bunkhouse is the green door. First chair's at half eight. Don't oversleep, you're running it."

- **Mo** (leaning out of the lift shack): "Welcome to the best worst job on the mountain!" (rider: thumbs-up)

### Missions

| ID                  | Name            | M/S | Giver  | Available when                      | Pulse |
|---------------------|-----------------|-----|--------|-------------------------------------|-------|
| a1-01-first-chair   | First Chair     | M   | June   | Day 1, on leaving the bunkhouse     | 0     |
| a1-02-corduroy      | Corduroy        | M   | Ilse   | A1-01 done                          | 0     |
| a1-03-dexs-shack    | Dex's Shack     | M   | Dex    | A1-01 done                          | 0     |
| a1-04-quiet-woods   | The Quiet Woods | M   | Wren   | A1-01 done, from day 2              | 0     |
| a1-05-last-sweep    | Last Sweep      | M   | Hank   | A1-01 done, 14:30 to 15:45          | 0     |
| a1-06-beacon-basics | Beacon Basics   | M   | Hank   | A1-05 done                          | 0     |
| a1-07-camcorder     | The Camcorder   | M   | Tavi   | Any two of A1-02, A1-03, A1-04 done | 0     |
| a1-08-the-deal      | The Deal        | M   | June   | A1-02 to A1-07 done, 14:00 or later | 1     |
| a1-s1-glove-tree    | The Glove Tree  | S   | Mo     | A1-01 done                          |       |
| a1-s2-pepper        | Cocoa for Two   | S   | Pepper | Pet Pepper any time                 |       |

### A1-01 First Chair

The tutorial. It teaches walking, the lift and the basic ride, and it opens the Kettle Express.

**Trigger:** stepping out of the bunkhouse on day 1. The clock stays at 08:15 until objective 3.

**Objectives:**

1.  goto the Kettle Express lift shack on foot (HUD hint: "B to clip in or out. Walk with W A S D.").

2.  talk to Mo at the shack. Mini-tutorial: press E at the panel to start the lift. The lift starts and the first guests (crowd spots) fill the maze.

3.  wait until 08:30 for the first chair, then ride the Kettle Express to the top (hint: "Hold W to hurry. Hold E to skip.").

4.  goto the base on Kettle Run, staying inside its corridor. Hints appear by turn: carving, braking (S + A/D), tucking, jumping (hold and release Space).

5.  talk to June at the bottom of the lift.

**Rewards:** \$100, 200 XP, the Kettle Staff jacket and beanie, the Kettle Express opens for good, the phone (P) unlocks with Today, Messages, Map and Settings.

**Lines:**

- Start (scene at the shack). **Mo:** "Big red button. Don't press the little red one." **Mo:** "Kidding. There's only one. Probably."

- During, at the top. **June** (radio toast): "Ride it down. If you fall, fall gracefully. People are watching."

- End (scene at the lift). **June:** "Not bad. Lift's yours mornings. Afternoons, go meet people. The crews, I mean." **June:** "This mountain runs on them more than me."

### A1-02 Corduroy (meet the Gatekeepers)

**Trigger:** A1-01 done. Ilse's marker appears at the timing hut; she also texts: "You. Liftie. Timing hut. Bring your edges."

**Objectives:**

1.  talk to Ilse at the timing hut. Gatekeepers become tier 1; Kettle Slalom unlocks.

2.  challenge gate-time at tier 1: Kettle Run top to base in under 1.3 x par.

3.  medal bronze or better at Kettle Slalom.

**Rewards:** \$250, 300 XP, 100 Gatekeepers Rep.

**Lines:**

- Start. **Ilse:** "I coached three Olympians. Now I coach whoever shows up. Today, that's you." **Ilse:** "Top to bottom. No stopping. I'll be timing."

- During, after objective 2. **Ilse:** "Acceptable. Now gates. Go round them, not through them."

- End. **Ilse:** "You turn late, but you turn. We can fix late."

### A1-03 Dex's Shack (meet the Park Rats)

**Trigger:** A1-01 done. Dex's marker at the park shack. Text: "yo the new liftie!! come see the park. bring tricks. or bring snacks. both ideal".

**Objectives:**

1.  talk to Dex. Park Rats become tier 1; Kettle Slopestyle opens.

2.  trick land three different tricks in the small park (any names, solid or clean). Hints teach grabs (Shift + direction) and spins (A/D in the air).

3.  trick a 50-50 or slide on any rail for 1 s.

4.  medal bronze or better at Kettle Slopestyle.

**Rewards:** \$250, 300 XP, 100 Park Rats Rep.

**Lines:**

- Start. **Dex:** "Welcome to the park! I built every feature with this shovel. Her name is Shovel." **Dex:** "Show me three tricks. Any three. Even a sad one counts."

- During, after the rail. **Dex:** "That was rail-y good. Sorry. I'm not sorry."

- End. **Dex:** "You're in, {nick}. Park Rats for life. There's no paperwork, we lost the paperwork."

### A1-04 The Quiet Woods (meet the Glade Folk)

**Trigger:** A1-01 done, from day 2. Wren's marker at the Fernwood fire pit. No text: Wren doesn't text. Mo mentions her instead: "Old lady at the fire pit asked if you can turn. I said probably?"

**Objectives:**

1.  talk to Wren. Glade Folk become tier 1; Fernwood and Glade Dash open.

2.  follow Wren's ghost through Fernwood, staying within 15 m of her to the bottom (her run is a bot line through the trees at about 12 m/s). Falling behind more than 15 m restarts from the last checkpoint gate.

3.  challenge glade-misses: 10 near misses without a branch hit.

4.  collect the first shrine, just off Wren's line (a little cairn with a carved bird).

**Rewards:** \$250, 300 XP, 100 Glade Folk Rep, the first shrine's payout.

**Lines:**

- Start. **Wren:** "Trees don't move, kid. You do. Follow me."

- During, after the follow. **Wren:** "You looked at the trees. Look at the gaps."

- End, at the shrine. **Wren:** "There's ten of these. People leave them. You'll find the rest, or you won't."

### A1-05 Last Sweep (meet Hank and Bowie)

**Trigger:** A1-01 done and the clock between 14:30 and 15:45. Hank's marker at the patrol room in the village; text from Hank: "Patrol here. Need a sweeper for last chair. You'll do. Bring your eyes."

**Objectives:**

1.  talk to Hank. Bowie is with him.

2.  pet Bowie.

3.  ride the last chair (any time after 15:30) and follow Hank's ghost down Kettle Run slowly, staying within 25 m and below 12 m/s (a sweep is slow).

4.  collect 2 lost gloves he points out on the way (they go on the glove tree).

**Rewards:** \$200, 300 XP. The Backcountry crew becomes tier 1.

**Lines:**

- Start. **Hank:** "Real patrol. Not the other kind. We dig people out, we don't chase them." **Hank:** "That's Bowie. He outranks me."

- During. **Hank:** "Sweep means slow. Check the edges. Check the trees. Check for gloves, people lose a lot of gloves."

- End. **Hank:** "Mountain's empty, everyone's home. Best feeling there is."

### A1-06 Beacon Basics

Introduces the beacon search, which A3-04 uses for real.

**Trigger:** A1-05 done, next game day. Hank texts: "Beacon park by the patrol room. 10 minutes. Wear something warm, you'll be digging."

**Objectives:**

1.  talk to Hank at the beacon park (a fenced flat beside the patrol room).

2.  beacon find and dig up one buried practice transmitter.

3.  beacon find and dig up two at once, under 3 real minutes.

**Beacon search rules** (on foot, beacon equipped): the HUD shows a distance in metres and an arrow toward the nearest transmitter. Beyond 20 m the arrow wobbles up to 30 degrees and the number updates once a second; from 20 m to 3 m it is steady; under 3 m the arrow disappears and only the number shows (as on a real beacon), so the player crosses back and forth to find the lowest number. Press E to probe the snow in front of you; a hit within 0.5 m rings, then hold E for 3 s to dig. With two transmitters, press Q to mark the found one so the beacon shows the next.

**Rewards:** \$200, 300 XP, 100 Backcountry Rep, the avalanche beacon.

**Lines:**

- Start. **Hank:** "Beacon, probe, shovel. In that order, every time. Nobody goes past a rope without all three."

- During, after the first dig. **Hank:** "Good. That took you 90 seconds. Real people have about 15 minutes. So, faster."

- End. **Hank:** "Here's yours. Wear it like a seatbelt. And never ride a closure. Ever."

### A1-07 The Camcorder (meet Tavi; the Filmer track)

**Trigger:** any two of A1-02, A1-03, A1-04 done. Tavi texts with a blurry photo of the player mid-air: "is this you??? I need you in my film. edit bay above the café".

**Objectives:**

1.  talk to Tavi in the edit bay. Filmer tier 1 is set: helmet cam, the Clips app and feed, photo-1 and photo-2.

2.  Do something good, then press V to save a clip (hint: "V saves the last 15 seconds. You never have to plan.").

3.  clip post a clip scoring 20 or more.

4.  photo photo-1, First Chair.

**Rewards:** \$200, 300 XP, 500 Followers.

**Lines:**

- Start. **Tavi:** "This camcorder is from 2004. It's seen things. Mostly my cousin's wedding." **Tavi:** "Here, take the helmet cam. Everything you ride, you're already filming."

- During, after the first post. **Tavi:** "Eleven views! Okay, nine are me. It's a start!"

- End. **Tavi:** "People see this mountain, people come to this mountain. That's the whole plan. It's a good plan."

### A1-08 The Deal

**Trigger:** A1-02 to A1-07 done and the clock at 14:00 or later. June's marker at the lodge office; June texts "Come up to the office when you're done riding. Nothing bad. Mostly."

**Objectives:**

1.  goto the lodge office (upstairs, on foot).

2.  Scene scn-a1-08-deal.

3.  wait for the day's recap, which shows the Mountain Pulse for the first time with a highlighted explainer.

**Rewards:** \$300, 500 XP, Pulse +1. The Pulse appears on the recap and the Crews app from now on.

**Lines (scene, June at her desk, an envelope with a gold Summit logo):**

- **June:** "Summit Corp wants to buy Kettle Peak. They've wanted to for six years."

- **June:** "This year the numbers say I should let them. The offer runs out the day of the Kettle Classic."

- **June:** "I'm not selling. But I need a reason I can say out loud to the bank."

- **June:** "People. On this mountain. Having a good time. You, the crews, Tavi's films. Bring them back."

- **June:** "Bank calls it revenue. I call it the Pulse." (rider: nod)

### A1-S1 The Glove Tree (side)

**Trigger:** A1-01 done; Mo's marker in the lift maze. **Objectives:** collect 3 gloves anywhere on the mountain, then goto the glove tree under the Kettle Express. **Rewards:** \$150, 150 XP, the glove-tree beanie (a rare cosmetic, given here once). **Lines:** **Mo:** "Every glove on that tree is a story. Mostly the story is 'I dropped my glove'."

### A1-S2 Cocoa for Two (side)

**Trigger:** petting Pepper for the first time. **Objectives:** goto the café terrace with Pepper following (she follows for 3 real minutes after a pet), then buy a cocoa and sit. **Rewards:** 100 XP, a photo of the two of you added to the album. **Lines:** none; Pepper sits on the next chair and the café crowd says "aww".

## Act 2: Summit Comes Calling

Midwinter. The upper mountain opens, and so does Summit's campaign: a branded tent at the base, a sponsored rider, a slopestyle on Kettle's own park, and Mountain Ops roping off runs as "premium terrain". The act teaches Rope Drops, Heat and After Hours. About 6 to 9 game days.

### Opening scene (scn-act2-open)

The morning act 2 opens. The rider steps out of the bunkhouse to find a huge inflatable Summit arch over the village path.

- **Mo:** "It appeared overnight. Like a mushroom. A mushroom with a marketing budget."

- **June** (text, arriving as the scene ends): "Summit Chair's been shut for two seasons. Hank and I are checking it today. Come with us."

From this morning the village has 1 Summit poster per 10 points the Pulse is below 100, swapped out as it rises (Ambient).

### Missions

| ID                        | Name                    | M/S | Giver | Available when                    | Pulse |
|---------------------------|-------------------------|-----|-------|-----------------------------------|-------|
| a2-01-upper-mountain      | Up Top                  | M   | June  | Act 2's first morning             | 0     |
| a2-02-branded             | Branded                 | M   | Cass  | A2-01 done                        | 0     |
| a2-03-summit-invitational | The Summit Invitational | M   | Pell  | A2-02 done, the next game day     | 2     |
| a2-04-premium-terrain     | Premium Terrain         | M   | Dex   | A2-01 done                        | 0     |
| a2-05-lights-out          | Lights Out              | M   | June  | A2-04 done, after 15:00           | 0     |
| a2-06-cass-gs             | Gate to Gate            | S   | Cass  | A2-03 done and Gatekeepers tier 2 |       |
| a2-07-blue-woods          | Blue Woods              | S   | Wren  | Glade Folk tier 2                 |       |
| a2-08-poster-boy          | Poster Boy              | S   | Dex   | A2-04 done                        |       |
| a2-09-the-tour            | The Tour                | M   | Pell  | A2-02 to A2-05 done               | 1     |

### A2-01 Up Top

**Trigger:** the first morning of act 2. June's marker at the Summit Chair base.

**Objectives:**

1.  ride the Summit Chair with June and Hank (Bowie rides on Hank's lap). Scene on the chair, scn-a2-01-chair.

2.  pet Bowie at the summit patrol hut. The hut becomes Hank's home spot.

3.  goto the three red check flags at the tops of Cornice Chute, Raven's Drop and the summit start of Kettle Run, in any order.

4.  goto the base by any run.

**Rewards:** \$300, 400 XP, 100 Backcountry Rep. The Summit Chair and the Kettle Gondola open for good.

**Lines:**

- Start, on the chair. **Hank:** "Last time this chair ran, Bowie was a puppy." **June:** "Last time this chair ran, I had a budget." **Hank:** "Bowie misses it more."

- During, at the summit. **Hank:** "Patrol hut's up here now. Bowie's office. Knock first."

- End. **June** (text): "Upper mountain's open. First time in two years. Go ride it like you mean it."

### A2-02 Branded (meet Cass)

**Trigger:** A2-01 done. A Summit tent appears at the base. Scene scn-a2-02-cass plays the next time the rider stops in the village: Cass rides into the village fast, sprays the crowd and stops at the tent while Pell applauds.

**Objectives:**

1.  Scene. Cass's marker appears at the Summit tent.

2.  talk to Cass.

3.  medal beat Cass's ghost on Kettle Slopestyle. Cass's bot is tuned to score halfway between silver and gold.

**Rewards:** \$400, 400 XP, 150 Park Rats Rep. Cass's marker moves to wherever Summit's banner is up.

**Lines:**

- Scene. **Pell:** "Kettle Peak, meet Cass Vale. Summit Corp's newest ambassador of fun." **Cass:** "Ambassador of winning, technically."

- Talk. **Cass:** "You're the liftie with the clips. Cute." **Cass:** "Ride the park. I'll put my run up. Let's see who this mountain belongs to."

- End, if the player wins (it always ends with a win, since the objective can be retried). **Cass:** "Home slope. Lucky bounce. Rematch." **Dex** (text): "DID YOU SEE THEIR FACE. I'm framing it."

### A2-03 The Summit Invitational

Summit sponsors a contest on Kettle's own park, with banners over Dex's features.

**Trigger:** A2-02 done, the next game day. Pell texts the whole village (a group message): "You're invited to the Summit Invitational! Free hats! Mandatory fun!"

**Objectives:**

1.  talk to Dex at the park shack. He's furious about the banners on his rails.

2.  medal the one-off event summit-invitational: the Kettle Slopestyle course, scored as slopestyle, with Cass and two Summit riders as ghosts. Target: beat gold. The bleachers fill by Pulse.

3.  Scene scn-a2-03-podium.

**Rewards:** \$600, 600 XP, 200 Park Rats Rep, Pulse +2, a Summit Invitational trophy for the lodge shelf.

**Lines:**

- Start. **Dex:** "They put a logo on Shovel. ON SHOVEL." **Dex:** "Go win it. For Shovel."

- Podium scene. **Pell:** "And the Summit Invitational goes to... {nick}! Hm. That's not on the card." **Pell:** "Our special Most On-Brand Rider award goes to Cass Vale!" **Cass** (holding a trophy shaped like a logo): "I'll take it." The crowd chants the rider's nickname. (rider: laugh)

- End. **June** (text): "Saw the podium. Bank manager was in the crowd. He cheered. Bank managers don't cheer."

### A2-04 Premium Terrain (meet Rourke; first Rope Drop)

**Trigger:** A2-01 done. The next morning Cornice Chute is roped off with an orange rope and a laminated sign. Dex texts: "they roped the chute. THE CHUTE. come to the summit".

**Objectives:**

1.  goto the top of Cornice Chute. Scene scn-a2-04-rourke.

2.  Rope Drop tutorial: duck the rope (Heat explained in a 3-line HUD card), reach the bottom without being caught. The first attempt starts at Heat level 1 with one Ops skier, not level 2, and has no bonus objective.

3.  escape lose your pursuer at least once on the way (tips: glades, cliffs, the crowd).

**Rewards:** \$400, 400 XP, 100 Backcountry Rep, counts as a Rope Drop for Pulse. Daily Rope Drops start the next morning.

**Lines:**

- Scene. **Rourke:** "Good morning. This run is now Premium Terrain. Rule 7, subsection B." **Rourke:** "Summit Pass holders only. Do you have a Summit Pass?" (rider: shrug) **Rourke:** "I didn't think so. Have a compliant day." **Dex** (when Rourke has gone): "So. The rope's like a metre high."

- During, on the first escape. **Dex** (radio toast): "HE'S STILL LOOKING AT THE TREES. Incredible."

- End. **Rourke** (text, from an unknown number): "This is Mountain Ops. We saw that. We have laminated your photo."

### A2-05 Lights Out (After Hours)

**Trigger:** A2-04 done and the clock at 15:00 or later. June texts: "Staff ride after close. Express stays on for us. Don't tell Summit."

**Objectives:**

1.  goto the rental shop and buy the headlamp (\$250). Hank's text if the player tries to start without it: "Headlamp. I'm not lending you mine, it's got my name on it."

2.  Accept "Stay out tonight" at 16:00.

3.  bank a pot of \$200 or more at the mid-mountain warming hut.

**Rewards:** \$300, 500 XP. After Hours is offered every day from now on.

**Lines:**

- Start (scene at the lift at 16:30, blue hour). **June:** "Best hour on the mountain. Nobody here but us and the snowcats." **Mo:** "And Mountain Ops." **June:** "And Mountain Ops. Mind them."

- During, when the pot first passes \$100. **Hank** (radio toast): "Hut's lit up mid-mountain. Bank it before they bank you."

- End. **June:** "That's how we used to do it. Lights on, mountain to ourselves. Felt like ours."

### A2-06 Gate to Gate (side)

**Trigger:** A2-03 done and Gatekeepers tier 2. Cass texts: "GS. You and me. Ilse can hold the watch." **Objectives:** talk to Ilse, then medal beat Cass's ghost on Kettle GS (Cass's bot at gold). **Rewards:** \$400, 400 XP, 200 Gatekeepers Rep. **Lines:** **Ilse:** "Cass turns early. You turn late. Let's see who's right." End: **Cass:** "Ilse coached you. That's cheating." **Ilse:** "That's coaching."

### A2-07 Blue Woods (side)

**Trigger:** Glade Folk tier 2. Wren's marker at the fire pit. **Objectives:** medal silver at Fernwood Flow, then collect two shrines in blue glades. **Rewards:** \$350, 400 XP, 150 Glade Folk Rep. **Lines:** **Wren:** "Blue trees. Closer together. Same rule: look at the gaps." End: **Wren:** "You're quieter in the trees now. Good."

### A2-08 Poster Boy (side)

**Trigger:** A2-04 done. Dex's marker at the park shack. **Objectives:** photo three different Rourke posters in the village (each poster is a photo target), then goto the park shack. **Rewards:** \$300, 300 XP, 100 Park Rats Rep, a "Rule 14" sticker on the rider's helmet. **Lines:** **Dex:** "I'm making a zine. It's called 'Rourke Looking At Things'. I need content." End: **Dex:** "The one where he's pointing at a snowflake. Iconic."

### A2-09 The Tour

Act 2's last main mission. It reveals what Summit wants.

**Trigger:** A2-02 to A2-05 done. June texts: "Pell wants a tour. I need a witness. Gondola, 13:00."

**Objectives:**

1.  goto the Kettle Gondola base at or after 13:00, then ride it with June and Pell. Scene scn-a2-09-gondola.

2.  At the mid-mountain restaurant, Pell films the view for an ad. trick land a 540 or bigger in sight of the restaurant terrace (a zone) so it's in his shot.

3.  Scene scn-a2-09-terrace.

**Rewards:** \$500, 600 XP, Pulse +1.

**Lines:**

- Gondola. **Pell:** "Picture it. Summit Kettle™. A tram to the top, heated walkways, a 14-dollar pretzel." **June:** "We have pretzels." **Pell:** "Not 14-dollar ones."

- Terrace. **Pell:** "Great energy! We'll use that in the ad. Without the Kettle jacket." **Pell:** "Of course the backcountry would have to close. Liability. And the glades are a bit... treey."

- **June** (to the rider, as Pell leaves): "Close the backcountry. Cut the trees. Over my dead snowcat."

Act 3 opens the morning after A2-09 is done and the player has 10 tier-ups.

## Act 3: Storm Cycle

Deep winter. Storms come in a fixed three-day cycle (storm, powder morning, bluebird), the backcountry opens, and the act's centre is a real avalanche burial that shows why the real patrol matter. Summit's plan leaks, Rourke pulls your pass, and you win it back at night. About 6 to 9 game days.

### Opening scene (scn-act3-open)

The first morning of act 3 is a storm day. The bunkhouse window is half buried.

- **Mo** (digging out the lift shack): "I've been shovelling since five. I've found two cars and a Park Rat."

- **Dex** (climbing out of the snow): "I was building a jump. I am the jump now."

- **Hank** (text): "Storm cycle's here. Powder tomorrow. Backcountry soon. Come see me at the hut."

### Missions

| ID                        | Name                | M/S | Giver  | Available when                         | Pulse |
|---------------------------|---------------------|-----|--------|----------------------------------------|-------|
| a3-01-powder-day          | Powder Day          | M   | Wren   | The first powder morning of act 3      | 0     |
| a3-02-the-sled            | The Sled            | M   | Hank   | A3-01 done                             | 0     |
| a3-03-skin-track          | Skin Track          | S   | Hank   | Backcountry tier 2 and A3-02 done      |       |
| a3-04-closed-means-closed | Closed Means Closed | M   | Hank   | A3-02 done, on the next powder morning | 3     |
| a3-05-leaked              | Leaked              | M   | Tavi   | A3-04 done                             | 0     |
| a3-06-pass-pulled         | Pass Pulled         | M   | Rourke | A3-05 done                             | 1     |
| a3-07-cass-storm          | Whiteout            | S   | Cass   | Glade Folk tier 3, on a storm day      |       |
| a3-08-deep-woods          | Deep Woods          | S   | Wren   | Glade Folk tier 4                      |       |
| a3-09-the-offer           | The Offer           | M   | June   | A3-04 to A3-06 done                    | 1     |

### A3-01 Powder Day

**Trigger:** the first powder morning of act 3. A "Powder day" alert goes out at dawn, and a crowd waits at the Kettle Express before opening. Wren texts for the only time in the game: "Now."

**Objectives:**

1.  goto the Kettle Express maze before 08:30 and ride the first chair.

2.  challenge glade-fresh: ride 40 fresh-track cells before 09:30.

3.  talk to Wren at the fire pit.

**Rewards:** \$400, 500 XP, 200 Glade Folk Rep.

**Lines:**

- Start, in the lift maze. **Mo:** "Rope drop at half eight. People have been here since seven. One guy brought a tent."

- During, at 30 cells. **Wren** (radio toast): "Don't stop. It won't wait."

- End. **Wren:** "That's why I live here, kid. Forty years. Still the best morning there is."

### A3-02 The Sled

**Trigger:** A3-01 done. Hank's marker at the summit patrol hut.

**Objectives:**

1.  talk to Hank. He hands over the snowmobile (it is given here whatever the Backcountry tier).

2.  Drive the sled along Summit Road to the backcountry gate (E to get on and off; W, S, A, D to drive).

3.  goto through the gate. The gate checks the beacon (Core systems, Gear that opens places).

4.  ride one full line in the north bowl backcountry zone and back to the cat track.

**Rewards:** \$400, 500 XP, 200 Backcountry Rep, the snowmobile, backcountry gates open (each zone's crew-tier rule still applies).

**Lines:**

- Start. **Hank:** "Sled's older than you. Treat her nice. She doesn't like reverse."

- During, at the gate. **Hank:** "Beacon on, probe, shovel. Past this rope, nobody's coming quick. Ride like it."

- End. **Hank:** "That's the best snow on the mountain and Summit wants to close it. Liability, they say." **Hank:** "I say: education."

### A3-03 Skin Track (side)

**Trigger:** Backcountry tier 2 (touring gear in the shop) and A3-02 done. **Objectives:** equip touring gear, goto the north ridge by climbing (B to switch to touring), then ride the ridge line down. **Rewards:** \$350, 400 XP, 150 Backcountry Rep. **Lines:** **Hank:** "You earn your turns up there." End: **Hank:** "Burns, doesn't it? Best kind."

### A3-04 Closed Means Closed

The heart of the act. Played straight: the jokes stop until the guest is safe.

**Trigger:** A3-02 done, on the next powder morning. Raven's Drop's upper bowl has a real **avalanche closure**: a red and black rope and a sign ("AREA CLOSED: AVALANCHE DANGER"), which looks nothing like Summit's orange premium-terrain ropes. At 10:00 a scene plays wherever the rider is.

**Objectives:**

1.  Scene scn-a3-04-alarm: Hank's radio, Bowie barking at the hut.

2.  goto the debris field at the bottom of the bowl by the route Hank marks (around the ridge, never across the slope). The slide itself is scripted for this mission: a debris zone appears and the closed slope is a hard boundary the rider can't enter.

3.  beacon search the debris with Bowie. Bowie runs ahead and circles a spot within 3 m of the guest once the rider is within 15 m. Find, probe and dig (hold E 3 s, then 3 more).

4.  Scene scn-a3-04-rescue. Hank and the patrol arrive on sleds. The guest is fine.

There is a 5 real-minute clock on screen for tension, but no fail state: if it runs out, Hank arrives and the dig finishes with him. The rewards are the same.

**Rewards:** \$500, 600 XP, 300 Backcountry Rep, Pulse +3, a patrol pin on the rider's jacket.

**Lines:**

- Alarm. **Hank** (radio): "Slide in Raven's upper bowl. Someone went under the closure. Patrol, all units." **Hank:** "{nick}, you're closest. Go around by the ridge. Not across."

- Rescue. **Brayden** (coughing, very new goggles): "The rope was just a suggestion. Right?" **Hank:** "Not that rope. Never that rope." **Hank** (to the rider): "You did everything right. That's the job." Bowie leans on the rider's leg.

- After, text from **Brayden**: "thank you. I'm cancelling my Summit Platinum and buying a beacon. also I owe you a beer. or a cocoa idk your vibe".

- After, text from **June**: "Heard. Proud of you. Go sit by a fire."

Note for the build: the slide is never something the rider can trigger or ride. It is a scripted change for this mission only, and avalanche-closed terrain stays off limits in every mode.

### A3-05 Leaked

**Trigger:** A3-04 done. Tavi texts a blurry photo of a slide deck: "Pell left his tablet on the gondola. I gave it back. AFTER taking a photo. look at slide 9".

**Objectives:**

1.  talk to Tavi in the edit bay. Scene scn-a3-05-deck (the slide reads: "Phase 2: close backcountry, remove 40% of glades, tram to summit, Platinum Lounge").

2.  clip post a clip scoring 50 or more filmed in a backcountry zone or a black or double-black glade, for Tavi's "Keep Kettle Wild" edit.

**Rewards:** \$400, 500 XP, 2,000 Followers.

**Lines:**

- Start. **Tavi:** "Remove forty percent of the glades. Like they're a hairstyle." **Tavi:** "I'm cutting a film. Show people what they'd lose."

- End. **Tavi:** "It's going everywhere. Wren's niece in Melbourne shared it. Wren doesn't know what sharing is."

### A3-06 Pass Pulled

**Trigger:** A3-05 done. The next time the rider boards any lift, Rourke is standing in the maze with his hole punch. Scene scn-a3-06-punch.

**Objectives:**

1.  Scene. The rider's staff pass is "clipped": in the morning and afternoon, lifts still work (Mo lets you on), but the phone shows the pass as Suspended until this mission is done. This is purely a joke; nothing is locked.

2.  Accept After Hours tonight.

3.  bank a pot of \$1,000 or more while at Heat level 4 or higher.

4.  Scene scn-a3-06-honor at the hut.

**Rewards:** \$600, 700 XP, 100 Rep with every crew, Pulse +1, the laminated "Honorary Compliance" card (a cosmetic on the rider's jacket).

**Lines:**

- Punch. **Rourke:** "That video. Unauthorized riding, unauthorized filming, unauthorized vibes." *click* **Rourke:** "Your pass is clipped. Rule 22 allows one appeal." **Mo** (behind him): "Rule 22's the one where you win a dare, right?" **Rourke:** "...It is a formal appeal."

- Honor, at the hut. **Rourke** (out of breath): "Banked. At Heat four. Rule 22 is very clear." **Rourke:** "I have laminated you a card. Please don't tell Mr Pell I enjoyed that."

### A3-07 Whiteout (side)

**Trigger:** Glade Folk tier 3 and a storm day. Cass texts: "Summit wants a storm clip. Raven's Dash. Unless you're scared of trees." **Objectives:** medal beat Cass's ghost on Raven's Dash in the storm (Cass's bot at gold). **Rewards:** \$450, 500 XP, 250 Glade Folk Rep. **Lines:** End: **Cass:** "Okay. You're fast in there. Annoyingly." **Cass:** "Doesn't change anything. I'm still taking the Classic."

### A3-08 Deep Woods (side)

**Trigger:** Glade Folk tier 4. **Objectives:** medal silver at Deep Woods, then collect the tenth shrine, which only appears after it (Wren's own, at the top of the double-black glade). **Rewards:** \$500, 500 XP, 300 Glade Folk Rep. **Lines:** **Wren:** "I built that one when I was your age. Nobody's found it in thirty years." **Wren:** "It's yours now. Look after the trees, kid."

### A3-09 The Offer

**Trigger:** A3-04 to A3-06 done. June texts: "Lodge. Everyone. 15:00."

**Objectives:**

1.  goto the lodge at 15:00 or later. Scene scn-a3-09-offer with June, Pell, Cass and all five leaders.

**Rewards:** \$500, 600 XP, Pulse +1.

**Lines:**

- **Pell:** "Summit has raised its offer. Generously. It expires at the Kettle Classic." **Pell:** "And Cass will be riding the Classic. Wearing our logo. Very visibly."

- **June:** "Then we'll make it a good one." **June:** "Four events, one per crew. A cross race to finish, summit to base. Everyone rides."

- **Dex:** "I'll build the course." **Ilse:** "I'll time it." **Wren:** "I'll come." **Hank:** "I'll bring Bowie." **Tavi:** "I'll film it. And premiere your part on the stage, {nick}."

- **Cass** (to the rider, on the way out): "See you at the bottom. I'll be the one waiting."

Act 4 opens the morning after A3-09 is done and the player has 15 tier-ups.

## Act 4: The Kettle Classic

Spring. Sunny mornings, slush after 13:00 below mid-mountain, costumes and the pond skim. Each crew leader runs a practice for their Classic event, Tavi cuts your video part, and the season ends with the Kettle Classic, where your last opponent is Cass (D4). About 4 to 6 game days.

### Opening scene (scn-act4-open)

The first morning of act 4 is bluebird and warm. Mo is in shorts.

- **Mo:** "Spring! The season of sunburn and regret."

- **June** (pinning a festival poster to the lodge door): "Kettle Classic. A week Saturday. Bring everyone you've ever met."

### Missions

| ID                   | Name                   | M/S | Giver | Available when                                    | Pulse |
|----------------------|------------------------|-----|-------|---------------------------------------------------|-------|
| a4-01-thaw           | Thaw                   | M   | June  | Act 4's first morning                             | 0     |
| a4-02-pond-skim      | Pond Life              | S   | Dex   | A4-01 done                                        |       |
| a4-03-dex-practice   | Practice: Park         | M   | Dex   | A4-01 done                                        | 0     |
| a4-04-wren-practice  | Practice: Trees        | M   | Wren  | A4-01 done                                        | 0     |
| a4-05-ilse-practice  | Practice: Gates        | M   | Ilse  | A4-01 done                                        | 0     |
| a4-06-hank-practice  | Practice: Big Mountain | M   | Hank  | A4-01 done                                        | 0     |
| a4-07-final-cut      | Final Cut              | M   | Tavi  | A4-01 done and 6 clips posted in total            | 2     |
| a4-08-torchlight     | Torchlight             | S   | Hank  | A4-03 to A4-07 done, the night before the Classic |       |
| a4-09-kettle-classic | The Kettle Classic     | M   | June  | A4-03 to A4-07 done                               | 4     |

### A4-01 Thaw

**Trigger:** act 4's first morning. June's marker at the village pond.

**Objectives:** talk to June at the pond, then goto the base of Kettle Run after 13:00 (riding the first slush of the year).

**Rewards:** \$300, 400 XP. The pond skim, costumes in the outfit store, and the Classic practice missions open.

**Lines:** **June:** "Pond skim's back. My grandfather started it. He never made it across once." **June:** "Four crews, four events, then the cross race. Go see each of them. They've each got something to teach you before Saturday."

### A4-02 Pond Life (side)

**Trigger:** A4-01 done. **Objectives:** buy or wear any costume, then medal bronze or better at Kettle Pond Skim (making it across). **Rewards:** \$300, 300 XP, 150 Park Rats Rep, a framed photo on the lodge wall. **Lines:** **Dex** (dressed as a banana): "Don't ask. Just commit." End, if the player sank on any attempt: **Dex:** "The pond always wins once. It's tradition."

### The Classic practices (A4-03 to A4-06)

Each Classic event is a one-off course made for the festival. In the practice missions it opens to the player whatever their crew tier, since the Classic is for everyone. Each practice has the same shape:

1.  talk to the leader at their hangout.

2.  medal silver or better on the practice version of their Classic event (Cass's ghost is not in practice).

3.  The leader gives one tip, shown as a line and as a hint on the event's result screen from then on.

| Mission | Classic event (one-off ID) | Course                                       | Type                | Reward                             | Leader's line at the start                                                                     | Tip at the end                                                   |
|---------|----------------------------|----------------------------------------------|---------------------|------------------------------------|------------------------------------------------------------------------------------------------|------------------------------------------------------------------|
| A4-03   | classic-slopestyle         | Pro Line's features, opened for the Classic  | Slopestyle          | \$400, 500 XP, 250 Park Rats Rep   | **Dex:** "I built the Classic line. It has a rail shaped like June's snowcat. You're welcome." | **Dex:** "Link everything. Judges love a combo that never ends." |
| A4-04   | classic-flow               | Fernwood, extended through the blue glade    | Tree run: Flow line | \$400, 500 XP, 250 Glade Folk Rep  | **Wren:** "I'll ride it with you once. Then you're on your own."                               | **Wren:** "Fast is good. Close is better."                       |
| A4-05   | classic-gs                 | Kettle GS gates, reset by Ilse               | Giant slalom        | \$400, 500 XP, 250 Gatekeepers Rep | **Ilse:** "I set these gates for Cass. Turns early, Cass does. Make them pay."                 | **Ilse:** "Gate four. Go high. Trust me."                        |
| A4-06   | classic-big-mountain       | Cornice Chute, judged as a big mountain line | Big mountain line   | \$400, 500 XP, 250 Backcountry Rep | **Hank:** "Big mountain. Judges want a real line. And they want you alive."                    | **Hank:** "Control first. The air comes free."                   |

### A4-07 Final Cut

Your season's video part, which premieres at the Classic. It's the last story beat the plan asked for.

**Trigger:** A4-01 done and at least 6 clips posted across the season. Tavi texts: "time to cut your part. bring your best stuff. bring snacks."

**Objectives:**

1.  talk to Tavi in the edit bay.

2.  Pick 4 posted clips from the album for the part. The phone suggests the four with the highest scores, one per crew style if possible.

3.  Watch the auto-edit (the replay editor's one-button edit across the four, about 60 s with a title card: "{nick}: The Last Season").

The **part score** is the average of the four clips' scores. It's shown at the premiere and sets one of three crowd reactions (below 50, 50 to 74, 75 and over).

**Rewards:** \$500, 500 XP, 2,000 Followers, Pulse +2.

**Lines:** **Tavi:** "Every frame of this was an accident you made look on purpose. That's filmmaking." End: **Tavi:** "It's done. I cried twice. Once was the edit, once was the cocoa."

### A4-08 Torchlight (side)

**Trigger:** A4-03 to A4-07 done, After Hours on the night before the Classic. **Objectives:** goto the summit at 19:00 and follow the line of torch-carrying riders (all five leaders as ghosts, slowly) down Kettle Run to the village, staying within 20 m. There is no Heat tonight. Fireworks start as the line reaches the base. **Rewards:** 300 XP, the torch prop for photo mode. **Lines:** **Hank:** "Every Kettle Classic since 1961. Slow and together." **Rourke** (holding a torch at the back): "Mountain Ops is... observing. Respectfully."

### A4-09 The Kettle Classic (the finale)

A whole game day. The bleachers fill by the Pulse, the posters are all Kettle ones by now if the Pulse is high, and Summit's tent is at the base with Pell and a banner reading "Summit Kettle™: Coming Soon?" (Tavi added the question mark).

**Trigger:** A4-03 to A4-07 done. The day starts with scene scn-a4-09-morning at the bunkhouse door: every leader is waiting outside.

**Structure:**

1.  Scene scn-a4-09-morning. If the Pulse is 80 or more, June gives the rider a thermos: the Cocoa Break signature unlocks.

2.  The four crew events, in any order, each a one-off with Cass's ghost riding it (Cass's bot at gold). Each pays as an event (first medal rows) and adds **Classic points**: bronze 1, silver 2, gold 3, platinum 4. The HUD keeps a Classic board: the rider's points against Cass's (Cass has 3 per event).

3.  At 15:00 (or once all four are done, whichever is later), medal **the Classic Cross** (classic-cross): summit to base, four wide, a cross course along Kettle Run with Dex's berms and Ilse's gates, against Cass and two Summit riders. The rider has to finish first to complete the mission; it can be retried as many times as needed, and after 3 tries the phone offers the Relaxed assist for this race only. Cass's bot is tuned to the course's gold time.

4.  Scene scn-a4-09-podium.

5.  Scene scn-a4-09-premiere at the festival stage at 16:30 (the video part plays on the big screen, crowd reaction by part score).

6.  The ending (next section).

**Rewards:** \$2,000, 2,000 XP, 500 Rep with every crew, Pulse +4, the Kettle Classic trophy for the lodge shelf, the Kettle Classic outfit.

**Lines:**

- Morning. **June:** "Whatever happens today, {nick}: thank you. I mean it." **Dex:** "Okay, emotional, cool, can we ride now."

- At the Cross start. **Cass:** "No ghosts this time. Just you and me." (rider: nod) **Cass:** "Good. I hate winning easy."

- During the Cross, if the rider leads at halfway. **Lou** (stadium speaker, the only time he's seen): "The liftie leads! Folks, the liftie leads!"

- Podium. **Cass** (second step): "Enjoy it. Next season I'm coming back faster." **Cass:** "...That was a good race. Don't tell Pell I said so." **Pell** (off to the side, on his phone): "No, they won. Yes, again. No, I don't know what a liftie is either."

- Premiere, by part score. Below 50: **Tavi:** "The crowd's polite! Polite is a type of love!" 50 to 74: the crowd cheers the biggest landing. 75 and over: the crowd is on its feet; **Tavi:** "I'm going to frame this crowd."

## Ending and epilogue

The ending is the last scene of A4-09, scn-ending, played at the festival stage at dusk. Kettle Peak is never sold (D5). The Pulse when the Classic ends picks one of four variants, which changes how big the win is: what June says, what happens to Summit and Rourke, and the epilogue stills.

### The shared part

- **Pell** (on stage, with a giant cardboard cheque): "Summit Corp's final offer. Very final. Extremely generous."

- **June** (taking the microphone): "I've had that letter on my desk all season. Here's my answer." She tears it in half. The crowd roars, in proportion to the Pulse.

- **June:** "Kettle Peak isn't for sale. It never was. I just needed reminding."

- Then the variant lines below, then the epilogue: six stills over music, each with one caption line, then the credits over the rider free-riding the mountain at blue hour (the player can ride during the credits; Esc skips).

### Variants by Pulse band

| Band               | Pulse     | June                                                                                                                    | Summit                                                                                | Rourke                                                                                       | Epilogue captions (in order)                                                                                                                                                                                                                                                     |
|--------------------|-----------|-------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1 Hanging on       | 0 to 39   | "Next year'll be lean. But we'll be here. Every chair."                                                                 | Pell: "We'll be in touch." He won't.                                                  | Packs his laminator and leaves with a wave.                                                  | "The Summit Chair ran weekends." / "Dex rebuilt the park with half the wood." / "Wren found two new stashes." / "Hank and Bowie kept everyone safe." / "Tavi's film got 3,000 views. Tavi framed it." / "Kettle Peak opened again next winter."                                  |
| 2 Back on its feet | 40 to 69  | "The bank says yes. Turns out people are revenue."                                                                      | Pell signs a lift ticket for himself, "for research".                                 | Stays as Ops chief at Summit's other resort; sends a postcard.                               | "The gondola got new cabins." / "Dex's park got a second line." / "Ilse took on two new racers." / "Bowie got a new vest." / "Tavi's film played at a festival." / "Kettle Peak had its best season in ten years."                                                               |
| 3 Packed           | 70 to 89  | "Season passes sold out in a day. I named the new snowcat after you." (a snowcat with {nick} painted on it drives past) | Summit withdraws; Pell buys a Kettle jacket at the shop and pretends he didn't.       | Asks June for a job. He's now the lift line's rule-explainer, with a much smaller laminator. | "The new snowcat is called {nick}." / "The Ridge T-bar runs every day." / "Glade Folk planted two hundred trees." / "Rourke's rules got kinder." / "Tavi's film got a sponsor." / "People drive six hours for Kettle Peak now."                                                  |
| 4 Legend season    | 90 to 100 | "Summit called. They want to partner. I said they can buy a lift ticket like everyone else."                            | Pell turns up next morning in Kettle gear with a season pass, asking Dex for lessons. | Becomes Mo's shift partner in the lift maze. They dance on sunny days.                       | "Kettle Peak was named Mountain of the Year." / "Pell learned to ride. Badly. Happily." / "Rourke and Mo won the lift-maze dance-off." / "Wren handed over the fire pit to you." / "Tavi's film premiered in three countries." / "Next season, Cass is coming back. So are you." |

In every variant, Cass gets the last line before the credits, from the bottom of the stage steps: **Cass:** "Same race next year. Don't get slow, liftie." (rider: thumbs-up) That keeps Cass the rival to the end.

### After the ending

The next morning starts as a free day (Progression, After the Classic). Bowie is waiting outside the bunkhouse for the first time in the game, with Hank: **Hank:** "He wanted to say congrats. I'm just the driver." The road-trip invitations arrive by text over the next free days (next section). The Pulse keeps counting and the crowds keep growing, so a band 1 ending can still lead to a packed village later, but the ending scene doesn't replay; it can be rewatched from the lodge photo wall.

## Road trip: Yukimura and Halvard Glacier

After the Classic, two more mountains open as short arcs (Phase 5; Freeride gets them as soon as they're built, D8). Each mountain is its own terrain file with its own runs, lifts, events and one local crew. Travel is the bus stop in the village: E opens a mountain picker, and the trip costs one game day (the rider arrives the next morning). Ability ranks, gear, cash and Followers carry everywhere; Kettle's dailies, Rope Drops and After Hours stay on Kettle Peak.

### How the invitations arrive

| Mountain        | Invited by                                    | Arrives when                                                                                    |
|-----------------|-----------------------------------------------|-------------------------------------------------------------------------------------------------|
| Yukimura        | Wren, for her old friend Aiko Mori            | A4-09 done and Glade Folk tier 5, or the 5th free day after the Classic, whichever comes first  |
| Halvard Glacier | Hank, for his old patrol partner Signe Brekke | A4-09 done and Backcountry tier 5, or the 5th free day after the Classic, whichever comes first |

**Wren** (her only long message): "Kid. Friend of mine runs an inn in Yukimura. Trees like you've never seen. She'll feed you. Go."

**Hank:** "Signe and I patrolled Halvard twenty years ago. She flies heli now. Says the snow's stupid good. Take your wing."

### Local crews

Each road-trip mountain has one crew with five tiers, using the same Rep thresholds and the same daily-challenge system (its own templates, drawn from the existing ones with that mountain's runs). Their tier-ups count toward nothing else; there are no more acts.

| Mountain        | Crew               | Leader                                                | Hangout               | Unlocks by tier                                                                                                                                                            |
|-----------------|--------------------|-------------------------------------------------------|-----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Yukimura        | The Lantern Club   | Aiko Mori, innkeeper, 60s, tiny, unstoppable in trees | The inn's onsen porch | 1 the village lift and the lower forest; 2 the shrine forest; 3 night lantern runs; 4 the upper birch glades; 5 the Lantern Club outfit and the Yukimura Lantern Run event |
| Halvard Glacier | The Glacier Guides | Signe Brekke, heli guide, 50s, calm as ice            | The heli pad hut      | 1 the glacier tram; 2 the first heli drop; 3 the north face (speed riding); 4 heli drops anywhere on the map; 5 the Glacier Guides outfit and the Halvard Descent event    |

### Yukimura missions

| ID                  | Name                 | Objectives                                                                                                                 | Rewards                                                    | Lines                                                                                     |
|---------------------|----------------------|----------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------|-------------------------------------------------------------------------------------------|
| ry-01-arrival       | Snow Country         | talk to Aiko at the inn; ride the village lift; follow Aiko's ghost through the lower forest                               | \$300, 500 XP, Lantern Club tier 1                         | **Aiko:** "Wren said you were quiet. Good. The trees here are loud enough."               |
| ry-02-shrine-forest | The Shrine Forest    | Reach Lantern Club tier 2; ride the shrine forest and collect its 5 small shrines                                          | \$400, 600 XP, 200 Rep                                     | **Aiko:** "Bow to the big cedar. Everyone does. Even the snowboarders."                   |
| ry-03-lanterns      | Lantern Night        | Reach tier 3; at night, follow the lantern line down the village run, then clip post a lantern-run clip scoring 60 or more | \$500, 700 XP, 2,000 Followers                             | **Aiko:** "Once a year the whole village rides down with lanterns. Tonight it's for you." |
| ry-04-the-run       | Yukimura Lantern Run | Reach tier 5; medal gold at the Yukimura Lantern Run (tree run: Flow line)                                                 | \$1,000, 1,000 XP, a lantern for the lodge shelf at Kettle | **Aiko:** "Tell Wren her student is better than her. Don't tell her I said it."           |

### Halvard Glacier missions

| ID               | Name            | Objectives                                                                                                | Rewards                                                     | Lines                                                                                     |
|------------------|-----------------|-----------------------------------------------------------------------------------------------------------|-------------------------------------------------------------|-------------------------------------------------------------------------------------------|
| rh-01-arrival    | The Big One     | talk to Signe; ride the glacier tram; ride the glacier top to the bottom (about 1,800 m of vertical)      | \$300, 500 XP, Glacier Guides tier 1                        | **Signe:** "Hank says you dug a man out in four minutes. Good. Up here you'll need that." |
| rh-02-first-drop | First Drop      | Reach tier 2; take the first heli drop (a scene, then the rider starts on a ridge); ride it to the pickup | \$500, 600 XP, 200 Rep                                      | **Signe:** "Helicopter's loud. Mountain's louder. Listen to the mountain."                |
| rh-03-north-face | North Face      | Reach tier 3; open the wing on the north face and fly through the 8-ring route                            | \$600, 700 XP, 300 Rep                                      | **Signe:** "Wing up, eyes on the rings. Land soft."                                       |
| rh-04-descent    | Halvard Descent | Reach tier 5; medal gold at the Halvard Descent (big mountain line, judged)                               | \$1,000, 1,000 XP, an ice axe for the lodge shelf at Kettle | **Signe:** "Tell Hank he still owes me a beer from 2006."                                 |

Heli drops are travel, like lifts: picking a drop point on the map (from tier 2, then anywhere at tier 4) plays a 10 s flight and places the rider on the ridge. They are never in events or replays.

## Ambient story

Small things that tell the story without stopping play. Each is data keyed by act and Pulse band, picked from the game layer's seeded random stream so the same save always shows the same thing.

### Tier-up messages

When a crew or the Filmer track reaches a tier, its leader texts. Tier 1 is set by the act 1 missions, so messages start at tier 2.

| Leader | Tier 2                                                   | Tier 3                                                                       | Tier 4                                                     | Tier 5                                                                      |
|--------|----------------------------------------------------------|------------------------------------------------------------------------------|------------------------------------------------------------|-----------------------------------------------------------------------------|
| Dex    | "Big Air's open! I built it taller than my ego. Barely." | "Pipe's cut. Also: signature tricks. You've earned showing off."             | "Pro Line's yours. It's scary. I'm scared and I built it." | "Legend. Like, actually. I'm getting it tattooed. Not your face. Probably." |
| Wren   | "Blue trees are open. Go."                               | "Black trees. Careful of the wells."                                         | "Deep Woods. Not many go in."                              | "You know them all now. The trees and the people."                          |
| Ilse   | "Timing lights are on. I'll be watching the splits."     | "Summit Cross is open. Elbows in."                                           | "Downhill. Tuck, breathe, don't think."                    | "I said I'd retire the hut after one more good racer. I'm not retiring."    |
| Hank   | "Touring gear's in the shop. Earn your turns."           | "Backcountry's yours. Every time: beacon, probe, shovel."                    | "Couloir's open. Respect it."                              | "Here's a wing. Don't tell your mother."                                    |
| Tavi   | "Follow drone! It follows you! Like me but quieter!"     | "Sponsors are emailing ME about YOU. I'm your manager now. Kidding. Unless?" | "FPV drone. I'm not crying, you're crying."                | "40,000 people watch you ride. Forty thousand!!"                            |

### Posters

The village has 10 poster boards. In act 1 they're Kettle event posters. From act 2, Summit posters take some boards: the number of Summit posters is 10 minus the Pulse / 10 (rounded down), so a rising Pulse visibly takes the village back. Rourke's Mountain Ops posters are the joke that escalates by act:

| Act | Mountain Ops poster                                                                                         |
|-----|-------------------------------------------------------------------------------------------------------------|
| 2   | "Mountain Ops: Keeping Fun Within Approved Limits." Rourke, arms folded.                                    |
| 2   | "Premium Terrain is a Privilege, Not a Right." Rourke pointing at a rope.                                   |
| 3   | "Have You Seen This Rider?" A blurry photo of the player mid-air. Someone has added a speech bubble: "yes". |
| 3   | "Rule 14: No Fun Beyond This Sign." With a tiny laminated copy stapled to it.                               |
| 4   | "Mountain Ops Spring Social. Approved Dancing 6 to 7 pm."                                                   |
| 4   | "Rourke: Employee of the Month (Self-Nominated)."                                                           |

### Crowds and the lift maze

Crowd sizes follow the Pulse (Progression, Mountain Pulse). On top of that, Mo's lift-maze chatter changes by act, one line each time the player boards the Kettle Express, cycling without repeats:

| Act | Mo                                                                                                                                                                                                              |
|-----|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1   | "Fun fact: chair 12 is haunted. By a guy named Dave. He's nice." / "You're the first liftie who can actually ride. No offence to me." / "June smiled yesterday. I wrote down the date."                         |
| 2   | "Summit gave me a free hat. I'm wearing it ironically. Mostly." / "Cass tipped me in Summit Bucks. What's a Summit Buck?" / "Rourke asked me to 'monitor rope compliance'. I said I'd think about it. I won't." |
| 3   | "I've been shovelling for three days. I've reached enlightenment." / "Bowie found my lunch. Search and rescue, technically." / "Everyone's watching Tavi's film. My mum asked if you're single."                |
| 4   | "Shorts season. My knees haven't seen the sun since October." / "Ticket line's out to the car park. The car park!" / "Whatever happens Saturday, best season I've worked. Don't make it weird."                 |

### Bowie and Pepper

| Act   | Bowie                                                                                                             | Pepper                                                   |
|-------|-------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------|
| 1     | At the patrol room in the village                                                                                 | Roams the village; follows you for 3 minutes after a pet |
| 2     | At the summit patrol hut; rides the Summit Chair with Hank on bluebird mornings                                   | Sits at the café terrace at noon                         |
| 3     | Visible on every rescue-themed beat; after A3-04, trots over to the rider whenever they're within 20 m of the hut | Sleeps by the lodge fire on storm days                   |
| 4     | Wears a Kettle Classic bandana                                                                                    | Wears a tiny costume at the pond skim (a banana)         |
| After | Waits at the bunkhouse door some mornings (20% of free days)                                                      | Follows you for as long as you like after a pet          |

### Radio (Phase 5)

Lou plays between songs on Kettle FM 98.1, one line every 2 to 4 songs, picked by act and Pulse band. Lines are flavour only. A few per bucket to start; the content file should grow to at least 10 per bucket.

| Bucket                   | Lines                                                                                                                                                                                               |
|--------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Act 1                    | "Kettle FM, ninety-eight point one, coming to you from a closet above the bar." / "Snow report: snowy. More at eleven."                                                                             |
| Act 2                    | "Summit Corp's new tent is at the base. They're giving away hats. The hats are also tents." / "Mountain Ops reminds you premium terrain is premium. Mountain Ops is not a sponsor of this station." |
| Act 3                    | "Patrol says: beacon, probe, shovel. I say: also a sandwich." / "Our liftie dug out a guest yesterday. Kettle Peak, folks. We look after each other."                                               |
| Act 4                    | "Pond skim tomorrow. Bring a towel. Bring two." / "Kettle Classic's on Saturday. If you're not here, where are you?"                                                                                |
| Pulse band 1             | "Quiet on the hill today. More snow for the rest of us."                                                                                                                                            |
| Pulse band 2             | "Lodge is buzzing. I had to wait for a table. I work here."                                                                                                                                         |
| Pulse band 3             | "The car park's full. The overflow car park's full. Someone parked on the pond."                                                                                                                    |
| Pulse band 4             | "They're calling it the best season in Kettle history. They being me. I'm calling it that."                                                                                                         |
| After a Rope Drop        | "Word is someone rode a premium run today without paying premium. Allegedly. Wink."                                                                                                                 |
| After an escape at night | "Mountain Ops reports a 'minor compliance event' on the upper mountain. Sounds fun."                                                                                                                |

# Data, saves and build order

How to lay this out in code: the data shapes, the save file and the order to build it in, each step with a test that proves it works.

## Data definitions

These are the shapes every other tab's tables turn into. Content is plain TypeScript objects (typed, checked by npm run typecheck, no JSON loading step). IDs are strings, as the Read me first conventions say.

### Sim side (deterministic, recorded)

// src/sim/setup.ts: copied by the sim once at the start of every ride segment; stored in the replay header.  
export interface RunSetup {  
physicsVersion: number;  
sport: Sport;  
assist: AssistLevel;  
abilities: Record\<AbilityId, number\>; // ranks 0..5; Freeride default 5  
gear: GearStats; // the equipped ski or board's six stats  
signature: SignatureId \| null;  
heatStart: number; // Heat points at start (0 outside chases)  
opsMode: 'off' \| 'chase' \| 'ropeDrop' \| 'afterHours';  
seed: number; // seeds the sim's one random generator  
snow: SnowOverrides; // the day's snow: fresh cells, ice, slush, crud (Core systems, Weather)  
closedZones: ZoneId\[\]; // roped runs, locked glades, shut backcountry gates  
startTimeOfDay?: number; // only for headlamp visibility rules in Ops sight, never physics  
}  
  
export interface GearStats { speed: number; grip: number; float: number; pop: number; stability: number; spin: number } // 1..5  
  
export type SimEvent =  
\| { t: 'takeoff'; speed: number; fromRail: boolean }  
\| { t: 'land'; trick: TrickResult \| null; quality: 'clean' \| 'solid' \| 'sketchy'; airTime: number; height: number }  
\| { t: 'grindStart'; railId: string }  
\| { t: 'grindEnd'; railId: string; trick: TrickResult }  
\| { t: 'press'; kind: 'nose' \| 'tail' \| 'butter'; time: number; trick: TrickResult }  
\| { t: 'nearMiss'; treeId: number; gap: number; speed: number }  
\| { t: 'branchHit'; treeId: number }  
\| { t: 'crash'; cause: 'tree' \| 'overRotate' \| 'underRotate' \| 'cliff' \| 'wall' \| 'treeWell' \| 'pursuer' }  
\| { t: 'reset'; kind: 'tap' \| 'rewind' }  
\| { t: 'zone'; zoneId: ZoneId; entered: boolean }  
\| { t: 'freshTrack'; cell: number }  
\| { t: 'boost'; stokeSpent: number }  
\| { t: 'signature'; id: SignatureId; landed: boolean \| null } // null when it starts  
\| { t: 'spotted' \| 'lost' \| 'caught'; pursuerId: number };  
  
export interface TrickResult { id: string; name: string; points: number; spin: number; flips: number; grab: string \| null }

### Game side (not recorded)

// What can make a mission, message or scene available. All listed conditions must hold.  
export interface Trigger {  
act?: number; // at least this act  
missionsDone?: string\[\];  
anyOf?: { missionsDone: string\[\]; count: number };  
tier?: { track: TrackId; min: number }\[\];  
tierUps?: number;  
timeBetween?: \[string, string\]; // '14:30', '15:45' game time  
weather?: Weather\[\];  
powderMorning?: boolean;  
day?: { min?: number; max?: number };  
pulse?: { min?: number; max?: number };  
onEvent?: 'leaveBunkhouse' \| 'boardLift' \| 'stopInVillage' \| 'dayStart' \| 'petPepper';  
nextDayAfter?: string; // a mission ID: only from the game day after it was done  
}  
  
export type Objective =  
\| { kind: 'goto'; zone: ZoneId \| SpotId; onFoot?: boolean; before?: string }  
\| { kind: 'ride'; lift?: LiftId; vehicle?: 'sled' \| 'heli'; run?: RunId }  
\| { kind: 'talk'; who: CastId }  
\| { kind: 'pet'; dog: 'bowie' \| 'pepper' }  
\| { kind: 'wait'; until: string }  
\| { kind: 'medal'; eventId: string; min: Medal; beatGhost?: GhostId }  
\| { kind: 'challenge'; template: string; tier: number }  
\| { kind: 'trick'; test: TrickTest; count?: number; inZone?: ZoneId }  
\| { kind: 'clip'; minScore: number; inZones?: ZoneId\[\] }  
\| { kind: 'photo'; photoId: string }  
\| { kind: 'follow'; ghost: GhostId; maxDist: number; maxSpeed?: number }  
\| { kind: 'collect'; what: 'glove' \| 'shrine' \| 'stash'; count: number; ids?: string\[\] }  
\| { kind: 'escape'; count: number }  
\| { kind: 'bank'; minPot: number; minHeatLevel?: number }  
\| { kind: 'beacon'; site: string; transmitters: number; timeLimit?: number }  
\| { kind: 'buy'; itemId: string }  
\| { kind: 'scene'; sceneId: string };  
  
export interface MissionDef {  
id: string; // 'a1-01-first-chair'  
act: 1 \| 2 \| 3 \| 4 \| 'road';  
main: boolean; // main missions gate the next act  
title: string;  
giver: CastId;  
trigger: Trigger;  
startMessage?: string; // PhoneMessage id sent when it becomes available  
startScene?: string;  
objectives: Objective\[\]; // in order; each is a checkpoint  
during?: Record\<number, Line\[\]\>; // lines shown when objective N completes  
endScene?: string;  
endLines?: Line\[\];  
reward: Payout;  
pulse?: number; // story Pulse for main missions  
unlocks?: UnlockId\[\]; // 'lift:kettle-express', 'item:snowmobile', 'mode:after-hours'...  
}  
  
export interface Payout {  
cash?: number; xp?: number;  
rep?: Partial\<Record\<CrewId, number\>\>;  
followers?: number;  
items?: string\[\]; // granted, not bought  
rareRoll?: number; // chance 0..1 of a rare cosmetic  
source: string; // 'event:kettle-gs:first-gold', 'daily:park-combo', 'mission:a2-03...'  
}  
  
export interface ChallengeTemplate {  
id: string; track: TrackId; text: string; // 'Bank a combo worth {n} points'  
params: \[number, number, number, number, number\] \| null;  
needs?: { tier?: number; zones?: ZoneId\[\]; act?: number };  
// Reads sim events and run state; returns progress 0..1. Pure, so it can be unit tested on recorded events.  
check: (state: ChallengeState, ev: SimEvent \| GameEvent, n: number) =\> number;  
}  
  
export interface EventDef { // extends the existing one in src/sim/events.ts  
id: string; name: string;  
type: 'slopestyle' \| 'bigAir' \| 'halfpipe' \| 'dash' \| 'flow' \| 'slalom' \| 'gs' \| 'downhill' \| 'cross'  
\| 'bigMountain' \| 'speedRiding' \| 'pondSkim' \| 'photo' \| 'stash';  
crew: TrackId \| null; tier: number; // the tier that opens it in Career  
act?: number; // waits for this act  
start: { pos: Vec3; heading: number; speed: number };  
finish: { a: Vec2; b: Vec2 } \| null;  
gates?: { pos: Vec2; side: 'left' \| 'right' \| 'pass'; width?: number }\[\];  
bounds?: ZoneId;  
timeLimit?: number; attempts?: number;  
medals: { bronze: number; silver: number; gold: number; platinum: number };  
ghosts?: GhostId\[\]; // bot runs to show  
oneOff?: boolean; // story events like summit-invitational  
}  
  
export interface ItemDef {  
id: string; name: string;  
kind: 'ski' \| 'board' \| 'jacket' \| 'pants' \| 'headwear' \| 'goggles' \| 'gloves' \| 'suit' \| 'costume' \| 'tool' \| 'vehicle';  
stats?: GearStats; touring?: boolean;  
price: number \| null; // null = not sold  
shop?: 'rental' \| 'outfit' \| 'cafe' \| 'desk';  
unlock?: { track: TrackId; tier: number } \| { mission: string } \| { sponsor: string } \| { act: number } \| 'start' \| 'rare';  
colourways?: number;  
}  
  
export interface CrewDef {  
id: TrackId; name: string; leader: CastId; hangout: SpotId;  
currency: 'rep' \| 'followers';  
thresholds: \[0, number, number, number, number\]; // total to reach tiers 1..5  
unlocks: UnlockId\[\]\[\]; // index = tier - 1  
abilities: AbilityId\[\];  
}  
  
export interface AbilityDef {  
id: AbilityId; crew: CrewId;  
apply: (t: TuningCopy, rank: number) =\> void; // edits a copy of tuning for one run, never the global  
}  
  
// Ghost and live rider share this (Modes tab, Online hooks).  
export interface RiderFrame {  
t: number; pos: Vec3; heading: number; pitch: number; roll: number; edge: number;  
spin: number; flip: number; grab: string \| null; sport: Sport; outfit: string; crashed: boolean;  
}  
  
export interface ClipRecord {  
id: string; day: number; mode: 'career' \| 'freeride';  
start: RiderStateSnapshot; ops?: OpsSnapshot; setup: RunSetup;  
inputs: Uint8Array; // one mask per step  
events: SimEvent\[\]; // with step numbers  
cells: number\[\]; // discovery cells on its path  
score: ClipScore; posted: boolean; camerasUsed: CameraId\[\];  
}

TuningCopy is a deep copy of the tuning objects for one run. Abilities and gear write into the copy, the rider reads the copy, and tuning.ts stays the rank 5, stat 3 baseline. That keeps the live tuning panel working and keeps runs deterministic.

## Content files

Three folders: src/sim/ gains what changes rides, src/game/ is the new game layer, and src/content/ holds the data the other tabs list. Nothing in src/content/ has logic beyond small pure check functions.

| Path                                                          | Holds                                                                     | From tab                   |
|---------------------------------------------------------------|---------------------------------------------------------------------------|----------------------------|
| src/sim/setup.ts                                              | RunSetup, building a TuningCopy from abilities and gear                   | Read me first, Progression |
| src/sim/events.ts (existing, extended)                        | SimEvent, event runner for every type                                     | Core systems, Modes        |
| src/sim/combo.ts                                              | Presses, combo chain, repeat counting                                     | Core systems               |
| src/sim/stoke.ts                                              | Stoke, boost, signatures                                                  | Core systems               |
| src/sim/zones.ts                                              | Zone tests (polygons, run corridors)                                      | Core systems               |
| src/sim/ops.ts                                                | Heat, pursuers, sight, caught and lost                                    | Core systems               |
| src/sim/vehicles.ts                                           | Snowmobile, touring, wing                                                 | Core systems               |
| src/sim/rewind.ts                                             | The 4-snapshot ring                                                       | Core systems               |
| src/game/ledger.ts                                            | Applies payouts, XP and levels, tiers, unlocks, Pulse                     | Progression                |
| src/game/activity.ts                                          | The activity runner: start, live state, end, Payout                       | Modes                      |
| src/game/clock.ts                                             | Game clock, day start and end, weather roll                               | Core systems               |
| src/game/dailies.ts                                           | Daily generator and tracking, real-day bonus                              | Modes                      |
| src/game/ropeDrop.ts, afterHours.ts, patrolChase.ts           | The three chase modes                                                     | Modes                      |
| src/game/clips.ts, discovery.ts, feed.ts                      | Buffer, clip score, editor, discovery grids, feed                         | Core systems               |
| src/game/story.ts                                             | Story director: triggers, missions, objectives, scenes, messages          | Story                      |
| src/game/pulse.ts                                             | Pulse from the save                                                       | Progression                |
| src/game/save.ts                                              | Slots, versions, migrations, autosave                                     | This tab                   |
| src/ui/phone/                                                 | The seven phone apps                                                      | Modes                      |
| src/ui/recap.ts, hud/, scene.ts, shop.ts, editor.ts, photo.ts | Screens                                                                   | Modes, Story               |
| src/content/economy.ts                                        | Payout table, XP curve, respec price                                      | Progression                |
| src/content/crews.ts, abilities.ts                            | Crews, thresholds, tier unlocks; ability effects                          | Progression                |
| src/content/items.ts, sponsors.ts                             | Gear, outfits, tools; sponsor contracts                                   | Progression                |
| src/content/events.ts                                         | Every EventDef (Kettle Slopestyle and Glade Dash move here)               | Modes                      |
| src/content/challenges.ts                                     | Daily templates, Rope Drop bonus objectives, After Hours night challenges | Modes                      |
| src/content/zones.ts, spots.ts                                | Zones; named spots for scenes, markers, hangouts                          | Core systems, Story        |
| src/content/photos.ts, feed.ts                                | Photo challenges; feed comment pools                                      | Modes, Progression         |
| src/content/story/cast.ts                                     | Cast, colours, home spots, idle lines                                     | Story                      |
| src/content/story/act1.ts to act4.ts, road.ts                 | Missions, their scenes and messages, one file per act                     | Story                      |
| src/content/story/ambient.ts                                  | Tier-up messages, posters, Mo's lines, dog schedules, radio               | Story                      |

npm run typecheck checks the shapes. A content lint test (Testing, below) checks the references between files.

## The save file

Everything is stored in the browser's localStorage (no server until online play). There are four keys: bluebird.career.1 to bluebird.career.3 for the Career slots and bluebird.freeride for the Freeride profile. Each value is one JSON string.

export interface CareerSave {  
version: number; // SAVE_VERSION; bump on any shape change  
seed: number; // random at slot creation; seeds dailies, weather, rare rolls  
created: string; playSeconds: number;  
rider: { nickname: string; sport: Sport; body: string; skin: string; hair: string; outfit: Record\<string, string\> };  
act: 1 \| 2 \| 3 \| 4 \| 5; // 5 = after the Classic  
day: number; clock: number; // game minutes since midnight, for resuming mid-day  
weatherToday: Weather; snowToday: SnowOverrides;  
xp: number; level: number; // level is derived but stored for the recap's 'level up' check  
abilityRanks: Record\<AbilityId, number\>;  
cash: number;  
rep: Record\<CrewId, number\>; followers: number;  
tiers: Record\<TrackId, number\>; // derived from rep/followers but stored to detect tier-ups  
owned: string\[\]; equipped: { ski: string; board: string; signature: Partial\<Record\<Sport, SignatureId\>\>; outfit: Record\<string, string\> };  
missions: Record\<string, { state: 'available' \| 'active' \| 'done'; step: number; progress?: number }\>;  
messages: { id: string; day: number; read: boolean }\[\];  
events: Record\<string, { best: number; medal: Medal \| null; ghost?: string }\>; // ghost = clip id  
dailies: { day: number; picks: { template: string; n: number; progress: number; done: boolean }\[\]; recent: string\[\]\[\] };  
bonus: { date: string; template: string; n: number; done: boolean } \| null;  
ropeDrop: { day: number; run: string; bonus: string; attempts: number; done: boolean } \| null;  
sponsors: Record\<string, { accepted: boolean; done: string\[\] }\>;  
collected: { gloves: string\[\]; shrines: string\[\]; stashes: string\[\] };  
ropeDropsWon: number;  
pulseYesterday: number;  
clips: ClipRecordStored\[\]; // max 50  
photos: { id: string; day: number; png?: undefined; challenge?: string; score: number }\[\]; // PNGs live in IndexedDB  
discovery: { visits: string; trickSpots: string; fresh: string }; // RLE then base64  
settings: { assist: AssistLevel; audio: Record\<string, number\> };  
}

The Freeride profile (FreerideSave) holds the Freeride menu settings, best result and ghost clip per event, the Patrol Chase top 10 with ghosts, its own clip album and feed counter, and custom challenges made or loaded.

### Rules

- **Store IDs, never positions.** Items, missions, events and templates are referred to by ID only.

- **Store facts, derive the rest.** Pulse, unlock lists and shop stock are computed from the save each time. Tiers and level are also derivable; they're stored only to detect a change.

- **Autosave** after every payout, at day end, when a mission step completes, and on page hide (visibilitychange). Mid-run state is never saved: leaving mid-run resumes from the last payout, at the bunkhouse door (Modes tab).

- **Write safely.** Write to bluebird.career.N.tmp, then copy to the real key, keeping the old one as .bak. On load, if the main key fails to parse or validate, load .bak and tell the player once.

- **Size.** Clips dominate: one 15 s clip is about 1,800 input bytes plus a snapshot and events, about 6 KB as base64. 50 clips is about 300 KB; the discovery grids are a few KB; total stays well under the 5 MB localStorage limit. Photos go to IndexedDB because PNGs are large.

- **Export and import.** Settings has "Export save" (downloads bluebird-slot1-day23.json) and "Import save" (validates, then migrates). This is also the backup story until online accounts exist.

### Versioning and migration

SAVE_VERSION starts at 1. Every change to the shape adds one function to MIGRATIONS in src/game/save.ts:

const MIGRATIONS: Record\<number, (s: any) =\> any\> = {  
// 1 -\> 2: added ropeDropsWon  
1: (s) =\> ({ ...s, version: 2, ropeDropsWon: 0 }),  
};  
export function migrate(s: any): CareerSave {  
while (s.version \< SAVE_VERSION) s = MIGRATIONS\[s.version\](s);  
return validate(s); // fills missing fields with defaults, drops unknown IDs with a console warning  
}

A renamed ID gets a line in an ID_RENAMES map applied during validate, so old saves keep their progress. A replay or clip whose physicsVersion is older than the current one is kept for viewing by its stored RiderFrames but can't count for a ghost race or a leaderboard.

## Build order

Each step leaves a playable game and ends with an acceptance test that proves it works. The order follows the main plan's phases: riding systems first (still Phase 2 and 3 quality), then Career (Phase 4), then more mountains (Phase 5). Each step that changes riding or visuals also gets a play test run and a look at the screenshots, as CLAUDE.md requires.

| \#  | Step                              | Builds                                                                                                         | Acceptance test                                                                                                                                                                        |
|-----|-----------------------------------|----------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1   | Sim events and RunSetup           | rider.events, RunSetup in the replay header, TuningCopy                                                        | Replaying any recording gives the same event list, step for step, as the live run (unit)                                                                                               |
| 2   | Presses, combos, rewind           | combo.ts, rewind.ts, HUD combo line                                                                            | Scripted input: Nose Press, 360, Boardslide within 1.5 s banks one combo at multiplier 1.5; a crash before banking loses it (unit). Hold R rewinds 3 s and pushes a reset event (unit) |
| 3   | Stoke, boost, signatures          | stoke.ts, the Stoke HUD meter                                                                                  | Double-tap W at 25+ Stoke spends 25 and accelerates; a signature from a high kicker lands clean; from a small one the input is ignored (unit + play test)                              |
| 4   | Zones and the trees               | zones.ts, gap sense picks the gap nearest the heading, tree wells, stumps, pillows, logs, stashes, fresh cells | A bot holding a straight line at a two-gap tree is nudged into the nearer gap (unit). Entering a tree well under 5 m/s crashes (unit)                                                  |
| 5   | All event types                   | Event runner for every type in Modes; one course per type on Kettle Peak                                       | Each new event's bot run scores gold exactly and the medal table matches the rule (unit); each has a start gate and finish screen (play test)                                          |
| 6   | Clips and discovery               | 60 s buffer, V, clip score, discovery grids, replay editor with chase and helmet cams                          | A saved clip rebuilt from its snapshot matches the live ride's positions to 1 mm (unit); clip score parts are each 0 to 100 on a recorded test ride (unit)                             |
| 7   | Mountain Ops and Patrol Chase     | ops.ts, Heat, pursuer sight and behaviour, Patrol Chase in Freeride                                            | A recorded chase replays with identical pursuer positions (unit); breaking sight behind a ridge triggers lost within the cool-down (unit); a full chase plays (play test)              |
| 8   | Saves and Freeride profile        | save.ts, the Freeride menu, best ghosts per event                                                              | Save, reload, migrate a v1 fixture to the current version without loss (unit)                                                                                                          |
| 9   | The ledger                        | Payouts, XP and levels, crews and tiers, abilities into RunSetup, shops and items, respec                      | Applying the payout table to a scripted week gives the expected level and tiers (unit); ability rank 0 Pop gives a lower jump than rank 5 (unit)                                       |
| 10  | The Career day                    | Clock, bunkhouse, phone (all apps), weather roll, recap, sleep                                                 | A day runs 08:15 to the recap with the clock, lifts closing at 16:00 and autosave (play test with \_\_game.career.skipTo('15:44'))                                                     |
| 11  | Dailies                           | Generator, tracking, rewards, real-day bonus                                                                   | Same save seed and day gives the same five picks; no template repeats within 2 days (unit)                                                                                             |
| 12  | Story director and act 1          | story.ts, triggers, objectives, scenes, messages, markers; act 1 content                                       | A scripted player completes A1-01 to A1-08 through \_\_game commands and act 2's gate opens at 4 tier-ups (e2e)                                                                        |
| 13  | Rope Drop, After Hours, act 2     | Both modes and act 2 content                                                                                   | Rope Drop: caught resets to the rope; a win pays once a day (unit). After Hours: a pot multiplies by level and is lost when caught (unit). Act 2 plays through (e2e)                   |
| 14  | Vehicles, backcountry, act 3      | Snowmobile, touring, beacon search, backcountry gates, the storm cycle, act 3 content                          | Beacon search finds a transmitter by distance alone (play test); act 3 plays through including A3-04's scripted slide, and the closed slope can't be entered (e2e)                     |
| 15  | Act 4, the Classic and the ending | Pond skim, practices, Final Cut, Classic day, four ending variants, Pulse readers                              | With \_\_game.career.setPulse(n) for 20, 50, 80 and 95, the ending scene picks the matching variant (e2e); crowds scale with the Pulse (screenshot check)                              |
| 16  | Filmer extras                     | Follow drone, tripod, FPV drone, slow motion, auto-edit, sponsors, feed, photo mode and photo challenges       | Auto-edit of a recorded ride picks the highest-scoring 10 to 15 s (unit); photo-4 is valid only with Bowie in frame (unit)                                                             |
| 17  | Phase 5                           | Custom challenges, the radio, Yukimura and Halvard Glacier with their crews and missions, heli drops           | A shared challenge link reloads the same gates (unit); each road-trip mission plays (e2e)                                                                                              |

A full season is "done" (the Phase 4 bar in the main plan) when a fresh Career save can be played from character creation through the ending without debug commands, which the balance sim (Testing) and one long human play test both confirm.

## Testing

Three layers, all in the existing setup: Vitest unit tests in test/unit, Playwright play tests in test/e2e (screenshots in test/artifacts/), and a new balance simulation.

### Unit tests

- **Determinism** (the most important): for each mode that touches the sim (free ride, each event type, Patrol Chase, Rope Drop, After Hours), record a ride, replay it from the header, and compare every step's position and event list. Run it again with a different TuningCopy from abilities and gear to prove the setup is applied, not the global tuning.

- **Scoring:** trick, press and combo maths from Core systems with fixed inputs; repeat decay in events and in free riding; clip score parts and Followers; originality on a hand-made grid.

- **Economy:** every row of the payout table; the XP curve values in the Progression table; tier thresholds; ability effects at ranks 0 and 5; gear stats at 1 and 5.

- **Pulse:** each part and cap; the total never exceeds 100; the bands.

- **Generators:** dailies (same seed, same picks; no 2-day repeats; parameters by tier), Rope Drop run and bonus, weather by act, rare rolls.

- **Save:** round trip, migration from a stored v1 fixture, .bak recovery from a corrupted string, ID renames.

- **Content lint** (test/unit/content.test.ts): every ID is unique and kebab-case; every reference (mission to cast, objective to event, zone, spot, item, template; unlock to item; message to mission) resolves; every main mission is reachable from the act's start by its triggers; every act's main missions together fit inside its gate; no line is longer than 20 words; every {token} in a line is a known one.

### Play tests (npm run test:e2e)

The window.\_\_game debug hook gains a career object so tests can drive the game layer without playing for hours: career.newSave(seed), career.skipTo(time), career.nextDay(), career.grant(payout), career.completeMission(id), career.setPulse(n) (overrides the computed value for tests only), career.state() (a read-only snapshot). It exists only in dev and test builds.

- One test per act: start at that act from a fixture save, complete its main missions with a mix of real riding (bots for medals) and completeMission for the slow parts, and check the next act opens.

- One screenshot per new screen: phone apps, recap, a scene, the shop, the replay editor, photo mode, the Classic stage at each Pulse band.

- Chase tests: Patrol Chase survives 60 s with a bot that hides in a glade; Rope Drop success and caught paths.

### Balance simulation

npm run balance (a new Node script, tools/balance.ts) plays a season on paper. It doesn't ride: it models a player by a profile ("park lover", "tree rider", "racer", "filmer", "does a bit of everything") as how many dailies, events, missions, clips and Rope Drops they complete per game day, and how well (medal odds by crew tier). It runs the real ledger, dailies generator and Pulse code over 60 game days and prints, per profile:

- The day each act opens and the day the Classic is reached.

- Level by day, and the day level 30 is reached.

- Each crew's tier by day.

- Cash earned against the cost of the gear that profile would want.

- The Pulse at the Classic, so each profile's ending band is known.

Targets to tune toward: act 2 by day 4 to 6, act 3 by day 12 to 16, act 4 by day 22 to 28, the Classic around day 30 to 35; the "does a bit of everything" profile reaches Pulse band 3 and level 25 to 30 at the Classic; no profile is stuck more than 3 game days at any gate. When a target misses, change the numbers in src/content/economy.ts, never the logic.
