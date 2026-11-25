Shader "Skins/FlowingLava"
{
    Properties
    {
        _Color ("Cool Crust", Color) = (1, 1, 1, 1)
        _SkinColor ("Skin Color", Color) = (0.05, 0.03, 0.025, 1)
        _AccentColor ("Hot Rock", Color) = (0.7, 0.08, 0.02, 1)
        _EnergyColor ("Lava Color", Color) = (1.0, 0.4, 0.05, 1)
        _MidColor ("Molten Core", Color) = (1.0, 0.85, 0.3, 1)
        _EmissionColor ("Emission Color", Color) = (1.0, 0.7, 0.4, 1)
        _NoiseTex ("Noise (RGB)", 2D) = "gray" {}
        _Speed ("Flow Speed", Range(0,8)) = 0.85
        _Intensity ("Glow Intensity", Range(0,8)) = 1.5
        _Scale ("Triplanar Scale", Float) = 0.85
        _BlendSharpness ("Blend Sharpness", Range(1,8)) = 4
        _Glossiness ("Smoothness", Range(0,1)) = 0.25
        _Metallic ("Metallic", Range(0,1)) = 0.05
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
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _SkinColor;
        fixed4 _AccentColor;
        fixed4 _EnergyColor;
        fixed4 _EmissionColor;
        fixed4 _MidColor;
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
            float3 flowA = IN.objPos * _Scale + float3(t * 0.12, -t * 0.22, t * 0.05);
            float3 flowB = IN.objPos * (_Scale * 1.7) + float3(-t * 0.08, t * 0.05, t * 0.14);
            fixed4 a = TriplanarSample(_NoiseTex, flowA, weights);
            fixed4 b = TriplanarSample(_NoiseTex, flowB, weights);

            // Dark crust plates broken up by bright molten channels that ramp red -> orange -> yellow-white.
            float magma = saturate(a.r * 0.65 + b.g * 0.35);
            float heat = smoothstep(0.38, 0.72, magma);
            float flicker = 0.88 + 0.12 * sin(t * 5.0 + magma * 10.0);

            float3 molten = lerp(_AccentColor.rgb, _EnergyColor.rgb, saturate(heat * 1.6));
            molten = lerp(molten, _MidColor.rgb, saturate((heat - 0.85) * 6.0));
            fixed3 crust = _Color.rgb * _SkinColor.rgb * (0.7 + 0.8 * a.b);

            o.Albedo = lerp(crust, molten * 0.45, heat);
            o.Emission = molten * _EmissionColor.rgb * heat * heat * _Intensity * flicker;
            o.Metallic = min(_Metallic, 0.5);
            o.Smoothness = lerp(_Glossiness, 0.55, heat);
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
