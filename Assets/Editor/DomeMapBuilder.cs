using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FpsMaps.Dome.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Dome.unity: a futuristic geodesic-dome arena (hex energy-glass dome over a
    /// ground-level interior, ring catwalk, outer corridors, vehicle/crate cover and elevated vantage points).
    /// All geometry sits on the Default layer so MovementConfig.wallLayers (~0) covers every surface.
    /// </summary>
    public static class DomeMapBuilder
    {
        private static readonly Color TealLight = new Color(0.20f, 0.95f, 0.90f);
        private static readonly Color VioletLight = new Color(0.62f, 0.30f, 1.00f);

        private static int colliderCount;

        [MenuItem("Tools/Maps/Build Dome")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Build();
        }

        [MenuItem("Tools/Maps/Open Dome")]
        public static void OpenFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DomeLayout.ScenePath) == null)
            {
                BuildFromMenu();
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(DomeLayout.ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Maps/Enter Dome Play Mode")]
        public static void OpenAndPlay()
        {
            OpenFromMenu();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DomeLayout.ScenePath) != null)
                EditorApplication.isPlaying = true;
        }

        /// <summary>CLI: -batchmode -executeMethod FpsMaps.Dome.EditorTools.DomeMapBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static Scene Build()
        {
            DomeMapAssets.EnsureAll();
            DomeMapAssets.EnsureFolder("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            colliderCount = 0;

            BuildLighting();
            BuildGround();
            BuildPerimeter();
            BuildDome();
            BuildInterior();
            BuildCorridors();
            BuildTowers();
            BuildYards();
            BuildCover();
            BuildHolograms();
            SpawnPointsAndTargets();
            SetEnvironmentLayers();
            SpawnPlayer();

            EnsurePlayerPresent();
            if (!EditorSceneManager.SaveScene(scene, DomeLayout.ScenePath))
                throw new InvalidOperationException("DomeMapBuilder: failed to save " + DomeLayout.ScenePath);

            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Dome map built: " + DomeLayout.ScenePath + " (" + DomeMapAssets.PanelCount + " glass panels, "
                + colliderCount + " environment colliders).");
            return scene;
        }

        public static void EnsureInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == DomeLayout.ScenePath)
                {
                    if (!scenes[i].enabled)
                    {
                        scenes[i] = new EditorBuildSettingsScene(DomeLayout.ScenePath, true);
                        EditorBuildSettings.scenes = scenes.ToArray();
                    }

                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(DomeLayout.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #region Primitive helpers

        private static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.isStatic = true;
            return go.transform;
        }

        private static GameObject Prim(
            PrimitiveType type, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale,
            Material mat, bool collider, bool isStatic = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;

            var r = go.GetComponent<Renderer>();
            if (mat != null)
                r.sharedMaterial = mat;

            Collider c = go.GetComponent<Collider>();
            if (!collider && c != null)
                Object.DestroyImmediate(c);
            else if (collider)
                colliderCount++;

            go.isStatic = isStatic;
            return go;
        }

        private static GameObject Box(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 size, Material mat, bool collider = true)
        {
            return Prim(PrimitiveType.Cube, name, parent, pos, rot, size, mat, collider);
        }

        private static GameObject Glow(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 size, Material mat)
        {
            GameObject go = Prim(PrimitiveType.Cube, name, parent, pos, rot, size, mat, false);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static GameObject Disc(string name, Transform parent, float radius, float topY, Material mat)
        {
            GameObject go = Prim(PrimitiveType.Cylinder, name, parent, new Vector3(0f, topY - 0.005f, 0f),
                Quaternion.identity, new Vector3(radius * 2f, 0.005f, radius * 2f), mat, false);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static GameObject HexPrism(string name, Transform parent, Vector3 basePos, float radius, float height, Material mat, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = basePos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(radius, height, radius);
            go.AddComponent<MeshFilter>().sharedMesh = DomeMapAssets.HexPrismMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = DomeMapAssets.HexPrismMesh;
            mc.convex = true;
            colliderCount++;
            go.isStatic = true;
            return go;
        }

        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (!shadows)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            go.isStatic = true;
            return go;
        }

        private static Light AddPoint(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Vector3 Rot90(Vector3 p, int quarterTurns)
        {
            Vector3 r = p;
            for (int i = 0; i < Mathf.Abs(quarterTurns) % 4; i++)
                r = new Vector3(r.z, r.y, -r.x);
            return r;
        }

        private static float Yaw(Vector3 from, Vector3 target)
        {
            Vector3 d = target - from;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        private static GameObject Ramp(
            string name, Transform parent, Vector3 startOnGround, float yawDeg, float run, float rise,
            float width, float thickness, Material mat)
        {
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float hyp = Mathf.Sqrt(run * run + rise * rise);
            Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f) * Quaternion.Euler(-angle, 0f, 0f);
            Vector3 topMid = startOnGround + DomeLayout.Direction(yawDeg) * (run * 0.5f) + Vector3.up * (rise * 0.5f);
            Vector3 center = topMid - rot * Vector3.up * (thickness * 0.5f);
            return Box(name, parent, center, rot, new Vector3(width, thickness, hyp), mat);
        }

        #endregion

        #region Lighting and atmosphere

        private static void BuildLighting()
        {
            RenderSettings.skybox = DomeMapAssets.Skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.26f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.15f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.05f, 0.12f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.9f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = new Color(0.07f, 0.06f, 0.15f);

            Transform g = Group("Lighting");

            var moonGo = new GameObject("Moon Light");
            moonGo.transform.SetParent(g, false);
            moonGo.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.62f, 1f);
            moon.intensity = 0.8f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.sun = moon;

            var rimGo = new GameObject("Violet Rim Light");
            rimGo.transform.SetParent(g, false);
            rimGo.transform.rotation = Quaternion.Euler(24f, 140f, 0f);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.62f, 0.32f, 1f);
            rim.intensity = 0.28f;
            rim.shadows = LightShadows.None;

            AddPoint("Core Light", g, new Vector3(0f, 5.5f, 0f), TealLight, 1.8f, 22f);
            AddPoint("Core Underglow", g, new Vector3(0f, 1.2f, 0f), VioletLight, 1.4f, 9f);

            for (int i = 0; i < 8; i++)
            {
                Vector3 p = DomeLayout.Direction(i * 45f + 22.5f) * 11f;
                p.y = 5.6f;
                AddPoint("Catwalk Light " + i, g, p, i % 2 == 0 ? TealLight : VioletLight, 1.3f, 13f);
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 p = DomeLayout.Direction(i * 90f) * 22f;
                p.y = 4.2f;
                AddPoint("Gate Light " + i, g, p, i % 2 == 0 ? TealLight : VioletLight, 1.5f, 15f);
            }

            float c = 34f;
            Vector3[] corners =
            {
                new Vector3(c, 7.5f, c), new Vector3(-c, 7.5f, c), new Vector3(c, 7.5f, -c), new Vector3(-c, 7.5f, -c)
            };
            for (int i = 0; i < corners.Length; i++)
                AddPoint("Corner Light " + i, g, corners[i], i % 2 == 0 ? VioletLight : TealLight, 1.6f, 22f);

            AddPoint("Tower NE Light", g, new Vector3(30f, 8.5f, 30f), TealLight, 1.4f, 14f);
            AddPoint("Tower SW Light", g, new Vector3(-30f, 8.5f, -30f), TealLight, 1.4f, 14f);
            AddPoint("Gantry NW Light", g, new Vector3(-30f, 7f, 32f), VioletLight, 1.4f, 14f);
            AddPoint("Yard SE Light", g, new Vector3(28f, 5f, -30f), VioletLight, 1.4f, 14f);
        }

        #endregion

        #region Ground and perimeter

        private static void BuildGround()
        {
            Transform g = Group("Ground");
            float size = DomeLayout.ArenaHalfSize * 2f + 4f;
            Box("Ground", g, new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(size, 1f, size), DomeMapAssets.Floor);

            Disc("Floor_Ring_Outer", g, 17.2f, 0.02f, DomeMapAssets.TrimTeal);
            Disc("Floor_Disc_Outer", g, 16.7f, 0.03f, DomeMapAssets.FloorDisc);
            Disc("Floor_Ring_Mid", g, 10.4f, 0.04f, DomeMapAssets.TrimViolet);
            Disc("Floor_Disc_Mid", g, 10.1f, 0.05f, DomeMapAssets.FloorDisc);
            Disc("Floor_Ring_Core", g, 5.6f, 0.06f, DomeMapAssets.TrimTeal);
            Disc("Floor_Disc_Core", g, 5.3f, 0.07f, DomeMapAssets.FloorDisc);

            for (int i = 0; i < 4; i++)
            {
                float az = i * 90f;
                Quaternion rot = Quaternion.Euler(0f, az, 0f);
                Vector3 dir = DomeLayout.Direction(az);
                Vector3 side = DomeLayout.Direction(az + 90f);
                Vector3 mid = dir * 27f;
                Glow("Floor_Lane_" + i, g, mid + Vector3.up * 0.02f, rot, new Vector3(3.6f, 0.02f, 18f), DomeMapAssets.FloorDisc);
                Glow("Trim_Lane_A_" + i, g, mid + side * 1.9f + Vector3.up * 0.035f, rot, new Vector3(0.14f, 0.02f, 18f), DomeMapAssets.TrimTeal);
                Glow("Trim_Lane_B_" + i, g, mid - side * 1.9f + Vector3.up * 0.035f, rot, new Vector3(0.14f, 0.02f, 18f), DomeMapAssets.TrimViolet);
            }
        }

        private static void BuildPerimeter()
        {
            Transform g = Group("Perimeter");
            float half = DomeLayout.ArenaHalfSize;
            float t = DomeLayout.PerimeterThickness;
            float h = DomeLayout.PerimeterHeight;
            float length = half * 2f + t;
            string[] names = { "N", "E", "S", "W" };

            for (int i = 0; i < 4; i++)
            {
                float az = i * 90f;
                Quaternion rot = Quaternion.Euler(0f, az, 0f);
                Vector3 dir = DomeLayout.Direction(az);
                Box("Wall_Perimeter_" + names[i], g, dir * half + Vector3.up * (h * 0.5f), rot, new Vector3(length, h, t), DomeMapAssets.Wall);

                Vector3 inner = dir * (half - t * 0.5f - 0.04f);
                Glow("Trim_Perimeter_Top_" + names[i], g, inner + Vector3.up * (h - 0.2f), rot, new Vector3(length - 2f, 0.18f, 0.08f), DomeMapAssets.TrimTeal);
                Glow("Trim_Perimeter_Low_" + names[i], g, inner + Vector3.up * 0.45f, rot, new Vector3(length - 2f, 0.12f, 0.08f), DomeMapAssets.TrimViolet);
                Glow("Trim_Perimeter_Mid_" + names[i], g, inner + Vector3.up * 4.4f, rot, new Vector3(length - 2f, 0.06f, 0.06f), DomeMapAssets.TrimTeal);
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = new Vector3((i % 2 == 0 ? 1f : -1f) * (half - 1.4f), 0f, (i < 2 ? 1f : -1f) * (half - 1.4f));
                HexPrism("Pylon_Corner_" + i, g, corner, 1.3f, h + 1.5f, DomeMapAssets.Platform);
                Glow("Trim_Pylon_Cap_" + i, g, corner + Vector3.up * (h + 1.55f), Quaternion.identity, new Vector3(1.8f, 0.12f, 1.8f),
                    i % 2 == 0 ? DomeMapAssets.TrimViolet : DomeMapAssets.TrimTeal);
            }
        }

        #endregion

        #region Dome shell, drum and interior

        private static void BuildDome()
        {
            Transform g = Group("Dome");

            float chord = 2f * DomeLayout.DrumRadius * Mathf.Tan(Mathf.PI / DomeLayout.DrumSegments);
            for (int k = 0; k < DomeLayout.DrumSegments; k++)
            {
                float az = k * (360f / DomeLayout.DrumSegments);
                if (Mathf.Abs(Mathf.Repeat(az, 90f)) < 0.01f)
                    continue;

                Quaternion rot = Quaternion.Euler(0f, az, 0f);
                Vector3 pos = DomeLayout.Direction(az) * DomeLayout.DrumRadius + Vector3.up * (DomeLayout.DrumHeight * 0.5f);
                Box("Wall_Drum_" + k, g, pos, rot,
                    new Vector3(chord + 0.3f, DomeLayout.DrumHeight, DomeLayout.DrumThickness), DomeMapAssets.Drum);
                Glow("Trim_Drum_Top_" + k, g, pos + Vector3.up * (DomeLayout.DrumHeight * 0.5f + 0.05f), rot,
                    new Vector3(chord, 0.1f, 0.35f), k % 2 == 0 ? DomeMapAssets.TrimTeal : DomeMapAssets.TrimViolet);
            }

            foreach (float door in DomeLayout.DoorAzimuthsDeg)
            {
                Quaternion rot = Quaternion.Euler(0f, door, 0f);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 jamb = DomeLayout.Direction(door + s * 7.2f) * DomeLayout.DrumRadius;
                    Glow("Trim_DoorJamb_" + door + "_" + s, g, jamb + Vector3.up * (DomeLayout.DrumHeight * 0.5f), rot,
                        new Vector3(0.22f, DomeLayout.DrumHeight, DomeLayout.DrumThickness + 0.2f), DomeMapAssets.TrimTeal);

                    Vector3 pylon = DomeLayout.Direction(door + s * 11.5f) * (DomeLayout.DrumRadius + 3.8f);
                    HexPrism("Pylon_Gate_" + door + "_" + s, g, pylon, 0.95f, 5.2f, DomeMapAssets.Platform);
                    Glow("Trim_PylonCap_" + door + "_" + s, g, pylon + Vector3.up * 5.25f, Quaternion.identity,
                        new Vector3(1.3f, 0.1f, 1.3f), DomeMapAssets.TrimViolet);
                }
            }

            MeshObject("DomeGlass", g, DomeMapAssets.GlassMesh, DomeMapAssets.Glass, false);
            MeshObject("DomeStruts", g, DomeMapAssets.StrutMesh, DomeMapAssets.TrimTeal, true);

            GameObject shell = new GameObject("DomeShell");
            shell.transform.SetParent(g, false);
            var mc = shell.AddComponent<MeshCollider>();
            mc.sharedMesh = DomeMapAssets.ShellColliderMesh;
            mc.convex = false;
            shell.isStatic = true;
            colliderCount++;
        }

        private static void BuildInterior()
        {
            Transform g = Group("Interior");

            HexPrism("Core_Platform", g, Vector3.zero, DomeLayout.CoreRadius, DomeLayout.CoreHeight, DomeMapAssets.Platform);
            Glow("Trim_Core_Cap", g, new Vector3(0f, DomeLayout.CoreHeight + 0.03f, 0f), Quaternion.identity,
                new Vector3(1.4f, 0.06f, 1.4f), DomeMapAssets.TrimTeal);

            Prim(PrimitiveType.Cylinder, "Trim_Core_Pylon", g, new Vector3(0f, DomeLayout.CoreHeight + 1.2f, 0f), Quaternion.identity,
                new Vector3(0.35f, 1.2f, 0.35f), DomeMapAssets.TrimViolet, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            float catInner = DomeLayout.CatwalkRadius - DomeLayout.CatwalkWidth * 0.5f;
            float segChord = 2f * DomeLayout.CatwalkRadius * Mathf.Tan(Mathf.PI / 24f);
            for (int k = 0; k < 24; k++)
            {
                float az = k * 15f;
                Quaternion rot = Quaternion.Euler(0f, az, 0f);
                Vector3 dir = DomeLayout.Direction(az);
                Vector3 pos = dir * DomeLayout.CatwalkRadius + Vector3.up * (DomeLayout.CatwalkTopY - DomeLayout.CatwalkThickness * 0.5f);
                Box("Catwalk_" + k, g, pos, rot, new Vector3(segChord + 0.25f, DomeLayout.CatwalkThickness, DomeLayout.CatwalkWidth), DomeMapAssets.Platform);

                float edge = DomeLayout.CatwalkWidth * 0.5f - 0.1f;
                Glow("Trim_Catwalk_Inner_" + k, g, dir * (DomeLayout.CatwalkRadius - edge) + Vector3.up * (DomeLayout.CatwalkTopY + 0.02f), rot,
                    new Vector3(segChord, 0.04f, 0.12f), DomeMapAssets.TrimViolet);
                Glow("Trim_Catwalk_Outer_" + k, g, dir * (DomeLayout.CatwalkRadius + edge) + Vector3.up * (DomeLayout.CatwalkTopY + 0.02f), rot,
                    new Vector3(segChord, 0.04f, 0.12f), DomeMapAssets.TrimTeal);
            }

            float rampStart = DomeLayout.CoreRadius + 0.2f;
            float rampRun = catInner - rampStart + 0.3f;
            for (int i = 0; i < 4; i++)
            {
                float az = 45f + 90f * i;
                Vector3 start = DomeLayout.Direction(az) * rampStart;
                GameObject ramp = Ramp("Ramp_Catwalk_" + i, g, start, az, rampRun, DomeLayout.CatwalkTopY, 3.4f, 0.5f, DomeMapAssets.Platform);
                Vector3 side = DomeLayout.Direction(az + 90f);
                Quaternion rot = ramp.transform.rotation;
                Vector3 up = rot * Vector3.up;
                Vector3 mid = ramp.transform.position + up * 0.27f;
                Glow("Trim_Ramp_A_" + i, g, mid + side * 1.6f, rot, new Vector3(0.1f, 0.04f, ramp.transform.localScale.z), DomeMapAssets.TrimTeal);
                Glow("Trim_Ramp_B_" + i, g, mid - side * 1.6f, rot, new Vector3(0.1f, 0.04f, ramp.transform.localScale.z), DomeMapAssets.TrimTeal);
            }

            for (int i = 0; i < 8; i++)
            {
                float az = 22.5f + 45f * i;
                Vector3 pos = DomeLayout.Direction(az) * DomeLayout.CatwalkRadius;
                HexPrism("Column_" + i, g, pos, 0.7f, DomeLayout.CatwalkTopY - DomeLayout.CatwalkThickness, DomeMapAssets.Platform, az);
                Glow("Trim_Column_" + i, g, pos + Vector3.up * 1.2f, Quaternion.Euler(0f, az, 0f), new Vector3(0.12f, 2f, 1.5f),
                    i % 2 == 0 ? DomeMapAssets.TrimTeal : DomeMapAssets.TrimViolet);
            }
        }

        #endregion

        #region Corridors, towers, yards, cover

        private static void BuildCorridors()
        {
            Transform g = Group("Corridors");
            string[] names = { "N", "E", "S", "W" };
            for (int i = 0; i < 4; i++)
            {
                float az = i * 90f;
                Quaternion rot = Quaternion.Euler(0f, az, 0f);
                Vector3 dir = DomeLayout.Direction(az);
                Vector3 side = DomeLayout.Direction(az + 90f);
                for (int s = -1; s <= 1; s += 2)
                {
                    string tag = s < 0 ? "L" : "R";
                    Vector3 centre = dir * 27f + side * (6f * s);
                    Box("Wall_Corridor_" + names[i] + "_" + tag, g, centre + Vector3.up * 3.5f, rot, new Vector3(0.8f, 7f, 12f), DomeMapAssets.Wall);
                    Vector3 inner = centre - side * (0.44f * s);
                    Glow("Trim_Corridor_Top_" + names[i] + "_" + tag, g, inner + Vector3.up * 6.8f, rot, new Vector3(0.08f, 0.16f, 12f), DomeMapAssets.TrimTeal);
                    Glow("Trim_Corridor_Low_" + names[i] + "_" + tag, g, inner + Vector3.up * 0.4f, rot, new Vector3(0.08f, 0.1f, 12f), DomeMapAssets.TrimViolet);
                }

                Box("Wall_Corridor_Lintel_" + names[i], g, dir * 33.2f + Vector3.up * 7.3f, rot, new Vector3(12.8f, 0.6f, 0.6f), DomeMapAssets.Drum);
            }
        }

        private static void BuildTowers()
        {
            Transform g = Group("Towers");
            BuildTower(g, "NE", new Vector3(30f, 0f, 30f), 90f, new Vector3(10f, 30f, 0f));
            BuildTower(g, "SW", new Vector3(-30f, 0f, -30f), 270f, new Vector3(-10f, -30f, 0f));
        }

        private static void BuildTower(Transform g, string tag, Vector3 centre, float rampYaw, Vector3 rampStartXZ)
        {
            float top = DomeLayout.TowerTopY;
            Box("Tower_" + tag, g, centre + Vector3.up * (top * 0.5f), Quaternion.identity, new Vector3(10f, top, 10f), DomeMapAssets.Drum);
            Glow("Trim_Tower_Ring_" + tag, g, centre + Vector3.up * (top - 0.4f), Quaternion.identity, new Vector3(10.1f, 0.12f, 10.1f), DomeMapAssets.TrimViolet);

            Vector3 rampStart = new Vector3(rampStartXZ.x, 0f, rampStartXZ.y);
            float run = Mathf.Abs(centre.x) - 5f - Mathf.Abs(rampStart.x);
            Ramp("Ramp_Tower_" + tag, g, rampStart, rampYaw, run, top, 4f, 0.5f, DomeMapAssets.Platform);

            Vector3 inward = DomeLayout.Direction(rampYaw + 180f);
            Vector3 lateral = DomeLayout.Direction(rampYaw + 90f);
            Vector3 forward = DomeLayout.Direction(rampYaw);
            float rimY = top + 0.5f;
            Quaternion rotAlong = Quaternion.Euler(0f, rampYaw, 0f);

            Box("Wall_TowerRim_" + tag + "_Back", g, centre + forward * 4.8f + Vector3.up * rimY, rotAlong, new Vector3(10f, 1f, 0.4f), DomeMapAssets.Platform);
            Box("Wall_TowerRim_" + tag + "_SideA", g, centre + lateral * 4.8f + Vector3.up * rimY, rotAlong, new Vector3(0.4f, 1f, 10f), DomeMapAssets.Platform);
            Box("Wall_TowerRim_" + tag + "_SideB", g, centre - lateral * 4.8f + Vector3.up * rimY, rotAlong, new Vector3(0.4f, 1f, 10f), DomeMapAssets.Platform);
            Box("Wall_TowerRim_" + tag + "_FrontA", g, centre + inward * 4.8f + lateral * 3.7f + Vector3.up * rimY, rotAlong, new Vector3(2.6f, 1f, 0.4f), DomeMapAssets.Platform);
            Box("Wall_TowerRim_" + tag + "_FrontB", g, centre + inward * 4.8f - lateral * 3.7f + Vector3.up * rimY, rotAlong, new Vector3(2.6f, 1f, 0.4f), DomeMapAssets.Platform);

            Glow("Trim_TowerRim_" + tag, g, centre + forward * 4.8f + Vector3.up * (top + 1.03f), rotAlong, new Vector3(10f, 0.06f, 0.2f), DomeMapAssets.TrimTeal);
            Box("Cover_TowerCrate_" + tag, g, centre + lateral * 1.5f + Vector3.up * (top + 0.6f), Quaternion.Euler(0f, 20f, 0f), new Vector3(1.2f, 1.2f, 1.2f), DomeMapAssets.Cargo);
        }

        private static void BuildYards()
        {
            Transform g = Group("Yards");

            Transform gantry = new GameObject("Gantry_NW").transform;
            gantry.SetParent(g, false);
            float topY = 4.5f;
            Vector3 gc = new Vector3(-30f, 0f, 32f);
            Box("Gantry_Deck", gantry, gc + Vector3.up * (topY - 0.25f), Quaternion.identity, new Vector3(14f, 0.5f, 5f), DomeMapAssets.Platform);
            Glow("Trim_Gantry_Edge", gantry, gc + Vector3.up * (topY + 0.02f) + new Vector3(0f, 0f, -2.4f), Quaternion.identity, new Vector3(14f, 0.04f, 0.14f), DomeMapAssets.TrimTeal);
            Glow("Trim_Gantry_Edge2", gantry, gc + Vector3.up * (topY + 0.02f) + new Vector3(0f, 0f, 2.4f), Quaternion.identity, new Vector3(14f, 0.04f, 0.14f), DomeMapAssets.TrimViolet);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 legPos = gc + new Vector3(sx * 6.4f, (topY - 0.5f) * 0.5f, sz * 2f);
                    Box("Gantry_Leg_" + sx + "_" + sz, gantry, legPos, Quaternion.identity, new Vector3(0.8f, topY - 0.5f, 0.8f), DomeMapAssets.Drum);
                }
            }

            Box("Wall_Gantry_Rail_N", gantry, gc + new Vector3(0f, topY + 0.5f, 2.3f), Quaternion.identity, new Vector3(14f, 1f, 0.3f), DomeMapAssets.Platform);
            Box("Wall_Gantry_Rail_S", gantry, gc + new Vector3(0f, topY + 0.5f, -2.3f), Quaternion.identity, new Vector3(14f, 1f, 0.3f), DomeMapAssets.Platform);
            Ramp("Ramp_Gantry", gantry, new Vector3(-12f, 0f, 32f), 270f, 11f, topY, 3.5f, 0.5f, DomeMapAssets.Platform);
            Vehicle("Vehicle_Gantry", gantry, new Vector3(-30f, 0f, 32f), 0f);

            Transform se = new GameObject("Yard_SE").transform;
            se.SetParent(g, false);
            Box("Container_SE_A", se, new Vector3(27f, 1.3f, -28f), Quaternion.identity, new Vector3(6f, 2.6f, 2.4f), DomeMapAssets.Vehicle);
            Box("Container_SE_B", se, new Vector3(33f, 1.3f, -32f), Quaternion.identity, new Vector3(2.4f, 2.6f, 6f), DomeMapAssets.Vehicle);
            Box("Container_SE_C", se, new Vector3(27f, 3.9f, -28f), Quaternion.identity, new Vector3(6f, 2.6f, 2.4f), DomeMapAssets.Vehicle);
            Glow("Trim_Container_SE_A", se, new Vector3(27f, 5.23f, -28f), Quaternion.identity, new Vector3(6f, 0.06f, 0.2f), DomeMapAssets.TrimViolet);
            Ramp("Ramp_Container_SE", se, new Vector3(33f, 0f, -22f), 180f, 7f, 2.6f, 2.4f, 0.4f, DomeMapAssets.Platform);
            Vehicle("Vehicle_SE_A", se, new Vector3(20f, 0f, -36f), 100f);
            Vehicle("Vehicle_SE_B", se, new Vector3(35.5f, 0f, -20f), 10f);
        }

        private static void BuildCover()
        {
            Transform g = Group("Cover");

            for (int k = 0; k < 4; k++)
            {
                Vector3 a = Rot90(new Vector3(17f, 1f, 15f), k);
                Vector3 b = Rot90(new Vector3(19.2f, 1f, 15f), k);
                Vector3 c = Rot90(new Vector3(18.1f, 3f, 15f), k);
                Quaternion yaw = Quaternion.Euler(0f, 90f * k, 0f);
                Box("Crate_Stack_" + k + "_A", g, a, yaw, new Vector3(2f, 2f, 2f), DomeMapAssets.Cargo);
                Box("Crate_Stack_" + k + "_B", g, b, yaw, new Vector3(2f, 2f, 2f), DomeMapAssets.Cargo);
                Box("Crate_Stack_" + k + "_C", g, c, yaw, new Vector3(2f, 2f, 2f), DomeMapAssets.Cargo);

                Vector3 barrier = Rot90(new Vector3(22f, 0.55f, 9f), k);
                Box("Barrier_" + k, g, barrier, yaw, new Vector3(3f, 1.1f, 0.5f), DomeMapAssets.Drum);
                Glow("Trim_Barrier_" + k, g, barrier + Vector3.up * 0.58f, yaw, new Vector3(3f, 0.06f, 0.2f), DomeMapAssets.TrimTeal);

                Vector3 container = Rot90(new Vector3(26f, 1.3f, 13f), k);
                Box("Container_" + k, g, container, yaw, new Vector3(6f, 2.6f, 2.4f), DomeMapAssets.Vehicle);
                Glow("Trim_Container_" + k, g, container + Vector3.up * 1.33f, yaw, new Vector3(6f, 0.06f, 0.2f), DomeMapAssets.TrimViolet);
            }

            for (int i = 0; i < 8; i++)
            {
                float az = 22.5f + 45f * i;
                Vector3 pos = DomeLayout.Direction(az) * 24f;
                HexPrism("Pillar_Hex_" + i, g, pos, 0.9f, 5f, DomeMapAssets.Platform, az);
                Glow("Trim_Pillar_" + i, g, pos + Vector3.up * 5.05f, Quaternion.identity, new Vector3(1.2f, 0.1f, 1.2f),
                    i % 2 == 0 ? DomeMapAssets.TrimTeal : DomeMapAssets.TrimViolet);
            }

            Vehicle("Vehicle_NW_A", g, new Vector3(-26f, 0f, 22f), 20f);
            Vehicle("Vehicle_NW_B", g, new Vector3(-34f, 0f, 24f), -75f);
            Vehicle("Vehicle_NE", g, new Vector3(30f, 0f, 19f), 160f);
            Vehicle("Vehicle_SW", g, new Vector3(-30f, 0f, -19f), -20f);
        }

        private static void Vehicle(string name, Transform parent, Vector3 pos, float yaw)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.isStatic = true;
            Transform t = root.transform;

            Box("Body", t, new Vector3(0f, 0.75f, 0f), Quaternion.identity, new Vector3(4.4f, 1.1f, 2f), DomeMapAssets.Vehicle);
            Box("Cabin", t, new Vector3(-0.4f, 1.65f, 0f), Quaternion.identity, new Vector3(2.2f, 0.8f, 1.7f), DomeMapAssets.Vehicle);
            Glow("Trim_Underglow", t, new Vector3(0f, 0.17f, 0f), Quaternion.identity, new Vector3(4f, 0.05f, 1.7f), DomeMapAssets.TrimViolet);
            Glow("Trim_Headlight", t, new Vector3(2.21f, 0.95f, 0f), Quaternion.identity, new Vector3(0.06f, 0.14f, 1.5f), DomeMapAssets.TrimTeal);
            Glow("Trim_Taillight", t, new Vector3(-2.21f, 0.95f, 0f), Quaternion.identity, new Vector3(0.06f, 0.14f, 1.5f), DomeMapAssets.TrimViolet);
            Glow("Trim_Canopy", t, new Vector3(-0.4f, 2.06f, 0f), Quaternion.identity, new Vector3(2.2f, 0.04f, 0.1f), DomeMapAssets.TrimTeal);
        }

        #endregion

        #region Holograms

        private static void BuildHolograms()
        {
            Transform g = Group("Holograms");

            GameObject globe = Prim(PrimitiveType.Sphere, "Holo_Globe", g, new Vector3(0f, 3.5f, 0f), Quaternion.identity,
                Vector3.one * 1.9f, DomeMapAssets.HoloGlobe, false, false);
            SetupHoloRenderer(globe);
            globe.AddComponent<DomeHoloFloat>().Configure(0.18f, 0.7f, 0f, new Vector3(0f, 22f, 0f));

            for (int i = 0; i < 2; i++)
            {
                GameObject ring = Prim(PrimitiveType.Cylinder, "Holo_Ring_" + i, g, new Vector3(0f, 3.5f, 0f),
                    Quaternion.Euler(i == 0 ? 18f : -22f, 0f, i == 0 ? 12f : -16f),
                    new Vector3(3.4f + i * 0.9f, 0.005f, 3.4f + i * 0.9f), DomeMapAssets.HoloRadar, false, false);
                SetupHoloRenderer(ring);
                ring.AddComponent<DomeHoloFloat>().Configure(0.18f, 0.7f, 0f, new Vector3(0f, i == 0 ? -35f : 28f, 0f));
            }

            for (int i = 0; i < 4; i++)
            {
                float az = 22.5f + 90f * i;
                Vector3 pos = DomeLayout.Direction(az) * 9.2f + Vector3.up * 5.2f;
                Holo("Holo_Inner_" + i, g, pos, DomeLayout.Direction(az), new Vector2(3.2f, 2f),
                    i % 2 == 0 ? DomeMapAssets.HoloBars : DomeMapAssets.HoloRadar, i * 0.9f);
            }

            for (int i = 0; i < 4; i++)
            {
                float az = i * 90f + 45f;
                Vector3 pos = DomeLayout.Direction(az) * 14.4f + Vector3.up * 6.0f;
                Holo("Holo_Dome_" + i, g, pos, DomeLayout.Direction(az), new Vector2(3.6f, 2.2f),
                    i % 2 == 0 ? DomeMapAssets.HoloRadar : DomeMapAssets.HoloBars, 1.7f + i);
            }

            for (int i = 0; i < 4; i++)
            {
                float az = i * 90f;
                Vector3 pos = DomeLayout.Direction(az) * 21f + Vector3.up * 6.2f;
                Holo("Holo_Gate_" + i, g, pos, DomeLayout.Direction(az), new Vector2(5.6f, 2.6f),
                    i % 2 == 0 ? DomeMapAssets.HoloBars : DomeMapAssets.HoloRadar, 0.5f * i);
            }

            for (int i = 0; i < 4; i++)
            {
                float az = i * 90f;
                Vector3 pos = DomeLayout.Direction(az) * (DomeLayout.ArenaHalfSize - 1.5f) + Vector3.up * 5.4f
                    + DomeLayout.Direction(az + 90f) * 12f;
                Holo("Holo_Billboard_" + i, g, pos, DomeLayout.Direction(az), new Vector2(7f, 3.4f),
                    i % 2 == 0 ? DomeMapAssets.HoloRadar : DomeMapAssets.HoloBars, 2.2f * i);
            }
        }

        private static void SetupHoloRenderer(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static void Holo(string name, Transform parent, Vector3 pos, Vector3 outward, Vector2 size, Material mat, float phase)
        {
            GameObject go = Prim(PrimitiveType.Quad, name, parent, pos, Quaternion.LookRotation(outward, Vector3.up),
                new Vector3(size.x, size.y, 1f), mat, false, false);
            SetupHoloRenderer(go);
            go.AddComponent<DomeHoloFloat>().Configure(0.14f, 0.9f, phase, Vector3.zero);
        }

        #endregion

        #region Spawns, targets, player

        private static void SpawnPointsAndTargets()
        {
            Transform spawns = Group("SpawnPoints");
            Vector3[] points =
            {
                new Vector3(0f, 0.1f, -34f), new Vector3(0f, 0.1f, 34f), new Vector3(34f, 0.1f, 0f), new Vector3(-34f, 0.1f, 0f),
                new Vector3(20f, 0.1f, 20f), new Vector3(-20f, 0.1f, -20f), new Vector3(20f, 0.1f, -20f), new Vector3(-20f, 0.1f, 20f),
                new Vector3(9f, 0.1f, 3f), new Vector3(-9f, 0.1f, -3f)
            };

            for (int i = 0; i < points.Length; i++)
            {
                string name = i == 0 ? "PlayerSpawn" : "Spawn_" + i.ToString("00");
                var go = new GameObject(name);
                go.transform.SetParent(spawns, false);
                Vector3 position = points[i];
                if (i == 0)
                    position.y = 0.55f;
                go.transform.position = position;
                go.transform.rotation = Quaternion.Euler(0f, Yaw(points[i], Vector3.zero), 0f);
                go.tag = "Respawn";
            }

            Transform targets = Group("Targets");
            CreateTarget(targets, "Target_Near", new Vector3(0f, 1f, -22f), 1f);
            CreateTarget(targets, "Target_Dome_N", new Vector3(0f, 1f, 9f), 1f);
            CreateTarget(targets, "Target_Dome_S", new Vector3(0f, 1f, -9f), 1f);
            Vector3 catwalk = DomeLayout.Direction(75f) * DomeLayout.CatwalkRadius;
            CreateTarget(targets, "Target_Catwalk", new Vector3(catwalk.x, DomeLayout.CatwalkTopY + 1f, catwalk.z), 1f);
            CreateTarget(targets, "Target_Tower_NE", new Vector3(30f, DomeLayout.TowerTopY + 1f, 30f), 1f);
            CreateTarget(targets, "Target_Tower_SW", new Vector3(-30f, DomeLayout.TowerTopY + 1f, -30f), 1f);
            CreateTarget(targets, "Target_Gantry", new Vector3(-30f, 4.5f + 1f, 32f), 1f);
            CreateTarget(targets, "Target_Yard_SE", new Vector3(22f, 1f, -30f), 1f);
            CreateTarget(targets, "Target_Far", new Vector3(0f, 1.5f, 30f), 1.5f);
        }

        private static void CreateTarget(Transform parent, string name, Vector3 pos, float scale)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            GameObject body = Prim(PrimitiveType.Cylinder, "Body", root.transform, Vector3.zero, Quaternion.identity,
                new Vector3(0.8f * scale, 1f * scale, 0.8f * scale), DomeMapAssets.TargetBody, true, false);
            body.AddComponent<HitZone>().Configure(HitZoneType.Body);

            GameObject head = Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.3f * scale, 0f), Quaternion.identity,
                Vector3.one * 0.5f * scale, DomeMapAssets.TargetHead, true, false);
            head.AddComponent<HitZone>().Configure(HitZoneType.Head);

            root.AddComponent<Target>();
        }

        private static void SpawnPlayer()
        {
            ClassLoadoutMenu.CreateDefaultClassDefinitions();
            TestRangeWeaponLoadout.EnsureWeaponConfigsOnly();

            GameObject player = TestRangePlayerSetup.ReplaceScenePlayer();
            if (player == null)
                throw new InvalidOperationException("DomeMapBuilder: failed to spawn PlayerCharacter.");

            TestRangePlayerSetup.WirePlayer(player);
            Debug.Log("Dome: PlayerCharacter placed at " + player.transform.position);
        }

        private static void EnsurePlayerPresent()
        {
            if (GameObject.Find("Player") != null)
                return;

            var movement = Object.FindAnyObjectByType<MovementController>();
            if (movement == null)
                throw new InvalidOperationException("DomeMapBuilder: Player is missing from the active scene.");

            movement.gameObject.name = "Player";
        }

        /// <summary>Keeps every environment collider on the Default layer (covered by MovementConfig.wallLayers = ~0).</summary>
        private static void SetEnvironmentLayers()
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                c.gameObject.layer = defaultLayer;
        }

        #endregion
    }
}
