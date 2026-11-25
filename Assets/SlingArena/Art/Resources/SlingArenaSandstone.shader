Shader "SlingArena/Blender Sandstone"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "PaintedStone"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 swatch : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fog : TEXCOORD3;
                half4 swatch : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = p.positionWS;
                output.positionCS = p.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(p);
                output.fog = ComputeFogFactor(output.positionCS.z);
                output.swatch = input.swatch;
                return output;
            }

            float GrainHash(float2 p)
            {
                float3 k = frac(float3(p.xyx) * float3(.1031, .1013, .0973));
                k += dot(k, k.yzx + 33.33);
                return frac((k.x + k.y) * k.z);
            }

            float Grain(float2 p)
            {
                float2 tile = floor(p);
                float2 u = frac(p);
                u = u * u * (3.0 - 2.0 * u);
                return lerp(lerp(GrainHash(tile), GrainHash(tile + float2(1, 0)), u.x),
                    lerp(GrainHash(tile + float2(0, 1)), GrainHash(tile + 1), u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                #if defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #else
                    Light light = GetMainLight(input.shadowCoord);
                #endif
                float3 pos = input.positionWS;
                half sand = 1.0 - smoothstep(.18, .26, input.swatch.a);
                half metal = smoothstep(.74, .82, input.swatch.a);
                half3 pigment = input.swatch.rgb;
                half vegetation = smoothstep(.025, .13, min(pigment.g - pigment.r, pigment.b - pigment.r));
                half court = 1.0 - smoothstep(.86, .99, max(abs(pos.x) / 4.0, abs(pos.z) / 5.7));
                pigment = lerp(pigment, half3(.97, .77, .48), sand * court * .24);
                pigment = lerp(pigment, half3(pigment.r * 1.09 + .115, pigment.g * 1.17 + .082, pigment.b * .73), vegetation);
                #ifndef UNITY_COLORSPACE_GAMMA
                    pigment = SRGBToLinear(pigment);
                #endif

                // Fine world-space grains fade out as they fall below a pixel.
                float2 surface = pos.xz + pos.y * float2(.73, .43);
                float broad = Grain(surface * 1.23);
                float fine = Grain(surface * 20.0);
                float fineVisibility = 1.0 - smoothstep(.30, .90, max(fwidth(surface.x), fwidth(surface.y)) * 20.0);
                half variation = (broad - .5) * lerp(.075, .092, sand)
                    + (fine - .5) * fineVisibility * lerp(.035, .051, sand);
                pigment *= 1.0 + variation * (1.0 - metal);
                pigment *= 1.0 - sand * smoothstep(.46, 1.0, length(float2(pos.x / 4.2, pos.z / 6.2))) * .06;

                half ndotl = saturate(dot(n, light.direction));
                half contact = lerp(.58, 1.0, light.shadowAttenuation);
                half3 lightColor = lerp(half3(1.02, .98, .92), min(light.color, 1.28), .20);
                half3 diffuse = half3(.70, .70, .70) + lightColor * (ndotl * contact * .38);
                half3 halfway = SafeNormalize(light.direction + GetWorldSpaceViewDir(pos));
                half sheen = pow(saturate(dot(n, halfway)), 22.0) * metal * contact * .13;
                half lantern = smoothstep(.92, .98, input.swatch.a);
                half3 result = pigment * (diffuse + lantern * .23) + half3(1, .89, .72) * sheen;
                return half4(MixFog(result, input.fog), 1.0);
            }
            ENDHLSL
        }

        UsePass "SlingArena/Toy Surface/ShadowCaster"
    }

    Fallback Off
}
