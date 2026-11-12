// V3: a round progress ring as a glass tube bent into a circle with the green liquid in it (the selection and
// calibration rings), the round sibling of V3/Tube. A white outline keeps it clear on any background; the tube is faint
// glass over a track colour (_Track) with a fine highlight along its outer top; the liquid (the vertex colour) fills
// clockwise from the top to _Fill, its front a rounded meniscus with a bright edge, darker toward the inside, a band of
// light refracted along the inner wall. It answers the gaze: _Steady
// (0-1) is how still the eyes are - the liquid is brightest and its front calm when they are still, duller and
// trembling when they wander - and _Lean (-1..1, y up) where the gaze is drifting, where the glass catches the light.
// Events (V3LiquidRing drives them): _Sweep, a light running through the liquid to its front (0-1, < 0 none); _Lock,
// the arc closing - a ring of light running in to the centre and a flash there (0-1, 0 none). _R and _T are the tube's
// radius and half thickness, _Q the quad's side, in the quad's px. _Span (0-1) and _Start (radians, clockwise from the
// top) make it an open arc with round ends (the result's readout). _Glow: the liquid's light round it - a tight bright
// halo and a wider faint one, fading out before the quad's edge.
Shader "V3/RingTube"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Q ("Quad size", Float) = 150
        _R ("Radius", Float) = 57
        _T ("Half thickness", Float) = 5
        _Fill ("Fill", Range(0, 1)) = 0
        _Steady ("Steadiness", Range(0, 1)) = 1
        _Lean ("Lean (xy)", Vector) = (0, 0, 0, 0)
        _Sweep ("Sweep", Float) = -1
        _Lock ("Lock", Range(0, 1)) = 0
        _Track ("Track", Color) = (0.11, 0.13, 0.17, 0.18)
        _Span ("Part of the circle the tube covers", Range(0, 1)) = 1
        _Start ("Where it starts (radians clockwise from the top)", Float) = 0
        _Px ("Edge softness (px; 0: by the quad)", Float) = 0
        _Glow ("Glow", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
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
            float _Q, _R, _T, _Fill, _Steady, _Sweep, _Lock, _Span, _Start, _Px, _Glow;
            float4 _Lean, _Track;

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
                float2 p = (i.uv - 0.5) * _Q;                       // from the centre, y up
                float r = length(p), t = _Time.y;
                float u = frac((atan2(p.x, p.y) - _Start) / 6.28318 + 1.0);   // 0 at the start, clockwise
                float px = _Px > 0.0 ? _Px : _Q / 150.0;              // about one screen pixel here
                float across = (r - _R) / _T;                         // -1 inside .. 1 outside the tube
                float tube = saturate((_T - abs(r - _R)) / px + 0.5);
                float outline = saturate((_T + 2.2 * px - abs(r - _R)) / px + 0.5);
                if (_Span < 0.999)
                {
                    // an open arc: only its span, with round ends
                    float e1 = _Start + _Span * 6.28318;
                    float2 q0 = float2(sin(_Start), cos(_Start)) * _R, q1 = float2(sin(e1), cos(e1)) * _R;
                    float d0 = min(length(p - q0), length(p - q1)), inside = step(u, _Span);
                    tube = max(tube * inside, saturate((_T - d0) / px + 0.5));
                    outline = max(outline * inside, saturate((_T + 2.2 * px - d0) / px + 0.5));
                }
                float4 col = float4(1.0, 1.0, 1.0, outline);
                // the glass: the track tinted through it, a fine highlight along the outer top
                col = Over(col, _Track.rgb, tube * _Track.a);
                float2 n = r > 0.0 ? p / r : float2(0.0, 1.0);
                col = Over(col, float3(1.0, 1.0, 1.0), tube * saturate(1.0 - abs(across - 0.55) / 0.25) * 0.5 * saturate(n.y * 0.7 + 0.5));

                // the liquid, to the front (which trembles when the eyes wander), with round ends
                float wobble = (1.0 - _Steady) * 0.012 * sin(t * 37.0) * sin(t * 23.0 + 1.3);
                float fill = saturate(_Fill + wobble * step(0.002, _Fill));
                float a0 = _Start + fill * 6.28318;
                float2 front = float2(sin(a0), cos(a0)) * _R, start = float2(sin(_Start), cos(_Start)) * _R;
                float cap = min(length(p - front), length(p - start));
                float along = u <= fill ? 1.0 : 0.0;
                float body = max(along * tube, saturate((_T - cap) / px + 0.5)) * step(0.004, fill);
                float3 lc = i.color.rgb * lerp(0.76, 1.08, saturate(across * 0.5 + 0.5));
                lc *= lerp(0.8, 1.06, _Steady);
                // light refracted along the inner wall
                lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.3 * saturate(1.0 - abs(across + 0.45) / 0.3));
                // the sweep running through to the front
                float du = abs(u - _Sweep * fill);
                lc = lerp(lc, float3(1.0, 1.0, 1.0), 0.55 * step(0.0, _Sweep) * exp(-pow(min(du, 1.0 - du) * 6.28318 * _R / (4.0 * _T), 2.0)));
                col = Over(col, lc, body * i.color.a);
                // the meniscus: a bright edge round the front's leading half (gone once the ring is full)
                float2 ahead = float2(cos(a0), -sin(a0));
                float lead = saturate(dot(p - front, ahead) / px + 0.5) * step(0.004, fill) * step(fill, 0.995);
                col = Over(col, float3(1.0, 1.0, 1.0), lead * saturate(1.0 - abs(length(p - front) - _T * 0.85) / (0.7 * px)) * 0.7 * lerp(0.5, 1.0, _Steady));

                // where the gaze is drifting, the glass catches the light
                float lean = length(_Lean.xy);
                if (lean > 0.01)
                    col = Over(col, float3(1.0, 1.0, 1.0), tube * pow(saturate(dot(n, _Lean.xy / lean)), 8.0) * saturate(lean) * 0.55);

                // the lock: a ring of light running in to the centre, a flash there
                if (_Lock > 0.0)
                {
                    float k = 1.0 - pow(1.0 - _Lock, 3.0);
                    float rl = lerp(_R, 0.0, k);
                    float ring = exp(-pow((r - rl) / (1.6 * px), 2.0)) * (1.0 - _Lock);
                    float flash = exp(-r * r / pow(7.0 * px, 2.0)) * sin(_Lock * 3.14159);
                    col = Over(col, lerp(i.color.rgb, float3(1.0, 1.0, 1.0), 0.5), saturate(ring * 0.9 + flash * 0.85));
                }
                // the liquid's light round it, under everything else
                float off = max(min(along > 0.5 ? abs(r - _R) - _T : 1e5, cap - _T), 0.0) / px;
                float glow = _Glow * step(0.004, fill) * (0.5 * exp(-off * off / 9.0) + 0.18 * exp(-off * off / 81.0));
                glow *= saturate((_Q * 0.5 - max(abs(p.x), abs(p.y))) / (4.0 * px)) * i.color.a;
                float ga = col.a + glow * (1.0 - col.a);
                col = float4((col.rgb * col.a + i.color.rgb * glow * (1.0 - col.a)) / max(ga, 1e-4), ga);
                return fixed4(col.rgb, col.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
