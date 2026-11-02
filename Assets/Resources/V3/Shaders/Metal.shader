// V3: the brushed-steel bezels. They mirror a photographer's studio (V3StudioSharp: strip soft boxes, dark room,
// light box behind) with a metal's fresnel; the reflection is smeared along the brushing (five samples spread along
// the mesh tangent, which runs round the ring), so the soft boxes become long bright streaks; the key light adds an
// anisotropic streak across the brushing. Fine concentric turning marks (two octaves of noise by radius, faded out
// where they would go finer than a pixel) break up the reflection and the streak.
Shader "V3/Metal"
{
    Properties
    {
        _Tint ("Steel tint", Color) = (0.86, 0.88, 0.91, 1)
        _Blur ("Reflection blur", Range(0, 1)) = 0.25
        _Aniso ("Smear along the brushing", Range(0, 1)) = 0.3
        _Brush ("Brush streak tightness", Float) = 70
        _Streak ("Brush streak", Float) = 0.8
        _Lathe ("Turning marks per unit", Float) = 260
        _Marks ("Turning marks strength", Range(0, 1)) = 0.5
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

            fixed4 _Tint;
            float _Blur, _Aniso, _Brush, _Streak, _Lathe, _Marks;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 tangent : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 obj : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.tangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.obj = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float ndv = saturate(dot(n, v));
                // turning marks: concentric, by the distance from the ring's centre
                float rad = length(i.obj.xy) * _Lathe, ang = atan2(i.obj.y, i.obj.x);
                float fade = saturate(1.5 - fwidth(rad) * 1.2);
                float marks = lerp(0.5, 0.62 * V3Noise1(rad) + 0.38 * V3Noise1(rad * 3.1 + ang * 2.0), fade);
                float m = (marks - 0.5) * 2.0 * _Marks;
                // the studio, smeared along the brushing
                float3 t = normalize(i.tangent), r = reflect(-v, n), env = 0;
                float rough = saturate(_Blur + m * 0.1);
                for (int k = -2; k <= 2; k++)
                    env += V3StudioSharp(normalize(r + t * (k * 0.5 * _Aniso)), rough);
                env *= 0.2;
                // a metal's fresnel: the steel's own colour head on, white at grazing angles
                float3 f = _Tint.rgb + (1.0 - _Tint.rgb) * pow(1.0 - ndv, 5.0);
                float3 col = env * f * (0.92 + 0.08 * m);
                // anisotropic streak across the brushing, broken up by the marks
                float th = dot(t, normalize(l + v));
                col += _LightColor0.rgb * _Streak * (0.75 + 0.5 * m) * pow(sqrt(saturate(1.0 - th * th)), _Brush) * saturate(dot(n, l));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
