# Story 014: A procedural snow shader

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Presentation
> **Type**: Visual/Feel
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/game-brief.md`, "After a rough gameplay demo": "Custom snow and terrain shaders for GMA2's
semi-realistic look (story 007 holds the first part)"; story 007's open criterion "A bluebird-day lighting preset
gives soft shadows, blue-tinted shadows on snow and snow sparkle".
**Requirement**: the user, 2026-10-27, starting sleep mode: "create the custom snow shaders which would probably have
procedural aspects as well".

Today the snow is URP's stock terrain shader (`Universal Render Pipeline/Terrain/Lit`) over five generated textures,
one per snow type (`SnowSurface`). Its sparkle is a few very smooth texels baked into those textures, so it repeats
with the tile and mostly reads as noise; its shade is blue only as far as the cool ambient makes it; and the light
falls off hard over every roller.

Written by Claude in sleep mode (the user asked for this work); the design choices are logged under Decisions and
are the user's to change.

## Design

A First Chair terrain shader, **`First Chair/Snow Terrain`** with its add pass (the terrain has five layers, so Unity
draws a second pass for the fifth), forked from URP 17.3's `TerrainLit` (Unity's Graphics repository, branch
`6000.3/staging`, under the Unity Companion License). Everything the stock shader does stays: layer blending,
holes, instancing, shadows, fog, the depth and shadow passes. The forward pass adds, all procedural, in world space,
so nothing repeats with a texture's tile:

1. **Glints.** The snow is divided into small cells (about 2 cm). Each holds a crystal with its own random tilt and
   size; it flashes when it mirrors the sun into the camera, so the sparkle moves as the rider and camera move. More in
   powder and fresh groomed snow, fewer in crud and slush, none on ice (which is glossy instead). Only in sunlight
   (the main light's shadow), and fading out with distance and with the pixel's footprint, so far snow never
   shimmers.
2. **Blue shade.** Snow out of the sun (in a shadow, or facing away from it) is tinted toward the sky's blue, the more
   the darker it is, as snow in shade looks in GMA2 and in photographs.
3. **Soft light.** Snow scatters light under its surface, so its terminator is soft: the lit side wraps a little past
   90 degrees and rollers fade from sun to shade instead of cutting off.
4. **Wind ripples.** Powder gets a soft procedural ripple in its normal (sastrugi), across the wind, fading with
   distance; groomed snow keeps its corduroy texture.

Every value is a field on `LightingSettings` (the Feel Lab's Light tab), which `LightingRig` pushes to the terrain's
material each frame, so the look tunes live. The scene builder makes the terrain material from the new shader.

## Acceptance Criteria

- [ ] `First Chair/Snow Terrain` and its add pass build in CI for Windows and Linux with no shader errors in the
  build log, and the terrain is not drawn magenta in the cloud build.
- [ ] From the chase camera, retained in `production/qa/evidence/story-014-procedural-snow-shader/`, before (stock
  shader, the previous build) and after: glints in sunlit snow and none in shade; shade bluer than before; a roller's
  terminator softer; powder with ripples; the five snow types still told apart.
- [ ] Each effect has a weight on `LightingSettings`, shown in the Feel Lab's Light tab; with every weight at 0 the
  snow looks as it did with the stock shader.
- [ ] The cloud ride's frame time is no more than 10 percent above the previous build's on the same ride.
- [ ] No change to the sim, the camera or the ski tracks.

## Out of Scope

- Ski-trail grooves read from a render texture (the persistent track ribbons stay); a later story if wanted.
- The deferred rendering path and the far-terrain base-map shader (both keep URP's stock look).
- Snow on trees, rails or props.

## Test Evidence

**Story Type**: Visual/Feel. Retained cloud-build screenshots and frame times in
`production/qa/evidence/story-014-procedural-snow-shader/`; the look sign-off is the user's, on the PC.

## Dependencies

- Depends on: Story 007 (the snow layers and the lighting rig), Story 009 (the Feel Lab).
- Unlocks: nothing blocks on it.

## Decisions (sleep mode, 2026-10-27; the user's to change)

- An HLSL fork of URP's own terrain shader rather than a Shader Graph: Shader Graph has no terrain target in URP, and
  the fork keeps every stock feature while adding the snow terms. The source is read from Unity's Graphics repository
  at the 6.3 branch (`docs/engine-reference/` has no terrain shader reference).
- The effects are added after URP's own lighting in the forward pass, not a lighting model of our own, so shadows,
  additional lights, fog and the ambient stay URP's.
