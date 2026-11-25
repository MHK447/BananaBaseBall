Shader "SlingArena/Toy Surface"
{
    Properties
    {
        _InkColor("Ink Color", Color) = (0.037, 0.025, 0.047, 1)
        _InkWidth("Ink Width", Float) = 0.019
        [HideInInspector] _InkMode("Ink Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ToyForward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _InkColor;
                float _InkWidth;
                float _InkMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positions.positionWS;
                output.positionCS = TransformWorldToHClip(positions.positionWS + normalWS * (_InkMode * _InkWidth));
                output.normalWS = normalWS;
                output.shadowCoord = GetShadowCoord(positions);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_InkMode > 0.5)
                    return half4(MixFog(_InkColor.rgb, input.fogFactor), 1);

                half3 normal = normalize(input.normalWS);
                #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light key = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #else
                    Light key = GetMainLight(input.shadowCoord);
                #endif

                half3 eye = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfAngle = SafeNormalize(key.direction + eye);
                half diffuse = saturate(dot(normal, key.direction) * 0.63 + 0.34);
                diffuse = lerp(diffuse, smoothstep(0.40, 0.63, diffuse), 0.21);
                half shade = lerp(0.56, 1.0, key.shadowAttenuation);
                half gloss = pow(saturate(dot(normal, halfAngle)), 24.0) * input.color.a * shade;
                half edge = pow(1.0 - saturate(dot(normal, eye)), 3.0) * input.color.a;
                half3 ambient = half3(0.62, 0.68, 0.77);
                half3 coloredKey = lerp(half3(0.91, 0.86, 0.76), min(key.color, 1.4), 0.27);
                half3 result = input.color.rgb * (ambient + coloredKey * diffuse * shade * 0.46);
                result += half3(0.93, 0.95, 1.0) * (gloss * 0.27 + edge * 0.033);
                return half4(MixFog(result, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - positionWS);
                #else
                    float3 direction = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
