# Sleep mode plan

Started: 2026-10-27T02:58:57Z · Until: none · Branch: claude/beautiful-gates-8blqmk · Start commit: 0d2953f4ba57a32baa5020e3b65714971aaf9017

The user (2026-10-27, starting this run): no stop time; carry on after usage resets, the weekly one too, and don't stop
for a weekly usage warning; continue what was in progress, then the next thing, and when the queue runs out keep
working on something else; stop only at the user's next message. Named work: procedural animation, custom snow
shaders with procedural aspects, and the mountain design tool. So the skill's defaults change for this run: the
Visual/Feel items below are queued (built, checked in CI and ridden here; their sign-off and the Unity editor check
stay the user's), an empty queue is refilled from the game brief instead of ending the run, and a wake-up is
scheduled after the weekly reset as well.

| # | Item | Skill | Status | Commits |
|---|------|-------|--------|---------|
| 1 | story-012-skier-detail-pass: ski bindings, pole grips and baskets; the player always drawn at LOD0; CI build, cloud ride, evidence | dev-story, then story-done | done (In Review) | 0d2953f, 729473d, 310c30e, f70a125 |
| 2 | story-013-procedural-animation (new): leg IK on the snow, lean and angulation, springs and follow-through, pole plants, layered over the 16 key poses | create the story, then dev-story | done (In Review) | 361ea91, bf20bd7, c875a57, ee60523, b90eccf, 76df2f5 |
| 3 | story-014-procedural-snow-shader (new): sparkle, blue-tinted shade, ski trails read as bump and detail, procedural surface detail; covers story 007's two open snow criteria | create the story, then dev-story | done (In Review) | 38cefda, 938e312, cadd45d, f3347e0, and this evidence commit |
| 4 | story-015-mountain-design-tool (new): a mountain data format and scripts that build the terrain, snow map, runs, trees and features from it (the game brief's "mountain-making tool"), for Claude now and the user later | create the story, then dev-story | done (In Review) | 6d02c24, a399708, and this evidence commit |
| 5 | story-016-varied-forest (new): the brief's next item, procedural trees: shape variants from the tree kit (slim fir, broad spruce, young tree, snag) chosen per tree by a hash in the view | create the story, then dev-story | done (In Review) | 76df2f5, 55ad865, 81380ac, 0481e8e, and this evidence commit |
| 6 | story-017-ski-grooves (new): story 007's last unbuilt criterion and the user's 2026-10-24 note ("tracks should build up where skiers pass, as in GMA2"): the track ribbons stamped into a render texture ahead of the camera, read by the snow shader as grooves | create the story, then dev-story | running | |

## Left for the PC
- story-008, 009, 010 and 011: In Review, waiting on the user's playtest and sign-off.
- story-003-test-slope-unity-view: its last criterion is a playtest on the user's PC (Windows build, gamepad).
- Every Visual/Feel item above ends In Review: the look sign-off and the Unity editor check are the user's.
