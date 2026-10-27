// SH_Env_TrackGrooveStamp.shader
// First Chair, written by Claude for Story 017 (production/epics/first-chair/story-017-ski-grooves.md).
// Purpose: stamps the ski track ribbons into the groove texture from above. A ribbon's across-track coordinate (its u,
// 0 at its left edge and 1 at its right) becomes a rounded groove, 1 on its centre line and 0 at its edges; where
// grooves cross, the deeper is kept (BlendOp Max). TrackGrooves draws every ribbon in its window with this shader, and
// First Chair/Snow Terrain reads the result as relief (SH_Env_SnowEffects.hlsl, SnowGrooves).
// Target: URP, every platform; drawn by a command buffer into an R8 render texture, outside the camera's passes.
// Cost: a few ALU a texel of ribbon, no texture samples. Variants: none.
Shader "Hidden/First Chair/Track Groove Stamp"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GrooveStamp"
            ZTest Always
            ZWrite Off
            Cull Off
            BlendOp Max
            Blend One One

            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment StampFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // World space to the groove texture's clip space: TrackGrooves' top-down view and the GPU projection for
            // rendering into a texture, so the texture's (0, 0) is the window's least X and Z on every platform.
            float4x4 _FCGrooveStampMatrix;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float across : TEXCOORD0;
            };

            Varyings StampVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = mul(_FCGrooveStampMatrix, float4(positionWS, 1.0));
                output.across = input.uv.x * 2.0 - 1.0;   // -1 at the left edge, 0 on the centre line, 1 at the right
                return output;
            }

            half4 StampFragment(Varyings input) : SV_Target
            {
                // A rounded groove, 1 - across squared: deepest on the centre line, its walls steepest at the edges.
                half depth = saturate(1.0h - (half)(input.across * input.across));
                return half4(depth, depth, depth, depth);
            }
            ENDHLSL
        }
    }
}
