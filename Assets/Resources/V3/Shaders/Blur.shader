// V3 home: one pass of a separable Gaussian blur on a coverage texture (red channel). _Step is one texel along the
// blur axis times sigma (in texels); 13 taps span +-3 sigma.
Shader "V3/Blur"
{
    Properties
    {
        _MainTex ("Coverage", 2D) = "black" {}
        _Step ("Axis step x sigma", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Step;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float sum = 0.0, weights = 0.0;
                for (int k = -6; k <= 6; k++)
                {
                    float w = exp(-0.125 * k * k);
                    sum += tex2D(_MainTex, i.uv + _Step.xy * (k * 0.5)).r * w;
                    weights += w;
                }
                float v = sum / weights;
                return fixed4(v, v, v, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
