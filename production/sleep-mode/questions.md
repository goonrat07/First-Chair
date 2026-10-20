# Sleep mode questions

Questions only you can answer. Each says whether it stopped an item; nothing was guessed.

## 1. Can a rider jump over trees? (Story 005; does not block anything)
- **Now:** trees have no height. A rider in the air who crosses a tree crashes into the trunk or clips the branches, at any height, then lands where that step began. Found by the engine review of Story 005.
- **Options:**
  - (a) **Keep it.** Trees always block, which is simple and readable at the follow camera.
  - (b) **Give trees a height.** Use a new number in the slope layout, or a tuning value for every tree, and skip the check while the rider is higher than the tree. Jumping a small tree from a kicker becomes possible.
  - (c) **Clear only the trunk.** Over a certain height the trunk can't be hit, but the branches still clip.
- **Recommendation:** (a) for the MVP, then revisit after the first playtest. Nothing waits on this; the code works as described in (a).
