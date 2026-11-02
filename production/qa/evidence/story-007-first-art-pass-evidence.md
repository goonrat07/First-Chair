# Story 007 evidence: first art pass toward the target look

Story 007 is the game brief's art item ("First real art toward the target look: skier, skis, poles, trees, snow trails
and lighting"). Its criteria were met piece by piece from 2026-10-24 to 2026-11-02, in this story's own sleep-mode
work and in stories 012 to 018, each of which has its own evidence record. This file maps each criterion to that
evidence and holds the sign-off. Everything was built in cloud sessions and seen in cloud builds (Xvfb, Mesa's software
renderer); no Unity Editor has opened the project, so every Unity-side piece is not verified in the editor.

| Criterion | Met by | Evidence |
|---|---|---|
| A rigged CH_Skier within 5k to 10k tris at LOD0, wearing PR_Ski and PR_SkiPole on its sockets | Story 012's detail pass and the user's rounder pass (2026-11-02): 5,897 / 4,673 / 2,523 tris at LOD0 / 1 / 2 with helmet and boots (body 4,801); 20 bones, 4 sockets; gates and the seat check pass | `art/source/CH_Skier/asset-brief.md` (Budget, Gate results); `story-012-skier-detail-pass/evidence.md` and its images |
| The pose driven from sim state, with clips for tuck, grabs and crashes, never fed back into the sim | 16 clips blended from sim state (`SkierPoseWeights`); the five grabs, spins and flips here (2026-10-24); story 013's lean, springs, legs on the snow and pole plants | `story-007-first-art-pass/2026-10-24-build-40056930596-*.png`, `2026-10-24-build-40117608164-japan-and-tail-in-the-air.png`; `story-013-procedural-animation/evidence.md` |
| Glade conifers from a tree skill: 300 to 1,500 tris, a billboard, GPU instancing | The tree kit and EN_Conifer; its billboard LOD (IoU 0.993); instancing on every imported environment material; story 016's four variants | `story-007-first-art-pass/2026-10-24-tree-billboard-atlas-on-snow-blue.png`, `2026-10-24-build-40120266835-west-tree-line-and-forest.png`; `story-016-varied-forest/evidence.md` |
| Snow trails: a render texture stamped where the skis touch, read by a URP snow shader; display only | Story 017's stamped map (folded into 018) and story 018's snow mesh pushed down by it: trenches with raised rims, as deep as each snow type sets, the skis sunk into them | `story-018-deformable-snow/` (its evidence record); the ribbons that stay: `story-007-first-art-pass/2026-10-26-build-40278892710-tracks-stay-across-runs.png` |
| A bluebird-day lighting preset: soft shadows, blue-tinted shadows on snow, sparkle | `LightingSettings` and the lighting pass (2026-10-26); story 014's blue shade, soft terminator, glints and ripples | `story-007-first-art-pass/2026-10-26-build-40281825872-lighting-before-after.png`, `2026-10-24-build-40060424205-snow-sparkle-4k-crop.png`; `story-014-procedural-snow-shader/evidence.md` |
| Equipment colours that read on snow | Graphite pole shafts (8.5:1 against sunlit snow; the old grey was 1.1:1), red grips and baskets | `story-007-first-art-pass/2026-10-24-build-40058662563-graphite-poles.png` |
| Retained screenshots from the follow camera, and sign-off | The screenshots above; the sign-off below | this file |

Not seen in the cloud: the look at a real GPU's frame rate, in motion, and in the Unity Editor. Those, and the look
itself, are the user's to judge on the PC.

## Sign-off

- [ ] Approved (the user, as art and creative lead): the skier, trees, snow trails and lighting are the first step
  toward the target look, and story 007 can close.
