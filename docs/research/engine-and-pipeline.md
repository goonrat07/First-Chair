# First Chair: engine and Blender pipeline research

October 19, 2026

This picks the engine, the Blender pipeline and the way Claude works with both,
for building First Chair's base skiing system first. It is based on the old
Bluebird gameplay spec, used as long-term design direction only (none of its
code or tech carries over), and on these decisions:

- **Engine:** compare Unity and Unreal and recommend one. The game runs on a
  personal Windows PC, so either engine is fine on hardware.
- **Look:** realistic-leaning, taking heavy inspiration from Grand Mountain
  Adventure 2.
- **Models:** Claude builds them, using a Blender skill pack from GitHub
  instead of improvising.
- **Replays:** exact replays are part of the base riding system from day one.
- **Sport:** skis first, snowboard later. The code leaves room for the board.
- **Workflow:** cloud sessions for now, then Claude Code on the PC for
  production.

## Recommendation

| Area | Pick | Why |
|---|---|---|
| Engine | **Unity 6, URP** | Scenes, prefabs and settings are text files that Claude can write, review and diff. Unity publishes an official Claude Code plugin. C# iterates quickly, and GMA2's look doesn't need Unreal's high-end rendering. |
| Riding sim | **Engine-independent C# library**: fixed 120 Hz step, fixed-point math | Exact replays need a sim that doesn't depend on engine physics. As plain C#, it builds and tests with `dotnet test` in the cloud and in GitHub Actions, with no Unity license needed. |
| Blender | **Blender 5.2 LTS, scripted and headless** | Verified in this cloud container: Blender 5.2.2 runs headless and exports GLB and FBX. |
| Blender skill pack | **[majidmanzarpour/blender-game-skills](https://github.com/majidmanzarpour/blender-game-skills)**, extended with our own skills for trees and terrain | Builds game-ready assets in measured, gated phases. Runs headless. Exports FBX or GLB with a Unity setting. Covers rigging and animation. |
| Claude and the editors | **Unity's official plugin** ([unity-agent-plugin](https://github.com/Unity-Technologies/unity-agent-plugin)) and the **Blender Lab MCP** on the PC | Both are official. Claude can drive the open Unity Editor and a live Blender session. |
| Terrain | **One 16-bit heightmap** read by both the sim and Unity Terrain | The physics and the visuals can never disagree about where the snow is. |

**What Unity costs us compared with Unreal:** Unreal reaches realistic lighting
faster (Lumen) and has the stronger large-terrain system (Landscape and World
Partition). If you'd rather push for photoreal over GMA2's clean look, Unreal
is the right pick. The rest of this plan still applies: the sim becomes a C++
library and Blender assets go through Send to Unreal. Section 3 compares the
two.

## 1. What the project needs

From the spec's baseline (the riding that has to work before anything else):

- A **deterministic 120 Hz riding sim**. The same inputs from the same start
  give the same ride, bit for bit. Inputs pack into one number per step, and
  a recording is a header plus those numbers.
- Carving and skidding, tuck, brake, poling on flats, and snow types (groomed,
  powder, crud, ice, slush), each with its own grip and drag.
- Hold-to-load jumps, spins, flips and grabs in the air, landings graded clean,
  solid or sketchy, crashes, and tap-to-reset.
- Rails and boxes. Tree near-misses build the Flow meter, branch clips cost
  speed, and trunk hits crash.
- Later systems read the sim's output and never change the sim: events, Stoke,
  pursuers, clips, Career.

From the answers above, the pipeline also needs to:

- Produce **realistic-leaning visuals in GMA2's style**: clean, simple shapes
  and convincing snow and light.
- Let **Claude build the models** with a skill pack, both in the cloud now and
  on the PC later.
- Handle **one big mountain**. Kettle Peak was about 3 km square, with glades,
  lifts and a village.

## 2. What GMA2's look is made of

[80.lv's interview with Toppluva](https://80.lv/articles/developing-a-ski-open-world-game-for-mobile)
on the first game explains the style. The sequel looks very similar.

- **Custom engine.** C++ and OpenGL, built for mobile. No Unity or Unreal.
- **Low-poly, untextured props against smooth, textured snow.** That contrast
  is most of the look. Props are modeled in Blender with no texture maps.
- **Snow tracks** are one large texture, updated continuously and used as a
  bump map. Snow has its own hand-made shading model.
- **Terrain** started in World Machine, then every jump, bump and drop was
  sculpted by hand in their editor. They threw away about 50 mountains. Pure
  noise "always came out pretty dull", and they tried real-world ski resort
  height data but didn't ship it.
- **Physics** aims "between realism and fun". Fully realistic jump length and
  crashes felt random and annoying.

What this means for First Chair:

- The look depends on lighting, snow shading and clean modeling, not on
  rendering power. Unity URP can do it.
- Scripted Blender models fit well, because simple, clean props are the style.
- Snow is the one place to spend real effort: a trail texture, sparkle, and
  soft shadows across the snow.

## 3. Unity vs Unreal for First Chair

| | Unity 6 (URP) | Unreal Engine 5.8 |
|---|---|---|
| GMA2-style look | Good. Needs tuning for realistic light: physical sky, exposure, post-processing. URP is where Unity is investing; [HDRP is in maintenance mode](https://unity.com/topics/render-pipelines-strategy-for-2026). | Excellent out of the box (Lumen, Nanite). Easier to push past GMA2 toward photoreal. |
| Big mountain terrain | Unity Terrain imports 16-bit RAW heightmaps. Fine at 3 km, with tiles if needed. | Landscape and World Partition are the strongest option for large terrain. |
| Deterministic riding sim | Pure C# library, testable with `dotnet test` anywhere. | Pure C++ library, testable with CMake. Longer compiles. |
| How much Claude can edit as text | Most of it: C#, plus scenes and prefabs as YAML once text serialization is forced. | C++ only. Blueprints, levels and assets are binary `.uasset` files, so changes go through the editor or MCP. |
| Official Claude Code plugin | [unity-agent-plugin](https://github.com/Unity-Technologies/unity-agent-plugin): Unity 6+, 29 skills, Unity CLI to drive the open Editor, run builds and tests. Listed in Anthropic's plugin directory. | [unreal-engine-skills-for-claude-code-plugin](https://github.com/epicgames/unreal-engine-skills-for-claude-code-plugin): drives the editor through Unreal's MCP server (experimental in 5.8). The editor must be open. On Windows the plugin's hook needs Git Bash or WSL. |
| Community MCP fallback | [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp): MIT, about 13.5k stars, Unity 2021.3 to 6.x. | [chongdashu/unreal-mcp](https://github.com/chongdashu/unreal-mcp), [flopperam/unreal-engine-mcp](https://github.com/flopperam/unreal-engine-mcp) |
| Blender import | FBX with [EdyJ's Blender-to-Unity FBX exporter](https://github.com/EdyJ/blender-to-unity-fbx-exporter) (about 1.3k stars) to fix axes and scale, or glTF. | [Send to Unreal](https://github.com/poly-hammer/BlenderTools): one click, community-maintained since Epic handed it off, 650 stars. Or FBX/glTF through Interchange. |
| Iteration speed | Fast C# recompile. | C++ compiles and editor restarts are slower. Blueprints are fast but binary. |
| Repo size | Moderate. | Large. Every asset is a binary `.uasset` in Git LFS. |

**Verdict:** Unity. The riding sim is custom either way, so Unreal's physics
gives us nothing. Its rendering lead matters less for GMA2's clean style than
Unity's text-based project files and quicker loop matter for building it with
Claude.

## 4. Blender pipeline

### Skill packs reviewed

| Repo | What it is | Headless | Stars, license | Fit |
|---|---|---|---|---|
| [majidmanzarpour/blender-game-skills](https://github.com/majidmanzarpour/blender-game-skills) | `blender-image-to-3d`: brief, blockout, forms, topology and LODs, UVs and bake, rig, animation, export, round-trip check. Each gate is measured (silhouette IoU, width profiles, tri budgets). | Yes (`blender --background`). MCP optional. | 150, MIT | **Best fit for characters and props**: skier, skis, poles, boots, snowmobile, lift chairs, buildings. FBX or GLB export with a Unity flag, plus an asset manifest. |
| [colinurbs/claude-modeling-skills](https://github.com/colinurbs/claude-modeling-skills) | Five skills: reference modeling, game assets (glTF), **game animation from open mocap on a shared skeleton**, VFX, UI. Numeric gates and a fresh-eyes critic. | Game skills yes. Reference modeling needs Blender MCP. | 12, MIT | Useful for animation and VFX. Small and young. |
| [elithril/blender-kiln](https://github.com/elithril/blender-kiln) | Claude Code plugin with `/kiln` commands, brief to GLB, batch runs from a YAML manifest, Poly Haven textures, Rigify, LODs via gltfpack. | Batch scripts yes. The main loop needs a running Blender MCP. | 54, MIT | Most mature tooling, but it validates for three.js and Babylon.js, not Unity. Optional AI generation goes through Hugging Face Spaces. |
| [ig-shadow-walker/BlenderXAlpha-3DGenSkill](https://github.com/ig-shadow-walker/BlenderXAlpha-3DGenSkill) | Text to AI-generated model via Alpha3D plus Blender MCP. | No | n/a | Depends on a third-party generation service. Not recommended. |

**Pick: blender-game-skills.** It's the only one that is both headless, so it
works in this cloud container today, and measured at every step. Gaps to plan
for:

- **It expects reference images.** Every gate compares renders against a
  reference. For each asset we collect references first (photos of real skis,
  bindings and lift chairs, or concept sketches). Text-only briefs lose the
  checks.
- **Trees and forests aren't covered.** There's no foliage, alpha-card or
  instancing guidance. We add our own `tree-kit` skill in the same gated
  format: species presets, tri budgets per LOD, and a billboard far LOD, with
  instancing done in Unity.
- **Terrain isn't covered either.** Section 5 handles it.
- **Big structures** like lift towers and the gondola need their own camera
  framing at the export gate. The default framing is tuned for characters.

The pack is small and has a single commit. Before installing, read its scripts
end to end, pin a commit, and vendor it into `tools/blender/skills/` so
upstream changes can't break the pipeline.

### Live Blender on the PC (MCP)

- **[Blender Lab MCP](https://www.blender.org/lab/mcp-server/)**: the official
  one, made by Blender developers. Needs Blender 5.1+ and is at v1.0.3.
  Recommended.
- **[ahujasid/blender-mcp](https://github.com/ahujasid/blender-mcp)** (now "MCP
  for Blender"): the popular community option, about 31.7k stars, MIT. Has
  built-in Poly Haven, Sketchfab and AI-generation integrations.

Both run arbitrary Python in Blender, so save before each session and keep the
socket on localhost.

### Materials

[Poly Haven](https://polyhaven.com) textures and HDRIs are CC0 (free to use
with no attribution), so they're safe for rock, bark, wood and sky lighting.
Following GMA2, props mostly use flat colors and baked ambient occlusion, and
texture work goes into the snow.

### Getting assets into Unity

Use FBX for anything rigged, because Unity's skinned-mesh path is the most
mature. The skill's `export_delivery.py --engine unity` produces the delivery
files. Use Z-up, 1 unit = 1 m, and put the origin at ground contact. Sources
(`.blend`) and exports (`.fbx`) both go in Git LFS.

## 5. Terrain

- **One source of truth:** a 16-bit heightmap. The sim samples it for physics
  and Unity Terrain renders from it. Zones (runs, glades, park, closed runs)
  are data layered on top.
- **The base system uses a scripted test slope**: groomer, powder bowl, a
  kicker line and a short glade, generated by a script. This keeps the riding
  work moving without waiting on terrain art.
- **Kettle Peak later:** block it out in [Gaea](https://quadspinner.com) or
  World Machine (what GMA used), then hand-tune every run, jump and drop.
  [BlenderGIS](https://github.com/domlysz/BlenderGIS) can import real elevation
  data for reference, but GMA found real-world data didn't play well, so treat
  it as a starting point at most.

## 6. Snow (the GMA2 look)

- **Trail map:** a render texture stamped where skis touch the snow, read by
  the snow shader as a bump and detail mask, the way GMA does it.
  [steveimm/unity-snow-shader](https://github.com/steveimm/unity-snow-shader)
  is a small Shader Graph example of the technique. It has no license, so we
  learn from it and write our own. MicroSplat Trax is the commercial fallback.
- **Trails are render-only.** They never feed back into the sim, so replays
  stay exact. Gameplay snow state, such as fresh powder cells, stays separate
  game data.
- **Shading:** a custom URP snow shader with subsurface-ish falloff, sparkle,
  blue-tinted shadows, and blending between groomed, powder and ice from a
  splat map.

## 7. Deterministic riding sim

- **Engine physics won't give exact replays.** PhysX and Chaos aren't built to
  produce bit-identical results across machines and builds, and plain float
  math can differ between compilers (for example, fused multiply-add).
- **The plan:**
  - `FirstChair.Sim` is a pure C# library with no `UnityEngine` references.
  - It runs a fixed 120 Hz step, with fixed-point math (Q32.32) for everything
    that moves the rider.
  - It uses one seeded RNG, and each step's input packs into one number.
  - A `RunSetup` header (sim version, sport, assist, seed) and the input stream
    together rebuild any run.
- **Math library:** [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp)
  (MIT, Q32.32, vectors, quaternions, trig, deterministic RNG, .NET Standard
  2.1 and Unity packages). The fallback is a small in-house `Fix64` modeled on
  [FixedMath.Net](https://github.com/asik/FixedMath.Net). One rider plus a few
  pursuers at 120 Hz is cheap, so fixed-point speed doesn't matter.
- **Unity's role:** reads sim state, interpolates between the last two steps,
  and handles camera, animation, audio and VFX. Unity never moves the rider.
- **The key test:** record a ride, replay it from the header, and compare
  every step's position and events. It runs on every push in GitHub Actions
  with only the .NET SDK, no Unity install or license.
- **Ski physics references:** GitHub has no credible open-source ski carving
  controller. Searches only returned spam mirrors. We write our own, using the
  spec's riding rules as the design reference and GMA's "between realism and
  fun" as the target feel.
- **Skier animation is procedural, driven by the sim.** Lean from edge angle,
  crouch from load, and pole plants come from sim state through Unity
  Animation Rigging. Authored clips cover tuck, grabs, crashes and celebrations.

## 8. How Claude works with the tools

**Now (cloud sessions):**

- Claude writes the sim and its tests and runs them here with `dotnet test`.
- Claude runs Blender 5.2 headless here to build and export assets.
- Claude pushes to GitHub. You pull, open in Unity, play, and report how it
  feels.

**Production (Claude Code on your PC):**

- Install the Unity plugin:
  `/plugin marketplace add Unity-Technologies/unity-agent-plugin`, then
  `/plugin install unity@unity-agent-plugin`. Claude can then drive the open
  Editor, run Play Mode tests and builds, and see the result.
- Run the Blender Lab MCP for live modeling sessions.
- **CI:** GitHub Actions runs the sim tests on every push. Unity builds stay
  local for now. GameCI's Personal-license activation
  [has had breakages](https://discussions.unity.com/t/unity-no-longer-supports-manual-activation-of-personal-licenses/926760),
  so cloud builds aren't worth the trouble yet.

## 9. Repo setup

- **Git LFS** for `.blend`, `.fbx`, `.png`, `.tga`, `.exr`, `.raw`, `.wav` and
  `.ogg`. GitHub Free includes
  [10 GiB of LFS storage and 10 GiB of bandwidth a month](https://docs.github.com/en/billing/concepts/product-billing/git-lfs),
  and each changed version of a file counts in full. Keep `.blend` files lean
  and prefer regenerating from scripts.
- **Unity settings:** force text serialization, visible meta files,
  `UnityYAMLMerge` for scenes and prefabs, and the standard
  [Unity .gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore).
- **Layout:**

```
Assets/, ProjectSettings/      Unity 6.3 LTS URP project at the repo root
                               (Claude Code Game Studios expects it there)
Packages/com.firstchair.sim/   the riding sim, an asmdef with noEngineReferences
sim/                           .csproj files that compile the same sim sources,
  FirstChair.Sim.Tests/          plus the determinism and physics tests
tools/blender/                 asset scripts, the vendored skill pack, tree-kit
art/source/                    .blend sources (LFS)
design/, docs/, production/    design brief, research, studio workflow files
```

## 10. Proposed first milestones (base skiing system)

Each milestone ends with an automated test, plus a play test where it changes
feel.

1. **Skeleton.** Unity 6 URP project, the sim package and test project, LFS,
   CI running sim tests. *Test:* CI is green on an empty sim.
2. **Glide and carve.** Heightfield, test slope, rider on snow: carve, skid,
   tuck, brake, pole. Input recording and replay. *Test:* a replay matches
   live play every step, and a scripted carve holds its line.
3. **See it.** Unity Terrain from the same heightmap, a placeholder skier from
   a Blender script, a chase camera, keyboard and gamepad input. *Play test.*
4. **Snow and air.** Snow types, hold-to-load jumps, spins, flips and grabs,
   landings, crashes, reset. *Test:* landing grades from scripted inputs.
5. **Trees and rails.** Near misses and Flow, branch clips, trunk crashes,
   rails and boxes with grinds, trick scoring. *Test:* scoring maths and Flow
   from scripted runs.
6. **First real art.** Skier, skis and poles through the skill pack. A tree-kit
   forest. The snow trail shader and lighting toward GMA2's look. *Screenshot
   review.*

Story, Career, Mountain Ops, clips and the rest of the spec come after
milestone 6.

## 11. Decisions for you

1. **Engine:** decided: Unity 6.3 LTS with URP (October 19, 2026).
2. **Camera:** GMA2 plays from a high camera that pulls back. The old spec
   used a chase cam behind the rider. The choice changes how much detail
   models need up close.
3. **Skill pack:** adopt blender-game-skills, plus our own tree-kit and
   terrain skills in the same format?

## Sources

- [80.lv: Developing a ski open-world game for mobile (Grand Mountain Adventure)](https://80.lv/articles/developing-a-ski-open-world-game-for-mobile)
- [Unity: official plugin for Claude Code](https://unity.com/blog/unity-plugin-for-claude-code) and [docs](https://docs.unity.com/en-us/ai/unity-plugin/claude-code)
- [Unity-Technologies/unity-agent-plugin](https://github.com/Unity-Technologies/unity-agent-plugin)
- [Unity: Render pipelines strategy for 2026](https://unity.com/topics/render-pipelines-strategy-for-2026)
- [epicgames/unreal-engine-skills-for-claude-code-plugin](https://github.com/epicgames/unreal-engine-skills-for-claude-code-plugin)
- [Using Unreal MCP in UE 5.8 (DEV Community)](https://dev.to/gamedevtoollab/using-unreal-mcp-in-ue-58-from-codex-setup-toolsets-and-safe-editor-automation-4pon)
- [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) and [star history](https://www.star-history.com/coplaydev/unity-mcp)
- [chongdashu/unreal-mcp](https://github.com/chongdashu/unreal-mcp), [flopperam/unreal-engine-mcp](https://github.com/flopperam/unreal-engine-mcp)
- [ahujasid/blender-mcp](https://github.com/ahujasid/blender-mcp)
- [Blender Lab MCP server](https://www.blender.org/lab/mcp-server/)
- [majidmanzarpour/blender-game-skills](https://github.com/majidmanzarpour/blender-game-skills)
- [colinurbs/claude-modeling-skills](https://github.com/colinurbs/claude-modeling-skills)
- [elithril/blender-kiln](https://github.com/elithril/blender-kiln)
- [ig-shadow-walker/BlenderXAlpha-3DGenSkill](https://github.com/ig-shadow-walker/BlenderXAlpha-3DGenSkill)
- [poly-hammer/BlenderTools (Send to Unreal)](https://github.com/poly-hammer/BlenderTools)
- [EdyJ/blender-to-unity-fbx-exporter](https://github.com/EdyJ/blender-to-unity-fbx-exporter)
- [mrdav30/FixedMathSharp](https://github.com/mrdav30/FixedMathSharp), [asik/FixedMath.Net](https://github.com/asik/FixedMath.Net)
- [domlysz/BlenderGIS](https://github.com/domlysz/BlenderGIS)
- [steveimm/unity-snow-shader](https://github.com/steveimm/unity-snow-shader)
- [GitHub Git LFS billing](https://docs.github.com/en/billing/concepts/product-billing/git-lfs)
- [Unity forum: Personal license manual activation](https://discussions.unity.com/t/unity-no-longer-supports-manual-activation-of-personal-licenses/926760)
