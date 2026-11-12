// V3: a straight progress guide as a tube of the keys' glass with green liquid in it. The tube is a capsule (_Size px,
// rounded ends): a pale glass floor, shaded a little under its upper edge, inside the keys' frame (a hairline and a
// cool grey band). The liquid (the vertex colour, a little darker below) fills it up to _Fill with a forward-bulging
// meniscus that tilts by _Slosh while it moves and bobs by _Bob (px) when it rises, then settles; a bright edge along
// the meniscus, a band of light refracted low in the liquid and one fine highlight along the top of the glass -
// nothing else moves in it. Light events (V3Slosh drives them): _Sweep, a bright band running through the liquid to
// its front when the level rises (position 0-1 of the tube, < 0 none); _Flash, the whole liquid and the frame lit up
// once it is full.
Shader "V3/Tube"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Size ("Tube size (px)", Vector) = (200, 12, 0, 0)
        _Fill ("Fill", Range(0, 1)) = 0
        _Sweep ("Sweep position (0-1, < 0 none)", Float) = -1
        _Flash ("Full flash (0-1)", Float) = 0
        _Slosh ("Slosh (-1..1)", Float) = 0
        _Bob ("Meniscus bob (px)", Float) = 0
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
            float _Fill, _Sweep, _Flash, _Slosh, _Bob;

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

            float4 Over(float4 under, float3 rgb, float a)
            {
                float outA = a + under.a * (1.0 - a);
                return float4((rgb * a + under.rgb * under.a * (1.0 - a)) / max(outA, 1e-4), outA);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = _Size.xy;
                float2 px = i.uv * size;                       // px from the bottom left, y up
                float r = size.y * 0.5, wall = 1.6;
                // the capsule: distance to its outline (< 0 inside)
                float2 c = float2(clamp(px.x, r, size.x - r), r);
                float d = length(px - c) - r;
                float body = saturate(0.5 - d);
                float inner = saturate(0.5 - (d + wall));
                // the groove's floor, shaded under its upper edge
                float3 floor_ = float3(0.905, 0.922, 0.945) * (1.0 - 0.07 * saturate((px.y - r) / r));
                float4 col = float4(floor_, body);
                // the frame: the keys' cool grey band and its outer hairline, lit by the light when it is full
                float3 frame = lerp(float3(0.70, 0.74, 0.78), float3(0.50, 0.55, 0.60), saturate(1.0 - abs(d + 0.5) / 0.8));
                frame = lerp(frame, lerp(i.color.rgb, float3(1.0, 1.0, 1.0), 0.3), _Flash);
                col = Over(col, frame, body * (1.0 - inner));
                float rin = r - wall;
                // the liquid: up to the meniscus, which bulges forward, tilts as it sloshes and bobs as it rises
                float across = (px.y - r) / max(rin, 1.0);     // -1 bottom .. 1 top
                float front = _Fill * size.x + 2.2 * (1.0 - across * across) - 1.2 + _Slosh * across * r * 1.4 + _Bob;
                float liquid = inner * saturate(front - px.x + 0.5) * step(0.001, _Fill);
                float3 lc = i.color.rgb * lerp(0.80, 1.06, saturate(px.y / size.y));
                // light refracted through the liquid: a soft bright band low down
                lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.24 * saturate(1.0 - abs(px.y - size.y * 0.32) / 1.4));
                lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.5 * step(0.0, _Sweep) * exp(-pow((px.x - _Sweep * size.x) / 9.0, 2.0)));
                lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.4 * _Flash);
                col = Over(col, lc, liquid * i.color.a);
                // a bright edge along the meniscus, and the glass's highlight along the top
                col = Over(col, float3(1.0, 1.0, 1.0), inner * step(0.001, _Fill) * saturate(1.0 - abs(front - px.x - 0.8) / 0.9) * 0.6);
                float hl = saturate(1.0 - abs(px.y - size.y * 0.74) / 0.7) * inner
                         * saturate((px.x - r * 0.8) / 4.0) * saturate((size.x - r * 0.8 - px.x) / 4.0);
                col = Over(col, float3(1.0, 1.0, 1.0), hl * 0.4);
                return fixed4(col.rgb, col.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
