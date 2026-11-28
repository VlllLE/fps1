using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FpsMaps.Bazaar.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Bazaar.unity from <see cref="BazaarLayout"/>: a sunlit desert market town laid out like a
    /// tactical-shooter map (two bomb sites, three lanes, attacker and defender yards). All geometry sits on the Default
    /// layer so MovementConfig.wallLayers covers every surface; bots navigate it through the runtime NavMesh built from
    /// those colliders. Plant and buy zones are trigger volumes with a <see cref="BazaarPlantZone"/> marker for a future bomb mode.
    /// </summary>
    public static class BazaarMapBuilder
    {
        private static int colliderCount;
        private static int blockCount;

        [MenuItem("Tools/Maps/Build Bazaar")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Build();
        }

        [MenuItem("Tools/Maps/Open Bazaar")]
        public static void OpenFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BazaarLayout.ScenePath) == null)
            {
                BuildFromMenu();
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(BazaarLayout.ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Maps/Enter Bazaar Play Mode")]
        public static void OpenAndPlay()
        {
            OpenFromMenu();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BazaarLayout.ScenePath) != null)
                EditorApplication.isPlaying = true;
        }

        /// <summary>CLI: -batchmode -executeMethod FpsMaps.Bazaar.EditorTools.BazaarMapBuilder.BuildBatch</summary>
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
            BazaarMapAssets.EnsureAll();
            BazaarMapAssets.EnsureFolder("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            colliderCount = 0;
            blockCount = 0;

            BazaarLayoutData layout = BazaarLayout.Create();
            BuildLighting(layout);
            BuildBlocks(layout);
            BuildZones(layout);
            BuildSpawns(layout);
            BuildWaypoints(layout);
            SetEnvironmentLayers();
            SpawnPlayer();

            EnsurePlayerPresent();
            if (!EditorSceneManager.SaveScene(scene, BazaarLayout.ScenePath))
                throw new InvalidOperationException("BazaarMapBuilder: failed to save " + BazaarLayout.ScenePath);

            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Bazaar map built: " + BazaarLayout.ScenePath + " (" + blockCount + " blocks, " + colliderCount
                + " environment colliders, " + layout.Spawns.Count + " spawns, " + layout.Waypoints.Count + " waypoints, "
                + layout.Zones.Count + " zones).");
            return scene;
        }

        public static void EnsureInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != BazaarLayout.ScenePath)
                    continue;

                if (!scenes[i].enabled)
                {
                    scenes[i] = new EditorBuildSettingsScene(BazaarLayout.ScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                }

                return;
            }

            scenes.Add(new EditorBuildSettingsScene(BazaarLayout.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #region Lighting

        private static void BuildLighting(BazaarLayoutData layout)
        {
            RenderSettings.skybox = BazaarMapAssets.Skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.68f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.75f, 0.7f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.36f, 0.3f);
            RenderSettings.ambientIntensity = 0.55f;
            RenderSettings.reflectionIntensity = 0.4f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0025f;
            RenderSettings.fogColor = new Color(0.82f, 0.82f, 0.78f);

            Transform g = Group("Lighting");

            var sunGo = new GameObject("Desert Sun");
            sunGo.transform.SetParent(g, false);
            sunGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 0.8f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;

            var fillGo = new GameObject("Sky Fill Light");
            fillGo.transform.SetParent(g, false);
            fillGo.transform.rotation = Quaternion.Euler(35f, 142f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.6f, 0.7f, 0.95f);
            fill.intensity = 0.3f;
            fill.shadows = LightShadows.None;

            for (int i = 0; i < layout.Lights.Count; i++)
            {
                LightSpec spec = layout.Lights[i];
                var go = new GameObject(spec.Name);
                go.transform.SetParent(g, false);
                go.transform.position = spec.Position;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = spec.Color;
                light.intensity = spec.Intensity;
                light.range = spec.Range;
                light.shadows = LightShadows.None;
            }
        }

        #endregion

        #region Geometry

        private static void BuildBlocks(BazaarLayoutData layout)
        {
            var groups = new Dictionary<string, Transform>();
            for (int i = 0; i < layout.Blocks.Count; i++)
            {
                BlockSpec b = layout.Blocks[i];
                string groupName = string.IsNullOrEmpty(b.Group) ? "Misc" : b.Group;
                if (!groups.TryGetValue(groupName, out Transform parent))
                {
                    parent = Group(groupName);
                    groups[groupName] = parent;
                }

                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = b.Name;
                go.transform.SetParent(parent, false);
                go.transform.position = b.Center;
                go.transform.localScale = b.Size;
                go.isStatic = true;

                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = BazaarMapAssets.For(b.Surface);
                if (b.Surface == BazaarSurface.Backdrop)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                else if (b.Surface == BazaarSurface.Lamp || !b.Solid)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                Collider collider = go.GetComponent<Collider>();
                if (!b.Solid)
                {
                    if (collider != null)
                        Object.DestroyImmediate(collider);
                }
                else
                {
                    colliderCount++;
                }

                blockCount++;
            }
        }

        private static void BuildZones(BazaarLayoutData layout)
        {
            Transform zones = Group("Zones");
            for (int i = 0; i < layout.Zones.Count; i++)
            {
                ZoneSpec z = layout.Zones[i];
                var go = new GameObject(z.Name);
                go.transform.SetParent(zones, false);
                go.transform.position = z.Bounds.center;
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = z.Bounds.size;
                go.AddComponent<BazaarPlantZone>().Configure(z.Kind, z.Site);
            }
        }

        private static void BuildSpawns(BazaarLayoutData layout)
        {
            Transform spawns = Group("Spawns");
            for (int i = 0; i < layout.Spawns.Count; i++)
            {
                SpawnSpec s = layout.Spawns[i];
                var go = new GameObject(s.Name);
                go.transform.SetParent(spawns, false);
                go.transform.position = s.Position;
                go.transform.rotation = Quaternion.Euler(0f, s.Yaw, 0f);
                go.tag = "Respawn";
            }
        }

        private static void BuildWaypoints(BazaarLayoutData layout)
        {
            Transform waypoints = Group("Waypoints");
            for (int i = 0; i < layout.Waypoints.Count; i++)
            {
                WaypointSpec w = layout.Waypoints[i];
                var go = new GameObject(w.Name);
                go.transform.SetParent(waypoints, false);
                go.transform.position = w.Position;
                go.AddComponent<BotWaypoint>();
            }
        }

        private static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.isStatic = true;
            return go.transform;
        }

        private static void SpawnPlayer()
        {
            ClassLoadoutMenu.CreateDefaultClassDefinitions();
            TestRangeWeaponLoadout.EnsureWeaponConfigsOnly();

            GameObject player = TestRangePlayerSetup.ReplaceScenePlayer();
            if (player == null)
                throw new InvalidOperationException("BazaarMapBuilder: failed to spawn PlayerCharacter.");

            TestRangePlayerSetup.WirePlayer(player);
            Debug.Log("Bazaar: PlayerCharacter placed at " + player.transform.position);
        }

        private static void EnsurePlayerPresent()
        {
            if (GameObject.Find("Player") != null)
                return;

            var movement = Object.FindAnyObjectByType<MovementController>();
            if (movement == null)
                throw new InvalidOperationException("BazaarMapBuilder: Player is missing from the active scene.");

            movement.gameObject.name = "Player";
        }

        private static void SetEnvironmentLayers()
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            foreach (Collider c in Object.FindObjectsByType<Collider>())
                c.gameObject.layer = defaultLayer;
        }

        #endregion
    }
}
