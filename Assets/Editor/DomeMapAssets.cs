using System.IO;
using UnityEditor;
using UnityEngine;

namespace FpsMaps.Dome.EditorTools
{
    /// <summary>Generates every texture, material and mesh asset used by the Dome map (no external art).</summary>
    public static class DomeMapAssets
    {
        public const string TextureDir = DomeLayout.RootFolder + "/Textures";
        public const string MaterialDir = DomeLayout.RootFolder + "/Materials";
        public const string MeshDir = DomeLayout.RootFolder + "/Meshes";

        private const float HexAspect = 1.7320508f;
        private static readonly Color Teal = new Color(0.10f, 0.95f, 0.90f);
        private static readonly Color Violet = new Color(0.62f, 0.28f, 1.00f);

        public static Material Floor { get; private set; }
        public static Material FloorDisc { get; private set; }
        public static Material Wall { get; private set; }
        public static Material Drum { get; private set; }
        public static Material Platform { get; private set; }
        public static Material Cargo { get; private set; }
        public static Material Vehicle { get; private set; }
        public static Material TrimTeal { get; private set; }
        public static Material TrimViolet { get; private set; }
        public static Material Glass { get; private set; }
        public static Material HoloBars { get; private set; }
        public static Material HoloRadar { get; private set; }
        public static Material HoloGlobe { get; private set; }
        public static Material TargetBody { get; private set; }
        public static Material TargetHead { get; private set; }
        public static Material Skybox { get; private set; }

        public static Mesh GlassMesh { get; private set; }
        public static Mesh StrutMesh { get; private set; }
        public static Mesh ShellColliderMesh { get; private set; }
        public static Mesh HexPrismMesh { get; private set; }
        public static int PanelCount { get; private set; }

        public static void EnsureAll()
        {
            EnsureFolder(DomeLayout.RootFolder);
            EnsureFolder(TextureDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(MeshDir);

            Texture2D floorAlbedo = WriteTexture("Floor_Albedo", 512, 512, MakeFloorAlbedo, true, TextureWrapMode.Repeat);
            Texture2D floorEmission = WriteTexture("Floor_Circuit_Emission", 512, 512, MakeFloorCircuit, true, TextureWrapMode.Repeat);
            Texture2D wallAlbedo = WriteTexture("Wall_Hex_Albedo", 256, 444, MakeHexAlbedo, true, TextureWrapMode.Repeat);
            Texture2D wallEmission = WriteTexture("Wall_Hex_Emission", 256, 444, MakeHexEmission, true, TextureWrapMode.Repeat);
            Texture2D crateAlbedo = WriteTexture("Cargo_Albedo", 256, 256, MakeCrateAlbedo, true, TextureWrapMode.Repeat);
            Texture2D crateEmission = WriteTexture("Cargo_Emission", 256, 256, MakeCrateEmission, true, TextureWrapMode.Repeat);
            Texture2D holoBars = WriteTexture("Holo_Bars", 256, 256, MakeHoloBars, true, TextureWrapMode.Clamp);
            Texture2D holoRadar = WriteTexture("Holo_Radar", 256, 256, MakeHoloRadar, true, TextureWrapMode.Clamp);
            Texture2D holoGlobe = WriteTexture("Holo_Globe", 256, 256, MakeHoloGlobe, true, TextureWrapMode.Repeat);
            Texture2D sky = WriteTexture("Sky_Nebula", 1024, 512, MakeSky, true, TextureWrapMode.Clamp);

            Shader tiled = LoadShader("Assets/Maps/Dome/Shaders/DomeWorldTiled.shader", "Dome/WorldTiled");
            Shader glass = LoadShader("Assets/Maps/Dome/Shaders/DomeEnergyGlass.shader", "Dome/EnergyGlass");
            Shader holo = LoadShader("Assets/Maps/Dome/Shaders/DomeHoloDisplay.shader", "Dome/HoloDisplay");
            Shader standard = Shader.Find("Standard");

            Floor = ConfigureTiled("Mat_Floor", tiled, floorAlbedo, floorEmission, new Color(0.9f, 0.95f, 1f),
                new Color(1f, 1f, 1f) * 1.6f, 8f, 1f, 0.75f, 0.55f, 0.45f);
            FloorDisc = ConfigureTiled("Mat_FloorDisc", tiled, floorAlbedo, floorEmission, new Color(0.8f, 0.85f, 1f),
                new Color(1f, 1f, 1f) * 2.4f, 5f, 1f, 0.85f, 0.6f, 0.9f);
            Wall = ConfigureTiled("Mat_Wall", tiled, wallAlbedo, wallEmission, new Color(1f, 1f, 1f),
                new Color(1f, 1f, 1f) * 1.7f, 3.2f, HexAspect, 0.5f, 0.65f, 0.35f);
            Drum = ConfigureTiled("Mat_Drum", tiled, wallAlbedo, wallEmission, new Color(0.75f, 0.85f, 1f),
                new Color(0.8f, 0.9f, 1f) * 2.0f, 2.6f, HexAspect, 0.6f, 0.7f, 0.5f);
            Platform = ConfigureTiled("Mat_Platform", tiled, wallAlbedo, wallEmission, new Color(0.9f, 0.8f, 1f),
                new Color(1f, 0.85f, 1f) * 2.2f, 1.8f, HexAspect, 0.7f, 0.75f, 0.6f);
            Cargo = ConfigureTiled("Mat_Cargo", tiled, crateAlbedo, crateEmission, Color.white,
                new Color(1f, 1f, 1f) * 1.8f, 2f, 1f, 0.45f, 0.5f, 0f);
            Vehicle = ConfigureTiled("Mat_Vehicle", tiled, crateAlbedo, crateEmission, new Color(0.55f, 0.6f, 0.75f),
                new Color(0.7f, 0.7f, 1f) * 1.4f, 2.4f, 1f, 0.85f, 0.8f, 0f);

            TrimTeal = ConfigureStandardEmissive("Mat_Trim_Teal", standard, new Color(0.02f, 0.05f, 0.06f), Teal * 2.6f);
            TrimViolet = ConfigureStandardEmissive("Mat_Trim_Violet", standard, new Color(0.04f, 0.02f, 0.07f), Violet * 2.6f);
            TargetBody = ConfigureStandardEmissive("Mat_Target_Body", standard, new Color(0.12f, 0.04f, 0.14f), new Color(0.95f, 0.2f, 0.7f) * 1.2f);
            TargetHead = ConfigureStandardEmissive("Mat_Target_Head", standard, new Color(0.9f, 0.95f, 1f), new Color(0.5f, 0.9f, 1f) * 0.8f);

            Glass = ConfigureGlass("Mat_EnergyGlass", glass);
            HoloBars = ConfigureHolo("Mat_Holo_Bars", holo, holoBars, Teal, 1.7f);
            HoloRadar = ConfigureHolo("Mat_Holo_Radar", holo, holoRadar, Violet, 1.9f);
            HoloGlobe = ConfigureHolo("Mat_Holo_Globe", holo, holoGlobe, new Color(0.35f, 1f, 0.95f), 1.5f);
            Skybox = ConfigureSkybox("Mat_Skybox_Nebula", sky);

            BuildMeshes();
            AssetDatabase.SaveAssets();
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        #region Meshes

        private static void BuildMeshes()
        {
            var panels = DomeGeometry.BuildPanels(
                DomeLayout.GeodesicFrequency,
                DomeLayout.DomeRadius,
                DomeLayout.DomeHeightScale,
                0.6f,
                DomeLayout.DoorAzimuthsDeg,
                DomeLayout.DoorClearHeight,
                DomeLayout.DoorHalfAngleDeg);
            PanelCount = panels.Count;

            GlassMesh = SaveMesh(DomeGeometry.BuildGlassMesh(panels, 0.94f), MeshDir + "/DomeGlass.asset");
            StrutMesh = SaveMesh(DomeGeometry.BuildStrutMesh(panels, 0.22f), MeshDir + "/DomeStruts.asset");
            ShellColliderMesh = SaveMesh(DomeGeometry.BuildShellColliderMesh(panels), MeshDir + "/DomeShellCollider.asset");
            HexPrismMesh = SaveMesh(DomeGeometry.BuildPrism(6, 0f), MeshDir + "/HexPrism.asset");
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        #endregion

        #region Materials

        private static Shader LoadShader(string path, string fallbackName)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
                shader = Shader.Find(fallbackName);
            if (shader == null)
                shader = Shader.Find("Standard");
            return shader;
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            return mat;
        }

        private static Material ConfigureTiled(
            string name, Shader shader, Texture2D albedo, Texture2D emission, Color tint, Color emissionColor,
            float tileSize, float aspect, float smoothness, float metallic, float pulse)
        {
            Material mat = LoadOrCreate(name, shader);
            SetTexture(mat, "_MainTex", albedo);
            SetTexture(mat, "_EmissionMap", emission);
            SetColor(mat, "_Color", tint);
            SetColor(mat, "_EmissionColor", emissionColor);
            SetFloat(mat, "_TileSize", tileSize);
            SetFloat(mat, "_TileAspect", aspect);
            SetFloat(mat, "_Glossiness", smoothness);
            SetFloat(mat, "_Metallic", metallic);
            SetFloat(mat, "_PulseAmount", pulse);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ConfigureStandardEmissive(string name, Shader standard, Color albedo, Color emission)
        {
            Material mat = LoadOrCreate(name, standard);
            SetColor(mat, "_Color", albedo);
            SetFloat(mat, "_Metallic", 0.3f);
            SetFloat(mat, "_Glossiness", 0.75f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SetColor(mat, "_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ConfigureGlass(string name, Shader shader)
        {
            Material mat = LoadOrCreate(name, shader);
            SetColor(mat, "_TintColor", new Color(0.06f, 0.5f, 0.62f, 0.14f));
            SetColor(mat, "_RimColor", new Color(0.25f, 1f, 0.95f, 1f));
            SetColor(mat, "_PulseColor", new Color(0.65f, 0.3f, 1f, 1f));
            SetFloat(mat, "_HexScale", 3f);
            SetFloat(mat, "_HexWidth", 0.08f);
            SetFloat(mat, "_RimPower", 2.2f);
            SetFloat(mat, "_RimIntensity", 1.4f);
            SetFloat(mat, "_PulseSpeed", 0.35f);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ConfigureHolo(string name, Shader shader, Texture2D map, Color tint, float intensity)
        {
            Material mat = LoadOrCreate(name, shader);
            SetTexture(mat, "_MainTex", map);
            SetColor(mat, "_Color", new Color(tint.r, tint.g, tint.b, 1f));
            SetFloat(mat, "_Intensity", intensity);
            SetFloat(mat, "_ScanSpeed", 2.5f);
            SetFloat(mat, "_ScanDensity", 220f);
            SetFloat(mat, "_Flicker", 0.25f);
            mat.renderQueue = 3100;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ConfigureSkybox(string name, Texture2D sky)
        {
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
                shader = Shader.Find("Skybox/Procedural");
            Material mat = LoadOrCreate(name, shader);
            SetTexture(mat, "_MainTex", sky);
            SetFloat(mat, "_Exposure", 1.15f);
            SetFloat(mat, "_Rotation", 40f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetTexture(Material mat, string prop, Texture tex)
        {
            if (mat.HasProperty(prop))
                mat.SetTexture(prop, tex);
        }

        private static void SetColor(Material mat, string prop, Color color)
        {
            if (mat.HasProperty(prop))
                mat.SetColor(prop, color);
        }

        private static void SetFloat(Material mat, string prop, float value)
        {
            if (mat.HasProperty(prop))
                mat.SetFloat(prop, value);
        }

        #endregion

        #region Textures

        private delegate void PixelFill(Color32[] pixels, int width, int height);

        private static Texture2D WriteTexture(
            string name, int width, int height, PixelFill fill, bool mipmaps, TextureWrapMode wrap)
        {
            string path = TextureDir + "/" + name + ".png";
            var pixels = new Color32[width * height];
            fill(pixels, width, height);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.wrapMode = wrap;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = mipmaps;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665423);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static int Mod(int v, int m)
        {
            int r = v % m;
            return r < 0 ? r + m : r;
        }

        private static Color32 ToColor32(Color c)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f),
                255);
        }

        private static void Fill(Color32[] px, Color color)
        {
            Color32 c = ToColor32(color);
            for (int i = 0; i < px.Length; i++)
                px[i] = c;
        }

        private static void MaxPaint(Color32[] px, int w, int h, int x, int y, Color c)
        {
            x = Mod(x, w);
            y = Mod(y, h);
            int i = y * w + x;
            Color32 o = px[i];
            Color32 n = ToColor32(c);
            px[i] = new Color32(
                (byte)Mathf.Max(o.r, n.r),
                (byte)Mathf.Max(o.g, n.g),
                (byte)Mathf.Max(o.b, n.b),
                255);
        }

        private static void MakeFloorAlbedo(Color32[] px, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float grain = Hash(x, y, 3) * 0.025f;
                    float panel = Hash(x / 128, y / 128, 9) * 0.03f;
                    float seam = (x % 128 < 2 || y % 128 < 2) ? 0.06f : 0f;
                    float bevel = (x % 128 < 5 || y % 128 < 5) ? 0.012f : 0f;
                    float v = 0.05f + grain + panel + seam + bevel;
                    px[y * w + x] = ToColor32(new Color(v * 0.9f, v * 1.0f, v * 1.35f));
                }
            }
        }

        private static void MakeFloorCircuit(Color32[] px, int w, int h)
        {
            Fill(px, Color.black);
            const int cell = 16;
            int cells = w / cell;
            var rng = new System.Random(20260);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (x % 128 == 0 || y % 128 == 0)
                        MaxPaint(px, w, h, x, y, Teal * 0.12f);
                }
            }

            for (int t = 0; t < 90; t++)
            {
                int cx = rng.Next(cells);
                int cy = rng.Next(cells);
                int dir = rng.Next(4);
                Color col = rng.NextDouble() < 0.62 ? Teal : Violet;
                int steps = rng.Next(3, 10);
                for (int s = 0; s < steps; s++)
                {
                    int run = rng.Next(1, 5);
                    for (int r = 0; r < run; r++)
                    {
                        int dx = dir == 0 ? 1 : dir == 2 ? -1 : 0;
                        int dy = dir == 1 ? 1 : dir == 3 ? -1 : 0;
                        int px0 = cx * cell + cell / 2;
                        int py0 = cy * cell + cell / 2;
                        for (int k = 0; k <= cell; k++)
                        {
                            int x = px0 + dx * k;
                            int y = py0 + dy * k;
                            MaxPaint(px, w, h, x, y, col * 0.95f);
                            MaxPaint(px, w, h, x + (dy != 0 ? 1 : 0), y + (dx != 0 ? 1 : 0), col * 0.55f);
                            MaxPaint(px, w, h, x - (dy != 0 ? 1 : 0), y - (dx != 0 ? 1 : 0), col * 0.55f);
                        }

                        cx = Mod(cx + dx, cells);
                        cy = Mod(cy + dy, cells);
                    }

                    dir = (dir + (rng.Next(2) == 0 ? 1 : 3)) % 4;
                }

                int padX = cx * cell + cell / 2;
                int padY = cy * cell + cell / 2;
                for (int oy = -5; oy <= 5; oy++)
                {
                    for (int ox = -5; ox <= 5; ox++)
                    {
                        float d = Mathf.Sqrt(ox * ox + oy * oy);
                        if (d <= 2.6f)
                            MaxPaint(px, w, h, padX + ox, padY + oy, col);
                        else if (d >= 4f && d <= 5f)
                            MaxPaint(px, w, h, padX + ox, padY + oy, col * 0.6f);
                    }
                }
            }
        }

        private static void HexCell(float px, float py, out float gx, out float gy, out float idx, out float idy)
        {
            const float rx = 1f;
            const float ry = 1.7320508f;
            const float hx = 0.5f;
            const float hy = 0.8660254f;
            float ax = px - rx * Mathf.Floor(px / rx) - hx;
            float ay = py - ry * Mathf.Floor(py / ry) - hy;
            float sx = px - hx;
            float sy = py - hy;
            float bx = sx - rx * Mathf.Floor(sx / rx) - hx;
            float by = sy - ry * Mathf.Floor(sy / ry) - hy;
            if (ax * ax + ay * ay < bx * bx + by * by)
            {
                gx = ax;
                gy = ay;
            }
            else
            {
                gx = bx;
                gy = by;
            }

            idx = px - gx;
            idy = py - gy;
        }

        private static float HexEdge(float gx, float gy)
        {
            float ax = Mathf.Abs(gx);
            float ay = Mathf.Abs(gy);
            float c = ax * 0.5f + ay * 0.8660254f;
            return 0.5f - Mathf.Max(c, ax);
        }

        private static void HexSample(int x, int y, int w, int h, out float edge, out float cellHash)
        {
            float px = x / (float)w * 2f;
            float py = y / (float)h * 2f * HexAspect;
            HexCell(px, py, out float gx, out float gy, out float idx, out float idy);
            edge = HexEdge(gx, gy);
            int ix = Mod(Mathf.RoundToInt(idx * 2f), 4);
            int iy = Mod(Mathf.RoundToInt(idy / 0.8660254f), 4);
            cellHash = Hash(ix, iy, 17);
        }

        private static void MakeHexAlbedo(Color32[] px, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    HexSample(x, y, w, h, out float edge, out float cellHash);
                    float v = 0.07f * (0.8f + 0.4f * cellHash) + Hash(x, y, 5) * 0.012f;
                    if (edge < 0.035f)
                        v = 0.015f;
                    else if (edge < 0.09f)
                        v *= 1.5f;
                    px[y * w + x] = ToColor32(new Color(v * 0.85f, v * 0.95f, v * 1.3f));
                }
            }
        }

        private static void MakeHexEmission(Color32[] px, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    HexSample(x, y, w, h, out float edge, out float cellHash);
                    Color c = Color.black;
                    if (edge < 0.03f)
                    {
                        if (cellHash < 0.45f)
                            c = Teal * 0.28f;
                        else if (cellHash < 0.82f)
                            c = Teal * 0.75f;
                        else
                            c = Violet * 0.95f;
                    }
                    else if (cellHash > 0.9f && edge > 0.12f && edge < 0.16f)
                    {
                        c = Violet * 0.55f;
                    }
                    else if (cellHash > 0.93f && edge >= 0.16f)
                    {
                        c = Violet * 0.12f;
                    }

                    px[y * w + x] = ToColor32(c);
                }
            }
        }

        private static void MakeCrateAlbedo(Color32[] px, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    float v = 0.13f + Hash(x, y, 11) * 0.02f;
                    if (edge < 14)
                        v = 0.22f + Hash(x / 8, y / 8, 4) * 0.02f;
                    if (Mathf.Abs(x - y) < 6 || Mathf.Abs(x + y - (w - 1)) < 6)
                        v = 0.2f;
                    px[y * w + x] = ToColor32(new Color(v * 0.9f, v * 0.95f, v * 1.25f));
                }
            }
        }

        private static void MakeCrateEmission(Color32[] px, int w, int h)
        {
            Fill(px, Color.black);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    if (edge >= 20 && edge < 23)
                        px[y * w + x] = ToColor32(Violet * 0.9f);
                    else if (edge >= 2 && edge < 4)
                        px[y * w + x] = ToColor32(Teal * 0.45f);
                    else if (Mathf.Abs(x - w / 2) < 9 && Mathf.Abs(y - h / 2) < 9 && (Mathf.Abs(x - w / 2) > 6 || Mathf.Abs(y - h / 2) > 6))
                        px[y * w + x] = ToColor32(Teal * 0.8f);
                }
            }
        }

        private static void SetMask(Color32[] px, int w, int h, int x, int y, float v)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
                return;
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
            int i = y * w + x;
            if (b > px[i].r)
                px[i] = new Color32(b, b, b, 255);
        }

        private static void MakeHoloBars(Color32[] px, int w, int h)
        {
            Fill(px, Color.black);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = 0f;
                    if (x < 4 || y < 4 || x >= w - 4 || y >= h - 4)
                        v = 0.9f;
                    else if (y > h - 40 && y < h - 12 && x > 14 && x < w - 14)
                        v = 0.45f + 0.2f * Hash(x / 6, 1, 2);
                    else if (x % 32 == 0 || y % 32 == 0)
                        v = 0.12f;

                    int bar = (x - 20) / 18;
                    if (x >= 20 && x < 20 + 12 * 18 && (x - 20) % 18 < 13 && y > 20 && y < h - 50)
                    {
                        float height = 20f + Hash(bar, 0, 7) * (h - 100f);
                        if (y - 20 < height)
                            v = Mathf.Max(v, 0.55f + 0.35f * Hash(bar, 3, 8));
                    }

                    SetMask(px, w, h, x, y, v);
                }
            }
        }

        private static void MakeHoloRadar(Color32[] px, int w, int h)
        {
            Fill(px, Color.black);
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            var rng = new System.Random(77);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float v = 0f;
                    foreach (float ring in new[] { 30f, 60f, 90f, 120f })
                    {
                        if (Mathf.Abs(d - ring) < 1.6f)
                            v = Mathf.Max(v, ring > 100f ? 0.9f : 0.6f);
                    }

                    if (Mathf.Abs(dx) < 1f || Mathf.Abs(dy) < 1f)
                        v = Mathf.Max(v, d < 125f ? 0.3f : 0f);

                    if (d < 120f && dx > 0f && dy > 0f)
                    {
                        float ang = Mathf.Atan2(dy, dx);
                        float sweep = Mathf.Clamp01(1f - ang / 1.1f);
                        v = Mathf.Max(v, sweep * sweep * 0.45f);
                    }

                    SetMask(px, w, h, x, y, v);
                }
            }

            for (int b = 0; b < 9; b++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float r = 20f + (float)rng.NextDouble() * 95f;
                int bx = Mathf.RoundToInt(cx + Mathf.Cos(a) * r);
                int by = Mathf.RoundToInt(cy + Mathf.Sin(a) * r);
                for (int oy = -3; oy <= 3; oy++)
                {
                    for (int ox = -3; ox <= 3; ox++)
                    {
                        if (ox * ox + oy * oy <= 9)
                            SetMask(px, w, h, bx + ox, by + oy, 1f);
                    }
                }
            }
        }

        private static void MakeHoloGlobe(Color32[] px, int w, int h)
        {
            Fill(px, Color.black);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = 0.06f;
                    if (x % 32 < 2)
                        v = 0.85f;
                    if (y % 32 < 2)
                        v = Mathf.Max(v, 0.85f);
                    float land = Mathf.PerlinNoise(x * 0.035f + 3f, y * 0.06f + 7f);
                    if (land > 0.58f)
                        v = Mathf.Max(v, 0.45f);
                    SetMask(px, w, h, x, y, v);
                }
            }
        }

        private static void MakeSky(Color32[] px, int w, int h)
        {
            Color horizon = new Color(0.08f, 0.42f, 0.55f);
            Color mid = new Color(0.14f, 0.06f, 0.32f);
            Color zenith = new Color(0.015f, 0.015f, 0.06f);
            Color ground = new Color(0.025f, 0.02f, 0.05f);

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1) * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    Color c;
                    if (t < 0f)
                    {
                        c = Color.Lerp(horizon * 0.35f, ground, Mathf.Clamp01(-t * 3f));
                    }
                    else
                    {
                        float a = Mathf.Clamp01(t / 0.35f);
                        c = Color.Lerp(horizon, mid, Mathf.Sqrt(a));
                        c = Color.Lerp(c, zenith, Mathf.Clamp01((t - 0.3f) / 0.7f));
                    }

                    float n = SeamlessNoise(u, t, 4f) * 0.6f + SeamlessNoise(u, t, 9f) * 0.4f;
                    float cloud = Mathf.Clamp01((n - 0.5f) * 2.4f);
                    float band = Mathf.Clamp01(1f - Mathf.Abs(t - 0.25f) * 1.8f);
                    Color nebula = Color.Lerp(Violet * 0.8f, Teal * 0.6f, SeamlessNoise(u, t, 2.5f));
                    c += nebula * cloud * cloud * band * 0.55f;

                    float star = Hash(x, y, 31);
                    if (t > 0.03f && star > 0.9975f)
                    {
                        float b = 0.35f + 0.65f * Hash(x, y, 32);
                        c += new Color(0.85f, 0.95f, 1f) * b;
                    }

                    px[y * w + x] = ToColor32(c);
                }
            }
        }

        private static float SeamlessNoise(float u, float v, float freq)
        {
            float a = Mathf.PerlinNoise(u * freq, v * freq + 11f);
            float b = Mathf.PerlinNoise((u - 1f) * freq, v * freq + 11f);
            return Mathf.Lerp(a, b, u);
        }

        #endregion
    }
}
