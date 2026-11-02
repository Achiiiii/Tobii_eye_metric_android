// V3: a straight progress guide as a glass tube with green liquid in it. The tube is a capsule (_Size px, rounded
// ends): faint glass, a grey rim where the wall is seen edge on, and one fixed faint highlight along the top of the
// glass. The liquid (the vertex colour, a little darker below) fills it up to _Fill with a forward-bulging meniscus
// that tilts by _Slosh while it moves and settles after (V3Slosh drives both). A slight current runs through it.
Shader "V3/Tube"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Size ("Tube size (px)", Vector) = (200, 12, 0, 0)
        _Fill ("Fill", Range(0, 1)) = 0
        _Slosh ("Slosh (-1..1)", Float) = 0
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
            float4 _Size;
            float _Fill, _Slosh;

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

            float Hash(float n) { return frac(sin(n) * 43758.5453); }
            float Noise(float x)
            {
                float i = floor(x), f = frac(x);
                return lerp(Hash(i), Hash(i + 1.0), f * f * (3.0 - 2.0 * f));
            }

            float4 Over(float4 under, float3 rgb, float a)
            {
                float outA = a + under.a * (1.0 - a);
                return float4((rgb * a + under.rgb * under.a * (1.0 - a)) / max(outA, 1e-4), outA);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = _Size.xy;
                float2 px = i.uv * size;                       // px from the bottom left, y up
                float r = size.y * 0.5, wall = 1.3;
                // the capsule: distance to its outline (< 0 inside)
                float2 c = float2(clamp(px.x, r, size.x - r), r);
                float d = length(px - c) - r;
                float body = saturate(0.5 - d);
                float inner = saturate(0.5 - (d + wall));
                float4 col = float4(0.88, 0.91, 0.95, body * 0.35);
                // the wall seen edge on
                col = Over(col, float3(0.52, 0.57, 0.65), body * saturate(1.0 - abs(d + wall * 0.5) / wall) * 0.6);
                // the liquid: up to the meniscus, which bulges forward and tilts as it sloshes
                float across = (px.y - r) / max(r - wall, 1.0);     // -1 bottom .. 1 top
                float front = _Fill * size.x + 2.2 * (1.0 - across * across) - 1.2 + _Slosh * across * r * 1.4;
                float liquid = inner * saturate(front - px.x + 0.5) * step(0.001, _Fill);
                float t = _Time.y;
                float3 lc = i.color.rgb * lerp(0.76, 1.08, saturate(px.y / size.y));
                lc *= 0.94 + 0.1 * Noise(px.x * 0.05 - t * 2.0);
                col = Over(col, lc, liquid * i.color.a);
                // the glass's fixed highlight along the top
                float hl = saturate(1.0 - abs(px.y - size.y * 0.74) / 0.7) * inner
                         * saturate((px.x - r * 0.8) / 4.0) * saturate((size.x - r * 0.8 - px.x) / 4.0);
                col = Over(col, float3(1.0, 1.0, 1.0), hl * 0.45);
                return fixed4(col.rgb, col.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
