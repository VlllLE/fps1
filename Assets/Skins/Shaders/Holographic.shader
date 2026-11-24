Shader "Skins/Holographic"
{
    Properties
    {
        _Color ("Base Tint", Color) = (0.05, 0.12, 0.18, 1)
        _SkinColor ("Skin Color", Color) = (0.05, 0.12, 0.18, 1)
        _AccentColor ("Iridescent A", Color) = (0.2, 0.95, 1.0, 1)
        _EnergyColor ("Iridescent B", Color) = (1.0, 0.25, 0.85, 1)
        _EmissionColor ("Emission Color", Color) = (0.4, 0.9, 1.0, 1)
        _NoiseTex ("Noise (RGB)", 2D) = "gray" {}
        _Speed ("Shift Speed", Range(0,8)) = 1.0
        _Intensity ("Holo Intensity", Range(0,8)) = 1.75
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 3.0
        _Scale ("Triplanar Scale", Float) = 1.5
        _BlendSharpness ("Blend Sharpness", Range(1,8)) = 4
        _Glossiness ("Smoothness", Range(0,1)) = 0.85
        _Metallic ("Metallic", Range(0,1)) = 0.2
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
        float _Speed;
        float _Intensity;
        float _FresnelPower;
        float _Scale;
        float _BlendSharpness;

        struct Input
        {
            float3 objPos;
            float3 objNormal;
            float3 worldPos;
            float3 worldNormal;
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
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);
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

            fixed4 noise = TriplanarSample(_NoiseTex, pos + float3(t * 0.05, t * 0.03, 0.0), weights);
            float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float ndotv = saturate(dot(normalize(IN.worldNormal), viewDir));
            float fresnel = pow(1.0 - ndotv, _FresnelPower);

            float shift = frac(noise.r * 0.65 + noise.g * 0.35 + t * 0.12 + fresnel);
            fixed3 iridescence = lerp(_AccentColor.rgb, _EnergyColor.rgb, shift);
            iridescence = lerp(iridescence, _EmissionColor.rgb, fresnel * 0.5);

            // Subtle scan lines in object space so the mesh carries the holo look.
            float scan = 0.65 + 0.35 * sin((IN.objPos.y + t * 0.4) * 8.0);

            fixed3 baseCol = _Color.rgb * _SkinColor.rgb;
            fixed3 holo = iridescence * (0.35 + fresnel) * scan * _Intensity;

            o.Albedo = baseCol + holo * 0.25;
            o.Emission = holo;
            o.Metallic = min(_Metallic, 0.5);
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
