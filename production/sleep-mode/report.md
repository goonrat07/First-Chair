# Sleep mode report

Started 2026-11-02T15:46:48Z · Ended (running) · Reason (running) · Commits 0 (`0d62120..`)

## Summary
- Finished: (running)
- Skipped: (running)
- Waiting on you: see production/sleep-mode/questions.md
- Needs the PC: see the plan's "Left for the PC"
- Next: (running)

## Before this run (2026-11-02, while you were here)
- Story 018 (deformable snow): the snow mesh near the rider, its shaders, the terrain cut, the track ribbons' fade, the
  skis' sink and footprints, and the per-terrain material fix are pushed (35d91b0, 063f657, 29c86a5, 0d62120). The
  first cloud build showed the test slope's mesh drawn with the test mountain's snow (one material pair shared by both
  scenes); 0d62120 gives each terrain its own. In powder on the test mountain (build 29c86a5) the skis cut real
  trenches with raised rims and sink into them.
- **On your PC:** reset `Assets/Data/Riding/LightingSettings.asset` (or delete it so the scene builder makes a new
  one): the old asset keeps story 017's groove values, which no longer exist, and misses story 018's depths.
- Story 017 is superseded: folded into 018. The snow research is saved in `docs/research/snow-deformation.md`.

## story-007-first-art-pass (In Review)
- Changed: `production/epics/first-chair/story-007-first-art-pass.md` (Status In Review, six criteria ticked with where
  each was met, an As built section); `production/qa/evidence/story-007-first-art-pass-evidence.md` (new: the map of
  criteria to evidence, and the sign-off row).
- Verdict (story-done): NOT ASSESSED: 6 of 7 criteria covered; the seventh is the user's sign-off. Left open.
- Decisions made for you: the story goes to In Review rather than staying Ready, since its build work is done; it
  does not close without your sign-off.
- Not verified: the look on a real GPU and in the Unity Editor.

## Brainstorms: story, gear, skills (drafts for you to choose from)
- **Story** (`design/drafts/story-outline.md`): three directions (Bluebird's *Last Season* trimmed for skis first; a
  cosier **First Chair** season with no villain, where every act opens a lift and its first chair is the act's
  opening scene, ending at the season's **Last Chair**; a ski-bum road trip) and a recommended mix: B's spine, A's
  cast and tone, C as the post-game. An act-by-act outline, what it needs that doesn't exist yet, six decisions.
- **Gear** (`design/drafts/gear-progression.md`): Bluebird's six stats, archetype skis, or cosmetic-only skis;
  recommended archetypes with five bars (Edge, Glide, Float, Pop, Swing), each a real sim tuning value, eight skis
  that trade off rather than upgrade, functional gear that opens places, prestige cosmetics; five decisions.
- **Skill tree** (`design/drafts/skill-tree.md`): Bluebird's ranks, a tree of moves, or mastery by use; recommended:
  a tree whose nodes are mostly the brief's "next riding layer" (Stoke and Boost, Combos, Presses and Butters,
  Corks, Tweaks, Signature tricks), earned by riding in each branch's style; the tuned feel stays the floor; five
  decisions.
- Research behind them: GMA2, Shredders, Steep's Olympics campaign, Riders Republic, Lonely Mountains: Snow Riders,
  SSX (2012); sources are linked in each draft.
- Decisions made for you: none; every choice is listed as a decision for you at the end of each draft.
- Note: one bundled shell command (commit, push and reading a ride's log) was refused by auto mode; the same commit
  on its own went through. Commands are kept simpler since.

## story-018-deformable-snow (In Review)
- Changed: the snow mesh, its shaders, the terrain cut, the track map and ribbons, the skis' footprints and sink
  (35d91b0, 063f657, 29c86a5, before this run); each terrain's own materials (0d62120); the add pass only where slush
  lies (49c1146, this run); the As built section, criteria 1, 2, 3 and 5 ticked, the evidence folder (12 images, the
  frame times, `evidence.md`, a stamp).
- Tests: 25 EditMode tests for the mesh, the trench, the CPU depth and the slush blocks (268 Core tests pass locally);
  CI's EditMode job passes; `dotnet test sim/`: 435 passed, no sim file changed.
- Seen in the cloud builds: real trenches with raised rims in powder, the skis sunk to the boots; a 2 cm cut on the
  groomer; on ice no cut, the tracks tinted only; switched off, 0.1 levels of 255 from the build before; no line where
  the mesh meets the terrain.
- Frame time (software rendering): about 3 times the frame with the add pass always on, about 2.2 times with it only
  where slush lies.
- Decisions made for you: the add pass is left out where no slush lies (no visible change); the cloud rides now wait
  for counted frames, not seconds, so a slower build still pins the rider at the same sim time.
- Not verified: the Unity Editor; the look in motion at a real GPU's frame rate; 60 frames a second on your PC.

## Fix: nothing laid on the snow while grinding
- Changed: `RiderContact.OnSnow` (new, Core): not in the air, crashed, grinding a rail or box, or on the lift. The skier
  view's tracks (and with them story 018's footprints and trenches) and the spray use it; the placeholder rider view
  adds the grind check. Before, a grind counted as on the snow, so the ribbon (and now a trench) ran under the rail.
- Tests: 3 EditMode tests; the grinding one failed against the old rule first. 271 Core tests pass locally.
- Not verified: in a build on a rail (the cloud rides don't reach the park's rails yet) and in the editor.

## Brainstorms, more specific: the story's acts 1 to 4, and the gear measured in the sim
- **Story** (`design/drafts/story-outline.md`): the prologue and acts 1 to 4 mission by mission (38 missions), each
  with its giver, objectives, what it teaches and opens, sample lines in the cast's voices, and, from act 2 on, what it
  needs beyond today's riding (gates and timing, ghosts, bots or recorded runs, a run score, night lighting, weather
  and time of day changing the snow, surface lifts, combos, the replay editor, water). New options: Cass watched or
  ridden against in act 2; night riding kept or cut; the combo mission kept or moved; the act 3 burial as written,
  lighter or left out; the festival's results fixed or played for real.
- **Gear** (`design/drafts/gear-progression.md`): every ski's bars applied to the default tuning and the same
  scripted rides run in the real sim (a throwaway probe; nothing in the sim or the game changed). The first draft's 3
  to 5% a step moved the pace by under 2%, too little to feel, and two mappings were backwards (Float's turn factors
  made the powder ski slower through powder turns; Glide on powder let the race ski beat the rental there). The bars
  now act on measured values (Edge 15%, Glide 8%, Float 15%, Pop and Swing 4%): about 5% in a ski's best place and
  15% in the air. Two new decisions: how big the differences should be, and whether powder should slow a rider more
  so float matters.
- **Skill tree**: a note that its 2% ranks on grip and drag would not be felt either.
- Decisions made for you: none; each choice is listed in the drafts.
