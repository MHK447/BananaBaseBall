Shader "SlingArena/UI Contour"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Artwork ("Sample Artwork", Float) = 0
        _RivalColor ("Rival team", Color) = (1,0,0,1)
        _ScoreSplit ("Team health balance", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ COLOSSEUM_TEAM_SCORE
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Input
            {
                float4 position : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 contour : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 contour : TEXCOORD1;
                float4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            fixed4 _RivalColor;
            float _ScoreSplit;
            float _Artwork;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            Varyings vert(Input v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.position);
                o.color = v.color;
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace()) o.color.rgb = UIGammaToLinear(o.color.rgb);
                o.uv = v.uv;
                o.contour = v.contour;
                float4 bounds = clamp(_ClipRect, -2e10, 2e10);
                float2 pixel = o.position.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                o.mask = float4(v.position.xy * 2 - bounds.xy - bounds.zw,
                    .25 / (.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixel)));
                return o;
            }

            fixed4 frag(Varyings i) : SV_Target
            {
                // Distances are in corner radii. Their derivatives follow animation, shear and viewport scale.
                float2 q = abs(i.contour.xy) - i.contour.zw + 1;
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - 1;
                float pixel = max(length(float2(ddx(d), ddy(d))), 1e-5);
                fixed4 color = i.color;
                if (_Artwork > .5) color *= tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                #ifdef COLOSSEUM_TEAM_SCORE
                    // The divider leans 14 degrees. Its position follows the blue team's share of health.
                    float divider = i.contour.x - .249328 * i.contour.y - _ScoreSplit * i.contour.z;
                    float rival = saturate(.5 + divider / max(1e-5, 1.3 * fwidth(divider)));
                    float gloss = i.contour.y - .2 * i.contour.w;
                    color.rgb = lerp(color.rgb, _RivalColor.rgb, rival);
                    color.rgb = lerp(color.rgb, 1, .13 * saturate(.5 + gloss / max(1e-5, fwidth(gloss))));
                #endif
                color.a *= saturate(.5 - d / (1.3 * pixel));
                #ifdef UNITY_UI_CLIP_RECT
                    float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                    color.a *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - .001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
