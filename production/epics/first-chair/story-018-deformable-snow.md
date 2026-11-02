# Story 018: Deformable snow near the rider

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Presentation
> **Type**: Visual/Feel (the snow in a build) + Logic (the mesh, height and profile math)
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-02

## Context

**GDD**: `design/game-brief.md`, MVP: "First real art toward the target look: skier, skis, poles, trees, snow trails and
lighting"; story 007's snow trails criterion.
**Requirement**: the user, 2026-11-02, after seeing story 017's grooves: "I want it to look closer to the snow of grand
mountain adventure 2 and steep where it actually had depth. I want the ski to carve through and into the snow and
actually displace it and pack it down leaving a 3d track instead of a line behind the ski."
**Research and the choice**: `docs/research/snow-deformation.md`. Of a snow mesh near the rider, tessellating the
terrain, and faking the depth in the shader, the user chose the snow mesh (2026-11-02).

Story 017 drew the tracks as shading only; this story folds it in. Its map of where the skis went near the camera
(`TrackGrooveWindow`, `TrackGrooves`, the stamp shader) is this story's input.

## Design

1. **A snow mesh follows the rider.** Nested square grids, about 2.5 to 3 cm between vertices near the rider and
   doubling outward to about 16 m each way, stitched where the spacing changes so no crack opens. The whole mesh moves
   in steps of its coarsest spacing, so every vertex stays on its own grid and nothing swims as the rider moves.
2. **Each vertex sits on the terrain**, taking its height from the terrain's heightmap, then is pushed down where a ski
   went and up beside it: Rise of the Tomb Raider's trail with raised edges. The raised snow comes from the trench
   itself (a blurred copy of the trench, less the trench), so crossings and track ends get it too.
3. **Depth by snow type**, from the terrain's splat map where the mesh is drawn: deep in powder, a shallow cut on the
   groomer, crud and slush between, none on ice. The trench floor is packed: smoother and a little darker.
4. **Story 017's map drives it**, widened to the window the mesh covers.
5. **The terrain is cut away under the mesh**, and a skirt at the mesh's edge hides any seam. Deformation fades to
   nothing before the edge, where the mesh lies on the terrain.
6. **The mesh draws with the terrain's snow**: the same lighting (`SH_Env_SnowEffects.hlsl`) and splat textures. It casts
   and receives shadows, so a trench's wall shades its floor.
7. **The skis sink into powder**: the view lowers them by the depth under the rider. Display only.
8. **Settings**: every depth and size is a field, on the Feel Lab's Light tab (depths) and the component (sizes).

## As built (2026-11-02)

Not verified in the editor: everything below was written in a cloud session, compiled against the Unity player
assemblies, built in CI and ridden in the cloud build under Xvfb with Mesa's software renderer.

- **The mesh** (`SnowSkinLayout`, `SnowSkin`; a040562, 35d91b0): nested square grids, 3 cm a cell beside the rider,
  three levels each twice as coarse, 128 cells a half: out to 15.36 m each way, 328,704 triangles, stitched with no
  T-junctions, moved in steps of 12 cm so nothing swims. The terrain's heights (each sample, read exactly) and normals
  go to the GPU once as textures.
- **The shader** (`SH_Env_SnowSkin.shader`, its add pass, `SH_Env_SnowSkin.hlsl`, `SH_Env_SnowSkinPasses.hlsl`): each
  vertex is set on the terrain, pushed down by the stamped track map and up where its blurred copy reaches past it
  (`SnowTrench.Offset`), as deep as each snow type sets (the splat weights read at their texel centres, as the terrain
  reads them). Per pixel the trench's slope bends the normal and its packed floor takes a blue tint, which also marks
  the tracks on ice, where nothing is cut. The colour pass is the snow terrain's own fragment, so away from the tracks
  the mesh draws as the terrain does; it casts and takes shadows.
- **The terrain cut** (`SH_Env_SnowSkinCommon.hlsl`, `SH_Env_SnowTerrainCut.hlsl`): the terrain discards itself under
  the mesh in its colour (both passes), shadow and depth passes. The mesh's last 0.5 m reaches past the cut, 2 cm under
  the terrain, and the trenches fade out over the 3 m before it, so the two meet flat.
- **The track map** (`TrackGrooves`): story 017's, now centred on the rider (32 m at 2,048 texels) and blurred in two
  passes of seven taps for the raised snow; story 017's zipper fix (a profile flat before the ribbon's edge) kept.
- **Ribbons** (`SH_Env_TrackRibbon.shader`): their own translucent shader, fading out inside the mesh where the
  trenches draw the tracks, and back in beyond it.
- **Footprints and the sink** (29c86a5): a footprint under each ski (`GrooveFootprint`) is stamped like a track, so the
  trench runs under the skis; the skier is drawn down by the depth under the rider (`SnowSkin.DepthAt`, the shader's
  depth on the CPU through `SnowTrench.SplatDepth`), eased over 0.08 s and not in the air, on rails or on the lift;
  the leg IK takes the same sink.
- **Per-terrain materials** (0d62120): the first cloud build drew the test slope's mesh with the test mountain's snow
  (one material pair shared by every scene, rewritten by each scene's build); each terrain has its own now.
- **The add pass only where it adds** (49c1146): the mesh draws its slush pass only while slush lies under it
  (`SplatLayerBlocks`); nothing on screen changes.
- **Settings:** depths on the Feel Lab's Light tab (`LightingSettings`: strength 1; groomed 0.02 m, powder 0.15, crud
  0.08, ice 0, slush 0.06; raise 0.6; the floor's tint and its strength 0.55); the sink on the View tab
  (`RiderViewSettings`: share of the depth 1, easing 0.08 s); sizes on the components (`SnowSkin`: 3 cm, 3 levels, 128
  cells, 0.5 m overlap, 3 m fade, 2 cm drop; `TrackGrooves`: 32 m, 2,048 texels, 5 cm rim).
- **Tests:** `SnowSkinLayoutTests` (7), `SnowTrenchTests` (12), `SplatLayerBlocksTests` (6); the Core tests run
  locally under NUnit: 268 passed; CI's EditMode job passes.
- **Deviations:** design item 3's packed floor is darker and bluer but not smoother (no smoothness change); item 5's
  skirt is the overlap band dropped under the terrain; the mesh reaches 15.36 m, not 16.
- **On the user's PC:** reset or delete `Assets/Data/Riding/LightingSettings.asset`, which keeps story 017's values
  and misses this story's depths (the scene builder keeps an existing asset).

## Acceptance Criteria

- [x] The mesh layout (rings, stitching, snapping), the terrain height and the trench and raised-snow profile are plain
  C# with EditMode tests. *(`SnowSkinLayoutTests`, `SnowTrenchTests`, `SplatLayerBlocksTests`: 25 tests; the terrain
  height is the shader's bilinear of the heightmap's own samples.)*
- [x] The mesh and the terrain build in CI for Windows and Linux with no shader errors, and nothing is drawn magenta in
  the cloud build. *(Runs 41374948805 and 41477452195, the first and the last: the four new shaders compile for every
  graphics API with no shader error in either player's log; no magenta in any ride of these builds.)*
- [ ] From the chase camera, retained in `production/qa/evidence/story-018-deformable-snow/`: a trench with raised snow
  beside it and its own shadow in powder, a shallow cut on the groomer, none on ice; the skis sunk into powder; no seam
  where the mesh meets the terrain; switched off, the snow looks as it did before.
- [ ] The cloud ride's frame time is recorded against the build before. On the user's PC it should hold 60 frames a
  second (the user's check).
- [x] No change to the sim: `dotnet test sim/` passes and replays stay exact. *(No file under
  `Packages/com.firstchair.sim/` changed; `dotnet test sim/`: 435 passed, the replay tests among them.)*

## Out of Scope

- Spray and snow chunks thrown by the ski (story 008's spray stays as it is).
- Trenches beyond the mesh (past about 16 m the tracks are the ribbons' tint only).
- Gameplay from snow depth: the sim never reads the deformation.
- Snowboard tracks (no snowboard yet).

## Test Evidence

**Story Type**: Visual/Feel + Logic. EditMode tests for the layout and the profile math; retained cloud-build
screenshots and a frame-time record in `production/qa/evidence/story-018-deformable-snow/`; the look sign-off and the
60 fps check are the user's, on the PC.

## Dependencies

- Depends on: story 017's track map (folded in), story 014's snow terrain shader, story 007's track ribbons.
- Unlocks: closes story 007's snow trails criterion with real depth.
