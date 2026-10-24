# Sleep mode plan

Started: 2026-10-24T02:38:00Z · Until: none · Branch: claude/beautiful-gates-8blqmk · Start commit: 0f146e0

The user chose tonight's items on 2026-10-24 (story 003 "Decisions", story 007 "Next"). Unity now builds on GitHub
Actions (`unity.yml`; the repo is public, so minutes are free), and each Linux build is ridden in this session under
Xvfb with scripted keys, so Unity-side items can run here. They are still reported as **not verified in the Unity
editor**, and feel stays the user's call. Each item follows `/dev-story`'s method (tests first where there is logic,
commit after every step); none of them closes a story, so there is no `/story-done` tonight.

| # | Item | Skill | Status | Commits |
|---|------|-------|--------|---------|
| 1 | Story 003 playtest fixes: ride build 40055981760 (40054775510 and 40055604010 were cancelled by later pushes), confirm skis, poles and the lower, narrower camera | dev-story (verify) | done | 82d24de (CI queues instead of cancelling), (this commit) |
| 2 | Speed: retune story 002's numbers toward a GMA2-like pace (40-60 km/h on the groomer), tests show before and after, sim version bump | dev-story | done | (this commit) |
| 3 | Tree lines along the groomer and park edges in `make_test_slope.py` (real, collidable trees in the sim layout) | dev-story | done | (this commit) |
| 4 | Show tricks: rotate the skier by the sim's spin and flip angles in the air | dev-story | done | (this commit) |
| 5 | Grab poses for CH_Skier (Mute, Tip, Tail, Japan, Safety) and the view blending them in the air | dev-story | done | (this commit) |
| 6 | Old ski tracks over the groomers and powder (display only) | dev-story | done | (this commit) |
| 7 | Camera drift: let the rider move a little side to side before the camera follows | dev-story | done | (this commit) |
| 8 | Story 007: equipment colours that read on snow, GPU instancing on the tree | dev-story | done | (this commit) |
| 9 | Story 007: tree billboard (far LOD) | dev-story | queued | |
| 10 | Story 007: stamped snow trails with a URP snow shader, blue shading and sparkle | dev-story | queued | |

## Left for the PC
- Story 003: the gamepad check, and the user's own playtest of each new build (feel, look, speed).
- Story 007: sign-off on the look.
- Feel tuning beyond item 2: the user judges the new speed and says what to change.
