// V3: a pressable piece with a fine glass rim (V3GlassFace): a rounded rectangle _Size px whose face stays an opaque,
// nearly white milky glass - so the words keep their contrast - with a faint shade just inside the rim (the glass's
// thickness) and a soft diagonal sheen; round it a narrow rim of polished glass (_Edge px): a fine bright line on its
// outer edge (brightest toward the light, top left), a pale grey-green fall across it and a fine dark line where it
// meets the face, which is what draws the piece's outline. The piece sits 2.5 px in from its rect, and round it lies
// its own contact shadow, rounded with it and a little deeper below (so it is laid on the window's soft shadow only).
// _Lit (0-1): chosen - light from inside, the face toward the pale green, the rim a light green; _Press (0-1): pushed
// in - a little darker, shaded along the top; _Ripple: touched - a disc of the green's light spreading from the touch
// (centre px from the middle, radius px, strength), inside the piece. _Clear (0-1): how much of what is behind shows
// through the face (the dialogs' panels). _Sheen (0-1, < 0 none): a light passing across the glass once, bright on
// the rim and faint on the face (a panel coming up). The vertex colour tints it.
Shader "V3/GlassKey"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Size ("Size (px)", Vector) = (210, 72, 0, 0)
        _Radius ("Corner radius (px)", Float) = 8
        _Edge ("Rim (px)", Float) = 4
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
            float _Radius, _Edge, _Lit, _Press, _Clear, _Sheen;

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
                float2 size = _Size.xy, h = size * 0.5 - 2.5;     // the piece, 2.5 px in: its contact shadow lies round it
                float2 p = (i.uv - 0.5) * size;                   // px from the middle, y up
                float d = Box(p, h, _Radius);
                float a = saturate(0.5 - d);
                float e = -d;                                       // px in from the edge
                float2 n = normalize(float2(Box(p + float2(0.5, 0.0), h, _Radius) - d, Box(p + float2(0.0, 0.5), h, _Radius) - d) + 1e-5);
                float2 L = normalize(float2(-0.5, 0.8));
                float lam = dot(n, L);
                // the face: opaque, nearly white, lighter toward the top, a fine grain, a soft diagonal sheen and the
                // glass's thickness just inside the rim
                float3 face = float3(0.975, 0.979, 0.983) * lerp(0.96, 1.02, i.uv.y);
                face *= 1.0 + 0.02 * (Hash(floor(p)) - 0.5);
                face += 0.03 * exp(-pow((i.uv.x * 0.6 + i.uv.y * 0.4 - 0.66) / 0.12, 2.0));
                face *= 1.0 - 0.07 * exp(-max(e - _Edge, 0.0) / 5.0);
                // the rim: a pale grey-green fall from the outside in, lit from the top left
                float k = saturate(e / _Edge);
                float3 rim = lerp(float3(0.92, 0.95, 0.94), float3(0.80, 0.87, 0.85), k) * lerp(0.9, 1.05, saturate(lam * 0.5 + 0.5));
                // chosen: light from inside - the face toward the pale green, strongest in the middle; the rim green
                float mid = 1.0 - 0.55 * saturate(length(p / h));
                face = lerp(face, float3(0.86, 1.0, 0.94), _Lit * 0.6 * mid);
                rim = lerp(rim, float3(0.60, 0.90, 0.77), _Lit * 0.6);
                float3 col = lerp(rim, face, smoothstep(_Edge - 0.6, _Edge + 0.6, e));
                // the fine bright line on the rim's outer edge (toward the light) and the fine dark line inside it
                col = lerp(col, float3(1.0, 1.0, 1.0), 0.75 * saturate(1.0 - abs(e - 0.7) / 0.8) * lerp(0.5, 1.0, saturate(lam * 0.5 + 0.5)));
                float3 hair = lerp(float3(0.58, 0.65, 0.63), float3(0.30, 0.62, 0.48), _Lit);
                col = lerp(col, hair, 0.55 * saturate(1.0 - abs(e - _Edge) / 0.75));
                // a light passing across, from the top left
                float across = (i.uv.x * size.x + (1.0 - i.uv.y) * size.y * 0.6) / (size.x + size.y * 0.6);
                float sheen = step(0.0, _Sheen) * exp(-pow((across - _Sheen) / 0.07, 2.0));
                col = lerp(col, float3(1.0, 1.0, 1.0), sheen * lerp(0.85, 0.25, smoothstep(_Edge - 0.6, _Edge + 0.6, e)));
                // pushed in: a little darker, shaded along the top of the face
                col *= 1.0 - 0.05 * _Press;
                col *= 1.0 - 0.14 * _Press * saturate(1.0 - (h.y - p.y - _Edge) / 7.0);
                col *= i.color.rgb;
                // the contact shadow round the piece, rounded with it, a little deeper below (the light is above)
                float ds = Box(p + float2(0.0, 0.8), h, _Radius);
                float sh = 0.16 * saturate(1.0 - max(ds, 0.0) / 2.4) * (1.0 - a);
                float body = a * (1.0 - _Clear * smoothstep(_Edge - 0.6, _Edge + 0.6, e));
                float alpha = body + sh;
                // touched: the green's light (#5FE3AE) spreading from the touch, blended as light (as the UI blends it)
                float3 lin = GammaToLinearSpace(col);
                lin = lerp(lin, GammaToLinearSpace(float3(0.373, 0.890, 0.682)), _Ripple.w * saturate(_Ripple.z - length(p - _Ripple.xy) + 0.5));
                float3 rgb = (lin * body + GammaToLinearSpace(float3(0.094, 0.125, 0.188)) * sh) / max(alpha, 1e-4);
                return fixed4(rgb, alpha * i.color.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
