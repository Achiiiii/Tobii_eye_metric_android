// V3: the focus layer (V3Focus) - a full-screen image that shows what is drawn under it out of focus, by _Radius px
// (a disc of taps on the grabbed screen), under a veil of the light box's white (_Veil, its alpha how much). At 0 it
// is switched off; page changes run it from soft to sharp, and cards hold it soft behind them.
Shader "V3/FocusBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Radius ("Blur radius (px)", Float) = 0
        _Veil ("Veil", Color) = (0.95, 0.96, 0.98, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        GrabPass { "_V3FocusGrab" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _V3FocusGrab;
            float4 _V3FocusGrab_TexelSize;
            float _Radius;
            float4 _Veil;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float4 grab : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grab = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.grab.xy / i.grab.w;
                float2 px = _V3FocusGrab_TexelSize.xy * _Radius;
                // 16 taps on a golden-angle spiral filling the disc
                float3 sum = tex2D(_V3FocusGrab, uv).rgb;
                for (int k = 1; k < 16; k++)
                {
                    float r = sqrt(k / 15.0), a = k * 2.39996;
                    sum += tex2D(_V3FocusGrab, uv + float2(cos(a), sin(a)) * r * px).rgb;
                }
                float3 col = lerp(sum / 16.0, _Veil.rgb, _Veil.a);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
