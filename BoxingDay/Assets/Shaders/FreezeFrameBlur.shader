// Simple Kawase-style blur for the built-in pipeline, used via Graphics.Blit.
// Applied iteratively (with downsampling) over a captured freeze-frame.
Shader "Hidden/FreezeFrameBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Offset", Float) = 1.0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Offset;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 o = _MainTex_TexelSize.xy * _Offset;

                fixed4 c = tex2D(_MainTex, i.uv + float2( o.x,  o.y));
                c += tex2D(_MainTex, i.uv + float2(-o.x,  o.y));
                c += tex2D(_MainTex, i.uv + float2( o.x, -o.y));
                c += tex2D(_MainTex, i.uv + float2(-o.x, -o.y));

                return c * 0.25;
            }
            ENDCG
        }
    }
}
