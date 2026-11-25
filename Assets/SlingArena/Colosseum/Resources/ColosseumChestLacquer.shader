Shader "SlingArena/UI/Hinged Chest Lacquer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Chest painting", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Input { float4 position:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 position:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 local:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _ClipRect;
            int _UIVertexColorAlwaysGammaSpace;

            Varyings vert(Input i)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(i.position);
                o.local = i.position.xy;
                o.color = i.color;
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace()) o.color.rgb = UIGammaToLinear(o.color.rgb);
                o.uv = i.uv;
                return o;
            }

            // Keep the modelling, cast shadows and specular edges from the painted sprite.
            // Copper and gold have different hues even in shadow. Only the brass clasp stays gold.
            half3 Lacquer(half3 rgb, float2 px)
            {
                half shade = rgb.r;
                half hue = (rgb.g - rgb.b) / max(.025h, rgb.r - rgb.b);
                half metal = smoothstep(.485h, .60h, hue) * (1 - smoothstep(.13h, .34h, rgb.b / max(.02h, shade)));
                metal = max(metal, smoothstep(.64h, .78h, hue));

                half v = saturate((shade - .11h) / .89h);
                half3 emerald = lerp(half3(.047,.23,.26), half3(.037,.62,.55), saturate(v * 1.48h));
                emerald = lerp(emerald, half3(.18,.89,.69), smoothstep(.49h,.95h,v));
                emerald = lerp(emerald, half3(.80,1.0,.91), smoothstep(.26h,.67h,rgb.b) * v);
                // A broad neutral reflection reads as polished steel at the scale of a lobby reward.
                half3 steel = lerp(half3(.17,.29,.35), half3(.57,.73,.77), smoothstep(.12h,.82h,v));
                steel = lerp(steel, half3(1.0,1.0,.96), smoothstep(.48h,.99h,rgb.g));
                half3 paint = lerp(emerald, steel, metal);
                // The narrow exposed edge picks up blue-green ambient light from the stage.
                half3 rim = lerp(half3(.14,.29,.31), half3(.09,.21,.24), saturate((px.x + px.y - 140.0) / 220.0));
                paint = lerp(rim, paint, smoothstep(.065h,.24h,shade));
                float2 latchDistance = abs(px - float2(107.4,137.8)) - float2(18.3,19.7) + 5.8;
                float lock = 1 - smoothstep(-.55,.75,length(max(latchDistance,0)) + min(max(latchDistance.x,latchDistance.y),0) - 5.8);
                return lerp(paint, rgb, lock);
            }

            // Small domed fasteners follow the two silver straps on the body.
            half3 Rivets(half3 paint, float2 p)
            {
                float row = p.y < 165.0 ? 147.5 : 184.0;
                float column = p.x < 108.0 ? 68.0 : 149.0;
                float2 d = p - float2(column + (row - 147.5) * .038, row);
                half socket = 1 - smoothstep(1.85,2.32,length(d - float2(.08,.22)));
                half head = 1 - smoothstep(1.37,1.78,length(d));
                half light = saturate(.66h - .19h * d.x - .31h * d.y);
                half3 silver = lerp(half3(.28,.43,.47),half3(.90,.98,.95),light);
                return lerp(lerp(paint, half3(.20,.37,.40), socket * .68h), silver, head);
            }

            fixed4 frag(Varyings i) : SV_Target
            {
                fixed4 c = i.color;
                if (i.uv.x >= 0)
                {
                    half4 paint = tex2D(_MainTex, i.uv);
                    half3 rgb = paint.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = LinearToGammaSpace(rgb);
                    #endif
                    float2 px = i.uv * float2(256,-256) + float2(0,256);
                    rgb = Rivets(Lacquer(rgb, px), px);
                    #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = GammaToLinearSpace(rgb);
                    #endif
                    c *= half4(rgb,paint.a);
                }
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
