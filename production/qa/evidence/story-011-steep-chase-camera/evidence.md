# Story 011 evidence: a Steep-style chase camera

Ridden in cloud sessions on 2026-10-26 (Xvfb, Mesa llvmpipe software rendering, xdotool for the keys and the mouse,
the Linux player from each `unity.yml` run). No Unity Editor has opened this project, and nobody has played these
builds on the PC yet.

## Earlier builds (before the final numbers)

| Build | What it showed | Image |
|---|---|---|
| Run 40406647164 (commit 50c6358), the first chase build | Follows the travel through carves and traverses and never dips into the snow, but too wide and too level: the slope's far end at mid-screen, the rider about 20 percent of the height | `2026-10-26-build-40406647164-first-build-rest.png` |
| Run 40410870423 (commit 87ab14a), the framing retune | At rest the framing matches the model: the rider 52 to 81 percent of the height, the slope's far end 39 percent down | `2026-10-26-build-40410870423-retuned-rest.png` |
| The same build, in a fast tuck | Left: the rider shrank (30 percent of the height at 127 km/h) and the far end sank to mid-screen; the blur, at the cloud's 7 frames a second, was six times a 60 fps PC's, with a halo of the rider's colours. Right: the new tuck numbers and the 60 fps blur strength, loaded live through the Feel Lab: the rider 41 percent of the height, the far end 31 percent down, the rider sharp | `2026-10-26-build-40410870423-fast-tuck-before-after.png` |

Notes:
- The cloud sim runs well below real time (each frame is capped at 12 sim steps), so the fast-tuck rides load gravity
  times 4 through the Feel Lab to reach race speed. The camera reads only the speed, the tuck and the snow, so its
  framing at a given speed is the same; only its bank in turns, set per g, reads smaller.

## The final build

Pending: the build with the new tuck numbers and the frame-rate-independent blur.
