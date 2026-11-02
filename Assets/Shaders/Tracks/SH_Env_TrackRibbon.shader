// SH_Env_TrackRibbon.shader
// First Chair, written by Claude for Story 018 (production/epics/first-chair/story-018-deformable-snow.md).
// Purpose: the ski track ribbons (SkiTrack, and the scene builder's old tracks): a translucent tint on the snow, lit as
// URP Lit's transparent surface with no smoothness and no metal, which the ribbons drew with before, so far off they
// look as they did. Near the rider the snow mesh (SnowSkin) cuts real trenches with their own packed-floor tint, so
// there the ribbon fades out, over the band where the trenches fade in: one track, not a film over a trench.
// Target: URP Forward and Forward+, desktop. Cost: URP's lighting for a transparent surface, a box test for the fade.
// Variants: URP's main-light shadow and fog keywords only.
Shader "First Chair/Track Ribbon"
{
    Properties
    {
        _BaseColor ("Colour (alpha: how much it tints the snow)", Color) = (0.55, 0.65, 0.86, 0.55)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RibbonVertex
            #pragma fragment RibbonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Cull;
            CBUFFER_END

            float4 _FCSnowSkin;     // SnowSkin: x, y the mesh's centre (m); z the cut's half side; w where its trenches start fading toward it

            // A cut smaller than this (m) is no snow mesh.
            static const float RIBBON_MIN_CUT = 1e-3;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings RibbonVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = (half)ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // 0 well inside the snow mesh, rising to 1 at its cut; 1 everywhere with no mesh.
            half SkinFade(float3 positionWS)
            {
                float2 fromCentre = abs(positionWS.xz - _FCSnowSkin.xy);
                float reach = max(fromCentre.x, fromCentre.y);
                float fade = saturate((reach - _FCSnowSkin.w) / max(_FCSnowSkin.z - _FCSnowSkin.w, RIBBON_MIN_CUT));
                return (half)lerp(1.0, fade, step(RIBBON_MIN_CUT, _FCSnowSkin.z));
            }

            half4 RibbonFragment(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                half3 normal = normalize(input.normalWS);
                inputData.normalWS = frontFace ? normal : -normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                half alpha = _BaseColor.a * SkinFade(input.positionWS);
                half4 color = UniversalFragmentPBR(inputData, _BaseColor.rgb, /* metallic */ 0.0h, /* specular */ half3(0.0h, 0.0h, 0.0h),
                    /* smoothness */ 0.0h, /* occlusion */ 1.0h, /* emission */ half3(0.0h, 0.0h, 0.0h), alpha);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return half4(color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
