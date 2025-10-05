Shader "Unlit/VertexColorTextureTransparentOrCutout"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Toggle(_ALPHACLIP)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Clip Threshold", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ _ALPHACLIP
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 col : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.col = v.color; // 頂点カラーαを使用
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c *= i.col; // テクスチャ * 頂点カラー（α含む）

                #ifdef _ALPHACLIP
                clip(c.a - _Cutoff);
                #endif

                return c;
            }
            ENDCG
        }
    }
}
