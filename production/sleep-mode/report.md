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
