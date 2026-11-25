Shader "Skins/StormArc"
{
    Properties
    {
        _Color ("Base Tint", Color) = (0.6, 0.7, 1.0, 1)
        _SkinColor ("Skin Color", Color) = (0.16, 0.2, 0.3, 1)
        _EnergyColor ("Arc Color", Color) = (0.25, 0.55, 1.0, 1)
        _AccentColor ("Flash Color", Color) = (0.85, 0.92, 1.0, 1)
        _EmissionColor ("Emission Tint", Color) = (0.8, 0.9, 1.0, 1)
        _NoiseTex ("Noise (RGB)", 2D) = "gray" {}
        _CircuitTex ("Circuit Mask (optional)", 2D) = "white" {}
        _Speed ("Arc Speed", Range(0,8)) = 2.0
        _Intensity ("Arc Intensity", Range(0,8)) = 1.4
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

            fixed4 noise = TriplanarSample(_NoiseTex, pos + float3(t * 0.04, -t * 0.06, t * 0.03), weights);
            fixed4 circuit = TriplanarSample(_CircuitTex, pos * 0.85 + float3(0.0, t * 0.05, 0.0), weights);

            // Static look: dark mottled storm cloud body with thin bright arcs along the circuit traces.
            float cloud = noise.r * 0.6 + noise.g * 0.4;
            float trace = saturate(circuit.r * 1.2 + circuit.g * 0.25);
            float arcBranch = trace * (0.45 + noise.g * 0.9);
            float arc = smoothstep(0.30, 0.85, arcBranch);
            float flicker = 0.7 + 0.3 * sin(t * 13.0 + noise.r * 30.0);
            float core = smoothstep(0.78, 1.0, arcBranch) * flicker;

            fixed3 baseCol = _Color.rgb * _SkinColor.rgb * (0.6 + 0.9 * cloud);
            float3 emit = _EnergyColor.rgb * (arc * flicker) * _Intensity
                        + _AccentColor.rgb * core * _Intensity * 0.6;
            // Soft clamp so a high intensity never blows the whole mesh out to white.
            emit = _EmissionColor.rgb * (1.0 - exp(-emit)) * 1.25;

            o.Albedo = baseCol;
            o.Emission = emit;
            o.Metallic = min(_Metallic, 0.5);
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
