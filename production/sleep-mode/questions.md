# Questions waiting on the user

## Story 009: close it after your playtest? (carried over from the 2026-10-26 run)
- **Item:** story-009-gma-style-riding-and-feel-lab (In Review)
- **Question:** After riding the latest build on your PC, does the riding feel right enough to close story 009?
- **Options:** Close it (then tune further in the Feel Lab as new stories) / Keep it open and tell me what feels off
  (recommended if anything feels wrong: say it in your own words, and paste Feel Lab "Copy values" if you tuned)
- **Waiting on it:** nothing; the story's code and evidence are in.

## Story 007: sign off the first art pass?
- **Item:** story-007-first-art-pass (In Review since 2026-11-02)
- **Question:** Its six build criteria are met across this story and stories 012 to 018 (the map:
  `production/qa/evidence/story-007-first-art-pass-evidence.md`). Does the look, on your PC, sign it off?
- **Options:** Approve (tick the sign-off row and the story closes) / Not yet: name what is off (the story stays open
  and what you name becomes its next work) (recommended: ride the latest build first)
- **Waiting on it:** nothing; it only closes the story.

## Story 018: sign off the deformable snow, and does it hold 60 frames a second?
- **Item:** story-018-deformable-snow (In Review since 2026-11-02)
- **Question:** On your PC, do the trenches, the raised snow and the skis' sink read as skis cutting into snow, and
  does the game hold 60 frames a second with the snow mesh on? (In the cloud's software renderer it costs about 2.2
  times the frame; a GPU should make far less of it, but only your PC can say.)
- **Options:** Approve both (the story closes) / The look needs changes: say what (the depths, raise and tint are on the
  Feel Lab's Light tab, the sink on its Effects tab) / Too slow: say the frame rate (the mesh's size and spacing are
  fields on the SnowSkin object in the scene, and groove strength 0 switches it off)
- **Waiting on it:** nothing; it closes the story. The playtest checklist covers it:
  `production/qa/playtest-checklist-2026-11-02.md`.

## Next art story: rocks and props on the mountains?
- **Item:** a possible story 019, from the brief's 2026-10-23 ideas ("procedural generation of trees and props where it
  makes sense"). Not started: it changes the mountain file format and, for rocks you can hit, the sim, which sleep mode
  leaves to you.
- **Question:** Should the mountains get rocks (low-poly, flat-coloured, snow on top, GMA2's look) and other props
  (fences, signs, rope lines), and should rocks be things you can crash into?
- **Options:** Rocks you can hit: made by script like the trees, placed by the mountain tool, and in the sim as
  obstacles like tree trunks (recommended: a mountain with only trees and snow reads empty, and obstacles make lines
  matter) / Rocks as scenery only, where the rider can't reach (cliff bands, the map's edges): no sim change / Not yet:
  something else first
- **Waiting on it:** the story itself; nothing else.

## The drafts: story, gear, skill tree, missions (your choices, no rush)
- **Item:** the brainstorm drafts you asked for, in `design/drafts/`: `story-outline.md` (directions, then the
  prologue and acts 1 to 4 mission by mission), `gear-progression.md` (skis measured in the sim),
  `skill-tree.md` (ranks measured in the sim), `mission-system.md` (how missions would read the riding).
- **Question:** each draft ends with its own numbered decisions (6 to 7 each). Which directions do you want?
- **Options:** answer any of them in your own words; or pick the recommended option on each (marked in the drafts)
  and say so; or say which draft to turn into stories first (recommended: the prologue, P1 to P3, on the test
  mountain, which proves missions end to end).
- **Waiting on it:** any story-mode, gear or skill work. Nothing in the current game waits on it.
