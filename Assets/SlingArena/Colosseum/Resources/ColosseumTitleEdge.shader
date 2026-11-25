Shader "SlingArena/UI/Title Poster"
{
    Properties
    {
        [PerRendererData] _MainTex ("Arena artwork", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct AppData { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            Varyings vert(AppData v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                float2 edge = saturate(i.uv);
                float2 outside = abs(i.uv - edge);
                half4 c = tex2D(_MainTex, edge);
                if (max(outside.x, outside.y) > .0001)
                {
                    // Extend only a soft wash sampled from the source's own mip chain.
                    // Low frequencies replace the edge details within a few pixels.
                    float2 distancePx = outside * _MainTex_TexelSize.zw;
                    float distance = max(distancePx.x, distancePx.y);
                    float lod = min(8.0, log2(1 + distance * 1.8));
                    c = tex2Dlod(_MainTex, float4(edge, 0, lod));
                    c.rgb *= 1 - .08 * smoothstep(0, 72, distance);
                }
                return c*i.color;
            }
            ENDCG
        }
    }
}
