Shader "SlingArena/Atmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Name "Marsh particles"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 style : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 uv : TEXCOORD0;
                half2 style : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.style = input.style;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half2 p = input.uv;
                half radius = dot(p, p);
                half halo = saturate(1.0 - radius);
                half alpha = halo * halo;
                if (input.style.x < .5)
                {
                    half seed = input.style.y * 12.0;
                    half grain = .66 + .15 * sin(p.x * 8.0 + p.y * 5.0 + seed)
                        + .18 * sin(p.x * 14.0 - p.y * 7.0 - seed);
                    alpha *= grain;
                }
                else if (input.style.x > 1.5)
                {
                    half2 axes = abs(p);
                    half flare = saturate(1.0 - (axes.x + axes.y + axes.x * axes.y * 8.0));
                    alpha = max(flare * flare, alpha * .14);
                }
                else
                {
                    half core = saturate(1.0 - radius * 9.0);
                    alpha = max(core * core, alpha * .38);
                }
                return half4(input.color.rgb, input.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
