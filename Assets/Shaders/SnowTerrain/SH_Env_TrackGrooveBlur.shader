// SH_Env_TrackGrooveBlur.shader
// First Chair, written by Claude for Story 018 (production/epics/first-chair/story-018-deformable-snow.md).
// Purpose: blurs the stamped track map (TrackGrooves) one axis at a time, so the snow mesh can raise the snow a ski
// pushed aside: where the blurred map reaches past the trench, the snow rises (SnowTrench.Offset). Seven taps a pass,
// their weights a Gaussian's from TrackGrooves (SnowTrench.GaussianWeights); taps step more than a texel apart for a
// wider rim, read between texels by the bilinear filter.
// Target: URP, every platform; drawn by a command buffer into R8 render textures, outside the camera's passes, over a
// quad already in clip space. Each texel reads the source at its own centre (from its pixel position), so the copy
// lines up texel for texel on every graphics API. Cost: 7 bilinear samples a texel. Variants: none.
Shader "Hidden/First Chair/Track Groove Blur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GrooveBlur"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex BlurVertex
            #pragma fragment BlurFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FCGrooveBlurSource);
            SAMPLER(sampler_FCGrooveBlurSource);
            float4 _FCGrooveBlurTexel;      // 1 / width, 1 / height of the source and the target (they match)
            float4 _FCGrooveBlurStep;       // xy one tap's step in UV: along X in the first pass, along Z in the second
            float4 _FCGrooveBlurWeights;    // the centre tap's weight, then each pair's out to three steps

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings BlurVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy, 0.5, 1.0);   // the quad is made in clip space
                return output;
            }

            half Tap(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_FCGrooveBlurSource, sampler_FCGrooveBlurSource, uv, 0).r;
            }

            half4 BlurFragment(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy * _FCGrooveBlurTexel.xy;
                float2 s = _FCGrooveBlurStep.xy;
                half sum = Tap(uv) * (half)_FCGrooveBlurWeights.x
                    + (Tap(uv + s) + Tap(uv - s)) * (half)_FCGrooveBlurWeights.y
                    + (Tap(uv + 2.0 * s) + Tap(uv - 2.0 * s)) * (half)_FCGrooveBlurWeights.z
                    + (Tap(uv + 3.0 * s) + Tap(uv - 3.0 * s)) * (half)_FCGrooveBlurWeights.w;
                return half4(sum, sum, sum, sum);
            }
            ENDHLSL
        }
    }
}
