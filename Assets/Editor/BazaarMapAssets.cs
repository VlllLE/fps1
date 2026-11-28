using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FpsMaps.Bazaar.EditorTools
{
    /// <summary>Generates the procedural sunlit-desert-town textures, world-tiled materials and sky used by the Bazaar map (no external art).</summary>
    public static class BazaarMapAssets
    {
        public const string TextureDir = BazaarLayout.RootFolder + "/Textures";
        public const string MaterialDir = BazaarLayout.RootFolder + "/Materials";

        private const int Size = 256;

        private static readonly Dictionary<BazaarSurface, Material> Materials = new Dictionary<BazaarSurface, Material>();

        public static Material Skybox { get; private set; }

        public static Material For(BazaarSurface surface)
        {
            if (Materials.TryGetValue(surface, out Material m) && m != null)
                return m;
            throw new InvalidOperationException("BazaarMapAssets.EnsureAll must run before requesting " + surface);
        }

        public static void EnsureAll()
        {
            EnsureFolder(BazaarLayout.RootFolder);
            EnsureFolder(TextureDir);
            EnsureFolder(MaterialDir);
            Materials.Clear();

            Texture2D sand = WriteTexture("Sand", MakeSand);
            Texture2D cobble = WriteTexture("Cobble", MakeCobble);
            Texture2D sandstone = WriteTexture("Sandstone", MakeSandstone);
            Texture2D plaster = WriteTexture("Plaster", MakePlaster);
            Texture2D roofTile = WriteTexture("RoofTile", MakeRoofTile);
            Texture2D tile = WriteTexture("FloorTile", MakeTile);
            Texture2D wood = WriteTexture("Wood", MakeWood);
            Texture2D crate = WriteTexture("Crate", MakeCrate);
            Texture2D corrugated = WriteTexture("Corrugated", MakeCorrugated);
            Texture2D foliage = WriteTexture("Foliage", MakeFoliage);
            Texture2D awningRed = WriteTexture("AwningRed", (u, v) => MakeAwning(u, v, new Color(0.82f, 0.2f, 0.16f)));
            Texture2D awningBlue = WriteTexture("AwningBlue", (u, v) => MakeAwning(u, v, new Color(0.18f, 0.38f, 0.72f)));
            Texture2D awningYellow = WriteTexture("AwningYellow", (u, v) => MakeAwning(u, v, new Color(0.95f, 0.74f, 0.16f)));
            Texture2D awningGreen = WriteTexture("AwningGreen", (u, v) => MakeAwning(u, v, new Color(0.2f, 0.52f, 0.3f)));
            Texture2D white = WriteTexture("White", (u, v) => Color.white);

            Shader tiled = LoadShader();

            Add(BazaarSurface.Sand, tiled, sand, null, new Color(0.8f, 0.7f, 0.5f), Color.black, 5f, 0.04f, 0f);
            Add(BazaarSurface.Cobble, tiled, cobble, null, new Color(0.86f, 0.78f, 0.64f), Color.black, 3.2f, 0.1f, 0f);
            Add(BazaarSurface.Sandstone, tiled, sandstone, null, new Color(0.93f, 0.76f, 0.5f), Color.black, 2.8f, 0.06f, 0f);
            Add(BazaarSurface.SandstoneDark, tiled, sandstone, null, new Color(0.72f, 0.54f, 0.36f), Color.black, 2.8f, 0.06f, 0f);
            Add(BazaarSurface.Whitewash, tiled, plaster, null, new Color(0.82f, 0.79f, 0.73f), Color.black, 4f, 0.06f, 0f);
            Add(BazaarSurface.Terracotta, tiled, plaster, null, new Color(0.88f, 0.52f, 0.36f), Color.black, 4f, 0.06f, 0f);
            Add(BazaarSurface.RoofTile, tiled, roofTile, null, new Color(0.86f, 0.46f, 0.3f), Color.black, 2.4f, 0.1f, 0f);
            Add(BazaarSurface.Limestone, tiled, tile, null, new Color(0.76f, 0.72f, 0.63f), Color.black, 2.4f, 0.22f, 0f);
            Add(BazaarSurface.WoodDark, tiled, wood, null, new Color(0.62f, 0.44f, 0.3f), Color.black, 2f, 0.2f, 0f);
            Add(BazaarSurface.Crate, tiled, crate, null, new Color(0.92f, 0.74f, 0.5f), Color.black, 1.2f, 0.2f, 0f);
            Add(BazaarSurface.PaintedBlue, tiled, wood, null, new Color(0.22f, 0.5f, 0.78f), Color.black, 2f, 0.3f, 0f);
            Add(BazaarSurface.MetalGrey, tiled, corrugated, null, new Color(0.62f, 0.64f, 0.67f), Color.black, 3f, 0.5f, 0.55f);
            Add(BazaarSurface.AwningRed, tiled, awningRed, null, Color.white, Color.black, 2.4f, 0.08f, 0f);
            Add(BazaarSurface.AwningBlue, tiled, awningBlue, null, Color.white, Color.black, 2.4f, 0.08f, 0f);
            Add(BazaarSurface.AwningYellow, tiled, awningYellow, null, Color.white, Color.black, 2.4f, 0.08f, 0f);
            Add(BazaarSurface.AwningGreen, tiled, awningGreen, null, Color.white, Color.black, 2.4f, 0.08f, 0f);
            Add(BazaarSurface.CarWhite, tiled, white, null, new Color(0.9f, 0.91f, 0.92f), Color.black, 4f, 0.65f, 0.4f);
            Add(BazaarSurface.CarOchre, tiled, white, null, new Color(0.86f, 0.62f, 0.2f), Color.black, 4f, 0.65f, 0.4f);
            Add(BazaarSurface.CarGreen, tiled, white, null, new Color(0.22f, 0.5f, 0.38f), Color.black, 4f, 0.65f, 0.4f);
            Add(BazaarSurface.PaintWhite, tiled, white, null, new Color(0.82f, 0.82f, 0.8f), Color.black, 4f, 0.2f, 0f);
            Add(BazaarSurface.PaintAttack, tiled, white, null, new Color(0.95f, 0.5f, 0.12f), Color.black, 4f, 0.2f, 0f);
            Add(BazaarSurface.PaintDefend, tiled, white, null, new Color(0.18f, 0.5f, 0.9f), Color.black, 4f, 0.2f, 0f);
            Add(BazaarSurface.PaintPlant, tiled, white, null, new Color(0.95f, 0.82f, 0.15f), Color.black, 4f, 0.2f, 0f);
            Add(BazaarSurface.Foliage, tiled, foliage, null, new Color(0.46f, 0.7f, 0.3f), Color.black, 1.6f, 0.15f, 0f);
            Add(BazaarSurface.Lamp, tiled, white, white, new Color(0.3f, 0.28f, 0.25f), new Color(1f, 0.82f, 0.5f) * 1.8f, 1f, 0.3f, 0f);
            Add(BazaarSurface.Backdrop, tiled, plaster, null, new Color(0.88f, 0.76f, 0.6f), Color.black, 9f, 0.04f, 0f);

            Skybox = ConfigureSkybox("Mat_Skybox_Day");
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
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(BazaarLayout.RootFolder + "/Shaders/BazaarWorldTiled.shader");
            if (shader == null)
                shader = Shader.Find("Bazaar/WorldTiled");
            if (shader == null)
                shader = Shader.Find("Standard");
            return shader;
        }

        private static void Add(
            BazaarSurface surface, Shader shader, Texture2D albedo, Texture2D emission, Color tint, Color emissionColor,
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

            SetFloat(mat, "_SunSize", 0.045f);
            SetFloat(mat, "_SunSizeConvergence", 5f);
            SetFloat(mat, "_AtmosphereThickness", 1.05f);
            SetColor(mat, "_SkyTint", new Color(0.5f, 0.62f, 0.85f));
            SetColor(mat, "_GroundColor", new Color(0.7f, 0.64f, 0.52f));
            SetFloat(mat, "_Exposure", 1.2f);
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

        private static Color MakeSand(float u, float v)
        {
            float g = 0.82f + (Fbm(u, v, 6, 11) - 0.5f) * 0.2f + (Pixel(u, v, 3) - 0.5f) * 0.07f;
            float ripple = 0.5f + 0.5f * Mathf.Sin((u * 3f + Noise(u, v, 4, 5) * 0.8f) * Mathf.PI * 2f);
            g *= 0.95f + ripple * 0.05f;
            return Grey(g);
        }

        private static Color MakeCobble(float u, float v)
        {
            const int cells = 6;
            float cu = u * cells;
            float cv = v * cells;
            int ix = Mathf.FloorToInt(cu);
            int iy = Mathf.FloorToInt(cv);
            float fu = cu - ix;
            float fv = cv - iy;
            float edge = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
            float g = 0.78f + (Hash(ix, iy, 9) - 0.5f) * 0.26f + (Pixel(u, v, 7) - 0.5f) * 0.06f;
            if (edge < 0.09f)
                g *= 0.58f;
            return Grey(g);
        }

        private static Color MakeSandstone(float u, float v)
        {
            const int rows = 6;
            const int perRow = 3;
            float row = v * rows;
            int rowIndex = Mathf.FloorToInt(row);
            float offset = rowIndex % 2 == 0 ? 0f : 0.5f / perRow;
            float col = (u + offset) * perRow;
            int colIndex = Mathf.FloorToInt(col);
            float fu = col - colIndex;
            float fv = row - rowIndex;
            bool joint = fu < 0.03f || fu > 0.97f || fv < 0.05f || fv > 0.95f;
            float g = 0.84f + (Hash(colIndex, rowIndex, 15) - 0.5f) * 0.14f + (Fbm(u, v, 7, 51) - 0.5f) * 0.14f;
            if (joint)
                g *= 0.7f;
            return Grey(g);
        }

        private static Color MakePlaster(float u, float v)
        {
            float g = 0.9f + (Fbm(u, v, 5, 61) - 0.5f) * 0.16f + (Pixel(u, v, 13) - 0.5f) * 0.035f;
            float streak = Noise(u * 3f, v * 0.2f, 6, 71);
            g *= Mathf.Lerp(1f, 0.82f, Mathf.Clamp01((streak - 0.6f) * 3f));
            return Grey(g);
        }

        private static Color MakeRoofTile(float u, float v)
        {
            float rows = v * 8f;
            int row = Mathf.FloorToInt(rows);
            float fr = rows - row;
            float scallop = 0.55f + 0.45f * Mathf.Sin(u * Mathf.PI * 2f * 6f + row * 1.7f);
            float g = 0.62f + scallop * 0.22f + (Hash(row, Mathf.FloorToInt(u * 6f), 17) - 0.5f) * 0.12f;
            if (fr < 0.1f)
                g *= 0.62f;
            return Grey(g);
        }

        private static Color MakeTile(float u, float v)
        {
            float fu = Mathf.Repeat(u * 4f, 1f);
            float fv = Mathf.Repeat(v * 4f, 1f);
            bool grout = fu < 0.04f || fv < 0.04f;
            int ix = Mathf.FloorToInt(u * 4f);
            int iy = Mathf.FloorToInt(v * 4f);
            float g = grout ? 0.5f : 0.88f + ((ix + iy) % 2 == 0 ? 0.04f : -0.04f) + (Pixel(u, v, 19) - 0.5f) * 0.05f;
            return Grey(g);
        }

        private static Color MakeWood(float u, float v)
        {
            const int planks = 6;
            float p = v * planks;
            int index = Mathf.FloorToInt(p);
            float fp = p - index;
            float grain = Noise(u * 2f, (index + fp * 0.1f) / planks, 8, 111 + index);
            float g = 0.72f + (Hash(index, 0, 23) - 0.5f) * 0.18f + (grain - 0.5f) * 0.2f;
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
            float g = 0.72f + (Hash(index, 1, 29) - 0.5f) * 0.2f + (grain - 0.5f) * 0.22f;
            if (fp < 0.05f || fp > 0.96f)
                g *= 0.5f;
            float brace = Mathf.Abs(Mathf.Repeat(u + v, 1f) - 0.5f);
            if (brace > 0.46f)
                g *= 0.75f;
            return Grey(g);
        }

        private static Color MakeCorrugated(float u, float v)
        {
            float ridge = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 8f);
            float g = 0.6f + ridge * 0.25f + (Fbm(u, v, 4, 81) - 0.5f) * 0.14f;
            float seam = Mathf.Abs(v - 0.5f);
            if (seam > 0.488f || seam < 0.006f)
                g *= 0.65f;
            return Grey(g);
        }

        private static Color MakeFoliage(float u, float v)
        {
            float g = 0.7f + (Fbm(u, v, 8, 131) - 0.5f) * 0.45f;
            return Grey(g);
        }

        private static Color MakeAwning(float u, float v, Color stripe)
        {
            bool colored = Mathf.FloorToInt(u * 8f) % 2 == 0;
            Color c = colored ? stripe : new Color(0.96f, 0.94f, 0.88f);
            float shade = 0.95f + (Pixel(u, v, 41) - 0.5f) * 0.06f;
            return new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
        }

        #endregion
    }
}
