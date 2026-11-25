Shader "Skins/ChromaticShift"
{
    Properties
    {
        _Color ("Base Tint", Color) = (1, 1, 1, 1)
        _SkinColor ("Skin Color", Color) = (0.1, 0.1, 0.12, 1)
        _EnergyColor ("Shift A", Color) = (1.0, 0.2, 0.45, 1)
        _AccentColor ("Shift B", Color) = (0.2, 0.85, 1.0, 1)
        _MidColor ("Shift Mid", Color) = (1.0, 0.8, 0.2, 1)
        _EmissionColor ("Emission Color", Color) = (0.9, 0.9, 1.0, 1)
        _NoiseTex ("Noise (RGB)", 2D) = "gray" {}
        _Speed ("Shift Speed", Range(0,8)) = 1.1
        _Intensity ("Chrome Intensity", Range(0,8)) = 1.25
        _Scale ("Triplanar Scale", Float) = 1.2
        _BlendSharpness ("Blend Sharpness", Range(1,8)) = 4
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _Metallic ("Metallic", Range(0,1)) = 0.95
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
        fixed4 _MidColor;
        float _Speed;
        float _Intensity;
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

        float3 Palette3(float3 a, float3 m, float3 b, float x)
        {
            x = frac(x) * 3.0;
            float3 c = lerp(a, m, saturate(x));
            c = lerp(c, b, saturate(x - 1.0));
            return lerp(c, a, saturate(x - 2.0));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 weights = TriplanarWeights(IN.objNormal, _BlendSharpness);
            float t = _Time.y * _Speed;
            float3 pos = IN.objPos * _Scale;

            fixed4 noise = TriplanarSample(_NoiseTex, pos, weights);
            float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float ndotv = saturate(dot(normalize(IN.worldNormal), viewDir));
            float fresnel = pow(1.0 - ndotv, 2.5);

            // Three-stop colour ramp laid out in object space so the bands are visible without animation.
            float band = pos.x * 0.55 + pos.y * 0.45 + pos.z * 0.35 + noise.r * 0.8 + t * 0.2;
            float3 shift = Palette3(_EnergyColor.rgb, _MidColor.rgb, _AccentColor.rgb, band);
            shift = lerp(shift, _EmissionColor.rgb, fresnel * 0.35);

            fixed3 baseCol = _Color.rgb * _SkinColor.rgb;
            o.Albedo = lerp(baseCol, shift, 0.7);
            o.Emission = shift * (0.08 + fresnel * 0.6) * _Intensity * 0.3;
            o.Metallic = min(_Metallic, 0.5);
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
