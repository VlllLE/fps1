Shader "Skins/EmberAsh"
{
    Properties
    {
        _Color ("Ash Tint", Color) = (0.07, 0.06, 0.05, 1)
        _SkinColor ("Skin Color", Color) = (0.07, 0.06, 0.05, 1)
        _EnergyColor ("Ember Color", Color) = (1.0, 0.45, 0.08, 1)
        _AccentColor ("Coal Color", Color) = (0.25, 0.1, 0.05, 1)
        _EmissionColor ("Emission Color", Color) = (1.0, 0.55, 0.12, 1)
        _NoiseTex ("Noise (RGB)", 2D) = "gray" {}
        _Speed ("Rise Speed", Range(0,8)) = 0.9
        _Intensity ("Ember Intensity", Range(0,8)) = 2.8
        _Scale ("Triplanar Scale", Float) = 0.95
        _BlendSharpness ("Blend Sharpness", Range(1,8)) = 4
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _NoiseTex;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _SkinColor;
        fixed4 _EnergyColor;
        fixed4 _AccentColor;
        fixed4 _EmissionColor;
        float _Speed;
        float _Intensity;
        float _Scale;
        float _BlendSharpness;

        struct Input
        {
            float3 objPos;
            float3 objNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
            o.objNormal = v.normal;
        }

        float3 TriplanarWeights(float3 n, float sharpness)
        {
            float3 w = pow(abs(n), sharpness);
            return w / max(w.x + w.y + w.z, 1e-5);
        }

        fixed4 TriplanarSample(sampler2D tex, float3 pos, float3 weights)
        {
            fixed4 x = tex2D(tex, pos.zy);
            fixed4 y = tex2D(tex, pos.xz);
            fixed4 z = tex2D(tex, pos.xy);
            return x * weights.x + y * weights.y + z * weights.z;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 weights = TriplanarWeights(IN.objNormal, _BlendSharpness);
            float t = _Time.y * _Speed;
            float3 rise = IN.objPos * _Scale + float3(t * 0.03, t * 0.2, -t * 0.02);

            fixed4 noise = TriplanarSample(_NoiseTex, rise, weights);
            float coals = saturate(pow(1.0 - noise.b, 1.5));
            float sparks = saturate(pow(noise.r * noise.g, 3.5));
            float breath = 0.75 + 0.25 * sin(t * 3.5 + noise.g * 8.0);

            fixed3 ash = _Color.rgb * _SkinColor.rgb;
            fixed3 coal = lerp(ash, _AccentColor.rgb, coals);
            fixed3 emit = _EmissionColor.rgb * _EnergyColor.rgb * (sparks * 2.2 + coals * 0.35) * _Intensity * breath;

            o.Albedo = coal + emit * 0.1;
            o.Emission = emit;
            o.Metallic = _Metallic;
            o.Smoothness = lerp(_Glossiness, 0.45, sparks);
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
