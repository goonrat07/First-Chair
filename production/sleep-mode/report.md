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
