// V3: a straight progress guide as a glass tube with green liquid in it. The tube is a capsule (_Size px, rounded
// ends): faint glass, a grey rim where the wall is seen edge on, and one fixed faint highlight along the top of the
// glass. The liquid (the vertex colour, a little darker below) fills it up to _Fill with a forward-bulging meniscus
// that tilts by _Slosh while it moves, bobs by _Bob (px) when it rises and settles after; a bright edge along the
// meniscus, a band of light refracted low in the liquid, small bubbles drifting up, and a slight current. Light events
// (V3Slosh drives them all): _Sweep, a bright band running through the liquid to its front when the level rises
// (position 0-1 of the tube, < 0 none); _Flash, the whole liquid and the tube's wall lit up once it is full.
Shader "V3/Tube"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Size ("Tube size (px)", Vector) = (200, 12, 0, 0)
        _Fill ("Fill", Range(0, 1)) = 0
        _Slosh ("Slosh (-1..1)", Float) = 0
        _Bob ("Meniscus bob (px)", Float) = 0
        _Sweep ("Sweep position (0-1, < 0 none)", Float) = -1
        _Flash ("Full flash (0-1)", Float) = 0
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
            float _Fill, _Slosh, _Bob, _Sweep, _Flash;

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
                // the wall seen edge on, lit by the liquid's light when it is full
                float rim = body * saturate(1.0 - abs(d + wall * 0.5) / wall);
                col = Over(col, lerp(float3(0.52, 0.57, 0.65), lerp(i.color.rgb, float3(1.0, 1.0, 1.0), 0.35), _Flash), rim * lerp(0.6, 0.95, _Flash));
                // the liquid: up to the meniscus, which bulges forward, tilts as it sloshes and bobs as it rises
                float across = (px.y - r) / max(r - wall, 1.0);     // -1 bottom .. 1 top
                float front = _Fill * size.x + 2.2 * (1.0 - across * across) - 1.2 + _Slosh * across * r * 1.4 + _Bob;
                float liquid = inner * saturate(front - px.x + 0.5) * step(0.001, _Fill);
                float t = _Time.y;
                float3 lc = i.color.rgb * lerp(0.76, 1.08, saturate(px.y / size.y));
                lc *= 0.94 + 0.1 * Noise(px.x * 0.05 - t * 2.0);
                // light refracted through the liquid: a soft bright band low down
                lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.28 * saturate(1.0 - abs(px.y - size.y * 0.3) / 1.4));
                // small bubbles drifting up and back along the liquid, each a bright ring
                for (int b = 0; b < 6; b++)
                {
                    float speed = 0.35 + 0.25 * Hash(b * 7.1);
                    float k = frac(t * speed + Hash(b * 3.7));
                    float2 bp = float2(frac(Hash(b * 5.3) - t * 0.02 * speed) * max(front - 4.0, 0.0) + 2.0, lerp(wall + 1.5, size.y - wall - 1.5, k));
                    float bd = length(px - bp);
                    lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.55 * saturate(1.0 - abs(bd - 1.1) / 0.6) * sin(k * 3.14159));
                }
                // the sweep running through, and the full flash
                lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.55 * step(0.0, _Sweep) * exp(-pow((px.x - _Sweep * size.x) / 9.0, 2.0)));
                lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.45 * _Flash);
                col = Over(col, lc, liquid * i.color.a);
                // a bright edge along the meniscus
                col = Over(col, float3(1.0, 1.0, 1.0), inner * step(0.001, _Fill) * saturate(1.0 - abs(front - px.x - 0.8) / 0.9) * 0.6);
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
