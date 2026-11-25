// V3: a piece of glass (V3GlassFace): a rounded rectangle _Size px, sitting 2.5 px in from its
// rect. Its face is an even, nearly white milky glass - so the words keep their contrast - a little lighter toward
// the top, with a soft light just inside its frame. Round it runs a frame of clear glass 3 px wide, which is what
// marks the piece out: a hairline on its outer edge (darker away from the light), a cool grey band lit from the top
// left, and a fine highlight where it meets the face, brightest toward the light and again at the far corner.
// It is laid on one soft shadow (V3.PatchShadow). _Lit (0-1): chosen - light from inside, the face toward the pale
// green, the hairline green; _Press (0-1): pushed in - a little darker, shaded along the top; _Ripple: touched - a
// disc of the green's light spreading from the touch (centre px from the middle, radius px, strength), inside the
// piece. _Clear (0-1): how much of what is behind shows through the face (the dialogs' panels). _Sheen (0-1, < 0
// none): a light passing across the glass once, bright on the edge and faint on the face. _Trace (0-1): how far the
// green's light has run round the frame, from the top left, clockwise - the frame's glass lit from within (its
// hairline and highlight kept), fading in from where it starts and out ahead of a brighter head, with a faint glow
// just outside and just inside; the start fills in as it comes round, so the full ring closes without a seam. The vertex colour tints it.
Shader "V3/GlassKey"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Size ("Size (px)", Vector) = (210, 72, 0, 0)
        _Radius ("Corner radius (px)", Float) = 12
        _Lit ("Lit", Range(0, 1)) = 0
        _Press ("Pressed", Range(0, 1)) = 0
        _Ripple ("Ripple (centre px, radius px, strength)", Vector) = (0, 0, 0, 0)
        _Clear ("Clear", Range(0, 1)) = 0
        _Sheen ("Sheen", Float) = -1
        _Trace ("Trace", Range(0, 1)) = 0
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
            float4 _Size, _Ripple;
            float _Radius, _Lit, _Press, _Clear, _Sheen, _Trace;

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

            float Box(float2 p, float2 h, float r)
            {
                float2 d = abs(p) - (h - r);
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            // How far along the outline (half size h, corner radius r) the point q on it lies, from the top left corner's
            // end clockwise, in px; the whole length in total.
            float Along(float2 q, float2 h, float r, out float total)
            {
                float2 i = h - r;
                float lx = 2.0 * i.x, ly = 2.0 * i.y, qa = 1.5707963 * r;
                total = 2.0 * (lx + ly) + 4.0 * qa;
                float2 c = clamp(q, -i, i), v = q - c;
                if (abs(v.x) < 1e-3)
                    return v.y > 0.0 ? c.x + i.x : lx + 2.0 * qa + ly + (i.x - c.x);
                if (abs(v.y) < 1e-3)
                    return v.x > 0.0 ? lx + qa + (i.y - c.y) : 2.0 * lx + 3.0 * qa + ly + (c.y + i.y);
                if (v.x > 0.0 && v.y > 0.0)
                    return lx + r * atan2(v.x, v.y);
                if (v.x > 0.0)
                    return lx + qa + ly + r * atan2(-v.y, v.x);
                if (v.y < 0.0)
                    return 2.0 * lx + 2.0 * qa + ly + r * atan2(-v.x, -v.y);
                return 2.0 * lx + 3.0 * qa + 2.0 * ly + r * atan2(v.y, -v.x);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = _Size.xy, h = size * 0.5 - 2.5;     // the piece, 2.5 px in from its rect
                float2 p = (i.uv - 0.5) * size;                   // px from the middle, y up
                float r = min(_Radius, min(h.x, h.y));
                float d = Box(p, h, r);
                float a = saturate(0.5 - d);
                float e = -d;                                       // px in from the edge
                float2 n = normalize(float2(Box(p + float2(0.5, 0.0), h, r) - d, Box(p + float2(0.0, 0.5), h, r) - d) + 1e-5);
                float lam = dot(n, normalize(float2(-0.5, 0.8)));   // 1 toward the light (top left), -1 away
                const float frame = 3.0;
                float inside = smoothstep(frame - 0.6, frame + 0.6, e);   // past the frame, on the face
                // the dwell's light round the frame: fading in over 24 px from its start (filled in as the head comes
                // round, so the ring closes without a seam) and out over 16 px ahead of a brighter head
                // (round the last 3 %, the head fades and the soft front closes, so the full ring is even)
                float traced = 0.0, head = 0.0;
                if (_Trace > 0.0)
                {
                    float total;
                    float s = Along(p - n * d, h, r, total);
                    float closing = smoothstep(0.97, 1.0, _Trace);
                    float run = s - _Trace * total - 12.0 * closing;
                    float tail = lerp(smoothstep(0.0, 24.0, s), 1.0, smoothstep(0.8, 1.0, _Trace));
                    traced = saturate(_Trace * 40.0) * (1.0 - smoothstep(-10.0, 6.0, run)) * tail;
                    head = saturate(_Trace * 40.0) * exp(-(run + 4.0) * (run + 4.0) / 80.0) * tail * (1.0 - closing);
                }
                // the face: even, nearly white, a little lighter toward the top, a fine grain, and a soft light just
                // inside the edge
                float3 face = float3(0.958, 0.964, 0.970) * lerp(0.985, 1.015, i.uv.y);
                face *= 1.0 + 0.016 * (Hash(floor(p)) - 0.5);
                face = lerp(face, float3(1.0, 1.0, 1.0), 0.6 * exp(-max(e - frame, 0.0) / 6.0));
                // chosen: light from inside - the face toward the pale green, strongest in the middle
                float mid = 1.0 - 0.55 * saturate(length(p / h));
                face = lerp(face, float3(0.86, 1.0, 0.94), _Lit * 0.6 * mid);
                // the traced frame's light just inside it
                face = lerp(face, float3(0.86, 1.0, 0.94), traced * 0.7 * exp(-max(e - frame, 0.0) / 3.0));
                // the frame: clear glass, a cool grey lit from the top left (green when chosen)
                float3 band = lerp(float3(0.66, 0.70, 0.74), float3(0.83, 0.86, 0.885), saturate(0.5 + 0.5 * lam));
                band = lerp(band, float3(0.40, 0.78, 0.62), _Lit * 0.75);
                // traced: the band's glass lit green from within, still lighter toward the light, the head toward white
                float3 lightBand = lerp(float3(0.22, 0.74, 0.55), float3(0.45, 0.88, 0.70), saturate(0.5 + 0.5 * lam));
                band = lerp(band, lightBand, traced * 0.92);
                band = lerp(band, float3(0.82, 1.0, 0.93), head * 0.75);
                float3 col = lerp(band, face, inside);
                // its outer hairline, darker away from the light; the highlight where it meets the face, brightest
                // toward the light and again at the far corner
                float3 hair = lerp(float3(0.46, 0.51, 0.56), float3(0.26, 0.60, 0.46), saturate(_Lit + traced * 0.85));
                col = lerp(col, hair, lerp(0.5, 0.95, saturate(0.5 - 0.5 * lam)) * saturate(1.0 - abs(e - 0.5) / 0.8));
                float shine = 0.35 + 0.65 * saturate(pow(saturate(lam), 1.5) + 0.7 * pow(saturate(-lam), 4.0));
                col = lerp(col, float3(1.0, 1.0, 1.0), shine * saturate(1.0 - abs(e - frame - 0.4) / 0.8));
                // a light passing across, from the top left
                float across = (i.uv.x * size.x + (1.0 - i.uv.y) * size.y * 0.6) / (size.x + size.y * 0.6);
                float sheen = step(0.0, _Sheen) * exp(-pow((across - _Sheen) / 0.07, 2.0));
                col = lerp(col, float3(1.0, 1.0, 1.0), sheen * lerp(0.85, 0.25, inside));
                // pushed in: a little darker, shaded along the top of the face
                col *= 1.0 - 0.05 * _Press;
                col *= 1.0 - 0.14 * _Press * saturate(1.0 - (h.y - p.y - frame) / 7.0);
                col *= i.color.rgb;
                float alpha = a * (1.0 - _Clear * inside);
                // touched: the green's light (#3FD09A) spreading from the touch, blended as light (as the UI blends it)
                float3 lin = GammaToLinearSpace(col);
                float3 green = GammaToLinearSpace(float3(0.247, 0.816, 0.604));
                lin = lerp(lin, green, _Ripple.w * saturate(_Ripple.z - length(p - _Ripple.xy) + 0.5));
                // traced: the green's glow in the 2.5 px round the piece
                float halo = (traced * 0.4 + head * 0.35) * exp(-max(d, 0.0) / 0.9) * saturate(2.5 - d) * (1.0 - a);
                float outA = alpha + halo;
                lin = (lin * alpha + green * halo) / max(outA, 1e-4);
                return fixed4(lin, outA * i.color.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
