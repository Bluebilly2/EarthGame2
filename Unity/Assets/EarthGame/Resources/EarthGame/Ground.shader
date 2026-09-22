// The ground with its own grain (M1.6e): a terrain shader of this project's own, in place of the pipeline's. The
// tile's colour map (M1.4d: one picture stretched once over the kilometre, two texels a post) is multiplied by a grain
// made here from the world position — a fine scale and a coarser one — and the grain's slope tilts the normal so the
// sun shows it. The stock terrain layer could carry the map or a tiled detail but not both without averaging them, which
// washed the colour out (DEBTS 2026-09-10); made in the shader there is nothing to average. The grain fades out by
// _GrainSeenM so the far ground stays the map's colour and never shimmers. Lit as the stand is (StandLit): the sun, the
// sky and the sun's shadows, no specular, fogged. The terrain draws non-instanced with one layer given all the weight,
// so of the terrain system's properties only _Splat0 and its _ST are read.
Shader "EarthGame/Ground"
{
    Properties
    {
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0 (R)", 2D) = "grey" {}
        _Grain ("Grain: the share of the colour's brightness it moves, 0 to 1", Range(0, 1)) = 0.18
        _FineM ("Grain: the finest scale, m", Float) = 0.12
        _GrainM ("Grain: the middle scale, m", Float) = 0.4
        _ClodM ("Grain: the coarse scale, m", Float) = 1.7
        _GrainSeenM ("Grain: faded out by this distance, m", Float) = 110
        _Relief ("Grain: how far it tilts the normal, 0 to 1", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "TerrainCompatible" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Grain;
            float _FineM;
            float _GrainM;
            float _ClodM;
            float _GrainSeenM;
            float _Relief;
        CBUFFER_END

        // Set by the terrain for its one layer: the map's tiling over the terrain's own 0..1 texcoord.
        CBUFFER_START(_Terrain)
            half4 _Splat0_ST;
        CBUFFER_END

        TEXTURE2D(_Splat0);
        SAMPLER(sampler_Splat0);

        // A number in [0, 1) from a lattice point, the same for the same point on every machine that draws it.
        float Hash(float2 p)
        {
            float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
            q += dot(q, q.yzx + 33.33);
            return frac((q.x + q.y) * q.z);
        }

        // Value noise in [-1, 1]: the lattice's hashes blended with a smooth step, so it has no seams and a gentle slope.
        float Noise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
            return 2.0 * lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) - 1.0;
        }

        // How much of a scale a pixel can show: all of it while the pixel's footprint on the ground is small against the
        // scale, none once the footprint is two thirds of it, so no scale is drawn finer than the screen and nothing shimmers.
        float Shown(float footprintM, float scaleM)
        {
            return saturate(1.5 - 2.25 * footprintM / scaleM);
        }

        // The grain at a point of the ground: three scales, the finest sharp-edged (a ridge, as clods and grains are lit on one
        // side), each shown as far as the pixel's footprint allows, weighted, in about [-1, 1].
        float Grain(float2 xz, float footprintM)
        {
            float fine = 1.0 - 2.0 * abs(Noise(xz / _FineM));
            return 0.45 * fine * Shown(footprintM, _FineM) + 0.35 * Noise(xz / _GrainM) * Shown(footprintM, _GrainM) + 0.2 * Noise(xz / _ClodM) * Shown(footprintM, _ClodM);
        }

        // How much of the grain is shown at a distance from the eye: all of it near, none at _GrainSeenM.
        float Seen(float3 positionWS)
        {
            return saturate(1.0 - distance(positionWS, _WorldSpaceCameraPos.xyz) / _GrainSeenM);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.texcoord * _Splat0_ST.xy + _Splat0_ST.zw;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 colour = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, input.uv).rgb;
                float3 normalWS = normalize(input.normalWS);
                // A uniform branch: with the grain flattened (-eg-hide grain) none of its work is done, so what it costs the
                // frame is what the switch measures, not only what it shows.
                if (_Grain > 0.0)
                {
                    float seen = Seen(input.positionWS) * _Grain;
                    float2 xz = input.positionWS.xz;
                    // The pixel's footprint on the ground, m: what the screen can show of the grain here.
                    float2 dxz = fwidth(xz);
                    float footprint = max(dxz.x, dxz.y);
                    float g = Grain(xz, footprint) * seen;
                    // Brightness first; then the troughs a touch darker and warmer, as damp hollows and shadowed clods are.
                    colour *= 1.0 + g;
                    colour = lerp(colour, colour * half3(1.04, 0.97, 0.90), saturate(-g * 3.0));
                    // The grain's slope tilts the normal: finite differences a hand's breadth apart, scaled by the relief.
                    float step = 0.08;
                    float2 slope = float2(Grain(xz + float2(step, 0), footprint) - Grain(xz - float2(step, 0), footprint),
                                          Grain(xz + float2(0, step), footprint) - Grain(xz - float2(0, step), footprint)) * (_Relief * Seen(input.positionWS) / (2.0 * step) * 0.08);
                    normalWS = normalize(normalWS - float3(slope.x, 0, slope.y));
                }
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 sky = SampleSH(normalWS);
                half lit = saturate(dot(normalWS, sun.direction)) * sun.shadowAttenuation;
                half3 lit3 = colour * (sky + sun.color * lit);
                return half4(MixFog(lit3, input.fog), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings VertShadow(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 FragShadow(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings VertDepth(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half FragDepth(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VertNormals
            #pragma fragment FragNormals

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings VertNormals(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 FragNormals(Varyings input) : SV_Target
            {
                return half4(normalize(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
