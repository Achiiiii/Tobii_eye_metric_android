// V3 home: shows the E's 3D view (V3Home's render texture) over the page. Rendered over a clear background, its
// colour is already multiplied by its coverage - the antialiased edges and the soft light round the E - so it is laid
// on with premultiplied alpha (One, OneMinusSrcAlpha); the usual UI blend would multiply it again and darken every
// edge and glow.
Shader "V3/Premul"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                return fixed4(c.rgb * i.color.rgb, c.a) * i.color.a;
            }
            ENDCG
        }
    }
    Fallback Off
}
