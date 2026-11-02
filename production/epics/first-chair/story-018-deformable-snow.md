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

## Acceptance Criteria

- [ ] The mesh layout (rings, stitching, snapping), the terrain height and the trench and raised-snow profile are plain
  C# with EditMode tests.
- [ ] The mesh and the terrain build in CI for Windows and Linux with no shader errors, and nothing is drawn magenta in
  the cloud build.
- [ ] From the chase camera, retained in `production/qa/evidence/story-018-deformable-snow/`: a trench with raised snow
  beside it and its own shadow in powder, a shallow cut on the groomer, none on ice; the skis sunk into powder; no seam
  where the mesh meets the terrain; switched off, the snow looks as it did before.
- [ ] The cloud ride's frame time is recorded against the build before. On the user's PC it should hold 60 frames a
  second (the user's check).
- [ ] No change to the sim: `dotnet test sim/` passes and replays stay exact.

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
