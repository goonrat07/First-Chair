# Draft: the season loop

> **Status:** Worked out with the user in conversation on 2026-11-02 and 2026-11-03. **Decided** marks what the user chose;
> *Proposed* marks Claude's suggestions, open until the user confirms them. Numbers marked *Proposed* are starting
> points to tune in the Feel Lab, not balance. This is the game after the base skiing system: the brief's rule stands
> that everything else waits until riding works and feels right. It replaces parts of the earlier drafts; see "What
> this changes in the earlier drafts" at the end.

## Ground rules (from the project, not new)

- **Anything that changes the ride goes through the sim.** The loadout, stamina and the other skiers change how a
  run plays, so they are sim inputs and sim state: a run's `RunSetup` carries the loadout, stamina at the start and
  any buffs, and recordings store them, so replays and ghosts stay exact. The clock, shops, crews, sponsors, perks
  and the cabin never touch the sim.
- **Forgiving** (the brief: warm and forgiving). Nothing fails a day or a season, and no injury ends a season. A
  crash costs a moment and some stamina. *Proposed:* an event missed comes round again on the calendar.

## The premise

- **Decided:** you are a regular at the mountain. You have skied here for many seasons and know all the staff. You
  rent a small cabin beside a short beginner run with Mo.
- **Decided:** Mo works the lifts, so Mo has the inside news each morning (which lift opens first, how busy it will
  be, who is in town), and you see each other at the bottom of the lift through the day.
- *Proposed:* the story is the season you go from local legend to a known name, as sponsors start to notice you.
  Cass is the season's new face and your rival.
- **Decided:** Cass rides for Summit Corp, the resort company trying to buy the mountain. Summit Corp is cruel and
  evil; Cass is not: she is competitive by nature and not in it for money. Her arc and the finale (a blizzard the
  day before the Winter Classic, where you save her; she then skis the Classic for the mountain) are in
  `story-outline.md`.
- *Proposed:* the cabin is home base. Mornings: the forecast on the radio, Mo's gossip, the loadout, the bag, then
  ski down the beginner run to the lifts. Evenings: the day's results, sponsor packages, the shop, planning
  tomorrow. It shows your progress: the gear wall, trophies, and sponsor perks such as the drink fridge.

## A day on the clock

- **Decided:** a running clock limits how much you can ski in a day, so each day you balance missions, races,
  events, challenges and filming.
- **Decided:** some lifts take longer than others. *Proposed:* a lift's ride time comes from its length (already in
  the mountain file) and a speed per lift (a new field), so a slow old double costs more of the day than a fast quad.
- **Decided:** big races and events start at set times. Time trials and freestyle challenges are always there: you
  ski into their start, as in GMA2. *Proposed:* bronze, silver and gold medals.
- **Decided:** requests can be tied to the time of day, such as "land a backflip during golden hour", for filming
  and for anything else. *Proposed:* any objective in `mission-system.md` can carry a time window.
- **Decided:** filming depends on the time of day, and the light counts a little toward a clip's originality.
  *Proposed:* originality from trick novelty (not a trick you posted lately), spot novelty (somewhere new) and
  conditions (golden hour, fresh snow, storm light), with time of day a small part of it.
- **Decided:** a season is 50 days of about 20 real minutes each, roughly 17 hours, and you can skip a day (bad
  weather, a rest). *Proposed:* a 30-day season as the first version, to test the loop.
- *Proposed:* lifts run from 9:00 to 16:00. In-game time runs about 20 times real time while riding, and a lift
  ride costs its own fixed time, so a day holds roughly 8 to 12 runs.
- *Proposed:* weekdays are quiet and powder lasts; weekends bring crowds, faster track-out and the bigger events.
- *Proposed:* on windy days the top lifts close (wind holds), so the forecast can change the plan.

## Mornings: forecast, calendar, loadout, bag

- **Decided:** a morning forecast. *Proposed:* snowfall, temperature and wind, plus the grooming report (which runs
  the cats groomed overnight).
- **Decided:** a season calendar with the events on their days.
- **Decided:** you pick your gear and pack your bag each morning, for what you want to do that day.
- **Decided:** saved loadouts, so a "race day" or "powder day" set is one choice.
- **Decided:** demo days, to try gear for a day before buying it.
- *Proposed:* gear can be swapped mid-day at the base locker, which costs time (a perk can shorten it).

## The loadout: what changes the riding

**Decided:** gear should change how riding feels and what you can and can't do, and a day's build should be a real
choice, with depth in the ballpark of Cyberpunk 2077 but a little less specialised. Boots and bindings change the
riding less than skis do.

| Slot | Choices | What it changes |
|---|---|---|
| Skis | each ski's five bars (`gear-progression.md`: Edge, Glide, Float, Pop, Swing), plus perks and a few hard limits | the most. *Proposed:* bigger differences than the measured 5% (about 15 to 25% in a ski's speciality), perks such as twin tips landing switch cleanly and fat skis floating where narrow skis bog down |
| Boots | flex | **Decided:** comfortable boots lose stamina more slowly. *Proposed:* stiff boots bite sooner and hold at speed but tire you faster; soft boots forgive landings but wander at speed |
| Bindings | release setting (DIN) | **Decided:** tight racing bindings go a little faster, but a crash costs more stamina and risks a minor injury, since the skis don't come off. *Proposed:* loose bindings make crashes cheaper, but a hard landing can pop a ski off |
| Bindings | mount point | **Decided** as a choice. *Proposed:* forward for park (spins, switch), back for powder (float) |
| Wax | matched to the day | **Decided** as a choice. *Proposed:* chosen for the temperature and snow, it wears off through the day; re-wax at a hut, the cabin or with a wax kit |
| Edge tune | sharp or dull | **Decided** as a choice. *Proposed:* sharp edges grip ice but catch in crud |
| Cosmetic only | clothes, the bag's look, goggles, helmet | **Decided:** wear whatever you want; none of it changes the riding |

*Proposed:* every build can still ski everything at a basic level. Specialising makes some things much better and
others a little worse; the hard limits are few and clear (no avalanche gear, no backcountry gate).

## The bag

- **Decided:** packing is a simple weight limit, and what you carry competes for it: bring a drone and there is less
  room for snacks. Snacks are cheap; their limit is weight, not money.
- **Decided:** backcountry gear (beacon, probe, shovel and, *proposed*, skins) doesn't count toward the weight.
- **Decided:** the bag itself is cosmetic. A skill-tree node raises the limit a little, for every bag.
- **Decided:** snacks and drinks are eaten on a lift or when stopped. *Proposed:* eating when stopped is an input the
  sim records, so replays stay exact.

*Proposed* items and weights (the limit starts at 20 points; each rank of the bag-space node adds 2):

| Item | Weight | What it does |
|---|---|---|
| Drone | 8 | film yourself without Tavi, from the air |
| Drone battery | 2 each | a few minutes of flying each |
| Action camera | 2 | first-person clips; less following than the drone or Tavi |
| Wax kit | 3 | re-wax on the hill when the wax wears off |
| Multitool | 1 | change the release setting or mount point mid-day |
| Edge file | 1 | touch up the edges on the hill |
| Thermos | 3 | a hot drink you refill at huts |
| First-aid kit | 3 | patch a minor injury on the spot and win back some of the lost stamina cap |
| Bar | 1 | a small stamina refill |
| Energy drink | 1 | a quick refill and a short buff |
| Sandwich | 2 | a big refill |

## Stamina

**Decided** (as proposed in the conversation, all agreed):

- Stamina drains as you ski, faster in powder, on steeps and in a tuck. A crash or fall takes a chunk; more with
  tight bindings. Comfortable boots slow the drain.
- It refills slowly on lifts. Huts let you rest (which costs time), eat a meal for a faster refill and a temporary
  buff, and buy snacks for the bag.
- Low stamina is mild: a weaker pop and slower edges. At zero you sit down to rest, which costs time; it never ends
  the day.
- No season-ending injuries. A bad crash can give a minor injury that lowers your stamina cap for the rest of that
  day.

*Proposed:* stamina is sim state (it changes the ride), drained and refilled by sim rules from the rider's state
and events, so the same inputs always give the same stamina.

## Warming huts

- **Decided:** small quests (for example, land a backflip) for a little money or XP; rest to win back stamina, at the
  cost of time; meals that refill faster and give temporary buffs; snacks and drinks to buy for the bag.
- *Proposed:* refill the thermos; re-wax; quest XP goes to the skill tree's points.

## Crews, gear and rewards

- **Decided:** gear is restricted by crew (faction) level: you must reach a level in the freestyle crew to buy its
  park skis, and so on.
- **Decided:** challenges and races are quite rewarding for gear, so there is a real reason to do them.
- **Decided:** three crews: Freestyle (park), Race and Freeride (powder, trees and steeps). Filming is not a crew:
  you film in every style, and it grows your following, which is what sponsors look at.
- *Proposed:* Dex leads Freestyle, Ilse Race and Wren Freeride, and Hank's patrol opens the backcountry. Camera gear
  such as the drone unlocks by following rather than by crew level.

## Following and sponsors

- **Decided:** many sponsor offers over the season as your crew levels and social following grow, not one at the
  end. *Proposed:* the following grows with the clips you post, your results and your challenge medals.
- **Decided:** sponsors clash: you can't hold two that compete (one ski brand at a time), so choosing a sponsor
  shapes your builds.
- *Proposed:* offers climb a ladder, local first (the ski shop, the café), then regional teams, then big brands.
  Each offer needs a crew level or a following, asks something of you (ride their skis in events, post two clips a
  week) and pays in gear, money or perks.

## Perks

**Decided:** permanent upgrades won from events, sponsors, achievements and crew standing. The user's examples: a
drink sponsor's mini fridge in the cabin with free drinks; a discount at the warming huts; mid-day gear swaps 15%
quicker.

*Proposed* more:

- June, after so many seasons: load the first chair 15 minutes before the public on powder days.
- A groomer you know: the grooming report the night before.
- The ski shop's sponsorship: a tuning bench in the cabin, with free wax and edges each morning.
- Tavi's crew: a snowmobile tow to filming spots.
- Hank and the patrol: backcountry gates open earlier. Wren: hidden stashes marked on the map.
- Hut regular: the best meal at the usual price.
- Achievements: for example, 50 days on the hill for 5% more stamina.
- *Proposed:* the drink sponsor gets a made-up brand, such as "Summit Rush".

## Other skiers and tracks that build up

**Decided:** other skiers ride alongside you and leave tracks; tracks build up through the day in ways that make
sense, so on powder days the first chair is worth it. Other skiers are **solid**: you can crash into them, and near
misses count.

*Proposed* algorithm:

1. **Cells.** The mountain is split into cells about 10 m across. Each holds its snow type, its new snow's depth and
   how tracked out it is, from 0 (fresh) to 1 (tracked out).
2. **Overnight.** The forecast's snowfall adds depth and resets off-piste cells to fresh; the grooming cats reset the
   groomed runs.
3. **Traffic.** Each lift carries a set number of riders an hour, from its capacity and how busy the day is
   (weekend, holiday, powder day). Each rider picks a run by skill and taste (beginners to the greens, powder hounds
   to the freshest bowls), nearer runs more often.
4. **Coverage.** Passes land roughly at random, like raindrops on pavement: after N passes, each a track w wide
   across a slope W wide, the untouched share is about e^(−N·w/W). So wide bowls stay fresh longer than narrow
   chutes, stashes beside the lift go first, and long traverses keep fresh snow into the afternoon.
5. **What you see.** Near you, a few riders are real skiers on generated lines (turns down the fall line, around the
   trees), stamping tracks as yours do. Farther off, coverage shows as old tracks. Past a threshold a cell's powder
   becomes crud for the riding.
6. **The same every time.** Everything comes from the day's seed and the clock. The sim reads the snow as it stands
   at the start of each run, never mid-run.

**Decided:** other skiers follow the skier's code. Skiers below you hold their line, and avoiding them is your job.
Skiers above you steer around you, so nobody hits you from behind, where the chase camera can't see.

**Decided:** what a collision costs depends on speed. A bump at low speed is a stumble; a hit at speed is a full
crash with a chunk of stamina, and the other skier falls too.

*Proposed:* the skiers near you are sim obstacles moving along their generated lines, and the steering of skiers
above you is a sim rule too, so replays stay exact. Holding their lines comes first; the steering comes next. A near
miss fills Flow as a tree's does.

## What this changes in the earlier drafts

| Draft | What changes |
|---|---|
| `story-outline.md` | The premise is replaced: you are a regular, not the liftie. The cast, acts and missions stay as material until they are rewritten around the new premise and the season calendar. |
| `gear-progression.md` | The loadout above replaces "eight skis on five bars" as the whole gear system. The five bars and the measured tuning they act on carry over as the skis' stats, made bigger; gear is earned through crew levels, challenges, events and sponsors. |
| `skill-tree.md` | Gains a bag-space node. Points still come from riding in each branch's style; hut quests' XP may add to them. |
| `mission-system.md` | Still the basis. Objectives gain time windows; challenges are always-open start gates; near misses with skiers join the trees'. |
| `progression-timeline.md` | Replaced: the season is laid out in days on the calendar, not in acts. |

## Build order (Proposed)

1. **In the sim, one story each, tests first:** stamina; the loadout in `RunSetup` (ski bars and perks, boot flex,
   release setting, mount point, wax, edge tune); solid skiers on generated lines with near misses; deep powder that
   bogs narrow skis down. Each is tuned in the Feel Lab.
2. **The day:** the clock and lift times, always-open challenges and timed events, huts, the bag, the forecast, and
   the tracks building up.
3. **Around it:** crews and their shops, following, sponsors and perks, the cabin, the calendar.
4. **The story** on top, rewritten for the new premise.

## Open questions

None left here (answered on 2026-11-03: who Cass rides for, the crews, the season's length, Mo's job, how other
skiers steer and what a collision costs). The story's beats continue in `story-outline.md`.
