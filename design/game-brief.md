# Game Brief: First Chair

<!-- Minimal-tier design record. The long-term design (Career, story, crews,
Mountain Ops, clips, economy) is in design/reference/bluebird-spec.md and comes
after this MVP. What the user says in session overrides both. -->

**One-sentence pitch:** A warm, sunlit skiing game where you carve, jump and thread the trees of a small family-run mountain, and every run you ride can be replayed exactly.

## Core loop
- Pick a line down the mountain and ride it: carve, tuck, jump, grab, slip between trees.
- Clean landings, tricks and close calls build score and the Flow meter, which makes you faster.
- A crash resets in about a second, so you go straight back up and try a better line.

## Player goal & fail state — what "working" looks like
- A run is complete when the rider gets from top to bottom of the test slope under full player control: every speed change, landing and crash traces to the player's inputs and the snow.
- Failing is a crash: a trunk hit, a bad landing or over-rotation. It resets to the last safe spot in about a second.
- Every run replays exactly from its recording.

## MVP — what must exist to be the game
- Deterministic riding sim (fixed 120 Hz) with input recording and exact replay.
- Skiing on snow: carve, skid, tuck, brake, pole on flats, and five snow types (groomed, powder, crud, ice, slush), each with its own grip and drag.
- Air: hold-to-load jumps, spins, flips and grabs, landings graded clean, solid or sketchy, crashes, and tap-to-reset.
- Trees: near misses fill Flow, branch clips cost speed, trunk hits crash.
- Rails and boxes with grinds, and single-trick scoring and naming.
- One test slope (groomer, powder bowl, kicker line, short glade) on a heightmap shared by the sim and Unity Terrain, with a high, pulled-back follow camera like GMA2's (a Steep-style chase cam can come later) and keyboard plus gamepad input.
- First real art toward the target look: skier, skis, poles, trees, snow trails and lighting.

## Out of scope — not building this
- Snowboard. It comes next, once skiing feels right; the sim leaves room for it.
- Combos, presses and butters, rewind, Stoke, boost and signature tricks: the next riding layer.
- Career, story, missions, crews, economy, Mountain Ops and Heat, clips and the replay editor.
- Lifts, walking, the village and the full Kettle Peak mountain. One test slope only.
- Online play, leaderboards and multiplayer.

## Build order
1. Sim skeleton and the replay determinism test, running under `dotnet test` and CI.
2. Glide and carve on the test slope. This is the core fun, so prove turning feels good first.
3. Unity view: terrain from the same heightmap, placeholder skier, chase camera and input, playable on the PC.
4. Snow types, jumps, air tricks, landings, crashes and reset.
5. Trees and Flow, then rails and trick scoring.
6. Art pass toward the target look.

---
**Who it's for / what they feel:** Players who love the flow of skiing a mountain and want it warm and forgiving, where a crash costs a second, not a run.

**Art & audio direction:** Realistic-leaning light and snow with clean, low-detail models and flat colors, after Grand Mountain Adventure 2. Audio direction is not set yet.

**Reference game:** Grand Mountain Adventure 2: its free-skiing feel on one slope with jumps and trees, without its resort-wide progression.
