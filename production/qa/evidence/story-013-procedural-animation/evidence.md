# Story 013 evidence: procedural animation on top of the key poses

Ridden in a cloud session on 2026-10-27 (Xvfb, Mesa llvmpipe software rendering, xdotool for the keys and the mouse,
the Linux player from `unity.yml` run 40510043401, commit a399708). That build carries the four layers (c875a57), the
lean that counts the frame's tilt to the slope (ee60523) and the lean that waits for speed (b90eccf). Its other changes,
the snow shader (938e312) and the mountain tool (a399708), don't touch the skier. EditMode tests 238/238; the Linux
and Windows players build. No Unity Editor has opened this project, and nobody has played these builds on the PC yet.

**How the cloud draws.** Each 1280x720 frame takes about 1.3 s on the cloud's four CPU cores (about 0.75 frames a
second, read from Mesa's HUD and counted with an X Damage frame counter), and the sim runs at most 12 steps (0.1 s)
a frame, so a ride runs in slow motion. The springs settle in a fraction of a second, so they settle between two
frames. The shots show where each layer settles, not how it moves.

**On and off.** The ride ran twice: every layer at its default ("on"), and every layer's weight at 0 through the
Feel Lab ("off": `leanWeight`, `springWeight`, `legWeight`, `skiPitchWeight`, `plantWeight`). The keys are held for
wall-clock seconds, so the two rides' lines differ a little.

| What | Result | Image |
|---|---|---|
| The first ride's bug and its fix | The first edge change, at 8 to 15 km/h. Top (ee60523, the lean layer alone): the skier lies almost flat on the snow, as in the first cloud ride of c875a57. Middle (ee60523, no layers): the clips and the frame's tilt alone, about 45 degrees. Bottom (a399708, every layer on): the same lean as the clips alone, because the extra lean now waits for speed (`TurnLean.SpeedFade`: none below about 22 km/h, all from about 50 km/h) | `2026-10-27-build-40510043401-lean-fix.png` |
| A slow and a fast carve | First column: a slow right turn at 5 to 7 km/h, the same with the layers on and off. Then 40 km/h: a right carve, and the edge change to the left, where with the layers on the skier leans further than with them off, by about 10 degrees by eye (`leanMaxExtraDegrees` 15, about 70 percent of it at 40 km/h) | `2026-10-27-build-40510043401-carves.png` |
| An edge change with a pole plant | 40 km/h. With the layers on, the new turn's inside (left) hand reaches forward and down for the plant (third frame), then swings back into the clip's pose; with them off, the arms keep the clip's pose. The pole's tip on the snow is hidden behind the body from the chase camera | `2026-10-27-build-40510043401-fast-edge-change.png` |
| A roller or kicker lip with both skis on the snow | Not shown. Over the test slope's rollers both rides keep the skis on the snow; the legs layer's correction (a few centimetres) can't be told apart from behind at the chase camera's distance, and the park's kicker wasn't reached (as in story 012's rides) | the PC |
| A landing with the hips compressing | Not shown. The compression peaks about 0.1 s after touchdown and is over within about half a second, between two cloud frames 1.3 s apart; the jump's frames show the air pose and then the landed stance | the PC |
| The layers' weights in the Feel Lab | The Effects tab lists `RiderViewSettings`' fields with their ranges (the shot shows the top of the list); each layer's weight and numbers follow further down, after the spray's. A shot with the list filtered to the weights didn't take (the player drops keys typed faster than its frames) | `2026-10-27-build-40510043401-feel-lab-effects.png` |
| With every weight at 0 | Each layer's change is multiplied by its weight, so at 0 the bones keep the clip blend; the "off" frames show the clips' own poses | the code, the "off" rows |
| No change to the sim, the camera or the clips | The story's commits touch only `Riding/Core` math, `SkierProceduralPose`, `SkierView`, `RiderViewSettings` and EditMode tests. `dotnet test sim/` passes (435, the replay determinism test among them) | the diff |

Notes:

- **The lean at speed.** At 40 km/h edge changes the clips and the frame's tilt already lean the skier 45 to 50
  degrees, and the layer adds about 10 more. Whether that reads as a carve or too far is the user's call on the PC;
  the Effects tab has `leanMaxExtraDegrees`, the clips' assumed lean and the two fade speeds.
- **What the cloud can't show.** Motion: the springs' swing and settle, the pole's timing, the landing. Those need
  the game at its real frame rate, on the PC.
- `stamp.txt` hashes the story's code and tests as they were in a399708.
