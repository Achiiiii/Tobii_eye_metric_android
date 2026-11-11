// V3: a round lens of the trial frame on the cover page. Clear (_Frost 0): almost no body - a faint grey-green tint,
// denser toward its ground edge, a fine bright line inside that edge (brightest toward the light) and the studio's
// window reflected as soft bands across the upper left. Frosted (_Frost 1): the opal ground glass of the home's E, thick
// enough to hide what is behind it, with a fine grain, lit from the top left and shaded just inside the edge. _R is the
// glass's radius and _Size the quad's side, in px.
Shader "V3/Lens"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Size ("Quad (px)", Float) = 140
        _R ("Radius (px)", Float) = 70
        _Frost ("Frost", Range(0, 1)) = 0
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
            float _Size, _R, _Frost;

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

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * _Size;                    // px from the middle, y up
                float e = _R - length(p);                           // px in from the edge
                float cover = saturate(e + 0.5);
                float2 q = p / _R;
                float lit = saturate(0.5 + 0.5 * dot(q, normalize(float2(-0.5, 0.8))));
                float hair = saturate(1.0 - abs(e - 3.2) / 0.8) * lerp(0.25, 0.9, lit);

                // clear: the tint, denser toward the edge, the bright line inside the edge
                float3 clear = lerp(float3(0.62, 0.73, 0.71), float3(1.0, 1.0, 1.0), hair);
                float clearA = lerp(0.05 + 0.42 * exp(-e / 3.0), 1.0, hair);
                // the studio's window reflected across the upper left
                float s = dot(q, normalize(float2(-1.0, 1.0)));
                float band = (0.3 * exp(-pow((s - 0.52) / 0.12, 2.0)) + 0.22 * exp(-pow((s - 0.76) / 0.04, 2.0))) * saturate(e / 6.0);
                float outA = band + clearA * (1.0 - band);
                clear = (float3(1.0, 1.0, 1.0) * band + clear * clearA * (1.0 - band)) / max(outA, 1e-4);

                // frosted: opal, grained, lit from the top left, shaded inside the edge
                float3 milk = float3(0.96, 0.967, 0.97) * lerp(0.93, 1.03, lit);
                milk *= 1.0 + 0.06 * (Hash(floor(p)) - 0.5);
                milk *= 1.0 - 0.1 * exp(-e / 7.0);
                milk = lerp(milk, float3(1.0, 1.0, 1.0), hair * 0.8);

                float3 col = lerp(clear, milk, _Frost);
                float a = lerp(outA, 0.96, _Frost) * cover;
                return fixed4(GammaToLinearSpace(col) * i.color.rgb, a * i.color.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
