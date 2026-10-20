# Sleep mode plan

Started: 2026-10-20T11:05:45Z · Until: none · Branch: claude/beautiful-gates-8blqmk · Start commit: 5268986

| # | Item | Skill | Status | Commits |
|---|------|-------|--------|---------|
| 1 | story-001-sim-core-replay | story-done | done | bb0e1c0 |
| 2 | story-002-skiing-on-snow | dev-story | running | |
| 3 | story-002-skiing-on-snow | story-done | queued | |
| 4 | story-003-test-slope-unity-view, sim half only: slope generator, snow-type map, feature data (kickers, trees), sim-side height checks | dev-story | queued | |
| 5 | story-004-air-landings-crashes (needs item 4's kicker data; goes ahead of 003's Unity half) | dev-story | queued | |
| 6 | story-004-air-landings-crashes | story-done | queued | |
| 7 | story-005-trees-and-flow (needs item 4's tree data and story 004) | dev-story | queued | |
| 8 | story-005-trees-and-flow | story-done | queued | |
| 9 | story-006-rails-and-trick-scoring | dev-story | queued | |
| 10 | story-006-rails-and-trick-scoring | story-done | queued | |

Story 003 stays In Progress after item 4: its Unity half is left for the PC, so it gets
no `/story-done` here.

## Left for the PC
- story-003-test-slope-unity-view, Unity half: Unity Terrain from the heightmap (1 cm
  height check), 120 Hz stepping with interpolation, placeholder skier, GMA2-style
  follow camera, keyboard and gamepad input, record and replay in Unity, and the Windows
  playtest with a retained screenshot.
- story-007-first-art-pass: Visual/Feel, needs the Unity Editor, screenshots and sign-off.
- Feel tuning for stories 002, 004 and 005: their tests pin behaviour, not final numbers
  (story 002: "Tune by playtest once Story 003 makes the rider visible").
