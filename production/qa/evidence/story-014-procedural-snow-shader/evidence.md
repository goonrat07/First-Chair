# Story 014 evidence: a procedural snow shader

The build is `unity.yml` run 40522231518 (commit cadd45d: the shader of 938e312 with the reworked glints and a gentler
default wrap): EditMode 238/238, the Linux and Windows players build. "Before" is run 40474108714 (commit ee60523, the
stock `Universal Render Pipeline/Terrain/Lit`). Both were ridden under Xvfb with Mesa's software renderer (llvmpipe,
4 cores) at 1280 x 720, which draws about 0.7 frames a second, so rides run in slow motion. Pinned views: the Feel
Lab's gravity at 0.001 and its Load saved restart the rider where it stands, so each variant draws the same frame
(only the lift's chairs move between shots). No Unity Editor has opened this project.

| What | Result | Evidence |
|---|---|---|
| The shaders build | `First Chair/Snow Terrain` and its add pass compile 64 ForwardLit variants each for glcore and vulkan in the Linux build, with no shader error in the log; the Windows player builds. The import-time "All subshaders removed" warnings come from the editor's `-nographics` and hit URP's own terrain base pass too. The terrain is not magenta in any shot | the run's log; every image here |
| With every weight at 0, the stock look | At the test slope's pinned start, cadd45d with the four snow weights at 0 against ee60523's stock shader: 866 pixels differ, all but 4 of them on the skier (its pose differs between the builds); the snow's colours match to a tenth of a level (shade 153.8/162.5/180.1 against 153.9/162.5/180.2) | `2026-10-27-build-40474108714-slope-stock.png`, `2026-10-27-build-40522231518-slope-weights-0.png`, `-slope-defaults.png` |
| Glints in sunlit snow, none in shade | At the defaults (strength 6), crisp white specks of one or two pixels across the lit corduroy, beside the soft flecks baked into the snow texture: 250 of the 478,448 lit snow pixels brighten by more than 60 levels (0.05 percent). At strength 18, 421, in the same cells. In the skier's shadow, 0 of 4,375 | `2026-10-27-build-40522231518-glint-crops.png` (a 240 x 135 patch at 3x: off, 6, 18, all on) |
| Shade bluer | The skier's shadow on the snow: blue minus red 26.3 with the stock shader, 31.6 with the defaults (shade 0.6) | `2026-10-27-slope-before-after.png` |
| A terminator softer | The corduroy's shaded sides lift more than its lit sides with the soft light alone (wrap 0.15): the 5th percentile of a patch beside the skier from 195 to 206, the 95th from 219 to 226, its contrast (standard deviation) 18 percent lower | `2026-10-27-build-40522231518-slope-wrap-crop.png` |
| Powder with ripples | Ridden on the test mountain into the powder between the blue and the red (the sim says where a rider who lets go is in powder: 6.5 to 13.5 s from the start) and pinned there at 68 km/h. At the defaults (0.25, 0.5 m, fading by 30 m) the ripples are faint: the snow changes by 5 levels near the camera, falling to none past 30 m, and none on the skier; the difference, amplified 12 times, shows long wandering crests across the wind. The demo (1, 3 m, fading by 200 m) shows them plainly. In a first ride pinned on the blue's groomer beside the powder, the demo's ripples covered the powder and left the corduroy as it was (powder only, as designed). Repeating "off" at the end changed 0.2 levels, so the pinned frame held still | `2026-10-27-build-40522231518-powder-weights-0.png`, `-powder-defaults.png`, `-powder-ripple-crops.png` (near powder at 2x), `-powder-ripple-difference-x12.png`, `-groomer-beside-powder-ripples-demo.png` |
| Each effect has a weight on `LightingSettings`, in the Light tab | The Light tab filtered to "snow": glint strength, cell size, smallest cell in pixels, sharpness, facet spread, fade distance, one glint amount per snow type, shade strength, wrap, ripple strength, wavelength, wind heading and fade distance | `2026-10-27-build-40522231518-feel-lab-light.png` |
| Frame time | The same pinned view, 60 s each, alternating builds: ee60523 680.3 ms an update, cadd45d 695.7 ms, 2.3 percent slower (the AC allows 10). The first build (938e312) measured the same within noise | `2026-10-27-frame-times-cadd45d.md`, `2026-10-27-frame-times-938e312.md` |
| No change to the sim, the camera or the tracks | Story 014's commits change only `Assets/Shaders/SnowTerrain/`, `LightingSettings`, `LightingRig` and the scene builder's terrain material | the diff |

Not seen in the cloud: the glints twinkling as the camera moves (each frame is a second and a half apart), and the
look at a real GPU's frame rate. Both are for the user's look review on the PC.
