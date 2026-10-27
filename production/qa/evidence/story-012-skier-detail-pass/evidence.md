# Story 012 evidence: a skier detail pass for the chase camera

Ridden in a cloud session on 2026-10-27 (Xvfb, Mesa llvmpipe software rendering, xdotool for the keys and the mouse,
the Linux player from each `unity.yml` run). No Unity Editor has opened this project, and nobody has played these
builds on the PC yet. The Blender-side gates (silhouettes, the seat check, budgets, round trips) are in
`art/source/CH_Skier/review/`, `art/source/PR_Ski/review/` and `art/source/PR_SkiPole/review/`; the asset brief lists
them.

**Before:** run 40423919050 (commit a5091fb), the last build before this story. **After:** run 40443808319 (commit
bf20bd7), which carries the new skier (0d2953f), the bindings, grips and baskets (729473d) and the full-detail lock
(310c30e). Its one other change, story 013's math, only adds engine-free code and tests that nothing in the build
calls yet. EditMode tests 224/224; the Linux and Windows players build.

Each image sets the two builds side by side, cropped from 1280x720 frames of the same ride. A ride's line differs a
little between builds, because the keys are held for wall-clock seconds while the cloud's slow sim drops steps.

| What | Result | Image |
|---|---|---|
| At rest, carving both ways and a traverse, on the test slope | Before: navy patches of the trouser seat show through the back of the jacket in every frame. After: none. The navy yoke stripe and the drawcord line over the hem read, and so do the helmet's vents and goggle strap, the cargo pockets on the trousers and the red gauntlets over the cuffs. The yellow, navy and red, the build and the proportions are the old skier's | `2026-10-27-build-40443808319-rest-carve-before-after.png` |
| A fast tuck, 21 to 162 km/h | Gravity times 4 through the Feel Lab, as for story 011. Before: the seat shows in every frame of the tuck. After: none, and the stripes still read at race speed, the rider 35 to 46 percent of the screen's height | `2026-10-27-build-40443808319-fast-tuck-before-after.png` |
| In the air with a grab | Space held and let go to jump off the test slope's snow, with Shift held for the grab: a Mute, a Tail (with S) and a Tip (with W). Before: the seat shows in all three grabs. After: none; the jacket's stripes and the gauntlets read with the arms out and the knees up | `2026-10-27-build-40443808319-air-grabs-before-after.png` |
| The skis' bindings | Three times the pixels. Before: a plain grey block under each heel. After: each heel piece behind its boot, with the light-grey step-in lever and a brake arm standing out at either edge. The toe pieces sit under the boot toes, out of sight from behind | `2026-10-27-build-40443808319-skis-before-after.png` |
| The full-detail mesh | Every ride's player log: `SkierView: CH_Skier LODGroup on CH_Skier, switch heights 0.25, 0.125, 0.01; forced to LOD0.` Unity's automatic LOD group would show LOD1 once the rider is under 25 percent of the screen's height. The chase camera keeps the rider at 34 to 46 percent, so the lock matters for wider views | the logs |
| Riding and the camera | Unchanged: from a5091fb to bf20bd7 no sim, camera or riding script changes; `SkierView` gains only the LOD lock | the diff |
| Whether the style and palette match and the new details read | Claude's read of these frames: they do. The sign-off is the user's, on the PC | the user |

Notes:

- **The park ride.** The park ride (tucking down the kicker lane, then holding the grab after the kicker) ran on both
  builds, and neither run left the kicker cleanly: the before run never reached it, and the after run rode up its
  side wall. So the in-air grabs above are jumps off flat snow.
- **Found in passing: the ski tracks drawn over the ski tails.** In both builds, once the rider moves, the track
  ribbon shows over the back half of each ski: the pinkish band on the tails in the skis image, in the tuck and carve
  columns. This story didn't cause it, since the before build shows it too. The ribbon sits 15 mm off the snow
  (`trackLift`) and was laid from under the boot, but the ski's top is only 17 mm up, so the slightest tilt puts the
  ribbon over the ski's back half. Fixed separately: tracks now start at the ski's trailing end (the tail riding
  forward, the tip riding switch), seen in story 013's build.
- `stamp.txt` hashes this story's art sources, gate scripts and exports as they were in bf20bd7. `SkierView.cs`
  isn't in it, because story 013 has changed it since.
