# Story 003 evidence: Unity half, before the PC playtest

Built and run on GitHub Actions, then run again in a cloud session. No Unity Editor has opened this project yet,
and nobody has played it on the user's PC. Acceptance criterion 8 (a Windows playtest top to bottom) is still open.

| What | Result | Source |
|---|---|---|
| Unity compiles every script (6000.3.27f1, URP 17.3, Input System 1.20.1) | Pass | `unity.yml` run 39854601931 |
| EditMode tests, including TerrainParityTests and RideReplayTests | 34/34 pass | same run, `editmode-test-results` artifact |
| Linux and Windows players build | Pass | same run, `FirstChair-StandaloneLinux64` and `-Windows64` artifacts |
| The follow camera frames the rider on the slope | Seen | `01-follow-camera-ci-linux.png`, captured by `ScreenshotOnArg` on the runner (Xvfb, Mesa llvmpipe) |
| Keyboard steering drives the sim; the rider leans into turns | Seen | `02-turning-right-cloud-linux.png` (D held 6 s), `03-turning-left-cloud-linux.png` (A held 6 s), the Linux player run under Xvfb and twm with xdotool in a cloud session |
| Gamepad input | Not verified | needs the user's PC |
| A Windows playtest from top to bottom | Not done | needs the user's PC |

Software rendering is slow, so the player logs "frames ran over the cap of 12 steps". The sim drops time rather than
jumping; a real GPU should not hit the cap.

Bugs found by these runs and fixed:
- The camera never moved, because its settings reference was null.
- The skier was magenta, because the primitives had no URP material.
- The lean was 60 degrees, which read as a fall.

See commits ccd4f5b, 757dced and 442cdb6.

`stamp.txt` holds the content hashes of the files these screenshots prove.

## 2026-10-24: the user's 2026-10-23 playtest fixes, seen in a build

Build `unity.yml` run 40055981760 (commit 999c2a5): EditMode tests, the Linux player and the Windows player all
pass. I rode the Linux player under Xvfb and twm with xdotool, at 1280x720 and at 3840x2160 to get enough pixels on
the rider. These are not verified in the Unity Editor, and the feel is still the user's call.

| What | Result | Source |
|---|---|---|
| Skis lie flat under the boots and point down the line (they stood vertical before) | Seen | `2026-10-24-build-40055981760-straight-skis-poles-4k-crop.png` |
| Poles hang from the hands with the baskets at the snow, trailing a little (they pointed backward and up before) | Seen | same, and the carve crops |
| In a left carve the skis follow the curving tracks on edge and the body leans in, inside the feet | Seen | `2026-10-24-build-40055981760-left-carve-4k-crops.png` (A held from a straight run; 0.5 s apart) |
| Narrower lens; rider near the bottom of the screen, so more slope ahead is visible | Seen | `2026-10-24-build-40055981760-follow-camera.png` (captured on the runner) |
| Tree lines along the runs, with their shadows | Seen | `2026-10-24-build-40055981760-tree-line.png` (the line between the groomer and the park) |
| Speed (40-60 km/h on key S-turns) | Not measurable here | Software rendering runs the game slower than real time, so the HUD's km/h after a given wall time is low. The sim test `KeyboardSTurnsDownGroomer_FiftyDegreesEachWay_HoldFortyToSixtyKmh` pins it instead. |

Flips and grab poses (commits a0eae79 and db9337b) came after this build. Run 40056335843 builds them.
