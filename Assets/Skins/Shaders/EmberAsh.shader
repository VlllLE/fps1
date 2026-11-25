Shader "Skins/EmberAsh"
{
    Properties
    {
        _Color ("Ash Tint", Color) = (1, 1, 1, 1)
        _SkinColor ("Skin Color", Color) = (0.13, 0.12, 0.115, 1)
        _EnergyColor ("Ember Color", Color) = (1.0, 0.6, 0.12, 1)
        _AccentColor ("Coal Color", Color) = (0.42, 0.07, 0.02, 1)
        _EmissionColor ("Emission Color", Color) = (1.0, 0.8, 0.4, 1)
        _NoiseTex ("Noise (RGB)", 2D) = "gray" {}
        _Speed ("Rise Speed", Range(0,8)) = 0.9
        _Intensity ("Ember Intensity", Range(0,8)) = 2.4
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

        #define SKIN_TILES_PER_METER 5.0

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
            float t = _Time.y * _Speed;
            float3 rise = IN.objPos * _Scale + float3(t * 0.03, t * 0.2, -t * 0.02);
            fixed4 noise = TriplanarSample(_NoiseTex, rise, weights);

            // Mostly cold charcoal ash with a few smouldering coal patches and small bright sparks.
            float coals = smoothstep(0.72, 0.95, 1.0 - noise.b);
            float sparks = smoothstep(0.6, 0.85, noise.r * noise.g);
            float breath = 0.75 + 0.25 * sin(t * 3.5 + noise.g * 8.0);

            fixed3 ash = _Color.rgb * _SkinColor.rgb * (0.7 + 0.9 * noise.r);
            fixed3 coal = lerp(ash, _AccentColor.rgb, coals);
            float3 emit = _AccentColor.rgb * coals * 0.6 * breath
                        + _EnergyColor.rgb * _EmissionColor.rgb * sparks * _Intensity * breath;

            o.Albedo = coal;
            o.Emission = emit;
            o.Metallic = min(_Metallic, 0.5);
            o.Smoothness = lerp(_Glossiness, 0.45, sparks);
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
