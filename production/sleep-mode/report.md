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
