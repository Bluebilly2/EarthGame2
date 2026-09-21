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
// water travelled through.
//
// The ripples came on 2026-09-21 (M1.4h, William's "ripples now"): four waves running downwind tilt the normal, and
// nothing else moves, so the wading and the depth column M1.4g made agree with the world are untouched. Their speeds
// are the deep-water dispersion's, their steepness the wind's (set by the client from the weather once a second),
// their time the game's awake seconds (set by the client), so a paused game's water stands with its world. Non-goals
// kept out on purpose: refraction, foam, caustics, reflections of the land, and the view from underneath.
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
        _Wind ("Wind: downwind east and north, and its speed m/s (set by the client)", Vector) = (1, 0, 0, 0)
        _RippleSeconds ("The ripples' time, s (set by the client while the game is awake)", Float) = 0
        _RippleSlope ("The ripples' steepest slope, at a full wind", Range(0, 0.3)) = 0.08
        _RippleCalm ("The ripples' slope in a calm", Range(0, 0.1)) = 0.012
        _RippleFullWindMs ("The wind at which the ripples are steepest, m/s", Float) = 8
        _RippleSeenFor ("Wavelengths of distance a ripple is drawn for before it fades", Float) = 120
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
                float4 _Wind;
                float _RippleSeconds;
                float _RippleSlope;
                float _RippleCalm;
                float _RippleFullWindMs;
                float _RippleSeenFor;
            CBUFFER_END

            // The four waves: wavelength m, how far off the downwind line (a blend towards across-wind), and the share of
            // the steepness each carries (the shares sum to one, so _RippleSlope is the field's steepest slope).
            static const float4 Ripples[4] = { float4(0.45, 0.35, 0.30, 0), float4(0.9, -0.25, 0.25, 0), float4(1.7, 0.15, 0.25, 0), float4(3.1, -0.1, 0.20, 0) };

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

                // The ripples (M1.4h): the waves' slopes at this pixel, summed, tilt the normal. Each wave travels at the
                // deep-water dispersion's speed (omega^2 = g k: a metre of wavelength at 1.25 m/s), the field is as steep
                // as the wind makes it, and each wave fades over the distance at which it would be finer than the pixels,
                // the short ones first, so the far water does not sparkle with aliasing.
                float2 downwind = length(_Wind.xy) > 1e-4 ? normalize(_Wind.xy) : float2(1, 0);
                float2 across = float2(-downwind.y, downwind.x);
                float steep = lerp(_RippleCalm, _RippleSlope, saturate(_Wind.z / max(_RippleFullWindMs, 0.1)));
                // The wind's gusts: patches of rougher and calmer water tens of metres across, carried downwind at the wind's
                // own speed — the cat's paws on a lake. Near, they vary the ripples; far, where no ripple is fine enough to
                // draw, they vary how much sky the surface gives back, which is what the eye sees of them from a distance.
                float along = dot(downwind, input.positionWS.xz) - _Wind.z * _RippleSeconds;
                float gust = 0.5 + 0.5 * sin(along * 0.1337) * sin(dot(across, input.positionWS.xz) * 0.0885 + 0.6 * sin(along * 0.0483));
                steep *= 0.55 + 0.9 * gust;
                float away = distance(GetCameraPositionWS(), input.positionWS);
                float2 slope = float2(0, 0);
                float unresolved = 0.0;
                [unroll]
                for (int w = 0; w < 4; w++)
                {
                    float wavelength = Ripples[w].x;
                    float2 dir = normalize(downwind + across * Ripples[w].y);
                    float k = 6.2831853 / wavelength;
                    float phase = k * dot(dir, input.positionWS.xz) - sqrt(9.81 * k) * _RippleSeconds;
                    float seen = saturate(1.0 - away / (wavelength * _RippleSeenFor));
                    unresolved += steep * Ripples[w].z * (1.0 - seen);
                    slope += dir * (steep * Ripples[w].z * seen * cos(phase));
                }
                float3 normalWS = normalize(float3(-slope.x, 1.0, -slope.y));
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
                // The ripples too fine to draw still roughen the surface: a rough surface seen at a grazing angle faces the eye
                // more than a flat one does, on average, by about its root-mean-square slope (0.7 of the steepness for a sum
                // of sines), and gives back that much less of the sky. Without this the far water, where every ripple has
                // faded, was the flat plate of sky the ripples were meant to break (the shore frame of 2026-09-21).
                float facing = saturate(dot(normalWS, toEye) + 0.7 * unresolved);
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
