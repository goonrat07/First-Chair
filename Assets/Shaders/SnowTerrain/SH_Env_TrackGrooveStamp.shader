// SH_Env_TrackGrooveStamp.shader
// First Chair, written by Claude for Story 017 (production/epics/first-chair/story-017-ski-grooves.md); since Story 018
// (story-018-deformable-snow.md) it also stamps the skis' footprints, and the snow mesh reads what it stamps.
// Purpose: stamps the ski track ribbons and the skis' footprints into the groove texture from above. A ribbon's
// across-track coordinate (its u, 0 at its left edge and 1 at its right) becomes a rounded groove, 1 on its centre line
// and 0 from 80 percent of the way to its edges; where grooves cross, the deeper is kept (BlendOp Max). TrackGrooves
// draws every ribbon and footprint in its window with this shader, and the snow mesh near the rider (SnowSkin,
// SH_Env_SnowSkin.hlsl) pushes the snow down by the result: the rounded groove is its trench's floor.
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

            // The groove is flat from this fraction of the way to the ribbon's edge on. The terrain's bilinear read
            // spreads a groove about a texel (a third of a 9 cm track's half-width at 2,048 texels over 32 m), and that
            // spread follows the texels' staircase; kept inside the ribbon it stays under the ribbon's tint. A groove
            // steepest at the very edge showed a texel-long zipper there along tracks diagonal to the texels (story
            // 017's first cloud ride), where the staircase met the ribbon's smooth edge on screen.
            static const float GROOVE_INSET = 0.8;

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
                // A rounded groove, (1 - (across / inset)^2)^2: deepest on the centre line, its walls steepest about
                // halfway out, meeting the untouched snow with no slope at the inset.
                float spread = saturate(input.across * input.across / (GROOVE_INSET * GROOVE_INSET));
                half depth = (half)((1.0 - spread) * (1.0 - spread));
                return half4(depth, depth, depth, depth);
            }
            ENDHLSL
        }
    }
}
