// V3 home: the 3D E optotype. Near-black matte ink with a soft key highlight, a faint sky fill on upward
// faces and a cool rim from behind.
Shader "V3/Ink"
{
    Properties
    {
        _Color ("Ink", Color) = (0.02, 0.02, 0.024, 1)
        _Spec ("Key highlight", Float) = 0.015
        _Gloss ("Highlight tightness", Float) = 4
        _Sky ("Sky fill on upward faces", Float) = 0.022
        _Rim ("Rim", Float) = 0.015
        _RimDir ("Rim light direction (towards the light)", Vector) = (0.55, 0.37, 0.74, 0)
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
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color;
            float _Spec, _Gloss, _Sky, _Rim;
            float4 _RimDir;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 h = normalize(l + v);
                float ndl = saturate(dot(n, l));
                float3 col = _Color.rgb;
                col += _LightColor0.rgb * pow(saturate(dot(n, h)), _Gloss) * _Spec * ndl;
                col += _Sky * saturate(n.y);
                col += _Rim * pow(1.0 - saturate(dot(n, v)), 3.0) * saturate(dot(n, normalize(_RimDir.xyz)) + 0.3);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
    Fallback Off
}
