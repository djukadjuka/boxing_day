// Full-screen VHS / analog-tape effect for the built-in pipeline, used via
// Graphics.Blit from VHSPostEffect.cs (camera OnRenderImage). Each effect is
// driven by a uniform so it can be toggled/tuned per-instance from the script.
// Time comes in as _VHSTime (unscaled) rather than the built-in _Time, so the
// wobble/noise keep animating regardless of Time.timeScale (e.g. while paused).
Shader "Hidden/VHS"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            float _VHSTime;
            float _ChromaticAberration; // horizontal RGB split, in UV units
            float _ScanlineIntensity;   // 0..1 strength of darkened scanlines
            float _ScanlineCount;       // number of scanlines down the screen
            float _NoiseIntensity;      // 0..1 static / grain amount
            float _WobbleIntensity;     // horizontal tape-warble amount, in UV units
            float _WobbleSpeed;         // warble animation speed
            float _Desaturation;        // 0..1 toward luminance (faded tape)
            float _VignetteIntensity;   // 0..1 darkened edges

            // Cheap hash -> [0,1) pseudo-noise from a 2D coord.
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // --- Horizontal tape warble: shift each row sideways over time.
                float wobble = sin((uv.y * 8.0 + _VHSTime * _WobbleSpeed)) *
                               sin((uv.y * 1.7 - _VHSTime * _WobbleSpeed * 0.6));
                uv.x += wobble * _WobbleIntensity;

                // --- Chromatic aberration: split the color channels horizontally.
                float ca = _ChromaticAberration;
                fixed4 col;
                col.r = tex2D(_MainTex, uv + float2( ca, 0.0)).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - float2( ca, 0.0)).b;
                col.a = 1.0;

                // --- Desaturation toward luminance (aged, washed-out tape).
                float luma = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb = lerp(col.rgb, luma.xxx, _Desaturation);

                // --- Scanlines: darken alternating horizontal lines.
                float scan = sin(uv.y * _ScanlineCount) * 0.5 + 0.5;
                col.rgb *= lerp(1.0, scan, _ScanlineIntensity);

                // --- Animated static / grain.
                float n = hash(i.uv * _MainTex_TexelSize.zw + frac(_VHSTime) * 100.0);
                col.rgb += (n - 0.5) * _NoiseIntensity;

                // --- Vignette: darken toward the screen edges.
                float2 d = i.uv - 0.5;
                float vig = 1.0 - dot(d, d) * (_VignetteIntensity * 2.0);
                col.rgb *= saturate(vig);

                return col;
            }
            ENDCG
        }
    }
}
