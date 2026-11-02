# Story 018 evidence: deformable snow near the rider

Built in a cloud session on 2026-11-02 and ridden in the cloud builds under Xvfb with Mesa's software renderer, which
draws a frame in 4 to 8 seconds with the mesh on, so the views are pinned (the Feel Lab's gravity at 0.001 and 'Load
saved', which restarts the run in place) after riding down for a counted number of frames (each frame is 0.1 s of sim;
the shots taken just before the pin show the rider moving). Builds: 063f657 (CI run 41374948805, the first), 29c86a5
(41375908154: the footprints and the sink), 0d62120 (41378281068: each terrain's own materials), 49c1146
(41477452195: the add pass only where slush lies), and c557d50 (41372681433, before the story). No Unity Editor has
opened the project, so nothing here is verified in the editor.

| Criterion | Result | Evidence |
|---|---|---|
| Layout, terrain height and profile are plain C# with EditMode tests | `SnowSkinLayoutTests` (7: plain grid, shared inner edges, no T-junctions, faces up, story size, snapping, bad sizes), `SnowTrenchTests` (12: the offset in and beside a trench, crossings, Gaussian taps, the CPU depth from splat weights), `SplatLayerBlocksTests` (6); the depth and slush-block tests seen failing against stubs first, the layout's caught by
mutations (stitching off, winding reversed), the slush blocks' edge test by a mutation with no texel slack; the Core
tests under NUnit: 268 passed; CI's EditMode job passed on every run after the snap test's fix (063f657) | the test files under `Assets/Tests/EditMode/` |
| CI builds for Windows and Linux with no shader errors; nothing magenta | Runs 41374948805 and 41477452195: "First Chair/Snow Skin" (8 vertex and 32 fragment variants on d3d11), its add pass, "First Chair/Track Ribbon" and "Hidden/First Chair/Track Groove Blur" compile for d3d11, glcore and vulkan with no shader error in either player's log; no magenta in any ride | the runs' build logs |
| In powder: a trench with raised snow and its own shadow; the skis sunk | Two trenches behind the rider with lit walls, raised rims and a shaded floor; the trench runs on under the skis, which are sunk in it to the boots; at double strength deeper still. Moving at 60 km/h the trenches run unbroken from the boots back. (The pinned shots show a jog where the footprint meets the ribbon: the pin's restart turns the rider to the fall line; it is not there in motion.) | `2026-11-02-build-41375908154-powder-off-on-double.png` (off, on, double), `2026-11-02-build-41375908154-powder-skis-sunk-zoom.png`, `2026-11-02-build-41378281068-powder-moving.png` |
| On the groomer: a shallow cut | 2 cm grooves behind the skis and along the old tracks, the skis down by the same; the blue ribbons come back with the mesh switched off | `2026-11-02-build-41378281068-groomer-off-on-double.png`, `2026-11-02-build-41378281068-groomer-moving.png` (43 km/h, with a zoom) |
| On ice: no cut | Ice's depth is 0: no trench and no sink at 91 km/h; the tracks show only as the packed-floor tint, flat scrape marks | `2026-11-02-build-41378281068-ice-moving.png` (with a contrast-stretched zoom behind the rider) |
| No seam where the mesh meets the terrain | At the test slope's start, the mesh switched on against off differs by 0.3 levels of 255 over the snow; amplified 20 times, the difference is the rider and shadow (sunk 2 cm), a faint speckle and the horizon line from the camera, which follows the rider down by the same 2 cm, and no line where the mesh hands over to the terrain about 15 m out; the far snow is exactly equal. The CI ride (moving, mesh on) shows the corduroy unbroken to the rider | `2026-11-02-build-41477452195-start-before-off-on-double.png` (bottom left: on), `2026-11-02-build-41477452195-start-on-minus-off-x20.png`, `2026-11-02-build-41378281068-ci-test-slope.png` |
| Switched off, the snow looks as it did before | Groove strength 0 in build 49c1146 against c557d50 (before the story) at the test slope's start: the frames differ by 0.1 levels of 255 on average, the same view | `2026-11-02-build-41477452195-start-before-off-on-double.png` (top left: before; top right: off) |
| Frame time recorded against the build before | Software rendering: about 3 times the frame with the add pass always on, about 2.2 times with it only where slush lies; the 60 frames a second check is the user's, on the PC | `2026-11-02-frame-times.md` |
| No change to the sim | No file under `Packages/com.firstchair.sim/` changed; `dotnet test sim/`: 435 passed, the replay tests among them | the diff |

**Found and fixed in the cloud builds:** the first build drew the test slope's mesh with the test mountain's snow (one
material pair shared by every scene; the test slope's corduroy gone inside the mesh, a straight edge where the terrain
took over): `2026-11-02-build-41374948805-test-slope-shared-materials-bug.png` (mesh off above, on below). 0d62120 gives
each terrain its own pair; `2026-11-02-build-41378281068-ci-test-slope.png` is the fixed build's CI shot, the corduroy
running unbroken to the rider.

Not seen in the cloud: the look at a real GPU's frame rate and in motion at 60 frames a second, the trenches from the
user's own riding, and the Unity Editor. Those are the user's, on the PC.

## Sign-off

- [ ] Approved (the user): the trenches, the raised snow and the sink read as skis cutting into snow, and the PC holds
  60 frames a second with the mesh on.
