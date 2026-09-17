Shader "Hidden/EditorMain/ObjectHoverMask"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4x4 _HoverMVP;
            float4 vert(float4 vertex : POSITION) : SV_POSITION { return mul(_HoverMVP, vertex); }
            float4 frag() : SV_Target { return 1; }
            ENDHLSL
        }
    }
}
