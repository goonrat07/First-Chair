# Sleep mode plan

Started: 2026-11-02T15:46:48Z · Until: none · Branch: claude/beautiful-gates-8blqmk · Start commit: 0d62120799f4dc4195bb143dc3808d52179e410e
Finished: 2026-11-02T21:26:38Z · Reason: done

The user (2026-11-02, starting this run): "keep working on stuff until I next send a message. If you run out of stuff
to work on, start brainstorming a more specific outline of the story and or gear progression and or skill tree." So,
as in the last run: no stop time, carry on after usage resets, and stop only at the user's next message. When the
work below runs out, the brainstorms come next (drafts with options for the user to choose from, not decisions), and
after them the queue is refilled from the game brief instead of ending the run. Visual/Feel items are built, checked
in CI and ridden here; their sign-off and the Unity editor check stay the user's.

| # | Item | Skill | Status | Commits |
|---|------|-------|--------|---------|
| 1 | story-018-deformable-snow: ride the fixed build (CI run for 0d62120) on the test slope (groomer, ice, the mesh's edge, switched off against the build before), record the frame times, evidence, As built, criteria | story-done | done (In Review; the look sign-off and the 60 fps check are the user's) | 951fd58, (this commit) |
| 2 | story-007-first-art-pass: map each criterion to the story and evidence that met it (stories 012 to 018), for the user's sign-off | story-done | done (In Review; sign-off is the user's) | (this commit) |
| 3 | Brainstorm: a more specific story outline for First Chair (from the Bluebird spec's Career and cast, and research), with options | (design draft) | done: `design/drafts/story-outline.md` | 4b91dbf |
| 4 | Brainstorm: gear progression (skis, boots, poles, clothing; what gear changes in the sim and what is cosmetic), with options | (design draft) | done: `design/drafts/gear-progression.md` | 4686ed9 |
| 5 | Brainstorm: a skill tree (abilities, points, respec; what goes through the sim), with options | (design draft) | done: `design/drafts/skill-tree.md` | 707cb35 |
| 6 | Refill (story 018 follow-up): the snow mesh skips its slush add pass where no slush lies under it, which changes nothing on screen; frame time measured against the build before | dev-story | done: about 11% of the frame saved in software rendering where no slush lies | 49c1146 |
| 7 | Refill: a PC playtest checklist for the twelve stories that wait on the user (003, 007 to 016, 018): where to get the build, what to ride and look for, which Feel Lab knobs, what each answers | (doc) | done: `production/qa/playtest-checklist-2026-11-02.md` | (this commit) |
| 8 | Refill (fix): no tracks, footprints, trenches or spray while grinding a rail or box (the views counted a grind as on the snow) | dev-story | done | (this commit) |
| 9 | Brainstorm, more specific: the story draft's prologue and act 1, mission by mission | (design draft) | done | 3ee15b8 |
| 10 | Proposal: rocks and props (from the brief's ideas); it changes the mountain format and the sim, so it waits on the user | (question) | skipped: question in `questions.md` | |
| 11 | Refill (fix, story 018): the skis were thin and half buried on the groomer (Pine Bowl, build 49c1146); they now ride on top of a cut 2 cm deep or less and sink by the rest of a deeper one | dev-story | done: ridden in the CI build (Pine Bowl and the test slope: skis full width; powder: still sunk) | b92d0be, (this commit) |
| 12 | Brainstorm, more specific: the gear draft's skis measured in the sim (each bar alone and the eight skis), and the per-step shares fixed so a ski is felt | (design draft) | done | 04f4af9 |
| 13 | Brainstorm, more specific: the story draft's act 2, mission by mission, with what each mission needs beyond today's riding | (design draft) | done | 99a9556 |
| 14 | Brainstorm, more specific: the story draft's acts 3 and 4, mission by mission | (design draft) | done | 6a6fb0c |
| 15 | Brainstorm, more specific: the skill tree's ranks measured in the sim at their top rank (2, 5 and 8% a step) | (design draft) | done | 335c3d0 |
| 16 | Brainstorm, more specific: how missions read the riding (objectives from the sim's own events, spots and gates on the mountain, act 1 as data, tests from recordings) | (design draft) | done | 770c9b7 |
| 17 | Brainstorm, more specific: the three drafts on one clock (what a player has, act by act) | (design draft) | done | (this commit) |

## Left for the PC
- story-008, 009, 010, 011, 012, 013, 014, 015, 016: In Review, waiting on the user's playtest and sign-off.
- story-003-test-slope-unity-view: its last criterion is a playtest on the user's PC (Windows build, gamepad).
- story-018: the look sign-off, the Unity editor check and the 60 frames a second check on the user's PC.
