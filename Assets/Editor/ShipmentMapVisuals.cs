using System;
using System.Collections.Generic;
using System.IO;
using CodMovementShooter.Maps.Shipment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Generates the procedural textures, materials and skybox for the Shipment map.
/// Everything is authored from code (no third-party assets): neon trim, glossy dark metal, holo signs.
/// </summary>
public static class ShipmentMapVisuals
{
    public const string RootDir = "Assets/Maps/Shipment";
    public const string TexturesDir = RootDir + "/Textures";
    public const string MaterialsDir = RootDir + "/Materials";

    private static readonly string[] SignKeys = { "Sign_Chevron", "Sign_Barcode", "Sign_Hazard", "Sign_Rings" };
    private static readonly string[] SignHues = { "Cyan", "Magenta", "Amber", "Violet" };

    public static Dictionary<string, Material> EnsureAssets()
    {
        EnsureFolder(RootDir);
        EnsureFolder(TexturesDir);
        EnsureFolder(MaterialsDir);

        var floorAlbedo = LoadOrCreateTexture("Floor_Albedo", 256, 256, false, TextureWrapMode.Repeat, FloorAlbedoPixel);
        var floorEmission = LoadOrCreateTexture("Floor_Emission", 256, 256, false, TextureWrapMode.Repeat, FloorEmissionPixel);
        var containerBody = LoadOrCreateTexture("Container_Corrugated", 256, 256, false, TextureWrapMode.Repeat, ContainerPixel);
        var wallPanel = LoadOrCreateTexture("Wall_Panel", 256, 256, false, TextureWrapMode.Repeat, WallPanelPixel);
        var hazardAlbedo = LoadOrCreateTexture("Hazard_Stripes", 128, 128, false, TextureWrapMode.Repeat, (x, y) => HazardPixel(x, y, false));
        var hazardEmission = LoadOrCreateTexture("Hazard_Emission", 128, 128, false, TextureWrapMode.Repeat, (x, y) => HazardPixel(x, y, true));
        var windowsAAlbedo = LoadOrCreateTexture("Skyline_Windows_A", 128, 256, false, TextureWrapMode.Clamp, (x, y) => WindowPixel(x, y, 11, false));
        var windowsAEmission = LoadOrCreateTexture("Skyline_Windows_A_Emission", 128, 256, false, TextureWrapMode.Clamp, (x, y) => WindowPixel(x, y, 11, true));
        var windowsBAlbedo = LoadOrCreateTexture("Skyline_Windows_B", 128, 256, false, TextureWrapMode.Clamp, (x, y) => WindowPixel(x, y, 47, false));
        var windowsBEmission = LoadOrCreateTexture("Skyline_Windows_B_Emission", 128, 256, false, TextureWrapMode.Clamp, (x, y) => WindowPixel(x, y, 47, true));

        var signTextures = new Dictionary<string, Texture2D>();
        for (int i = 0; i < SignKeys.Length; i++)
        {
            int pattern = i;
            signTextures[SignKeys[i]] = LoadOrCreateTexture(SignKeys[i], 256, 128, true, TextureWrapMode.Clamp,
                (x, y) => SignPixel(x, y, pattern));
        }

        var materials = new Dictionary<string, Material>();

        materials["Floor"] = LoadOrCreateMaterial("Mat_Floor", new Color(0.8f, 0.85f, 0.95f), 0.85f, 0.92f,
            floorAlbedo, new Vector2(27f, 18f), new Color(1f, 1f, 1f) * 1.3f, floorEmission, false);
        materials["WallPanel"] = LoadOrCreateMaterial("Mat_WallPanel", new Color(0.55f, 0.6f, 0.72f), 0.6f, 0.6f,
            wallPanel, new Vector2(12f, 2f), Color.black, null, false);
        materials["Barrier"] = LoadOrCreateMaterial("Mat_Barrier", new Color(0.3f, 0.5f, 0.6f), 0.55f, 0.55f,
            wallPanel, new Vector2(1f, 1f), Color.black, null, false);
        materials["CraneSteel"] = LoadOrCreateMaterial("Mat_CraneSteel", new Color(0.42f, 0.38f, 0.32f), 0.7f, 0.45f,
            wallPanel, new Vector2(1f, 4f), Color.black, null, false);
        materials["Hazard"] = LoadOrCreateMaterial("Mat_Hazard", Color.white, 0.4f, 0.5f,
            hazardAlbedo, new Vector2(2f, 2f), Color.white * 0.7f, hazardEmission, false);
        materials["EnergyField"] = LoadOrCreateMaterial("Mat_EnergyField", new Color(0.1f, 0.8f, 1f, 0.09f), 0f, 0.9f,
            null, Vector2.one, new Color(0.1f, 0.8f, 1f) * 0.8f, null, true);
        materials["Water"] = LoadOrCreateMaterial("Mat_Water", new Color(0.015f, 0.04f, 0.09f), 0.3f, 0.95f,
            null, Vector2.one, new Color(0.01f, 0.04f, 0.1f), null, false);
        materials["Skyline_A"] = LoadOrCreateMaterial("Mat_Skyline_A", Color.white, 0.2f, 0.3f,
            windowsAAlbedo, Vector2.one, Color.white * 1.5f, windowsAEmission, false);
        materials["Skyline_B"] = LoadOrCreateMaterial("Mat_Skyline_B", Color.white, 0.2f, 0.3f,
            windowsBAlbedo, Vector2.one, Color.white * 1.5f, windowsBEmission, false);

        foreach (string hue in ShipmentLayout.Hues)
        {
            Color c = ShipmentLayout.HueColor(hue);
            materials[ShipmentLayout.BodyMaterial(hue)] = LoadOrCreateMaterial("Mat_Body_" + hue,
                new Color(c.r * 0.5f + 0.06f, c.g * 0.5f + 0.06f, c.b * 0.5f + 0.06f), 0.55f, 0.5f,
                containerBody, Vector2.one, Color.black, null, false);
            materials[ShipmentLayout.TrimMaterial(hue)] = LoadOrCreateMaterial("Mat_Trim_" + hue,
                new Color(0.02f, 0.02f, 0.02f), 0f, 0.6f,
                null, Vector2.one, c * 2.4f, null, false);
        }

        for (int i = 0; i < SignKeys.Length; i++)
        {
            Color c = ShipmentLayout.HueColor(SignHues[i]);
            materials[SignKeys[i]] = LoadOrCreateMaterial("Mat_" + SignKeys[i], new Color(c.r, c.g, c.b, 1f), 0f, 0.8f,
                signTextures[SignKeys[i]], Vector2.one, c * 1.8f, signTextures[SignKeys[i]], true);
        }

        materials["Skybox"] = CreateSkybox();

        AssetDatabase.SaveAssets();
        return materials;
    }

    public static void ApplyScenePresentation(Material skybox)
    {
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.12f, 0.12f, 0.3f);
        RenderSettings.ambientEquatorColor = new Color(0.1f, 0.07f, 0.2f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.08f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.reflectionIntensity = 1f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.011f;
        RenderSettings.fogColor = new Color(0.07f, 0.04f, 0.15f);
    }

    private static Material CreateSkybox()
    {
        string path = MaterialsDir + "/Mat_Skybox_NightCity.mat";
        var shader = Shader.Find("Skybox/Procedural");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }

        if (shader != null)
        {
            mat.shader = shader;
            mat.EnableKeyword("_SUNDISK_NONE");
            mat.DisableKeyword("_SUNDISK_SIMPLE");
            mat.DisableKeyword("_SUNDISK_HIGH_QUALITY");
            SetFloat(mat, "_SunDisk", 0f);
            SetFloat(mat, "_SunSize", 0f);
            SetFloat(mat, "_AtmosphereThickness", 0.45f);
            SetFloat(mat, "_Exposure", 0.6f);
            SetColor(mat, "_SkyTint", new Color(0.32f, 0.12f, 0.55f));
            SetColor(mat, "_GroundColor", new Color(0.05f, 0.03f, 0.1f));
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LoadOrCreateMaterial(string name, Color color, float metallic, float smoothness,
        Texture2D albedo, Vector2 tiling, Color emission, Texture2D emissionMap, bool transparent)
    {
        string path = MaterialsDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness);
        mat.mainTexture = albedo;
        mat.mainTextureScale = tiling;
        mat.mainTextureOffset = Vector2.zero;

        if (transparent)
        {
            mat.SetFloat("_Mode", 2f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            mat.SetFloat("_Mode", 0f);
            mat.SetOverrideTag("RenderType", "");
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;
        }

        bool emissive = emission.maxColorComponent > 0.001f;
        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.SetTexture("_EmissionMap", emissionMap);
            if (emissionMap != null)
            {
                mat.SetTextureScale("_EmissionMap", tiling);
                mat.SetTextureOffset("_EmissionMap", Vector2.zero);
            }
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetFloat(Material mat, string property, float value)
    {
        if (mat.HasProperty(property))
            mat.SetFloat(property, value);
    }

    private static void SetColor(Material mat, string property, Color value)
    {
        if (mat.HasProperty(property))
            mat.SetColor(property, value);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static Texture2D LoadOrCreateTexture(string name, int width, int height, bool alpha,
        TextureWrapMode wrap, Func<int, int, Color> pixel)
    {
        string path = TexturesDir + "/" + name + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            return existing;

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y);
        }
        tex.SetPixels(pixels);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = alpha;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    private static Color FloorAlbedoPixel(int x, int y)
    {
        float brushed = Hash(x / 3, y, 1) * 0.55f + Hash(x, y, 2) * 0.45f;
        float v = 0.05f + brushed * 0.035f;
        if (x < 2 || y < 2)
            v = 0.018f;
        else if (x == 2 || y == 2)
            v += 0.03f;
        return new Color(v * 0.9f, v * 0.95f, v * 1.15f, 1f);
    }

    private static Color FloorEmissionPixel(int x, int y)
    {
        bool seam = x < 1 || y < 1;
        bool node = (x < 6 && y < 6);
        if (seam || node)
            return new Color(0f, 0.38f, 0.55f, 1f);
        return Color.black;
    }

    private static Color ContainerPixel(int x, int y)
    {
        float u = x / 256f;
        float rib = 0.5f + 0.5f * Mathf.Cos(u * Mathf.PI * 2f * 8f);
        float grime = Hash(x / 4, y / 4, 3) * 0.12f + Hash(x, y, 4) * 0.04f;
        float streak = Hash(x / 3, 0, 5) * 0.06f * (y / 256f);
        float v = 0.38f + rib * 0.22f + grime - streak;
        if (y < 6 || y > 249)
            v -= 0.12f;
        v = Mathf.Clamp01(v);
        return new Color(v, v, v, 1f);
    }

    private static Color WallPanelPixel(int x, int y)
    {
        int cx = x % 64;
        int cy = y % 128;
        float v = 0.2f + Hash(x / 2, y / 2, 6) * 0.05f;
        if (cx < 2 || cy < 2)
            v = 0.08f;
        else if (cx == 2 || cy == 2)
            v += 0.06f;
        bool rivet = (cx == 6 || cx == 57) && (cy == 6 || cy == 121);
        if (rivet)
            v = 0.45f;
        return new Color(v * 0.95f, v, v * 1.1f, 1f);
    }

    private static Color HazardPixel(int x, int y, bool emission)
    {
        bool stripe = ((x + y) / 16) % 2 == 0;
        Color amber = new Color(1f, 0.62f, 0.1f, 1f);
        if (emission)
            return stripe ? amber * 0.6f : Color.black;
        return stripe ? amber : new Color(0.05f, 0.05f, 0.06f, 1f);
    }

    private static Color WindowPixel(int x, int y, int seed, bool emission)
    {
        const int cell = 16;
        int cx = x / cell;
        int cy = y / cell;
        int lx = x % cell;
        int ly = y % cell;
        bool inWindow = lx >= 3 && lx < cell - 3 && ly >= 4 && ly < cell - 4;
        bool lit = inWindow && Hash(cx, cy, seed) < 0.38f;

        if (!lit)
        {
            if (emission)
                return Color.black;
            float v = 0.035f + Hash(x, y, seed + 1) * 0.02f;
            return new Color(v * 0.9f, v, v * 1.4f, 1f);
        }

        float pick = Hash(cx, cy, seed + 2);
        Color c = pick < 0.5f ? new Color(1f, 0.78f, 0.45f)
            : pick < 0.78f ? new Color(0.35f, 0.9f, 1f)
            : new Color(1f, 0.35f, 0.8f);
        float level = 0.55f + Hash(cx, cy, seed + 3) * 0.45f;
        return emission ? c * level : c * 0.35f;
    }

    private static Color SignPixel(int x, int y, int pattern)
    {
        const int w = 256;
        const int h = 128;
        bool border = x < 4 || y < 4 || x >= w - 4 || y >= h - 4;
        bool lit = border;

        if (!lit && y >= 16 && y < h - 16 && x >= 12 && x < w - 12)
        {
            switch (pattern)
            {
                case 0:
                {
                    int d = Mathf.Abs(y - h / 2);
                    int band = ((x - d) % 48 + 48) % 48;
                    lit = band < 16 && d < 44;
                    break;
                }
                case 1:
                {
                    lit = Hash(x / 4, 0, 21) < 0.55f && y > 20 && y < 100;
                    if (y >= 106 && y < 114)
                        lit = Hash(x / 6, 1, 22) < 0.6f;
                    break;
                }
                case 2:
                {
                    lit = ((x + y) / 12) % 2 == 0 && y > 22 && y < h - 22;
                    break;
                }
                default:
                {
                    float dx = x - w / 2f;
                    float dy = y - h / 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool ring = r < 56f && (r % 16f) < 3f;
                    bool cross = (Mathf.Abs(dx) < 2f || Mathf.Abs(dy) < 2f) && r < 60f;
                    lit = ring || cross;
                    break;
                }
            }
        }

        float alpha = lit ? 0.95f : 0.1f;
        return new Color(1f, 1f, 1f, alpha);
    }
}
