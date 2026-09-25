// The air below the horizon where no land is drawn (M1.6h, 2026-09-25): a bowl round the eye, from the horizon down, beyond
// all the land the game holds, drawn in the haze's own colour (Haze.hlsl), the sky's at the horizon in each direction. The
// land fades into that colour with distance and wholly by its edge, so from any height it ends in the air. Before, the
// procedural sky's dark ground showed below the horizon past the land's end, a brown band under a line.
//
// It is drawn after the sky, tested against the land's depth and writing none of its own, so the land hides it and the sky
// cannot draw over it. Drawn with the opaque things, which the renderer's forced depth priming draws only where the colour
// pass lands on the very depth the depth pass wrote, it drew nothing at 90 km while it drew whole at 5 (frames of
// 2026-09-25); drawn after the sky, with no depth pass to agree with, it draws at any distance.
Shader "EarthGame/Horizon"
{
    SubShader
    {
        Tags { "Queue" = "Transparent-1" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Haze.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(EgHorizon(input.positionWS - _WorldSpaceCameraPos), 1.0);
            }
            ENDHLSL
        }
    }
}
