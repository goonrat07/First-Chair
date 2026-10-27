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

## The final build: run 40423919050 (commit a5091fb)

EditMode tests 181/181 (the new frame-time and side-slide tests among them); the Linux and Windows players build.
Ridden on 2026-10-27. The fast tuck loads gravity times 4 through the Feel Lab and keeps the build's own camera
numbers; every other ride is at normal gravity. The stamp (`stamp.txt`) hashes the story's code and tests at a5091fb.

| What | Result | Image |
|---|---|---|
| At rest on the test slope | Straight behind (the rider centred, 0.0 percent off the middle), the rider 51 to 86 percent of the height, the slope's far end about 39 percent down | `2026-10-27-build-40423919050-slope-rest-carves-traverse.png` |
| Carving both ways and a traverse, 45 to 49 km/h | The camera follows the travel; on the traverse the slope tilts away to one side | the same |
| A fast tuck, 49 to 162 km/h | Closer and lower: the rider 35 to 46 percent of the height (34 at rest) and 3 to 6 percent of the width left of the middle, over the right shoulder as asked, the skis angling in from the lower left as in the Steep shot; the slope's far end about 29 percent down at 139 km/h; from about 100 km/h the edges streak, the rider sharp, no halo. The build before the slide (110f0c2) put the rider 0.0 to 0.2 percent off the middle in the same ride | `2026-10-27-build-40423919050-fast-tuck.png` |
| The test mountain | At rest and tucking beside the lift: the lift line overhead, the run sign, the hills behind | `2026-10-27-build-40423919050-mountain.png` |
| Off a kicker | The park's first kicker: up its left side and off it, in the air at 48 to 52 km/h (the shadow apart from the skis), the camera behind and level, rising with the rider; landed at 53 km/h for 52 points | `2026-10-27-build-40423919050-park-kicker.png` |
| Over a rollover into steeper snow | Pinned near the mountain's top (gravity 0.001 through the Feel Lab; 16 degrees under the rider, steeper below), the tilt off and then on: with it on, the view drops about 2 degrees (the helmet from 51 to 47 percent down) and shows more of the run ahead | `2026-10-27-build-40423919050-rollover-tilt-ab.png` |
| The Feel Lab's Camera tab | "Steep chase (default)" at the top and every number, the new ones among them (the tuck 2.4 m back and 1.8 m up, the side slides 0.15 and 0.4, the look-ahead fixed at 1.5, the rollover tilt 0.6); the old presets are gone | `2026-10-27-build-40423919050-feel-lab-camera-tab.png` |
| The camera never under the snow, the rider never hidden | Seen in every frame of these rides (the slope, the fast tuck, the mountain and three park runs) | all |
| The feel: how the close-in, the slide, the blur and the bank read in play | Not judged | the user, on the PC |

Notes:
- A park run's line depends on the frame rate, because the keys are held for wall-clock seconds while the sim drops
  steps on a slow machine; the camera only changes the render cost. Two runs of this build at the same timing took
  the same line, onto the first kicker's left side wall (0 points); a third, letting go of the tuck 15 s earlier, went
  off it (above). The build before the slide (110f0c2) jumped the same kicker cleanly, also for 52 points.
