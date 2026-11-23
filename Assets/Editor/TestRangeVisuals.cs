using UnityEngine;
using UnityEditor;
using System.IO;

public static class TestRangeVisuals
{
    private static Material matGround;
    private static Material matWall;
    private static Material matRamp;
    private static Material matWood;
    private static Material matConcrete;
    private static Material matTargetBody;
    private static Material matTargetHead;
    
    public static void EnsureAssets()
    {
        EnsureDir("Assets/TestRange");
        EnsureDir("Assets/TestRange/Textures");
        EnsureDir("Assets/TestRange/Materials");
        
        var groundTex = LoadOrCreateTexture("Assets/TestRange/Textures/Ground_Gravel.png", 256, MakeGravel);
        var wallTex = LoadOrCreateTexture("Assets/TestRange/Textures/Wall_Concrete.png", 256, MakeConcrete);
        var woodTex = LoadOrCreateTexture("Assets/TestRange/Textures/Wood_Plank.png", 128, MakeWood);
        
        matGround = LoadOrCreateMaterial("Assets/TestRange/Materials/Mat_Ground.mat", groundTex, 0.2f, 0.85f);
        matWall = LoadOrCreateMaterial("Assets/TestRange/Materials/Mat_Wall.mat", wallTex, 0.15f, 0.7f);
        matRamp = LoadOrCreateMaterial("Assets/TestRange/Materials/Mat_Ramp.mat", wallTex, 0.3f, 0.55f, new Color(0.55f, 0.45f, 0.38f));
        matWood = LoadOrCreateMaterial("Assets/TestRange/Materials/Mat_Wood.mat", woodTex, 0.05f, 0.35f, new Color(0.65f, 0.45f, 0.28f));
        matConcrete = LoadOrCreateMaterial("Assets/TestRange/Materials/Mat_ConcreteDark.mat", wallTex, 0.2f, 0.6f, new Color(0.35f, 0.36f, 0.38f));
        matTargetBody = CreateSolid("Assets/TestRange/Materials/Mat_TargetBody.mat", new Color(0.75f, 0.2f, 0.2f), 0.05f, 0.65f);
        matTargetHead = CreateSolid("Assets/TestRange/Materials/Mat_TargetHead.mat", new Color(0.95f, 0.92f, 0.88f), 0.02f, 0.8f);
        
        AssetDatabase.SaveAssets();
    }
    
    public static void ApplyScenePresentation()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0035f;
        RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.86f);
        
        var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        if (sky != null)
            RenderSettings.skybox = sky;
        
        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        }
        
        CreateFillLight();
    }
    
    public static void CreateProps()
    {
        CreateCrate("Crate_A", new Vector3(-3, 0.5f, 2), new Vector3(1.2f, 1f, 1.2f));
        CreateCrate("Crate_B", new Vector3(3.5f, 0.4f, 1), new Vector3(0.9f, 0.8f, 0.9f));
        CreateBarrier("Barrier_Left", new Vector3(-7, 0.6f, -2), new Vector3(2.5f, 1.2f, 0.4f));
        CreateBarrier("Barrier_Right", new Vector3(7, 0.6f, -2), new Vector3(2.5f, 1.2f, 0.4f));
        CreatePillar("Pillar_A", new Vector3(-2, 1.5f, 14));
        CreatePillar("Pillar_B", new Vector3(2, 1.5f, 14));
    }
    
    public static void ApplyMaterial(Renderer r, Material m)
    {
        if (r != null && m != null)
            r.sharedMaterial = m;
    }
    
    public static Material TargetBodyMaterial => matTargetBody;
    public static Material TargetHeadMaterial => matTargetHead;
    public static Material Ground => matGround;
    public static Material Wall => matWall;
    public static Material Ramp => matRamp;
    
    private static void CreateFillLight()
    {
        var fill = new GameObject("Fill Light");
        var light = fill.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.35f;
        light.color = new Color(0.7f, 0.82f, 1f);
        light.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(20f, 130f, 0f);
    }
    
    private static void CreateCrate(string name, Vector3 pos, Vector3 scale)
    {
        var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = name;
        crate.transform.position = pos;
        crate.transform.localScale = scale;
        crate.isStatic = true;
        ApplyMaterial(crate.GetComponent<Renderer>(), matWood);
    }
    
    private static void CreateBarrier(string name, Vector3 pos, Vector3 scale)
    {
        var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = name;
        barrier.transform.position = pos;
        barrier.transform.localScale = scale;
        barrier.isStatic = true;
        ApplyMaterial(barrier.GetComponent<Renderer>(), matConcrete);
    }
    
    private static void CreatePillar(string name, Vector3 pos)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = name;
        pillar.transform.position = pos;
        pillar.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
        pillar.isStatic = true;
        ApplyMaterial(pillar.GetComponent<Renderer>(), matWall);
    }
    
    private static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
    
    private static Texture2D LoadOrCreateTexture(string path, int size, System.Func<int, Texture2D> gen)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        
        var tex = gen(size);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    
    private static Material LoadOrCreateMaterial(string path, Texture2D albedo, float gloss, float metallic, Color? tint = null)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.mainTexture = albedo;
        mat.mainTextureScale = new Vector2(8f, 8f);
        mat.color = tint ?? Color.white;
        mat.SetFloat("_Glossiness", gloss);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }
    
    private static Material CreateSolid(string path, Color color, float gloss, float metallic)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Glossiness", gloss);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }
    
    private static Texture2D MakeGravel(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
                float c = 0.22f + n * 0.18f;
                tex.SetPixel(x, y, new Color(c * 0.9f, c * 0.95f, c));
            }
        }
        tex.Apply();
        return tex;
    }
    
    private static Texture2D MakeConcrete(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                float c = 0.45f + n * 0.12f;
                tex.SetPixel(x, y, new Color(c, c * 1.02f, c * 1.05f));
            }
        }
        tex.Apply();
        return tex;
    }
    
    private static Texture2D MakeWood(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.Sin(y * 0.35f) * 0.04f + Mathf.PerlinNoise(x * 0.05f, y * 0.25f) * 0.08f;
                float c = 0.5f + grain;
                tex.SetPixel(x, y, new Color(c * 1.05f, c * 0.8f, c * 0.55f));
            }
        }
        tex.Apply();
        return tex;
    }
}