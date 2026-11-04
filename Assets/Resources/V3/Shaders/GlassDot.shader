// V3 home: the loading's dot - a small drop of the E's milky frosted glass on the light box, drawn as a distance field.
// Three droplets circle it, swinging in to flow into it like metaballs and pulling away again; as the percentage runs
// (_Gather 0-1) they come in closer and at the end are taken in, leaving the one drop the E grows out of. Shaded as the
// E is: a milky face lighter toward the top, a grey-green chamfer lit from the top left with a fine bright line, a
// soft shadow below. _R is the drop's radius and _Q the quad's side, in px.
Shader "V3/GlassDot"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Q ("Quad size (px)", Float) = 120
        _R ("Drop radius (px)", Float) = 8
        _Gather ("Gather (0-1)", Range(0, 1)) = 0
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
            float _Q, _R, _Gather;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float SMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            // the drop and its droplets; p in px from the centre, y up
            float Field(float2 p)
            {
                float t = _Time.y, R = _R, g = _Gather;
                float d = length(p) - R * (1.0 + 0.05 * sin(t * 3.1));
                float taken = smoothstep(0.82, 1.0, g);
                for (int i = 0; i < 3; i++)
                {
                    float a = t * (1.6 + 0.35 * i) + i * 2.094;
                    // each swings in to touch and flow into the drop, and out again
                    float orbit = R * lerp(2.5, 1.5, g) * (0.72 + 0.28 * sin(t * (2.3 + 0.4 * i) + i * 1.7)) * (1.0 - taken);
                    float r = R * (0.42 + 0.08 * sin(t * 2.7 + i)) * (1.0 - taken);
                    d = SMin(d, length(p - float2(cos(a), sin(a)) * orbit) - r, R * 0.9);
                }
                return d;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * _Q;
                float d = Field(p);
                float2 n = normalize(float2(Field(p + float2(0.5, 0.0)) - d, Field(p + float2(0.0, 0.5)) - d) + 1e-5);
                float2 L = normalize(float2(-0.5, 0.8));
                float bevel = 0.3 * _R;
                float3 face = float3(0.975, 0.979, 0.983) * lerp(0.93, 1.03, saturate(p.y / (2.0 * _R) + 0.5));
                float lam = saturate(dot(n, L) * 0.5 + 0.5);
                float3 edge = float3(0.66, 0.74, 0.72) * lerp(0.82, 1.06, lam);
                edge += pow(saturate(dot(n, L)), 8.0) * 0.6 * saturate(1.0 - abs(-d - 1.0) / 1.2);
                float3 col = lerp(edge, face, smoothstep(bevel - 0.8, bevel + 0.8, -d));
                float a = saturate(0.5 - d);
                float ds = Field(p + float2(0.0, 2.5));
                float sa = 0.22 * exp(-max(ds, 0.0) / 3.0) * (1.0 - a);
                float outA = a + sa;
                float3 rgb = (col * a + float3(0.07, 0.09, 0.14) * sa) / max(outA, 1e-4);
                return fixed4(GammaToLinearSpace(rgb), outA * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
