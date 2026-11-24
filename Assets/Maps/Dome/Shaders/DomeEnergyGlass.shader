Shader "Dome/EnergyGlass"
{
    Properties
    {
        _TintColor ("Glass Tint", Color) = (0.08, 0.55, 0.65, 0.16)
        _RimColor ("Rim Color", Color) = (0.25, 1.0, 0.95, 1)
        _PulseColor ("Pulse Color", Color) = (0.6, 0.3, 1.0, 1)
        _HexScale ("Hex Scale", Float) = 3
        _HexWidth ("Hex Line Width", Range(0.01, 0.4)) = 0.08
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.4
        _PulseSpeed ("Pulse Speed", Float) = 0.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TintColor;
            fixed4 _RimColor;
            fixed4 _PulseColor;
            float _HexScale;
            float _HexWidth;
            float _RimPower;
            float _RimIntensity;
            float _PulseSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float HexDist(float2 p)
            {
                p = abs(p);
                float c = dot(p, float2(0.5, 0.8660254));
                return max(c, p.x);
            }

            float HexEdge(float2 uv)
            {
                float2 r = float2(1.0, 1.7320508);
                float2 h = r * 0.5;
                float2 a = uv - r * floor(uv / r) - h;
                float2 shifted = uv - h;
                float2 b = shifted - r * floor(shifted / r) - h;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                return 0.5 - HexDist(gv);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float ndv = abs(dot(normalize(i.worldNormal), viewDir));
                float rim = pow(1.0 - saturate(ndv), _RimPower) * _RimIntensity;

                float edge = HexEdge(i.uv * _HexScale);
                float hexLine = 1.0 - smoothstep(0.0, _HexWidth, edge);

                float wave = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed * 6.2831853
                    - i.worldPos.y * 0.35 + atan2(i.worldPos.z, i.worldPos.x) * 2.0);
                fixed3 energy = lerp(_RimColor.rgb, _PulseColor.rgb, wave);

                float alpha = _TintColor.a + rim * 0.35 + hexLine * (0.18 + 0.3 * wave);
                fixed3 col = _TintColor.rgb + energy * (rim + hexLine * (0.5 + wave));
                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}
