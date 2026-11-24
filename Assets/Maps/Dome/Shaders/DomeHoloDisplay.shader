Shader "Dome/HoloDisplay"
{
    Properties
    {
        _MainTex ("Holo Map (R = intensity)", 2D) = "white" {}
        _Color ("Tint", Color) = (0.2, 1.0, 1.0, 1)
        _Intensity ("Intensity", Range(0, 6)) = 1.6
        _ScanSpeed ("Scan Speed", Float) = 2.5
        _ScanDensity ("Scan Density", Float) = 220
        _Flicker ("Flicker", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Intensity;
            float _ScanSpeed;
            float _ScanDensity;
            float _Flicker;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.uv).r;
                float scan = 0.65 + 0.35 * sin(i.uv.y * _ScanDensity + _Time.y * _ScanSpeed);
                float tick = floor(_Time.y * 12.0);
                float glitch = step(0.96, frac(sin(tick * 91.3) * 43758.5453));
                float flick = 1.0 - _Flicker * glitch;
                float v = mask * scan * flick;
                fixed3 col = _Color.rgb * v * _Intensity;
                return fixed4(col, saturate(v * _Color.a));
            }
            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}
