using UnityEngine;

/// <summary>
/// Procedurally generated textures, meshes and materials shared by every shot effect, so no imported art is needed.
/// Everything is created on first use and recreated if the editor destroys it (play-mode exit, scene unload).
/// </summary>
internal static class ShotFxAssets
{
    private static Texture2D softTexture;
    private static Texture2D starTexture;
    private static Texture2D streakTexture;
    private static Texture2D holeTexture;
    private static Mesh quadMesh;
    private static Mesh streakMesh;
    private static Mesh casingMesh;
    private static Material softMaterial;
    private static Material starMaterial;
    private static Material streakMaterial;
    private static Material holeMaterial;
    private static Material casingMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        softTexture = null;
        starTexture = null;
        streakTexture = null;
        holeTexture = null;
        quadMesh = null;
        streakMesh = null;
        casingMesh = null;
        softMaterial = null;
        starMaterial = null;
        streakMaterial = null;
        holeMaterial = null;
        casingMaterial = null;
    }

    public static Material SoftMaterial => softMaterial != null ? softMaterial : softMaterial = CreateSpriteMaterial("ShotFx_Soft", SoftTexture());
    public static Material StarMaterial => starMaterial != null ? starMaterial : starMaterial = CreateSpriteMaterial("ShotFx_Star", StarTexture());
    public static Material StreakMaterial => streakMaterial != null ? streakMaterial : streakMaterial = CreateSpriteMaterial("ShotFx_Streak", StreakTexture());
    public static Material HoleMaterial => holeMaterial != null ? holeMaterial : holeMaterial = CreateSpriteMaterial("ShotFx_Hole", HoleTexture());

    public static Material CasingMaterial
    {
        get
        {
            if (casingMaterial != null)
                return casingMaterial;
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            casingMaterial = new Material(shader) { name = "ShotFx_Casing", hideFlags = HideFlags.HideAndDontSave };
            if (casingMaterial.HasProperty("_Metallic"))
                casingMaterial.SetFloat("_Metallic", 0.85f);
            if (casingMaterial.HasProperty("_Glossiness"))
                casingMaterial.SetFloat("_Glossiness", 0.7f);
            return casingMaterial;
        }
    }

    public static Texture2D SoftTexture()
    {
        if (softTexture == null)
            softTexture = Generate("ShotFx_SoftTex", 32, 32, (u, v) =>
            {
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                return new Color(1f, 1f, 1f, d * d * (3f - 2f * d));
            });
        return softTexture;
    }

    public static Texture2D StarTexture()
    {
        if (starTexture == null)
            starTexture = Generate("ShotFx_StarTex", 64, 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 1.35f), 1.6f);
                float angle = Mathf.Atan2(v, u);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2.5f)), 18f) * Mathf.Clamp01(1f - r) * 0.9f;
                float a = Mathf.Clamp01(core + spikes);
                Color c = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.97f, 0.8f), Mathf.Clamp01(core * 1.6f));
                c.a = a;
                return c;
            });
        return starTexture;
    }

    public static Texture2D StreakTexture()
    {
        if (streakTexture == null)
            streakTexture = Generate("ShotFx_StreakTex", 16, 64, (u, v) =>
            {
                float along = Mathf.Clamp01((v + 1f) * 0.5f);
                float across = Mathf.Clamp01(1f - Mathf.Abs(u));
                float a = Mathf.Pow(across, 1.4f) * Mathf.Pow(1f - along, 0.9f);
                Color c = Color.Lerp(new Color(1f, 0.5f, 0.12f), new Color(1f, 0.95f, 0.7f), across * (1f - along));
                c.a = a;
                return c;
            });
        return streakTexture;
    }

    public static Texture2D HoleTexture()
    {
        if (holeTexture == null)
            holeTexture = Generate("ShotFx_HoleTex", 32, 32, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float core = 1f - Mathf.Clamp01((r - 0.18f) / 0.12f);
                float scorch = Mathf.Clamp01(1f - r) * 0.35f;
                float a = Mathf.Clamp01(core + scorch * (1f - core));
                float shade = Mathf.Lerp(0.18f, 0.02f, core);
                return new Color(shade, shade, shade, a);
            });
        return holeTexture;
    }

    /// <summary>Unit quad in the XY plane centred on the origin.</summary>
    public static Mesh QuadMesh()
    {
        if (quadMesh == null)
        {
            quadMesh = new Mesh { name = "ShotFx_Quad", hideFlags = HideFlags.HideAndDontSave };
            quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quadMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quadMesh.RecalculateBounds();
        }

        return quadMesh;
    }

    /// <summary>Quad in the XZ plane running from z = 0 to z = 1 (bright end at z = 0): the forward spike of a muzzle flash.</summary>
    public static Mesh StreakMesh()
    {
        if (streakMesh == null)
        {
            streakMesh = new Mesh { name = "ShotFx_Streak", hideFlags = HideFlags.HideAndDontSave };
            streakMesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f)
            };
            streakMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            streakMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            streakMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            streakMesh.RecalculateBounds();
        }

        return streakMesh;
    }

    /// <summary>Capsule-ish cartridge case: a cylinder along the local Z axis, 1 unit long.</summary>
    public static Mesh CasingMesh()
    {
        if (casingMesh == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh source = temp.GetComponent<MeshFilter>().sharedMesh;
            casingMesh = Object.Instantiate(source);
            casingMesh.name = "ShotFx_Casing";
            casingMesh.hideFlags = HideFlags.HideAndDontSave;
            Vector3[] vertices = casingMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                vertices[i] = new Vector3(v.x, v.z, v.y * 0.5f);
            }

            casingMesh.vertices = vertices;
            int[] triangles = casingMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }

            casingMesh.triangles = triangles;
            casingMesh.RecalculateNormals();
            casingMesh.RecalculateBounds();
            if (Application.isPlaying)
                Object.Destroy(temp);
            else
                Object.DestroyImmediate(temp);
        }

        return casingMesh;
    }

    private static Material CreateSpriteMaterial(string name, Texture texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Standard");
        var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
        material.mainTexture = texture;
        return material;
    }

    private delegate Color Pixel(float u, float v);

    private static Texture2D Generate(string name, int width, int height, Pixel pixel)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width * 2f - 1f;
                float v = (y + 0.5f) / height * 2f - 1f;
                pixels[y * width + x] = pixel(u, v);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }
}
