// Vertex-colour lighting for Wacky Baseball (URP 17). Albedo = _BaseColor * vertex rgb * _MainTex; smoothness = (1 - vertex alpha) * _Gloss,
// so a plain colour (alpha 1) is matte. Receives and casts main-light shadows, takes ambient from the scene's ambient probe.
//   default: PBR, for the stadium and props
//   _TOON  : cel look for players, bats, balls and mascots: two tones with a soft terminator, cool shade tint, crisp highlight
//            and a rim. Globals _BBToon / _BBShade / _BBRim (Look.Apply) tune every toon material at once.
//   _GROUND: world-space detail noise everywhere and a mowing checker where uv2.x = 1 (grass)
//   _CROWD : seated fans bob with the global _BBCrowdExcite (uv2.x = phase, uv2.y = 0 seat / 1 body / 2 raised arms)
//   default and _TOON: uv2.y = emission strength (x albedo), for screens, lamps and LED ribbons
// Outline pass (inverted hull, comic ink line): back faces pushed out in screen space. Width = _OutlineWidth metres seen at the
// vertex depth, clamped to [_OutlineMinPx, _OutlineMaxPx] pixels at 1080p, times (1 - uv2.x) per vertex and _BBOutlineScale.
// It extrudes along tangent.xyz, where MeshKit.ToMesh(outline: true) bakes position-welded smooth normals so hard edges stay closed.
// _OutlineWidth = 0 turns it off (Mats also disables the pass).
Shader "BananaBall/Lit"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Gloss ("Max Smoothness", Range(0,1)) = 0.9
        _Metallic ("Metallic", Range(0,1)) = 0
        _Emission ("Emission Scale", Float) = 1
        _DetailTex ("Ground Detail", 2D) = "gray" {}
        _DetailScale ("Detail Tiling (per m)", Float) = 0.35
        _StripeSize ("Mowing Stripe (m)", Float) = 5.5
        _StripeContrast ("Stripe Contrast", Float) = 0.11
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0
        _Wrap ("Light Wrap", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _OutlineWidth ("Outline Width (m, 0 = off)", Float) = 0
        _OutlineMinPx ("Outline Min (px at 1080p)", Float) = 1.25
        _OutlineMaxPx ("Outline Max (px at 1080p)", Float) = 4.6
        _OutlineColor ("Outline Color", Color) = (0.05, 0.04, 0.06, 1)
        _OutlineTint ("Outline Albedo Tint", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "UniversalMaterialType"="Lit" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _MainTex_ST;
            float4 _DetailTex_ST;
            float4 _OutlineColor;
            float _Gloss;
            float _Metallic;
            float _Emission;
            float _DetailScale;
            float _StripeSize;
            float _StripeContrast;
            float _Cutoff;
            float _Wrap;
            float _Cull;
            float _OutlineWidth;
            float _OutlineMinPx;
            float _OutlineMaxPx;
            float _OutlineTint;
        CBUFFER_END

        TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
        TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
        float _BBCrowdExcite;
        float _BBOutlineScale;
        float4 _BBInkPush; // x metres, y metres per metre of depth: the ink hull sits that far behind the surface, so it shows on
                           // silhouettes but not through folds (crouched knees, bent elbows)
        float4 _BBToon;    // x terminator (N.L), y softness, z form gradient inside the lit tone, w sun spill into the shade
        float4 _BBShade;   // rgb shade tint, w sun scale on the lit tone
        float4 _BBRim;     // rgb rim colour, w where the rim starts (1 - N.V)

        float3 CrowdBob(float3 posOS, float2 uv2)
        {
            float excite = saturate(_BBCrowdExcite);
            float t = _Time.y * (1.6 + 6.0 * excite) + uv2.x * 6.2831;
            float hop = max(0.0, sin(t));
            float amt = min(uv2.y, 1.0);
            float arms = saturate(uv2.y - 1.0);
            float bob = (0.015 + 0.16 * excite) * hop * amt + arms * (0.04 + 0.28 * excite) * hop;
            float sway = sin(t * 0.45 + uv2.x * 3.1) * 0.025 * amt;
            return posOS + float3(sway, bob, 0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _GROUND _CROWD
            #pragma multi_compile_local _ _TOON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                float2 uv : TEXCOORD2;
                float2 uv2 : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz;
                #if defined(_CROWD)
                p = CrowdBob(p, v.uv2);
                #endif
                VertexPositionInputs pi = GetVertexPositionInputs(p);
                o.positionCS = pi.positionCS;
                o.positionWS = pi.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv2 = v.uv2;
                o.fogFactor = ComputeFogFactor(pi.positionCS.z);
                return o;
            }

            // Cel shading: lit tone and shade tone split at the terminator (and at cast-shadow edges), a crisp
            // highlight that shrinks as the surface gets glossier, a little sky in glossy shells, and a rim.
            half3 ToonLight(half3 albedo, half smooth, float3 n, float3 posWS, float4 shadowCoord)
            {
                float4 tn = _BBToon.y > 0.0 ? _BBToon : float4(0.02, 0.06, 0.35, 0.45);
                float4 sd = _BBShade.w > 0.0 ? _BBShade : float4(0.72, 0.76, 0.93, 0.85);
                float4 rm = _BBRim.w > 0.0 ? _BBRim : float4(0.22, 0.22, 0.2, 0.62);

                Light ml = GetMainLight(shadowCoord, posWS, half4(1, 1, 1, 1));
                float3 v = GetWorldSpaceNormalizeViewDir(posWS);
                half ndl = dot(n, ml.direction);
                half band = smoothstep(tn.x - tn.y, tn.x + tn.y, ndl);
                half lit = band * smoothstep(0.2, 0.8, ml.shadowAttenuation);
                half3 sky = SampleSH(n);
                half3 sun = ml.color;
                half form = lerp(1.0, 0.78 + 0.32 * saturate(ndl), tn.z);
                half3 litC = albedo * (sun * sd.w * form + sky * 0.5);
                // skin (and leather): a warm shade instead of the cool one, and a warm glow along the terminator, like light
                // scattered under the skin; picked from the albedo (warm, not too saturated), so uniforms and team colours keep theirs
                half rb = albedo.r - albedo.b;
                half warm = saturate(rb * 4.0 - 0.3) * saturate(1.6 - rb * 2.5) * saturate((albedo.r - albedo.g) * 6.0 + 0.2);
                half3 shadeTint = lerp(sd.rgb, half3(1.0, 0.8, 0.76), warm * 0.75);
                half3 shadeC = albedo * (sky * 0.95 + sun * tn.w * 0.35) * shadeTint;
                half3 c = lerp(shadeC, litC, lit);
                c += albedo * sun * half3(0.22, 0.06, 0.02) * (warm * band * (1.0 - band) * 4.0 * smoothstep(0.2, 0.8, ml.shadowAttenuation));

                float3 h = SafeNormalize(ml.direction + v);
                half nh = saturate(dot(n, h));
                half thr = lerp(0.9, 0.993, smooth);
                c += sun * (smoothstep(thr - 0.004, thr + 0.004, nh) * smooth * smooth * 0.6 * lit);

                half nv = saturate(dot(n, v));
                half fres = pow(1.0 - nv, 4.0);
                c += GlossyEnvironmentReflection(reflect(-v, n), 1.0 - smooth, 1.0) * ((0.04 + 0.5 * fres) * smooth * smooth);

                half rim = smoothstep(rm.w, rm.w + 0.08, 1.0 - nv);
                c += rm.rgb * (rim * (0.3 + 0.7 * lit) * saturate(n.y * 0.5 + 0.7));
                return c;
            }

            half4 frag (Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a - _Cutoff);
                half3 albedo = _BaseColor.rgb * i.color.rgb * tex.rgb;
                half smooth = saturate(1.0 - i.color.a) * _Gloss;
                half3 emission = half3(0, 0, 0);
                float3 n = SafeNormalize(i.normalWS);
                n = IS_FRONT_VFACE(face, true, false) ? n : -n;

                #if defined(_GROUND)
                    float2 wp = i.positionWS.xz;
                    half d1 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * _DetailScale).r;
                    half d2 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * (_DetailScale * 0.113) + 0.37).r;
                    half detail = lerp(0.84, 1.16, d1) * lerp(0.88, 1.12, d2);
                    float2 q = float2(wp.x + wp.y, wp.y - wp.x) * (0.70710678 / _StripeSize);
                    float2 sw = sin(q * 6.2831853);
                    float2 fw = max(fwidth(q) * 6.2831853 * 1.5, 1e-4);
                    float2 sq = clamp(sw / fw, -1.0, 1.0);
                    half chk = 0.5 + 0.5 * sq.x * sq.y;
                    half stripe = lerp(1.0 - _StripeContrast, 1.0 + _StripeContrast, chk);
                    albedo *= lerp(detail, detail * stripe, saturate(i.uv2.x));
                #elif defined(_CROWD)
                    albedo *= 0.92 + 0.16 * frac(i.uv2.x * 7.31);
                #else
                    emission = albedo * i.uv2.y * _Emission;
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                half4 c;

                #if defined(_TOON)
                    c = half4(ToonLight(albedo, smooth, n, i.positionWS, shadowCoord) + emission, 1.0);
                #else
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.positionCS = i.positionCS;
                    inputData.normalWS = n;
                    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                    inputData.shadowCoord = shadowCoord;
                    inputData.fogCoord = fogCoord;
                    inputData.bakedGI = SampleSH(n);
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    inputData.shadowMask = half4(1, 1, 1, 1);

                    SurfaceData s = (SurfaceData)0;
                    s.albedo = albedo;
                    s.metallic = _Metallic;
                    s.specular = half3(0, 0, 0);
                    s.smoothness = smooth;
                    s.normalTS = half3(0, 0, 1);
                    s.emission = emission;
                    s.occlusion = 1;
                    s.alpha = 1;

                    c = UniversalFragmentPBR(inputData, s);
                    if (_Wrap > 0)
                    {
                        // soft wrap for cloth and skin: a little light leaks past the terminator
                        Light ml = GetMainLight(shadowCoord);
                        half w = saturate((dot(n, ml.direction) + _Wrap) / (1.0 + _Wrap)) - saturate(dot(n, ml.direction));
                        c.rgb += albedo * ml.color * w * 0.35 * ml.shadowAttenuation;
                    }
                #endif

                c.rgb = MixFog(c.rgb, fogCoord);
                c.a = 1;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            // URP draws SRPDefaultUnlit next to UniversalForward for every opaque renderer, so the hull needs no extra material.
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_fog

            struct AttributesO
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                float2 uv2 : TEXCOORD1;
            };

            struct VaryingsO
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                float fogFactor : TEXCOORD0;
            };

            VaryingsO vertOutline (AttributesO v)
            {
                VaryingsO o = (VaryingsO)0;
                float3 nOS = dot(v.tangentOS.xyz, v.tangentOS.xyz) > 0.25 ? v.tangentOS.xyz : v.normalOS;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(nOS);
                float3 posVS = TransformWorldToView(posWS);
                posVS.z -= _BBInkPush.x + _BBInkPush.y * max(0.0, -posVS.z);
                float4 cs = TransformWViewToHClip(posVS);

                // push sideways in screen space by a pixel width: thick near, thin far, never vanishing
                float2 dir = mul((float3x3)GetWorldToHClipMatrix(), nWS).xy * _ScreenParams.xy;
                float dl = length(dir);
                dir = dl > 1e-5 ? dir / dl : float2(0.0, 0.0);
                float4x4 proj = UNITY_MATRIX_P;
                float res = min(_ScreenParams.x, _ScreenParams.y) / 1080.0;
                float ppm = 0.5 * _ScreenParams.y * abs(proj._m11) / max(cs.w, 0.05);   // pixels per metre at this depth
                float scale = _BBOutlineScale > 0.0 ? _BBOutlineScale : 1.0;
                float px = clamp(_OutlineWidth * ppm, _OutlineMinPx * res, _OutlineMaxPx * res) * scale * saturate(1.0 - v.uv2.x);
                cs.xy += dir * (px * 2.0) / _ScreenParams.xy * cs.w;

                o.positionCS = _OutlineWidth > 0.0 ? cs : float4(2.0, 2.0, 2.0, 1.0);
                half3 alb = _BaseColor.rgb * v.color.rgb;
                o.color = lerp(_OutlineColor.rgb, alb * 0.12, _OutlineTint);
                o.fogFactor = ComputeFogFactor(cs.z);
                return o;
            }

            half4 fragOutline (VaryingsO i) : SV_Target
            {
                return half4(MixFog(i.color, i.fogFactor), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _GROUND _CROWD
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz;
                #if defined(_CROWD)
                p = CrowdBob(p, v.uv2);
                #endif
                float3 pw = TransformObjectToWorld(p);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 ld = normalize(_LightPosition - pw);
                #else
                float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pw, nw, ld));
                o.positionCS = ApplyShadowClamping(cs);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _GROUND _CROWD

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz;
                #if defined(_CROWD)
                p = CrowdBob(p, v.uv2);
                #endif
                o.positionCS = TransformObjectToHClip(p);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
