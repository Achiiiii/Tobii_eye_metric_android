// V3 home: the headline seen through the lens. _MainTex is the sharp coverage, _Tex1-3 the same blurred more and
// more (sigma 1.2, 2.4, 3.6 px). The blur grows with each pixel's distance from the focus (_FocusU, in the texture's
// u) along the word (_SpanU wide), as the design: blur = min(4.5, max(0, d / span - .06) * 9) px, sigma = .8 blur -
// continuous across the word, so the letters' windows never show a seam.
// The letters are drawn from that coverage as layers, back to front: a dark side (the coverage shifted by
// _SideOffset px, for a letter standing on the light box), an outline round the outside (_StrokePx wide) and the face;
// each layer has its colour and alpha (alpha 0 = not drawn). The vertex alpha fades the whole.
Shader "V3/LensText"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sharp", 2D) = "black" {}
        _Tex1 ("Blur 1", 2D) = "black" {}
        _Tex2 ("Blur 2", 2D) = "black" {}
        _Tex3 ("Blur 3", 2D) = "black" {}
        _FocusU ("Focus (u)", Float) = 0.5
        _SpanU ("Word width (u)", Float) = 0.9
        _Px ("Texel (u, v)", Vector) = (0.001, 0.004, 0, 0)
        _Face ("Face", Color) = (0.227, 0.239, 0.263, 1)
        _Stroke ("Outline", Color) = (0, 0, 0, 0)
        _StrokePx ("Outline width (px)", Float) = 0
        _Side ("Side", Color) = (0, 0, 0, 0)
        _SideOffset ("Side offset (px, x right, y down)", Vector) = (2.5, 3.5, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _Tex1, _Tex2, _Tex3;
            float _FocusU, _SpanU, _StrokePx;
            float4 _Px, _Face, _Stroke, _Side, _SideOffset;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            // the coverage at a blur level (0-3), from the two nearest prepared levels
            float Cov(float2 uv, float l)
            {
                if (l < 1.0)
                    return lerp(tex2D(_MainTex, uv).r, tex2D(_Tex1, uv).r, l);
                if (l < 2.0)
                    return lerp(tex2D(_Tex1, uv).r, tex2D(_Tex2, uv).r, l - 1.0);
                return lerp(tex2D(_Tex2, uv).r, tex2D(_Tex3, uv).r, saturate(l - 2.0));
            }

            float4 Over(float4 under, float3 rgb, float a)
            {
                float outA = a + under.a * (1.0 - a);
                float3 c = (rgb * a + under.rgb * under.a * (1.0 - a)) / max(outA, 1e-4);
                return float4(c, outA);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float blur = min(4.5, max(0.0, abs(i.uv.x - _FocusU) / _SpanU - 0.06) * 9.0);
                float l = min(3.0, blur * 0.8 / 1.2);
                float face = Cov(i.uv, l);
                float4 c = float4(0, 0, 0, 0);
                if (_Side.a > 0.0)
                {
                    float side = Cov(i.uv - _Px.xy * float2(_SideOffset.x, -_SideOffset.y), l);
                    c = Over(c, _Side.rgb, side * _Side.a);
                }
                if (_Stroke.a > 0.0 && _StrokePx > 0.0)
                {
                    float2 d = _Px.xy * _StrokePx;
                    float grown = face;
                    grown = max(grown, Cov(i.uv + d * float2(1.0, 0.0), l));
                    grown = max(grown, Cov(i.uv + d * float2(-1.0, 0.0), l));
                    grown = max(grown, Cov(i.uv + d * float2(0.0, 1.0), l));
                    grown = max(grown, Cov(i.uv + d * float2(0.0, -1.0), l));
                    grown = max(grown, Cov(i.uv + d * float2(0.707, 0.707), l));
                    grown = max(grown, Cov(i.uv + d * float2(-0.707, 0.707), l));
                    grown = max(grown, Cov(i.uv + d * float2(0.707, -0.707), l));
                    grown = max(grown, Cov(i.uv + d * float2(-0.707, -0.707), l));
                    c = Over(c, _Stroke.rgb, saturate(grown - face) * _Stroke.a);
                }
                c = Over(c, _Face.rgb, face * _Face.a);
                return fixed4(c.rgb, c.a * i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
