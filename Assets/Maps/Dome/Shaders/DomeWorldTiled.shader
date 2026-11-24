Shader "Dome/WorldTiled"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (world tiled)", 2D) = "white" {}
        _EmissionMap ("Emission (world tiled)", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _TileSize ("Tile Width (m)", Float) = 3
        _TileAspect ("Tile Height / Width", Float) = 1
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0.3
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0
        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _PulseScale ("Pulse Spatial Frequency", Float) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _EmissionMap;
        fixed4 _Color;
        fixed4 _EmissionColor;
        float _TileSize;
        float _TileAspect;
        half _Glossiness;
        half _Metallic;
        float _PulseAmount;
        float _PulseSpeed;
        float _PulseScale;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        float2 TileUV(float2 p)
        {
            return float2(p.x / _TileSize, p.y / (_TileSize * _TileAspect));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 w = pow(abs(IN.worldNormal), 6.0);
            w = w / max(w.x + w.y + w.z, 1e-4);

            float2 uvx = TileUV(IN.worldPos.zy);
            float2 uvy = TileUV(IN.worldPos.xz);
            float2 uvz = TileUV(IN.worldPos.xy);

            fixed3 albedo = tex2D(_MainTex, uvx).rgb * w.x
                + tex2D(_MainTex, uvy).rgb * w.y
                + tex2D(_MainTex, uvz).rgb * w.z;
            fixed3 em = tex2D(_EmissionMap, uvx).rgb * w.x
                + tex2D(_EmissionMap, uvy).rgb * w.y
                + tex2D(_EmissionMap, uvz).rgb * w.z;

            float wave = sin(_Time.y * _PulseSpeed - length(IN.worldPos.xz) * _PulseScale);
            float pulse = 1.0 + _PulseAmount * (wave - 1.0) * 0.5;

            o.Albedo = albedo * _Color.rgb;
            o.Emission = em * _EmissionColor.rgb * pulse;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
