# Story 017: Ski grooves in the snow

> **Epic**: First Chair MVP (base skiing system)
> **Status**: In Progress
> **Layer**: Presentation
> **Type**: Visual/Feel (the grooves in a build) + Logic (the stamp window)
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-10-27

## Context

**GDD**: `design/game-brief.md`, MVP: "First real art toward the target look: skier, skis, poles, trees, snow trails and
lighting"; story 007's open criterion "Snow trails: a render texture is stamped where skis touch, and a URP snow shader
reads it as bump and detail. The trails are display only."
**Requirement**: the user's playtest note on 2026-10-24 (story 007): "Tracks should build up where skiers pass, as in
GMA2, not sit as streaks on a static texture. This belongs to the custom snow shader." Step one, the ribbons that stay on
the snow across runs, is in (716391a). The grooves were left for the PC because they needed a custom terrain shader
checked in the editor; story 014 has since built that shader and proved it in CI and cloud rides.

Today a track is a translucent ribbon lying on the snow: it darkens the snow where a ski passed, but the snow itself is
flat under it, so a powder line reads as a stripe, not a trench.

Written by Claude in sleep mode, continuing the brief once the named work was done; the design choices are logged under
Decisions and are the user's to change.

## Design

1. **Stamp the ribbons.** Every frame, the track ribbons (each ski's live track and the old tracks the scene builder
   lays) are drawn from above into a render texture covering a square window of the snow ahead of the camera, by a stamp
   shader that turns each ribbon's across-track coordinate into a rounded groove profile (deepest on its centre line),
   the deeper of two crossing grooves kept. The window follows the camera, snapped to whole texels so the grooves never
   swim. Display only: the sim never reads it.
2. **The snow shader reads it as relief.** `First Chair/Snow Terrain` samples the texture and bends the snow's normal
   by the groove's slope, as story 014's ripples do, and darkens the groove's floor a little (compressed snow). The
   depth is per snow type: deep in powder, less in crud and slush, a shallow cut in groomed snow, none on ice. Grooves
   fade out toward the window's edge and as they near a pixel's size.
3. **Settings:** the strength, the depth per snow type, the darkening and the fade are `LightingSettings` fields (the
   Feel Lab's Light tab); the window's size and resolution are on the stamping component.

## Acceptance Criteria

- [ ] The window math is pure C# with EditMode tests: snapped to whole texels, the camera inside the window and most of
  it ahead, still while the camera moves less than a texel, and world positions mapped to texture coordinates.
- [ ] The stamp shader and the terrain shader build in CI for Windows and Linux with no shader errors, and the terrain
  is not drawn magenta in the cloud build.
- [ ] From the chase camera, retained in `production/qa/evidence/story-017-ski-grooves/`: tracks read as grooves with a
  lit and a shaded wall, deepest in powder, a shallow cut on the groomer and none on ice; with the strength at 0 the
  snow looks as it did before (story 014's look).
- [ ] The cloud ride's frame time is no more than 10 percent above the previous build's on the same view.
- [ ] No change to the sim, the camera or the ribbons' own look.

## Out of Scope

- Snow pushed up beside a groove (berms) and spray marks; deformation of the mesh itself (the relief is in the shading).
- Grooves beyond the window: far tracks stay ribbons only.
- Snowboard tracks (no snowboard yet).

## Test Evidence

**Story Type**: Visual/Feel + Logic. EditMode tests for the window; retained cloud-build screenshots and frame times in
`production/qa/evidence/story-017-ski-grooves/`; the look sign-off is the user's, on the PC.

## Dependencies

- Depends on: Story 014 (the snow terrain shader), story 007's track ribbons (716391a), story 012's track fix (the
  ribbon starts at the ski's trailing end).
- Unlocks: closes story 007's last unbuilt criterion (the stamped snow trails).

## Decisions (sleep mode, 2026-10-27; the user's to change)

- The ribbons are the stamp: they already record every track, across runs, with their width, so the grooves need no
  second record of where skis went, and every ribbon in the window grooves the snow, old tracks included.
- A window ahead of the camera (64 m at 2,048 texels, 3 cm a texel) rather than the whole mountain, which at that detail
  would need tens of thousands of texels a side; past the window the ribbons carry on alone.
