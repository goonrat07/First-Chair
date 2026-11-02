// SH_Env_SnowSkinPasses.hlsl
// First Chair, written by Claude for Story 018 (production/epics/first-chair/story-018-deformable-snow.md).
// Purpose: the deformable snow mesh's vertex stages for its colour, shadow and depth passes. Each is URP 17.3's terrain
// vertex function for that pass (TerrainLitPasses.hlsl: SplatmapVert, ShadowPassVertex, DepthOnlyVertex) with the
// vertex placed by FCSnowSkinSurface (SH_Env_SnowSkin.hlsl) instead of read from the terrain's patch, so all three move
// the snow alike: the trench draws, casts its shadow and occludes as one surface. The colour pass's fragment is the snow
// terrain's own (SH_Env_SnowTerrainForward.hlsl, with FC_SNOW_SKIN).
// Target: URP Forward, desktop. Cost: see SH_Env_SnowSkin.hlsl. Variants: URP's.
#ifndef FIRST_CHAIR_SNOW_SKIN_PASSES_INCLUDED
#define FIRST_CHAIR_SNOW_SKIN_PASSES_INCLUDED

#include "Assets/Shaders/SnowTerrain/SH_Env_SnowSkin.hlsl"

// The colour pass: SplatmapVert's outputs for a vertex placed on the snow.
Varyings FCSnowSkinVert(Attributes v)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float3 positionWS, normalWS;
    float2 uv;
    FCSnowSkinSurface(v.positionOS.xyz, positionWS, normalWS, uv);
    VertexPositionInputs positions = FCSnowSkinPositionInputs(positionWS);

    o.uvMainAndLM.xy = uv;
    o.uvMainAndLM.zw = uv * unity_LightmapST.xy + unity_LightmapST.zw;
    #ifndef TERRAIN_SPLAT_BASEPASS
        o.uvSplat01.xy = TRANSFORM_TEX(uv, _Splat0);
        o.uvSplat01.zw = TRANSFORM_TEX(uv, _Splat1);
        o.uvSplat23.xy = TRANSFORM_TEX(uv, _Splat2);
        o.uvSplat23.zw = TRANSFORM_TEX(uv, _Splat3);
    #endif

#if defined(DYNAMICLIGHTMAP_ON)
    o.dynamicLightmapUV = uv * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
        float4 vertexTangent = float4(cross(float3(0, 0, 1), normalWS), 1.0);
        VertexNormalInputs normalInput = GetVertexNormalInputs(normalWS, vertexTangent);

        o.normal = half4(normalInput.normalWS, viewDirWS.x);
        o.tangent = half4(normalInput.tangentWS, viewDirWS.y);
        o.bitangent = half4(normalInput.bitangentWS, viewDirWS.z);
    #else
        o.normal = normalWS;
        OUTPUT_SH4(positionWS, o.normal.xyz, GetWorldSpaceNormalizeViewDir(positionWS), o.vertexSH, o.probeOcclusion);
    #endif

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(positions.positionCS.z);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        o.fogFactorAndVertexLight.x = fogFactor;
        o.fogFactorAndVertexLight.yzw = VertexLighting(positionWS, o.normal.xyz);
    #else
        o.fogFactor = fogFactor;
    #endif

    o.positionWS = positionWS;
    o.clipPos = positions.positionCS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        o.shadowCoord = GetShadowCoord(positions);
    #endif

    return o;
}

// The shadow pass: ShadowPassVertex for a vertex placed on the snow, so a trench's wall shades its floor.
VaryingsLean FCSnowSkinShadowVert(AttributesLean v)
{
    VaryingsLean o = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(v);

    float3 positionWS, normalWS;
    float2 uv;
    FCSnowSkinSurface(v.position.xyz, positionWS, normalWS, uv);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 clipPos = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    clipPos.z = min(clipPos.z, UNITY_NEAR_CLIP_VALUE);
#else
    clipPos.z = max(clipPos.z, UNITY_NEAR_CLIP_VALUE);
#endif

    o.clipPos = clipPos;
    o.texcoord = uv;
    return o;
}

half4 FCSnowSkinShadowFrag(VaryingsLean IN) : SV_TARGET
{
    return 0;
}

// The depth pass: DepthOnlyVertex for a vertex placed on the snow.
VaryingsLean FCSnowSkinDepthVert(AttributesLean v)
{
    VaryingsLean o = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    float3 positionWS, normalWS;
    float2 uv;
    FCSnowSkinSurface(v.position.xyz, positionWS, normalWS, uv);
    o.clipPos = TransformWorldToHClip(positionWS);
    o.texcoord = uv;
    return o;
}

half4 FCSnowSkinDepthFrag(VaryingsLean IN) : SV_TARGET
{
    return IN.clipPos.z;
}

#endif
