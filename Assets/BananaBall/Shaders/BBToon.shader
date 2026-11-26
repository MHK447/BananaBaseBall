// Cel shading with an inverted-hull outline for Show Ball (URP).
// Pass 1 (UniversalForwardOnly): two-tone ramp + rim. Pass 2 (SRPDefaultUnlit): outline.
// Disable the outline per material with Material.SetShaderPassEnabled("SRPDefaultUnlit", false).
Shader "BananaBall/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _ShadeColor ("Shade Tint", Color) = (0.70,0.68,0.84,1)
        _ShadeStep ("Shade Step", Range(-1,1)) = 0.08
        _ShadeSoft ("Shade Softness", Range(0.001,0.5)) = 0.035
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.2
        _RimStrength ("Rim Strength", Range(0,1)) = 0.16
        _OutlineColor ("Outline Color", Color) = (0.12,0.09,0.13,1)
        _OutlineWidth ("Outline Width (world)", Float) = 0.022
        _OutlineMax ("Outline Max (NDC)", Float) = 0.0062
        _Emission ("Flat Amount", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _MainTex_ST;
            float4 _ShadeColor;
            float _ShadeStep;
            float _ShadeSoft;
            float4 _RimColor;
            float _RimPower;
            float _RimStrength;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineMax;
            float _Emission;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pi = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pi.positionCS;
                o.positionWS = pi.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light l = GetMainLight();
                float ndl = dot(n, l.direction);
                float lit = smoothstep(_ShadeStep - _ShadeSoft, _ShadeStep + _ShadeSoft, ndl);
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float3 albedo = _BaseColor.rgb * i.color.rgb * tex.rgb;
                float3 lightCol = lerp(float3(1,1,1), l.color, 0.35);
                float3 col = lerp(albedo * _ShadeColor.rgb, albedo * lightCol, lit);
                float3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(saturate(1.0 - dot(n, vdir)), _RimPower) * _RimStrength;
                col += _RimColor.rgb * rim * (0.35 + 0.65 * lit);
                col = lerp(col, albedo, _Emission);
                return float4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float4 cs = TransformObjectToHClip(v.positionOS.xyz);
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
