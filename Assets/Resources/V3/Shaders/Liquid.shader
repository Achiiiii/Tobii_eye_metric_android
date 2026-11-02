// V3: the lit part of a progress guide as flowing liquid: a body darker below and lighter on top, and two layers of
// soft currents running forward at different speeds (no highlights). Along a straight guide (local x forward, uv.y
// up) or round an arc (_Radial: an image _Size px square, centred); _Dir is the way it fills. Drawn in the vertex
// colour.
Shader "V3/Liquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Radial ("Along an arc", Float) = 0
        _Dir ("Forward direction", Float) = 1
        _Size ("Arc image size (px)", Float) = 100
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

            sampler2D _MainTex;
            float _Radial, _Dir, _Size;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 local : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.local = v.vertex.xy;
                return o;
            }

            float Hash(float n) { return frac(sin(n) * 43758.5453); }
            float Noise(float x)
            {
                float i = floor(x), f = frac(x);
                return lerp(Hash(i), Hash(i + 1.0), f * f * (3.0 - 2.0 * f));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                // s: px along the guide, forward positive; up: 0 at the bottom of the body, 1 at its top
                float s, up;
                if (_Radial > 0.5)
                {
                    float2 p = (i.uv - 0.5) * _Size;
                    s = atan2(p.y, p.x) * length(p) * _Dir;
                    up = 0.6;
                }
                else
                {
                    s = i.local.x * _Dir;
                    up = i.uv.y;
                }
                float t = _Time.y;
                float3 c = i.color.rgb * lerp(0.74, 1.12, up);
                // two currents
                float n1 = Noise(s * 0.045 - t * 2.2);
                float n2 = Noise(s * 0.13 - t * 4.1 + up * 1.7);
                c *= 0.84 + 0.34 * (n1 * 0.6 + n2 * 0.4);
                return fixed4(saturate(c), tex.a * i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
