// SH_Env_SnowSkinCommon.hlsl
// First Chair, written by Claude for Story 018 (production/epics/first-chair/story-018-deformable-snow.md).
// Purpose: the snow terrain's part in the deformable snow. The snow mesh that follows the rider (SnowSkin,
// SH_Env_SnowSkin.shader) draws the snow inside a square, pushed down where the skis went; the terrain discards its
// pixels under that square in every pass it draws (colour, depth, shadows), so its own undeformed surface neither
// hides the trenches nor shades them. Past the square the mesh lies exactly on the terrain and drops just under it, so
// no seam shows where one ends and the other begins.
// Target: URP, desktop. Cost: one box test a pixel. Variants: none.
#ifndef FIRST_CHAIR_SNOW_SKIN_COMMON_INCLUDED
#define FIRST_CHAIR_SNOW_SKIN_COMMON_INCLUDED

float4 _FCSnowSkinClipUV;   // SnowSkin: the terrain UV box the mesh covers, (uMin, vMin, uMax, vMax); all 0 when none

// Discards a terrain pixel strictly inside the box, so an unset (all-zero) box discards nothing.
void FCClipUnderSnowSkin(float2 terrainUV)
{
    float2 inside = (float2)(terrainUV > _FCSnowSkinClipUV.xy) * (float2)(terrainUV < _FCSnowSkinClipUV.zw);
    clip(0.5 - inside.x * inside.y);
}

#endif
