Shader "SlingArena/Colosseum Toon"
{
    Properties
    {
        _InkColor("Ink Color", Color) = (0.13, 0.08, 0.12, 1)
        _InkWidth("Ink Width", Float) = 0.02
        _InkPush("Ink Push", Float) = 0
        [HideInInspector] _InkMode("Ink Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        // A powered-up hero marks the screen where its body and its ink are drawn, and the bands of light round it
        // are drawn only where that mark is missing: outside its whole silhouette, never between its own parts.
        [HideInInspector] _StencilRef("Stencil Ref", Float) = 0
        [HideInInspector] _StencilComp("Stencil Comp", Float) = 8
        [HideInInspector] _StencilPass("Stencil Pass", Float) = 0
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite("Z Write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ColosseumForward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            Stencil
            {
                Ref [_StencilRef]
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

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
                float _InkPush;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float3 smoothOS : TEXCOORD3;
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
                // The outline shell is puffed out along the normals and can be sunk away from the camera a little, which
                // hides the slivers that otherwise show between parts lying flush against each other.
                // Hard-edged shapes carry a welded direction for the shell so it stays in one piece.
                float3 shellOS = dot(input.smoothOS, input.smoothOS) > 0.0001 ? input.smoothOS : input.normalOS;
                float3 shellWS = TransformObjectToWorldNormal(shellOS);
                float3 inkWS = positions.positionWS + shellWS * (_InkMode * _InkWidth) - UNITY_MATRIX_V[2].xyz * (_InkMode * _InkPush);
                output.positionCS = TransformWorldToHClip(inkWS);
                output.normalWS = normalWS;
                output.shadowCoord = GetShadowCoord(positions);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_InkMode > 0.5)
                    return half4(MixFog(_InkColor.rgb, input.fogFactor), _InkColor.a);

                half3 normal = normalize(input.normalWS);
                #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light key = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #else
                    Light key = GetMainLight(input.shadowCoord);
                #endif

                half3 eye = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfAngle = SafeNormalize(key.direction + eye);
                half gloss = input.color.a;

                // Two soft cartoon bands: a warm lit side and a cool violet shade side.
                half facing = dot(normal, key.direction);
                half lit = smoothstep(-0.10, 0.30, facing) * lerp(0.25, 1.0, key.shadowAttenuation);
                half3 shade = half3(0.62, 0.58, 0.78);
                half3 sun = half3(1.05, 1.02, 0.96);
                half3 result = input.color.rgb * lerp(shade, sun, lit);

                // Sky bounce on anything facing up keeps tops bright and readable from the high camera.
                result += input.color.rgb * saturate(normal.y) * 0.05;

                // Glossy highlight plus a bright rim so silhouettes pop off the sand.
                half highlight = smoothstep(0.55, 0.80, pow(saturate(dot(normal, halfAngle)), 22.0)) * gloss * lit;
                half rim = smoothstep(0.50, 0.95, 1.0 - saturate(dot(normal, eye))) * (0.25 + gloss * 0.75) * saturate(normal.y * 0.6 + 0.6);
                result += half3(1.0, 0.98, 0.92) * (highlight * 0.42 + rim * 0.20);
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
