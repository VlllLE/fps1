using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FpsMaps.Triad.EditorTools
{
    /// <summary>Generates the procedural dusk-urban textures, world-tiled materials and sky used by the Triad map (no external art).</summary>
    public static class TriadMapAssets
    {
        public const string TextureDir = TriadLayout.RootFolder + "/Textures";
        public const string MaterialDir = TriadLayout.RootFolder + "/Materials";

        private const int Size = 256;

        private static readonly Dictionary<TriadSurface, Material> Materials = new Dictionary<TriadSurface, Material>();

        public static Material Skybox { get; private set; }

        public static Material For(TriadSurface surface)
        {
            if (Materials.TryGetValue(surface, out Material m) && m != null)
                return m;
            throw new InvalidOperationException("TriadMapAssets.EnsureAll must run before requesting " + surface);
        }

        public static void EnsureAll()
        {
            EnsureFolder(TriadLayout.RootFolder);
            EnsureFolder(TextureDir);
            EnsureFolder(MaterialDir);
            Materials.Clear();

            Texture2D asphalt = WriteTexture("Asphalt", MakeAsphalt);
            Texture2D concrete = WriteTexture("Concrete", MakeConcrete);
            Texture2D brick = WriteTexture("Brick", MakeBrick);
            Texture2D plaster = WriteTexture("Plaster", MakePlaster);
            Texture2D corrugated = WriteTexture("Corrugated", MakeCorrugated);
            Texture2D tar = WriteTexture("RoofTar", MakeTar);
            Texture2D tile = WriteTexture("FloorTile", MakeTile);
            Texture2D wood = WriteTexture("Wood", MakeWood);
            Texture2D crate = WriteTexture("Crate", MakeCrate);
            Texture2D barrier = WriteTexture("Barrier", MakeBarrier);
            Texture2D hazard = WriteTexture("Hazard", MakeHazard);
            Texture2D skyline = WriteTexture("Skyline", MakeSkyline);
            Texture2D skylineEmission = WriteTexture("Skyline_Emission", MakeSkylineEmission);
            Texture2D white = WriteTexture("White", (u, v) => Color.white);

            Shader tiled = LoadShader();

            Add(TriadSurface.Asphalt, tiled, asphalt, null, new Color(1f, 1f, 1f), Color.black, 4f, 0.15f, 0f);
            Add(TriadSurface.Concrete, tiled, concrete, null, new Color(0.88f, 0.87f, 0.84f), Color.black, 3f, 0.1f, 0f);
            Add(TriadSurface.ConcreteDark, tiled, concrete, null, new Color(0.52f, 0.52f, 0.55f), Color.black, 3f, 0.1f, 0f);
            Add(TriadSurface.Brick, tiled, brick, null, new Color(0.82f, 0.42f, 0.32f), Color.black, 2.4f, 0.08f, 0f);
            Add(TriadSurface.BrickLight, tiled, brick, null, new Color(0.92f, 0.76f, 0.56f), Color.black, 2.4f, 0.08f, 0f);
            Add(TriadSurface.BrickDark, tiled, brick, null, new Color(0.46f, 0.27f, 0.25f), Color.black, 2.4f, 0.08f, 0f);
            Add(TriadSurface.Plaster, tiled, plaster, null, new Color(0.92f, 0.86f, 0.74f), Color.black, 4f, 0.08f, 0f);
            Add(TriadSurface.MetalBlue, tiled, corrugated, null, new Color(0.4f, 0.56f, 0.76f), Color.black, 3f, 0.5f, 0.45f);
            Add(TriadSurface.MetalRust, tiled, corrugated, null, new Color(0.8f, 0.46f, 0.28f), Color.black, 3f, 0.35f, 0.45f);
            Add(TriadSurface.MetalGrey, tiled, corrugated, null, new Color(0.62f, 0.64f, 0.67f), Color.black, 3f, 0.5f, 0.55f);
            Add(TriadSurface.RoofTar, tiled, tar, null, new Color(0.7f, 0.7f, 0.72f), Color.black, 3f, 0.08f, 0f);
            Add(TriadSurface.FloorTile, tiled, tile, null, new Color(0.82f, 0.8f, 0.74f), Color.black, 2.4f, 0.4f, 0f);
            Add(TriadSurface.Wood, tiled, wood, null, new Color(0.88f, 0.68f, 0.48f), Color.black, 2f, 0.25f, 0f);
            Add(TriadSurface.Crate, tiled, crate, null, new Color(0.92f, 0.74f, 0.52f), Color.black, 1.2f, 0.2f, 0f);
            Add(TriadSurface.ContainerRed, tiled, corrugated, null, new Color(0.78f, 0.22f, 0.17f), Color.black, 2.4f, 0.45f, 0.4f);
            Add(TriadSurface.ContainerBlue, tiled, corrugated, null, new Color(0.2f, 0.36f, 0.78f), Color.black, 2.4f, 0.45f, 0.4f);
            Add(TriadSurface.ContainerGreen, tiled, corrugated, null, new Color(0.2f, 0.5f, 0.3f), Color.black, 2.4f, 0.45f, 0.4f);
            Add(TriadSurface.ContainerYellow, tiled, corrugated, null, new Color(0.92f, 0.72f, 0.16f), Color.black, 2.4f, 0.45f, 0.4f);
            Add(TriadSurface.Barrier, tiled, barrier, null, Color.white, Color.black, 1.8f, 0.1f, 0f);
            Add(TriadSurface.Hazard, tiled, hazard, null, Color.white, Color.black, 1f, 0.2f, 0f);
            Add(TriadSurface.Lamp, tiled, white, white, new Color(0.25f, 0.25f, 0.25f), new Color(1f, 0.78f, 0.45f) * 2.4f, 1f, 0.3f, 0f);
            Add(TriadSurface.Skyline, tiled, skyline, skylineEmission, new Color(0.7f, 0.7f, 0.85f), new Color(1f, 0.84f, 0.55f) * 1.7f, 14f, 0.2f, 0f);

            Skybox = ConfigureSkybox("Mat_Skybox_Dusk");
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

        #region Materials

        private static Shader LoadShader()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(TriadLayout.RootFolder + "/Shaders/TriadWorldTiled.shader");
            if (shader == null)
                shader = Shader.Find("Triad/WorldTiled");
            if (shader == null)
                shader = Shader.Find("Standard");
            return shader;
        }

        private static void Add(
            TriadSurface surface, Shader shader, Texture2D albedo, Texture2D emission, Color tint, Color emissionColor,
            float tileSize, float smoothness, float metallic)
        {
            string path = MaterialDir + "/Mat_" + surface + ".mat";
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

            SetTexture(mat, "_MainTex", albedo);
            SetTexture(mat, "_EmissionMap", emission);
            SetColor(mat, "_Color", tint);
            SetColor(mat, "_EmissionColor", emissionColor);
            SetFloat(mat, "_TileSize", tileSize);
            SetFloat(mat, "_TileAspect", 1f);
            SetFloat(mat, "_Glossiness", smoothness);
            SetFloat(mat, "_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            Materials[surface] = mat;
        }

        private static Material ConfigureSkybox(string name)
        {
            string path = MaterialDir + "/" + name + ".mat";
            Shader shader = Shader.Find("Skybox/Procedural");
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

            SetFloat(mat, "_SunSize", 0.06f);
            SetFloat(mat, "_SunSizeConvergence", 4f);
            SetFloat(mat, "_AtmosphereThickness", 1.55f);
            SetColor(mat, "_SkyTint", new Color(0.62f, 0.45f, 0.5f));
            SetColor(mat, "_GroundColor", new Color(0.3f, 0.25f, 0.24f));
            SetFloat(mat, "_Exposure", 1.05f);
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

        private static Texture2D WriteTexture(string name, Func<float, float, Color> fill)
        {
            string path = TextureDir + "/" + name + ".png";
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = fill((x + 0.5f) / Size, (y + 0.5f) / Size);
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
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
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665423);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float Lattice(int x, int y, int freq, int seed)
        {
            x = ((x % freq) + freq) % freq;
            y = ((y % freq) + freq) % freq;
            return Hash(x, y, seed);
        }

        /// <summary>Tileable value noise in 0..1.</summary>
        private static float Noise(float u, float v, int freq, int seed)
        {
            float x = u * freq;
            float y = v * freq;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lattice(x0, y0, freq, seed);
            float b = Lattice(x0 + 1, y0, freq, seed);
            float c = Lattice(x0, y0 + 1, freq, seed);
            float d = Lattice(x0 + 1, y0 + 1, freq, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float u, float v, int baseFreq, int seed)
        {
            return (Noise(u, v, baseFreq, seed) + 0.5f * Noise(u, v, baseFreq * 2, seed + 1) + 0.25f * Noise(u, v, baseFreq * 4, seed + 2)) / 1.75f;
        }

        private static float Pixel(float u, float v, int seed)
        {
            return Hash(Mathf.FloorToInt(u * Size), Mathf.FloorToInt(v * Size), seed);
        }

        private static Color Grey(float g)
        {
            g = Mathf.Clamp01(g);
            return new Color(g, g, g, 1f);
        }

        private static Color MakeAsphalt(float u, float v)
        {
            float g = 0.2f + Fbm(u, v, 8, 11) * 0.12f;
            if (Pixel(u, v, 3) > 0.965f)
                g += 0.16f;
            float crack = Mathf.Abs(Noise(u, v, 5, 21) - 0.5f);
            if (crack < 0.012f)
                g *= 0.55f;
            return Grey(g * 1.6f);
        }

        private static Color MakeConcrete(float u, float v)
        {
            float g = 0.72f + (Fbm(u, v, 6, 31) - 0.5f) * 0.3f + (Pixel(u, v, 5) - 0.5f) * 0.05f;
            float seam = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (seam < 0.008f)
                g *= 0.72f;
            float stain = Noise(u, v * 0.25f, 4, 41);
            g *= Mathf.Lerp(1f, 0.82f, Mathf.Clamp01((stain - 0.55f) * 3f));
            return Grey(g);
        }

        private static Color MakeBrick(float u, float v)
        {
            const int rows = 8;
            const int perRow = 4;
            float row = v * rows;
            int rowIndex = Mathf.FloorToInt(row);
            float offset = rowIndex % 2 == 0 ? 0f : 0.5f / perRow;
            float col = (u + offset) * perRow;
            int colIndex = Mathf.FloorToInt(col);
            float fu = col - colIndex;
            float fv = row - rowIndex;
            bool mortar = fu < 0.05f || fu > 0.95f || fv < 0.1f || fv > 0.9f;
            float g;
            if (mortar)
            {
                g = 0.55f + (Pixel(u, v, 7) - 0.5f) * 0.1f;
            }
            else
            {
                g = 0.8f + (Hash(colIndex, rowIndex, 9) - 0.5f) * 0.28f + (Pixel(u, v, 8) - 0.5f) * 0.08f;
                g *= 0.92f + 0.16f * Fbm(u, v, 6, 51);
            }

            return Grey(g);
        }

        private static Color MakePlaster(float u, float v)
        {
            float g = 0.84f + (Fbm(u, v, 5, 61) - 0.5f) * 0.22f + (Pixel(u, v, 13) - 0.5f) * 0.04f;
            float streak = Noise(u * 3f, v * 0.2f, 6, 71);
            g *= Mathf.Lerp(1f, 0.78f, Mathf.Clamp01((streak - 0.6f) * 3f));
            return Grey(g);
        }

        private static Color MakeCorrugated(float u, float v)
        {
            float ridge = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 8f);
            float g = 0.55f + ridge * 0.28f + (Fbm(u, v, 4, 81) - 0.5f) * 0.18f;
            float seam = Mathf.Abs(v - 0.5f);
            if (seam > 0.488f || seam < 0.006f)
                g *= 0.6f;
            if (Fbm(u, v, 5, 91) > 0.68f)
                g *= 0.78f;
            return Grey(g);
        }

        private static Color MakeTar(float u, float v)
        {
            float g = 0.3f + (Fbm(u, v, 10, 101) - 0.5f) * 0.14f;
            if (Pixel(u, v, 17) > 0.93f)
                g += 0.22f;
            return Grey(g * 1.5f);
        }

        private static Color MakeTile(float u, float v)
        {
            float fu = Mathf.Repeat(u * 4f, 1f);
            float fv = Mathf.Repeat(v * 4f, 1f);
            bool grout = fu < 0.04f || fv < 0.04f;
            int ix = Mathf.FloorToInt(u * 4f);
            int iy = Mathf.FloorToInt(v * 4f);
            float g = grout ? 0.45f : 0.82f + ((ix + iy) % 2 == 0 ? 0.05f : -0.04f) + (Pixel(u, v, 19) - 0.5f) * 0.05f;
            return Grey(g);
        }

        private static Color MakeWood(float u, float v)
        {
            const int planks = 6;
            float p = v * planks;
            int index = Mathf.FloorToInt(p);
            float fp = p - index;
            float grain = Noise(u * 2f, (index + fp * 0.1f) / planks, 8, 111 + index);
            float g = 0.7f + (Hash(index, 0, 23) - 0.5f) * 0.18f + (grain - 0.5f) * 0.2f;
            if (fp < 0.04f || fp > 0.97f)
                g *= 0.55f;
            return Grey(g);
        }

        private static Color MakeCrate(float u, float v)
        {
            const int planks = 5;
            float p = u * planks;
            int index = Mathf.FloorToInt(p);
            float fp = p - index;
            float grain = Noise(index + fp * 0.1f, v * 3f, 5, 121 + index);
            float g = 0.7f + (Hash(index, 1, 29) - 0.5f) * 0.2f + (grain - 0.5f) * 0.22f;
            if (fp < 0.05f || fp > 0.96f)
                g *= 0.5f;
            float brace = Mathf.Abs(Mathf.Repeat(u + v, 1f) - 0.5f);
            if (brace > 0.46f)
                g *= 0.75f;
            return Grey(g);
        }

        private static Color MakeBarrier(float u, float v)
        {
            float g = 0.75f + (Fbm(u, v, 6, 131) - 0.5f) * 0.25f;
            Color c = Grey(g);
            if (v > 0.2f && v < 0.3f)
            {
                bool red = Mathf.FloorToInt(u * 9f) % 2 == 0;
                c = red ? new Color(0.78f, 0.14f, 0.1f) : new Color(0.92f, 0.92f, 0.9f);
            }

            return c;
        }

        private static Color MakeHazard(float u, float v)
        {
            bool yellow = Mathf.Repeat((u + v) * 4f, 1f) < 0.5f;
            return yellow ? new Color(1f, 0.8f, 0.1f) : new Color(0.08f, 0.08f, 0.08f);
        }

        private static bool Window(float u, float v, out int col, out int row)
        {
            const int cols = 8;
            const int rows = 10;
            float cu = u * cols;
            float cv = v * rows;
            col = Mathf.FloorToInt(cu);
            row = Mathf.FloorToInt(cv);
            float fu = cu - col;
            float fv = cv - row;
            return fu > 0.22f && fu < 0.78f && fv > 0.25f && fv < 0.8f;
        }

        private static Color MakeSkyline(float u, float v)
        {
            float wall = 0.22f + (Fbm(u, v, 6, 141) - 0.5f) * 0.08f;
            if (!Window(u, v, out int col, out int row))
                return new Color(wall * 0.9f, wall, wall * 1.25f);
            bool lit = Hash(col, row, 31) > 0.55f;
            return lit ? new Color(0.95f, 0.82f, 0.55f) : new Color(0.07f, 0.09f, 0.14f);
        }

        private static Color MakeSkylineEmission(float u, float v)
        {
            if (!Window(u, v, out int col, out int row))
                return Color.black;
            bool lit = Hash(col, row, 31) > 0.55f;
            return lit ? new Color(1f, 0.85f, 0.55f) : Color.black;
        }

        #endregion
    }
}
