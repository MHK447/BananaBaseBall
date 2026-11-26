// Procedural daytime sky for Wacky Baseball: zenith-to-horizon gradient, sun disc and halo from the main light, drifting fair-weather clouds.
Shader "BananaBall/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.20, 0.45, 0.86, 1)
        _Horizon ("Horizon", Color) = (0.72, 0.85, 0.96, 1)
        _Ground ("Below Horizon", Color) = (0.42, 0.47, 0.50, 1)
        _SunColor ("Sun", Color) = (1, 0.93, 0.80, 1)
        _SunSize ("Sun Size", Range(0.001, 0.1)) = 0.018
        _Halo ("Sun Halo", Range(0, 4)) = 1.2
        _CloudColor ("Cloud", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Shade", Color) = (0.70, 0.76, 0.86, 1)
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.46
        _CloudScale ("Cloud Scale", Float) = 1.6
        _CloudSpeed ("Cloud Speed", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Zenith, _Horizon, _Ground, _SunColor, _CloudColor, _CloudShade;
                float _SunSize, _Halo, _CloudCover, _CloudScale, _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += a * Noise(p); p = p * 2.03 + float2(17.1, 3.7); a *= 0.5; }
                return s;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float up = saturate(y);
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(up, 0.55));
                sky = y < 0 ? lerp(_Horizon.rgb, _Ground.rgb, saturate(-y * 6.0)) : sky;

                float3 sunDir = _MainLightPosition.xyz;
                float sd = saturate(dot(d, sunDir));
                half3 sun = _SunColor.rgb * (smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, sd) * 6.0 + pow(sd, 18.0) * 0.55 * _Halo + pow(sd, 4.0) * 0.12 * _Halo);

                // clouds on a flattened dome
                float h = max(y, 0.0) + 0.08;
                float2 uv = d.xz / h * (0.25 * _CloudScale) + _Time.y * _CloudSpeed * float2(1.0, 0.35);
                float n = Fbm(uv * 2.2);
                float cover = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.22, n);
                float shade = smoothstep(0.35, 0.9, Fbm(uv * 2.2 + float2(0.04, 0.03)) - n + 0.5);
                half3 cloud = lerp(_CloudColor.rgb, _CloudShade.rgb, shade * 0.8) * (1.0 + pow(sd, 6.0) * 0.4);
                float fade = smoothstep(0.0, 0.18, y);
                half3 col = lerp(sky, cloud, cover * fade * 0.92) + sun * (1.0 - cover * 0.85 * fade);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
