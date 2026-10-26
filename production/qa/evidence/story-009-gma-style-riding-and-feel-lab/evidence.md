# Story 009 evidence: GMA-style riding, the Feel Lab and speed cues

Build `unity.yml` run 40256892551 (commit c838bb8): EditMode tests, the Linux player and the Windows player all pass.
The Linux player was ridden in a cloud session on 2026-10-26 (Xvfb, Mesa llvmpipe software rendering, xdotool for
the keys and the mouse). No Unity Editor has opened this project, and nobody has played this build on the PC yet.

| What | Result | Source |
|---|---|---|
| Unity compiles every script; EditMode tests pass | Pass | run 40256892551 |
| At speed the snow moves | At 53 km/h, between two frames 0.25 s apart, the snow (rows 35 to 95% of the frame) moved a median 0.76 px, p90 2.28 px; 67% of it has texture to track. Before (build 40122603687, 86 km/h): 0.09 px, 2 to 4% trackable | `2026-10-26-build-40256892551-snow-moving-at-53kmh.png` |
| Plain carves, a drift (Shift), a quick turn (Ctrl) and a hockey stop (S+D) in the build | Seen: spray off hard carves, drifts and quick turns; the skis thrown across in the hockey stop | `2026-10-26-build-40256892551-carve-drift-quick-turn-hockey-stop.png` |
| The Feel Lab opens with backtick; Riding and Camera tabs; the chase preset | Seen | `2026-10-26-build-40256892551-feel-lab-tabs-and-chase-preset.png` |
| A riding number edited, applied and copied out | "+" three times on Groomed carve grip (20 to 26.62, x1.1 a press), Apply: restart here (the run restarted at rest), Copy values; the clipboard held `Tuning.GroomedCarveGrip = 26.62` | `2026-10-26-build-40256892551-feel-lab-nudge-apply-copy.png` |
| An effects number edited live | Spray carve edge 0.6 to 0.66 | `2026-10-26-build-40256892551-feel-lab-effects-tab.png` |
| Gamepad (left shoulder quick-turns) | Not verified | needs the user's PC |
| The feel | Not judged | the user's playtest |

Notes:
- Software rendering runs the sim slower than real time, so the flow is smaller than it would be on a PC. The numbers
  compare two cloud runs of the same kind, before and after.
- A key tapped and released within one frame is missed at the cloud's low frame rate: the Feel Lab reads backtick with
  `wasPressedThisFrame`. Held for 0.7 s it opens every time. At a PC's frame rate a normal press spans frames.
- Since this build, `SkierView.cs` and `TestSlopeLook.cs` changed (ski tracks that stay, commit 716391a), so
  `stamp.txt` reports them changed. The turn frames show this build's tracks.

`stamp.txt` holds the content hashes, at commit c838bb8, of the files these screenshots prove.

## Sign-off (Visual/Feel)

- [ ] Approved: plain carves, drifts, quick turns and hockey stops feel right on the keyboard (the user, on the PC)
- [ ] Approved: speed reads at the follow camera (the user)
