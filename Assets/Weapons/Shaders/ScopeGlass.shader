Shader "Weapons/ScopeGlass"
{
    Properties
    {
        _Color ("Tint (RGB) and base opacity (A)", Color) = (0.25, 0.45, 0.55, 0.30)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.96
        _ReflectionStrength ("Edge reflection", Range(0, 1)) = 0.55
        _FresnelPower ("Fresnel power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        ZWrite Off
        Cull Back

        CGPROGRAM
        #pragma surface surf Standard alpha:premul
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness;
        half _ReflectionStrength;
        half _FresnelPower;

        struct Input
        {
            float3 viewDir;
            float3 worldNormal;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float facing = saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal)));
            float fresnel = pow(1.0 - facing, _FresnelPower);

            o.Albedo = _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = saturate(_Color.a + fresnel * _ReflectionStrength);
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Diffuse"
}
