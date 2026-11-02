// SH_Env_SnowSkinAdd.shader
// First Chair, written by Claude for Story 018 (production/epics/first-chair/story-018-deformable-snow.md).
// Purpose: the deformable snow mesh's add pass: terrain layers 4 to 7 (on the test slope, slush) on the same displaced
// mesh, added over the first pass as the snow terrain's add pass is ("Hidden/First Chair/Snow Terrain (Add Pass)"). Same
// vertex stage as "First Chair/Snow Skin" (SH_Env_SnowSkinPasses.hlsl), so the two passes meet at equal depth.
// Target: URP Forward and Forward+, desktop (Windows, Linux). Variants: URP's lighting keywords, as the terrain's.
Shader "Hidden/First Chair/Snow Skin (Add Pass)"
{
    Properties
    {
        // The terrain's layers, set by the scene builder from the terrain (TestSlopeLook.SnowSkinMaterial).
        _NumLayersCount ("Total Layer Count", Float) = 5.0
        _HeightTransition ("Height Transition", Range(0, 1.0)) = 0.0
        _Control ("Control (RGBA)", 2D) = "red" {}
        _Splat0 ("Layer 0 (R)", 2D) = "grey" {}
        _Splat1 ("Layer 1 (G)", 2D) = "grey" {}
        _Splat2 ("Layer 2 (B)", 2D) = "grey" {}
        _Splat3 ("Layer 3 (A)", 2D) = "grey" {}
        _Normal0 ("Normal 0 (R)", 2D) = "bump" {}
        _Normal1 ("Normal 1 (G)", 2D) = "bump" {}
        _Normal2 ("Normal 2 (B)", 2D) = "bump" {}
        _Normal3 ("Normal 3 (A)", 2D) = "bump" {}
        _Mask0 ("Mask 0 (R)", 2D) = "grey" {}
        _Mask1 ("Mask 1 (G)", 2D) = "grey" {}
        _Mask2 ("Mask 2 (B)", 2D) = "grey" {}
        _Mask3 ("Mask 3 (A)", 2D) = "grey" {}
        [Gamma] _Metallic0 ("Metallic 0", Range(0.0, 1.0)) = 0.0
        [Gamma] _Metallic1 ("Metallic 1", Range(0.0, 1.0)) = 0.0
        [Gamma] _Metallic2 ("Metallic 2", Range(0.0, 1.0)) = 0.0
        [Gamma] _Metallic3 ("Metallic 3", Range(0.0, 1.0)) = 0.0
        _Smoothness0 ("Smoothness 0", Range(0.0, 1.0)) = 0.5
        _Smoothness1 ("Smoothness 1", Range(0.0, 1.0)) = 0.5
        _Smoothness2 ("Smoothness 2", Range(0.0, 1.0)) = 0.5
        _Smoothness3 ("Smoothness 3", Range(0.0, 1.0)) = 0.5
        _NormalScale0 ("Normal Scale 0", Float) = 1.0
        _NormalScale1 ("Normal Scale 1", Float) = 1.0
        _NormalScale2 ("Normal Scale 2", Float) = 1.0
        _NormalScale3 ("Normal Scale 3", Float) = 1.0
        _DiffuseRemapScale0 ("Diffuse Remap Scale 0", Vector) = (1, 1, 1, 1)
        _DiffuseRemapScale1 ("Diffuse Remap Scale 1", Vector) = (1, 1, 1, 1)
        _DiffuseRemapScale2 ("Diffuse Remap Scale 2", Vector) = (1, 1, 1, 1)
        _DiffuseRemapScale3 ("Diffuse Remap Scale 3", Vector) = (1, 1, 1, 1)
        _MaskMapRemapOffset0 ("Mask Remap Offset 0", Vector) = (0, 0, 0, 0)
        _MaskMapRemapOffset1 ("Mask Remap Offset 1", Vector) = (0, 0, 0, 0)
        _MaskMapRemapOffset2 ("Mask Remap Offset 2", Vector) = (0, 0, 0, 0)
        _MaskMapRemapOffset3 ("Mask Remap Offset 3", Vector) = (0, 0, 0, 0)
        _MaskMapRemapScale0 ("Mask Remap Scale 0", Vector) = (1, 1, 1, 1)
        _MaskMapRemapScale1 ("Mask Remap Scale 1", Vector) = (1, 1, 1, 1)
        _MaskMapRemapScale2 ("Mask Remap Scale 2", Vector) = (1, 1, 1, 1)
        _MaskMapRemapScale3 ("Mask Remap Scale 3", Vector) = (1, 1, 1, 1)
        _LayerHasMask0 ("Layer Has Mask 0", Float) = 0.0
        _LayerHasMask1 ("Layer Has Mask 1", Float) = 0.0
        _LayerHasMask2 ("Layer Has Mask 2", Float) = 0.0
        _LayerHasMask3 ("Layer Has Mask 3", Float) = 0.0
        [HideInInspector] _MainTex ("BaseMap (RGB)", 2D) = "grey" {}
        [HideInInspector] _BaseColor ("Main Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Geometry+1" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex FCSnowSkinVert
            #pragma fragment SplatmapFragment

            #define _METALLICSPECGLOSSMAP 1
            #define _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 1
            #define FC_SNOW_SKIN
            #define TERRAIN_SPLAT_ADDPASS

            // -------------------------------------
            // Universal Pipeline keywords (the snow terrain's colour pass's)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma shader_feature_local_fragment _TERRAIN_BLEND_HEIGHT
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _MASKMAP

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
            #include "Assets/Shaders/SnowTerrain/SH_Env_SnowTerrainForward.hlsl"
            #include "Assets/Shaders/SnowTerrain/SH_Env_SnowSkinPasses.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
