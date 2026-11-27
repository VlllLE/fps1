using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FpsMaps.Triad.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Triad.unity from <see cref="TriadLayout"/>: a dusk urban block with three lanes, three
    /// height tiers and three enterable buildings. All geometry sits on the Default layer so MovementConfig.wallLayers
    /// covers every surface; bots navigate it through the runtime NavMesh built from those colliders.
    /// </summary>
    public static class TriadMapBuilder
    {
        private static int colliderCount;
        private static int blockCount;

        [MenuItem("Tools/Maps/Build Triad")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Build();
        }

        [MenuItem("Tools/Maps/Open Triad")]
        public static void OpenFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TriadLayout.ScenePath) == null)
            {
                BuildFromMenu();
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(TriadLayout.ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Maps/Enter Triad Play Mode")]
        public static void OpenAndPlay()
        {
            OpenFromMenu();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TriadLayout.ScenePath) != null)
                EditorApplication.isPlaying = true;
        }

        /// <summary>CLI: -batchmode -executeMethod FpsMaps.Triad.EditorTools.TriadMapBuilder.BuildBatch</summary>
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
            TriadMapAssets.EnsureAll();
            TriadMapAssets.EnsureFolder("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            colliderCount = 0;
            blockCount = 0;

            TriadLayoutData layout = TriadLayout.Create();
            BuildLighting(layout);
            BuildBlocks(layout);
            BuildSpawns(layout);
            BuildWaypoints(layout);
            SetEnvironmentLayers();
            SpawnPlayer();

            EnsurePlayerPresent();
            if (!EditorSceneManager.SaveScene(scene, TriadLayout.ScenePath))
                throw new InvalidOperationException("TriadMapBuilder: failed to save " + TriadLayout.ScenePath);

            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Triad map built: " + TriadLayout.ScenePath + " (" + blockCount + " blocks, " + colliderCount
                + " environment colliders, " + layout.Spawns.Count + " spawns, " + layout.Waypoints.Count + " waypoints).");
            return scene;
        }

        public static void EnsureInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != TriadLayout.ScenePath)
                    continue;

                if (!scenes[i].enabled)
                {
                    scenes[i] = new EditorBuildSettingsScene(TriadLayout.ScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                }

                return;
            }

            scenes.Add(new EditorBuildSettingsScene(TriadLayout.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #region Lighting

        private static void BuildLighting(TriadLayoutData layout)
        {
            RenderSettings.skybox = TriadMapAssets.Skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.5f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.46f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.2f, 0.2f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.7f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0065f;
            RenderSettings.fogColor = new Color(0.84f, 0.58f, 0.46f);

            Transform g = Group("Lighting");

            var sunGo = new GameObject("Dusk Sun");
            sunGo.transform.SetParent(g, false);
            sunGo.transform.rotation = Quaternion.Euler(14f, 62f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.62f, 0.34f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            RenderSettings.sun = sun;

            var fillGo = new GameObject("Sky Fill Light");
            fillGo.transform.SetParent(g, false);
            fillGo.transform.rotation = Quaternion.Euler(38f, 242f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.45f, 0.5f, 0.85f);
            fill.intensity = 0.35f;
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

        private static void BuildBlocks(TriadLayoutData layout)
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
                go.transform.rotation = Quaternion.Euler(b.Euler);
                go.transform.localScale = b.Size;
                go.isStatic = true;

                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = TriadMapAssets.For(b.Surface);
                if (b.Surface == TriadSurface.Skyline)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                else if (b.Surface == TriadSurface.Lamp)
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

        private static void BuildSpawns(TriadLayoutData layout)
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

        private static void BuildWaypoints(TriadLayoutData layout)
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
                throw new InvalidOperationException("TriadMapBuilder: failed to spawn PlayerCharacter.");

            TestRangePlayerSetup.WirePlayer(player);
            Debug.Log("Triad: PlayerCharacter placed at " + player.transform.position);
        }

        private static void EnsurePlayerPresent()
        {
            if (GameObject.Find("Player") != null)
                return;

            var movement = Object.FindAnyObjectByType<MovementController>();
            if (movement == null)
                throw new InvalidOperationException("TriadMapBuilder: Player is missing from the active scene.");

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
