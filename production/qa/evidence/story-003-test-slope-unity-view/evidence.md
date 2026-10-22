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
