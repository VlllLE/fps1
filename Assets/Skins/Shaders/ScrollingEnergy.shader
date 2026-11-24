Shader "Skins/ScrollingEnergy"
{
    Properties
    {
        _Color ("Base Tint", Color) = (0.05, 0.07, 0.1, 1)
        _SkinColor ("Skin Color", Color) = (0.05, 0.07, 0.1, 1)
        _EnergyColor ("Energy Color", Color) = (0.2, 1.0, 0.55, 1)
        _AccentColor ("Accent Color", Color) = (0.2, 1.0, 0.55, 1)
        _EmissionColor ("Emission Color", Color) = (0.2, 1.0, 0.55, 1)
        _CircuitTex ("Circuit Mask (R=trace G=halo B=phase)", 2D) = "white" {}
        _NoiseTex ("Noise (RGB)", 2D) = "gray" {}
        _Speed ("Scroll Speed", Range(0,8)) = 1.5
        _Intensity ("Energy Intensity", Range(0,8)) = 2.0
        _Scale ("Triplanar Scale", Float) = 1.25
        _BlendSharpness ("Blend Sharpness", Range(1,8)) = 4
        _Glossiness ("Smoothness", Range(0,1)) = 0.55
        _Metallic ("Metallic", Range(0,1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        #define SKIN_TILES_PER_METER 5.0

        sampler2D _CircuitTex;
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
            float3 pos = IN.objPos * _Scale;
            float t = _Time.y * _Speed;

            float3 scrollPos = pos + float3(0.0, t * 0.35, t * 0.15);
            fixed4 circuit = TriplanarSample(_CircuitTex, scrollPos, weights);
            fixed4 noise = TriplanarSample(_NoiseTex, pos * 0.7 + float3(t * 0.08, 0.0, 0.0), weights);

            float pulse = 0.55 + 0.45 * sin(t * 3.2 + circuit.b * 6.2831853 + noise.g * 4.0);
            float energy = saturate(circuit.r * 1.15 + circuit.g * 0.35) * pulse;
            fixed3 baseCol = (_Color.rgb * _SkinColor.rgb);
            fixed3 glow = (_EnergyColor.rgb * _AccentColor.rgb) * energy * _Intensity;

            o.Albedo = baseCol + glow * 0.15;
            o.Emission = glow * (_EmissionColor.rgb + 0.25);
            o.Metallic = min(_Metallic, 0.5);
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
