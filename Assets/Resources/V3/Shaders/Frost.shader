// V3 home: the E as a block of milky ground glass, the frost thinner toward the top left and densest toward the bottom
// right: the faces show what is behind them on the home - the light box, the headline and the two bezels - blurred
// under an opal white with a fine sandblasted grain, with light scattered inside the frosted part
// (brighter toward the top) and a faint green light along the middle of each stroke. The edges (_Edge = 1) are
// polished: the backdrop seen through thick glass, darkened to a grey-green that outlines the letter, the studio's
// reflections at grazing angles and a fine highlight.
// Nothing is grabbed from the screen: the backdrop is rebuilt here from the headline's own pre-blurred coverage texture
// (V3Home's blur level 3) and the bezels' known circles, which keeps it cheap on Kebbi.
Shader "V3/Frost"
{
    Properties
    {
        _Head ("Headline coverage (blurred)", 2D) = "black" {}
        _Glow ("Light inside (blurred E)", 2D) = "white" {}
        _Light ("Light colour", Color) = (0.49, 0.94, 0.75, 1)
        _Frost ("Frost", Range(0, 1)) = 0.5
        _Edge ("Edge (clear glass)", Float) = 0
        _HeadY ("Headline box top (px)", Float) = 84
        _HeadH ("Headline box height (px)", Float) = 252
        _Ink ("Headline ink (sRGB)", Vector) = (0.227, 0.239, 0.263, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Studio.cginc"

            sampler2D _Head, _Glow;
            float4 _Light, _Ink;
            float _Frost, _Edge, _HeadY, _HeadH;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 obj : TEXCOORD2;
                float4 screen : TEXCOORD3;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.obj = v.vertex.xyz;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float Ring(float d, float r, float w, float soft)
            {
                return 1.0 - smoothstep(w * 0.5, w * 0.5 + soft, abs(d - r));
            }

            // the home behind the E at a reference pixel (1024 x 600, y down), blurred by about `blur` px (sRGB, then
            // linear)
            float3 Backdrop(float2 px, float blur)
            {
                float3 c = lerp(float3(0.980, 0.984, 0.992), float3(0.925, 0.937, 0.953), saturate(px.y / 600.0));
                // the headline: its coverage, pre-blurred, spread wider by eight taps round the point
                float2 hu = float2(px.x / 1024.0, 1.0 - (px.y - _HeadY) / _HeadH);
                float2 k = float2(1.0 / 1024.0, 1.0 / _HeadH);
                // two rings of eight taps (half and full radius, turned against each other) for a wide, smooth spread
                float cov = 0.12 * tex2D(_Head, hu).r;
                for (int t = 0; t < 8; t++)
                {
                    float a = t * 0.7854, b = a + 0.3927;
                    cov += 0.06 * tex2D(_Head, hu + float2(cos(a), sin(a)) * 0.5 * blur * k).r;
                    cov += 0.05 * tex2D(_Head, hu + float2(cos(b), sin(b)) * blur * k).r;
                }
                c = lerp(c, _Ink.rgb, saturate(cov));
                // the two bezels (brushed silver) and their soft shadow just below
                float d = distance(px, float2(512.0, 334.0)), ds = distance(px, float2(513.0, 339.0)), soft = 1.0 + blur;
                c = lerp(c, c * 0.82, 0.6 * max(Ring(ds, 168.0, 9.0, soft + 4.0), Ring(ds, 112.0, 7.0, soft + 4.0)));
                c = lerp(c, float3(0.74, 0.76, 0.79), 0.85 * max(Ring(d, 168.0, 7.0, soft), Ring(d, 112.0, 5.0, soft)));
                return GammaToLinearSpace(c);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 h = normalize(l + v);
                float2 sp = i.screen.xy / i.screen.w;
                float2 px = float2(sp.x * 1024.0, (1.0 - sp.y) * 600.0);
                // where this point is on the E (the mesh lies flat: x across, z up the letter), for the light inside
                float2 ue = i.obj.xz * 0.806 + 0.5;
                float glow = tex2D(_Glow, ue).a;
                float3 L = _Light.rgb;
                float ndv = saturate(dot(n, v));
                float ndl = saturate(dot(n, l) * 0.5 + 0.5);
                float fr = pow(1.0 - ndv, 4.0);
                float3 milk = float3(0.984, 0.987, 0.990);
                float3 col;
                if (_Edge < 0.5)
                {
                    // milky ground glass all over, thinner toward the top left and densest toward the bottom right:
                    // the home behind diffused (one blur, its radius following the frost) under an opal white, with
                    // the fine grain of the sandblasting
                    float f = lerp(0.62, 1.0, smoothstep(0.0, 1.0, saturate(((1.0 - ue.y) * 0.65 + ue.x * 0.35 - 0.1) / 0.75)));
                    col = Backdrop(px, lerp(2.0, 16.0 + 48.0 * _Frost, f));
                    col = lerp(col, milk, f * (0.64 + 0.2 * _Frost));
                    col *= 1.0 + 0.08 * (V3Noise3(i.obj * 320.0) - 0.5) * f;
                    // the body: light scattered inside the block, brighter toward the lit top of the letter and
                    // falling off below, stronger the more frosted the glass is
                    float body = saturate((px.y - 334.0 + 160.0) / 320.0);
                    col *= lerp(1.0, lerp(1.03, 0.9, body), f);
                    col *= lerp(0.95, 1.02, ndl);
                    // clear glass keeps a faint reflection of the studio and a sharper sheen; frost a broad dull one
                    col = lerp(col, V3StudioSharp(reflect(-v, n), 0.03), 0.12 * (1.0 - f));
                    col += _LightColor0.rgb * pow(saturate(dot(n, h)), lerp(60.0, 10.0, f)) * lerp(0.22, 0.04, f);
                    // the light inside, faint, where the frost catches it along the middle of the strokes
                    col = lerp(col, L, smoothstep(0.62, 1.0, glow) * 0.1 * f);
                }
                else
                {
                    // the polished edges: thick glass seen from the side - the home behind bent a little and darkened
                    // to a grey-green by the thickness, which draws the letter's outline - the studio at grazing
                    // angles and a fine bright line where the bevels face the key light
                    col = Backdrop(px + float2(n.x, -n.y) * 8.0, 3.0) * float3(0.66, 0.74, 0.72);
                    col *= lerp(0.8, 1.0, ndl);
                    col = lerp(col, V3StudioSharp(reflect(-v, n), 0.03), saturate(fr * 0.5 + 0.04));
                    col += _LightColor0.rgb * pow(saturate(dot(n, h)), 120.0) * 0.9;
                }
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
