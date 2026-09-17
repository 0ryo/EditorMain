Shader "Hidden/EditorMain/ObjectHoverOutline"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "black" {}
        _OutlineColor ("Outline color", Color) = (1, 0.48, 0.04, 1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float center = tex2D(_MainTex, i.uv).r;
                float edge = 0;
                [unroll] for (int x = -1; x <= 1; x++)
                [unroll] for (int y = -1; y <= 1; y++)
                    edge = max(edge, tex2D(_MainTex, i.uv + float2(x,y) * _MainTex_TexelSize.xy * 2).r);
                return float4(_OutlineColor.rgb, _OutlineColor.a * saturate(edge-center));
            }
            ENDHLSL
        }
    }
}
