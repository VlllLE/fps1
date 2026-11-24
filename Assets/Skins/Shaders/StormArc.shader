Shader "Skins/StormArc"
{
    Properties
    {
        _Color ("Base Tint", Color) = (0.05, 0.06, 0.1, 1)
        _SkinColor ("Skin Color", Color) = (0.05, 0.06, 0.1, 1)
        _EnergyColor ("Arc Color", Color) = (0.55, 0.8, 1.0, 1)
        _AccentColor ("Flash Color", Color) = (0.9, 0.95, 1.0, 1)
        _EmissionColor ("Emission Color", Color) = (0.65, 0.85, 1.0, 1)
        _NoiseTex ("Noise (RGB)", 2D) = "gray" {}
        _CircuitTex ("Circuit Mask (optional)", 2D) = "white" {}
        _Speed ("Arc Speed", Range(0,8)) = 2.0
        _Intensity ("Arc Intensity", Range(0,8)) = 2.6
        _Scale ("Triplanar Scale", Float) = 1.15
        _BlendSharpness ("Blend Sharpness", Range(1,8)) = 4
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        #define SKIN_TILES_PER_METER 5.0

        sampler2D _NoiseTex;
        sampler2D _CircuitTex;
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
            float3 axisScale = float3(
                length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)),
                length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y)),
                length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z)));
            o.objPos = v.vertex.xyz * axisScale * SKIN_TILES_PER_METER;
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
            float3 pos = IN.objPos * _Scale;

            fixed4 noise = TriplanarSample(_NoiseTex, pos + float3(t * 0.08, -t * 0.12, t * 0.05), weights);
            fixed4 circuit = TriplanarSample(_CircuitTex, pos * 0.85 + float3(0.0, t * 0.15, 0.0), weights);

            float branch = saturate(circuit.r * 1.2);
            float flicker = 0.55 + 0.45 * sin(t * 17.0 + noise.r * 30.0);
            float bolt = pow(saturate(branch * (0.35 + noise.g)), 1.8) * flicker;
            float flash = pow(saturate(noise.b * circuit.g), 4.0) * flicker;

            fixed3 baseCol = _Color.rgb * _SkinColor.rgb;
            fixed3 arc = lerp(_EnergyColor.rgb, _AccentColor.rgb, flash);
            fixed3 emit = arc * _EmissionColor.rgb * (bolt + flash * 1.5) * _Intensity;

            o.Albedo = baseCol + emit * 0.08;
            o.Emission = emit;
            o.Metallic = min(_Metallic, 0.5);
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
