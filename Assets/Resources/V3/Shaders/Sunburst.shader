// V3 home: the E's faces - steel with a sunburst finish (brushed outward from the centre, as a watch dial). The
// brushing direction is radial in the face (worked out from the object position: the face lies in the mesh's x-z
// plane), so the studio's reflection (V3StudioSharp) smears along the rays and the key light leaves a fan-shaped
// streak that moves as the E leans. Fine rays (noise by angle, faded where finer than a pixel) break it up.
Shader "V3/Sunburst"
{
    Properties
    {
        _Tint ("Steel tint", Color) = (0.86, 0.88, 0.91, 1)
        _Blur ("Reflection blur", Range(0, 1)) = 0.3
        _Aniso ("Smear along the rays", Range(0, 1)) = 0.45
        _Rays ("Rays round the centre", Float) = 900
        _RayAmp ("Ray strength", Range(0, 1)) = 0.5
        _Streak ("Light streak", Float) = 0.6
        _Brush ("Streak tightness", Float) = 40
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
            float _Blur, _Aniso, _Rays, _RayAmp, _Streak, _Brush;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 obj : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
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
                // the brushing runs outward from the centre of the face
                float2 q = i.obj.xz;
                float len = length(q) + 1e-4;
                float3 t = normalize(UnityObjectToWorldDir(float3(q.x / len, 0.0, q.y / len)));
                // fine rays: noise by angle
                float a = atan2(q.y, q.x) * _Rays / 6.2832;
                float fade = saturate(1.5 - fwidth(a) * 1.2);
                float ray = lerp(0.5, 0.6 * V3Noise1(a) + 0.4 * V3Noise1(a * 2.7 + len * 40.0), fade);
                float m = (ray - 0.5) * 2.0 * _RayAmp;
                // the studio, smeared along the rays
                float3 r = reflect(-v, n), env = 0;
                float rough = saturate(_Blur + m * 0.1);
                for (int k = -2; k <= 2; k++)
                    env += V3StudioSharp(normalize(r + t * (k * 0.5 * _Aniso)), rough);
                env *= 0.2;
                float3 f = _Tint.rgb + (1.0 - _Tint.rgb) * pow(1.0 - ndv, 5.0);
                float3 col = env * f * (0.92 + 0.08 * m);
                // the fan-shaped streak of the key light across the rays
                float th = dot(t, normalize(l + v));
                col += _LightColor0.rgb * _Streak * (0.75 + 0.5 * m) * pow(sqrt(saturate(1.0 - th * th)), _Brush) * saturate(dot(n, l));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
