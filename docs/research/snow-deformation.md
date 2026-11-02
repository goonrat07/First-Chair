# First Chair: deformable snow research

November 2, 2026

The user, after seeing story 017's grooves in the cloud build: the snow should look like Grand Mountain Adventure 2's
and Steep's, "where it actually had depth". The ski should carve through and into the snow, displace it and pack it
down, leaving a 3D track instead of a line behind the ski. This note gathers what is known about how games do that,
compares the ways First Chair could, and records the choice.

## Decision (user, 2026-11-02)

**A snow mesh near the rider, truly displaced.** A finely divided snow surface follows the rider and is pushed down
where the skis went, with the displaced snow raised beside each track. Story 017's texture of where the skis went near
the camera is the input; 017 folds into the new story rather than finishing as shading only.

## What the reference games publish

| Game | What is known | Source |
|---|---|---|
| Grand Mountain Adventure 2 (Toppluva) | Its own engine, built from scratch by the studio. Tracks stay on the mountain until cleared (an in-game snow globe removes them). No technical write-up found. | Level Winner guide; studio blurb on the store pages |
| Steep (Ubisoft Annecy) | Built on the tech of Ghost Recon Wildlands, credited for its snow and terrain. A forum answer, not from the studio, says the deformation runs in the vertex shader of the snow mesh. No talk found. | GamesBeat; Polycount thread |
| Riders Republic (Ubisoft Annecy) | Nothing published on its snow found. | |

So the reference games say little themselves. Two shipped games have described the technique in detail, and both use
the shape GMA2 and Steep show on screen: a trench with raised edges, in real geometry.

## Published techniques

**Batman: Arkham Origins** (Colin Barré-Brisebois, GDC 2014). Each snow-covered area gets a heightmap. Whatever
touches the snow is rendered from below into it through an ankle-high orthographic frustum, blurred, and accumulated
frame to frame, with a small amount taken off each frame so trails fill back in while it snows. The consoles drew the
depth with relief mapping on flat snow; the PC version displaced real geometry with DX11 tessellation, with snow banks
from the displacement plus a macro normal map. With real geometry, shadows follow the surface, the trails cast their own
shadows and ambient occlusion darkens them. The heightmap update took under 1 ms on consoles and 2 to 4 MB.

**Rise of the Tomb Raider** (Michels and Sikachev, SIGGRAPH 2015 talk; GPU Pro 7, "Deferred Snow Deformation"). A
compute shader writes each foot as a quadratic dip into a 32-bit texture with atomic operations (16 bits of height and
16 of the foot's own height). The snow's height comes from the snow mesh as it is drawn, so the texture needs only the
deformers. The trail is pushed down and the snow beside it pushed up: where the shape is above the snow's height, a
rounded rise is computed from the foot's height, the depth of the dip and an artist's scale. Hardware tessellation adds
vertices only where they are needed, near the player, so the snow mesh itself stays light.

**Elsewhere.** Red Dead Redemption 2's PC settings include parallax occlusion mapping, and its PC release lists
higher-quality snow trails; Rockstar has not published the method. Horizon Zero Dawn: The Frozen Wilds keeps trails
after the character passes. Screen-space decals (stamped textures) give marks, not depth.

**In Unity's URP.** URP has no surface shaders, so tessellation means hand-written hull and domain stages, and URP's
terrain shader has no tessellation of its own. Community URP snow (for example Minions Art's) tessellates a separate
plane driven by a render texture from a top-down camera, not Unity Terrain.

## What First Chair has

- The snow is Unity Terrain drawn by `First Chair/Snow Terrain` (story 014), a fork of URP's terrain shader. The test
  slope's heightmap is 1,025 samples over 512 m, so its nearest vertices are 0.5 m apart: far too coarse for a 9 cm
  ski track.
- Each ski lays a track ribbon that stays across runs (story 007's step one); the scene builder lays old tracks.
- Story 017 stamps every ribbon over a 32 m square ahead of the camera into a 2,048-texel R8 render texture each frame
  (`TrackGrooves`, `TrackGrooveWindow`), and the snow shader reads it as relief in the shading only. That reads as a
  line, not a track, which is the user's point.

## The ways it could be done

| | Snow mesh near the rider (chosen) | Tessellate the terrain itself | Faked depth in the shader |
|---|---|---|---|
| How | A finely divided mesh follows the rider, displaced in its vertex shader by the deformation map; the terrain under it is cut away | Hull and domain stages added to the terrain shader's passes, displacing near the rider | Parallax occlusion mapping: the snow shader ray-marches the deformation map |
| Depth, shadows, sinking skis | Real: walls cast and catch shadows, skis can sink into powder | Real | Illusion only; no silhouette, no shadows unless marched again |
| Risk | The mesh's edge must meet the terrain without a seam; the terrain's shading must be repeated on the mesh | URP terrain doesn't support it; every terrain pass must displace alike; the terrain's own LOD seams | Breaks the project's shader rules (texture reads in a loop); artifacts at low angles |
| Runs where | Any GPU, the cloud's software renderer included | GPUs with tessellation | Any GPU |
| Cost | About 250k extra triangles near the rider | Generated triangles near the rider only | A loop of texture reads per pixel near tracks |

## Design sketch for the chosen approach

- **The snow mesh.** Nested square grids centred on the rider, each snapped to its own spacing so nothing swims as the
  rider moves: about 2.5 cm near the rider, doubling outward to about 16 m. It is built once in C# and moved each frame.
- **Its height.** Each vertex takes the terrain's height from the terrain's heightmap texture, following Unity's own
  triangulation so the mesh lies exactly on the terrain where nothing is deformed, minus the track's dip, plus the snow
  pushed up beside it. Normals come from the displaced height.
- **The deformation map.** Story 017's stamp, widened: each ribbon writes a dip across the ski's width and a rounded
  rise beyond its edges, after Rise of the Tomb Raider's trail elevation. The depth per snow type comes from the
  terrain's splat map where the mesh is drawn: deep in powder, a shallow cut on the groomer, none on ice. The trench's
  floor is packed: smoother and a little darker.
- **The terrain under it** is cut away inside the mesh's square; the mesh keeps no deformation at its edge and hangs a
  short skirt there, so no crack shows where the two meet.
- **Shading.** The mesh draws with the same snow lighting (`SH_Env_SnowEffects.hlsl`) and the terrain's own splat
  textures, and it casts and receives shadows.
- **The skis sink** into the trench in powder: the view lowers them by the local depth. Display only; the sim never
  reads any of this, so replays stay exact.

## Sources

- [Deformable Snow Rendering in Batman: Arkham Origins, GDC 2014 (GDC Vault)](https://gdcvault.com/play/1020177/Deformable-Snow-Rendering-in-Batman); [slides](https://www.slideshare.net/colinbb/gdc2014-deformable-snow-rendering-in-batman-arkham-origins)
- [Labs R&D: Rendering Techniques in Rise of the Tomb Raider, SIGGRAPH 2015](https://history.siggraph.org/learning/labs-rd-rendering-techniques-in-rise-of-the-tomb-raider)
- [Deferred Snow Deformation in Rise of the Tomb Raider, GPU Pro 7](https://www.taylorfrancis.com/books/9780203705179/chapters/10.1201/b22483-18)
- [Deformable Snow Rendering, bachelor's thesis, University of Koblenz (surveys both)](https://uko.opus.hbz-nrw.de/files/1928/Bachelor.pdf)
- [GamesBeat on Steep's technology](https://gamesbeat.com/ubisofts-steep-is-a-kinetic-dive-into-extreme-winter-sports/)
- [Polycount thread on Steep's snow](https://polycount.com/discussion/comment/2574890)
- [Level Winner guide to Grand Mountain Adventure](https://www.levelwinner.com/grand-mountain-adventure-guide-tips-tricks-strategies-to-earn-more-medals-and-ski-passes/)
- [TapSmart review of Grand Mountain Adventure 2](https://www.tapsmart.com/apps/books/review-gma2/)
- [Unity manual: tessellation, and URP's lack of surface shaders](https://docs.unity3d.com/6000.1/Documentation/Manual/SL-SurfaceShaderTessellation.html)
- [Minions Art: Interactive Snow with Tessellation for URP](https://www.patreon.com/posts/47452596)
- [Gamingbolt on Red Dead Redemption 2's tech](https://gamingbolt.com/red-dead-redemption-2-tech-analysis-animations-physics-post-processing-effects-weather-systems-and-more)
