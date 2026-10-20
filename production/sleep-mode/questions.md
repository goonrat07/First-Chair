# Sleep mode questions

Questions only you can answer. Each says whether it stopped an item; nothing was guessed.

## 1. Can a rider jump over trees? (Story 005; does not block anything)
- **Now:** trees have no height. A rider in the air who crosses a tree crashes into the trunk or clips the branches, at any height, then lands where that step began. Found by the engine review of Story 005.
- **Options:**
  - (a) **Keep it.** Trees always block, which is simple and readable at the follow camera.
  - (b) **Give trees a height.** Use a new number in the slope layout, or a tuning value for every tree, and skip the check while the rider is higher than the tree. Jumping a small tree from a kicker becomes possible.
  - (c) **Clear only the trunk.** Over a certain height the trunk can't be hit, but the branches still clip.
- **Recommendation:** (a) for the MVP, then revisit after the first playtest. Nothing waits on this; the code works as described in (a).

## 2. Should leaving a rail score as a trick? (Story 006; does not block anything)
- **Now:** flying off the end of a high rail lands as a short "Straight Air" worth about 20 to 30 points. The grind itself scores separately (200 points per second times balance, a placeholder value).
- **Options:**
  - (a) **Keep it.** A rail exit is just a small jump.
  - (b) **Add a rail-exit bonus,** or let a grind run into the next jump as one trick ("Boardslide to Backside 180" in the old spec). That is combo territory, which the brief keeps out of the MVP.
  - (c) **Score no air under a minimum air time.** This also covers the short hops off sharp crests that Story 004 flagged.
- **Recommendation:** (c) after the first playtest, because it fixes both cases with one tuning value. Nothing waits on this.

## Answers (2026-10-20)
1. **Trees always block.** Kept as built; revisit after the first playtest. No code change.
2. **Minimum air time.** Air shorter than a tuned minimum scores nothing. That covers short rail exits and the tiny hops off sharp crests. Added on the user's request with a 0.25 s placeholder (`Tuning.MinScoredAirTime`); a playtest picks the real value.
