// SH_Env_SnowTerrainCut.hlsl
// First Chair, written by Claude for Story 018 (production/epics/first-chair/story-018-deformable-snow.md).
// Purpose: the snow terrain's shadow and depth passes, cut away under the snow mesh near the rider like its colour pass
// (SH_Env_SnowSkinCommon.hlsl), so its undeformed surface neither shadows the mesh's trenches nor hides them in a depth
// prepass. URP 17.3's own terrain pass functions (TerrainLitPasses.hlsl), with the cut added; the depth pass's vertex is
// URP's with the terrain's UV taken through instancing as the shadow pass already does, since the cut needs it.
// Target: URP, desktop. Cost: one box test a pixel. Variants: URP's.
#ifndef FIRST_CHAIR_SNOW_TERRAIN_CUT_INCLUDED
#define FIRST_CHAIR_SNOW_TERRAIN_CUT_INCLUDED

#include "Assets/Shaders/SnowTerrain/SH_Env_SnowSkinCommon.hlsl"

half4 FCTerrainShadowFragment(VaryingsLean IN) : SV_TARGET
{
    FCClipUnderSnowSkin(IN.texcoord);
    return ShadowPassFragment(IN);
}

VaryingsLean FCTerrainDepthOnlyVertex(AttributesLean v)
{
    VaryingsLean o = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    TerrainInstancing(v.position, v.normalOS, v.texcoord);
    o.clipPos = TransformObjectToHClip(v.position.xyz);
    o.texcoord = v.texcoord;
    return o;
}

half4 FCTerrainDepthOnlyFragment(VaryingsLean IN) : SV_TARGET
{
    FCClipUnderSnowSkin(IN.texcoord);
    return DepthOnlyFragment(IN);
}

#endif
