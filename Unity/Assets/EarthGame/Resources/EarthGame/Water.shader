// The water a founder sees (M1.4g, 2026-09-20). Until this shader the water was URP Lit, opaque, at smoothness 0.92,
// which made a lake a blue lid over its own bed and — because a near-mirror shows the environment cubemap, which Unity
// builds from the sky once and nothing in this game refreshes — left the sea the brightest thing in the frame at half
// past seven at night, while every rough surface had gone dark with the sun.
//
// Two decisions follow from that, and both keep one owner for a fact the game already holds:
//
//  * How much water there is at a pixel is read from the scene's own depth, not from the mesh: the column between this
//    surface and whatever opaque thing lies behind it. That is the same number for a lake's plane, a four-metre creek
//    and the sea's plane, it lengthens as the eye looks along the water rather than down into it, as a real sightline
//    does, and it gives the soft line at the water's edge for nothing. The depth texture is on in PC_RPAsset and MSAA
//    is off, so the sample is the straightforward one.
//  * The light is the sky the rest of the game is lit by — the spherical harmonics SunAndSky writes each frame and the
//    main light — never a reflection probe. Water darkens with the country because it reads the same sky the ground
//    does, and no cubemap has to be refreshed for it.
//
// Beer-Lambert over that column does the rest: clear at nothing, nine tenths of the deep colour by _FullAtM metres of
// water travelled through. Non-goals kept out on purpose: ripples, refraction, foam, caustics, reflections of the land,
// and the view from underneath.
Shader "EarthGame/Water"
{
    Properties
    {
        _ShallowColour ("Water over a shallow bed", Color) = (0.42, 0.58, 0.55, 1)
        _DeepColour ("Water too deep to see through", Color) = (0.04, 0.13, 0.22, 1)
        _FullAtM ("Metres of water that hide nine tenths of the bed", Float) = 1.5
        _MostOpaque ("The most the deep water hides, 0 to 1", Range(0, 1)) = 0.95
        _SkyShown ("How much of the sky the surface gives back", Range(0, 3)) = 1.4
        _Glint ("How tight the sun's glint is", Float) = 220
        _GlintStrength ("How strong the sun's glint is", Range(0, 4)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColour;
                float4 _DeepColour;
                float _FullAtM;
                float _MostOpaque;
                float _SkyShown;
                float _Glint;
                float _GlintStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The water travelled through: the opaque scene behind this pixel, less this surface. Both are eye
                // distances, so the column lengthens when the eye looks along the water instead of down into it.
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-6);
                float behind = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float here = input.screenPos.w;
                float column = max(behind - here, 0.0);

                // Beer-Lambert: nine tenths of the bed hidden by _FullAtM metres. log(10) = 2.302585.
                float perMetre = 2.302585 / max(_FullAtM, 0.01);
                float hidden = saturate(1.0 - exp(-perMetre * column));

                float3 normalWS = float3(0, 1, 0);
                float3 toEye = normalize(GetCameraPositionWS() - input.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 sky = SampleSH(normalWS);
                half lit = saturate(dot(normalWS, sun.direction)) * sun.shadowAttenuation;
                half3 body = lerp(_ShallowColour.rgb, _DeepColour.rgb, hidden) * (sky + sun.color * lit);

                // Fresnel, by Schlick's approximation with water's normal-incidence reflectance of 0.02. Looking
                // straight down, water gives back a fiftieth of the sky and its bed is plain; looking along it, nearly
                // all of it, which is what tells an eye that a puddle is a puddle. Without this a creek of ankle-deep
                // water is honestly transparent and reads as ground (the frames of 2026-09-20, before this line).
                // What is given back is the sky the country is lit by, so it darkens with the country.
                float facing = saturate(dot(normalWS, toEye));
                float mirrored = 0.02 + 0.98 * pow(1.0 - facing, 5.0);
                half3 skyShown = sky * _SkyShown;

                // The sun's own glint on top. It rides on the sun, so it is gone with the sun and cannot light the sea
                // after dark.
                float3 halfway = normalize(sun.direction + toEye);
                half glint = pow(saturate(dot(normalWS, halfway)), _Glint) * _GlintStrength * sun.shadowAttenuation;
                half3 colour = lerp(body, skyShown, mirrored) + sun.color * glint;

                // What the water hides of its bed, what its surface gives back of the sky, and the glint: a still
                // surface over a bed you can see through is still a surface.
                half alpha = saturate(max(hidden * _MostOpaque, mirrored) + glint);
                return half4(MixFog(colour, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
