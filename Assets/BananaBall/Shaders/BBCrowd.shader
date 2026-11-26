// Crowd: the toon ramp plus a vertex bob driven by a global excitement value.
// uv2.x = per-fan phase (0..1), uv2.y = bob amount (0 = seat, 1 = body, 2 = raised hands).
Shader "BananaBall/Crowd"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.72,0.70,0.86,1)
        _OutlineColor ("Outline Color", Color) = (0.13,0.10,0.15,1)
        _OutlineWidth ("Outline Width (world)", Float) = 0.02
        _OutlineMax ("Outline Max (NDC)", Float) = 0.003
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _ShadeColor;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineMax;
        CBUFFER_END

        float _BBCrowdExcite;

        float3 CrowdBob(float3 posOS, float2 uv2)
        {
            float excite = saturate(_BBCrowdExcite);
            float t = _Time.y * (2.2 + 5.0 * excite) + uv2.x * 6.2831;
            float hop = max(0.0, sin(t));
            float amt = min(uv2.y, 1.0);
            float hands = saturate(uv2.y - 1.0);
            float bob = (0.025 + 0.22 * excite) * hop * amt + hands * (0.05 + 0.35 * excite) * hop;
            float sway = sin(t * 0.5 + uv2.x * 3.0) * 0.03 * amt;
            return posOS + float3(sway, bob, 0);
        }
        ENDHLSL

        Pass
        {
            Name "CrowdForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 p = CrowdBob(v.positionOS.xyz, v.uv2);
                o.positionCS = TransformObjectToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light l = GetMainLight();
                float lit = smoothstep(0.05, 0.12, dot(n, l.direction));
                float3 albedo = _BaseColor.rgb * i.color.rgb;
                float3 col = lerp(albedo * _ShadeColor.rgb, albedo, lit);
                return float4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CrowdOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 p = CrowdBob(v.positionOS.xyz, v.uv2);
                float4 cs = TransformObjectToHClip(p);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float3 nCS = mul((float3x3)GetWorldToHClipMatrix(), nWS);
                float2 dir = nCS.xy;
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float2(0, 0);
                float w = max(cs.w, 0.05);
                float ndc = min(_OutlineWidth * UNITY_MATRIX_P[1][1] / w, _OutlineMax);
                float2 off = dir * ndc * w;
                off.x *= _ScreenParams.y / _ScreenParams.x;
                cs.xy += off;
                o.positionCS = cs;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                return float4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
