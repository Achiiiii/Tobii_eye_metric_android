// V3: a piece of glass (V3GlassFace): a rounded rectangle _Size px, sitting 2.5 px in from its
// rect. Its face is an even, nearly white milky glass - so the words keep their contrast - a little lighter toward
// the top, with a soft light just inside its frame. Round it runs a frame of clear glass 3 px wide, which is what
// marks the piece out: a hairline on its outer edge (darker away from the light), a cool grey band lit from the top
// left, and a fine highlight where it meets the face, brightest toward the light and again at the far corner.
// It is laid on one soft shadow (V3.PatchShadow). _Lit (0-1): chosen - light from inside, the face toward the pale
// green, the hairline green; _Press (0-1): pushed in - a little darker, shaded along the top; _Ripple: touched - a
// disc of the green's light spreading from the touch (centre px from the middle, radius px, strength), inside the
// piece. _Clear (0-1): how much of what is behind shows through the face (the dialogs' panels). _Sheen (0-1, < 0
// none): a light passing across the glass once, bright on the edge and faint on the face. The vertex colour tints it.
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
            float _Radius, _Lit, _Press, _Clear, _Sheen;

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
                // the face: even, nearly white, a little lighter toward the top, a fine grain, and a soft light just
                // inside the edge
                float3 face = float3(0.958, 0.964, 0.970) * lerp(0.985, 1.015, i.uv.y);
                face *= 1.0 + 0.016 * (Hash(floor(p)) - 0.5);
                face = lerp(face, float3(1.0, 1.0, 1.0), 0.6 * exp(-max(e - frame, 0.0) / 6.0));
                // chosen: light from inside - the face toward the pale green, strongest in the middle
                float mid = 1.0 - 0.55 * saturate(length(p / h));
                face = lerp(face, float3(0.86, 1.0, 0.94), _Lit * 0.6 * mid);
                // the frame: clear glass, a cool grey lit from the top left (green when chosen)
                float3 band = lerp(float3(0.66, 0.70, 0.74), float3(0.83, 0.86, 0.885), saturate(0.5 + 0.5 * lam));
                band = lerp(band, float3(0.40, 0.78, 0.62), _Lit * 0.75);
                float3 col = lerp(band, face, inside);
                // its outer hairline, darker away from the light; the highlight where it meets the face, brightest
                // toward the light and again at the far corner
                float3 hair = lerp(float3(0.46, 0.51, 0.56), float3(0.26, 0.60, 0.46), _Lit);
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
                lin = lerp(lin, GammaToLinearSpace(float3(0.247, 0.816, 0.604)), _Ripple.w * saturate(_Ripple.z - length(p - _Ripple.xy) + 0.5));
                return fixed4(lin, alpha * i.color.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
