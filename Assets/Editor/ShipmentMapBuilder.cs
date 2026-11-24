using System;
using System.Collections.Generic;
using System.IO;
using CodMovementShooter.Maps.Shipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Builds Assets/Scenes/Shipment.unity: a tiny neon container-yard arena (MW3 Shipment-style layout,
/// original geometry) with the PlayerCharacter prefab, spawn points, night-city lighting and colliders
/// on the Default layer (included by MovementConfig.wallLayers) so wall bounce / slide / bhop work.
/// Layout data lives in <see cref="ShipmentLayout"/>; visuals in <see cref="ShipmentMapVisuals"/>.
/// </summary>
public static class ShipmentMapBuilder
{
    public const string ScenePath = ShipmentMapInfo.ScenePath;
    private const int EnvironmentLayer = 0;

    [MenuItem("Tools/Maps/Build Shipment")]
    public static void BuildShipment()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("BuildShipment: exit Play Mode before building the map.");

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder("Assets/Scenes");
        EnsureMovementConfig();
        WarnIfWallLayersExcludeEnvironment();

        var materials = ShipmentMapVisuals.EnsureAssets();
        var layout = ShipmentLayout.Build();

        ClassLoadoutMenu.CreateDefaultClassDefinitions();
        TestRangeWeaponLoadout.EnsureWeaponConfigsOnly();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        ShipmentMapVisuals.ApplyScenePresentation(materials["Skybox"]);
        CreateMoonLight();
        CreateReflectionProbe();
        CreateBlocks(layout, materials);
        CreateLights(layout);
        CreateSpawns(layout);
        CreatePlayer();
        DynamicGI.UpdateEnvironment();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("BuildShipment: failed to save " + ScenePath);

        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Shipment map build complete: " + ScenePath +
                  " (" + layout.Blocks.Count + " blocks, " + layout.Spawns.Count + " spawns).");
    }

    [MenuItem("Tools/Maps/Open Shipment")]
    public static void Open()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogWarning("Shipment scene not found. Run Tools/Maps/Build Shipment first.");
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/Maps/Enter Shipment Play Mode")]
    public static void OpenAndPlay()
    {
        Open();
        EditorApplication.isPlaying = true;
    }

    /// <summary>CLI: -executeMethod ShipmentMapBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        try
        {
            BuildShipment();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void CreateBlocks(ShipmentLayoutData layout, Dictionary<string, Material> materials)
    {
        var groups = new Dictionary<string, Transform>();

        foreach (var spec in layout.Blocks)
        {
            if (!groups.TryGetValue(spec.Group, out var parent))
            {
                var groupGo = new GameObject(spec.Group);
                parent = groupGo.transform;
                groups[spec.Group] = parent;
            }

            CreateBlock(spec, parent, materials);
        }
    }

    private static void CreateBlock(BlockSpec spec, Transform parent, Dictionary<string, Material> materials)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = spec.Name;
        go.layer = EnvironmentLayer;
        go.transform.SetParent(parent, false);
        go.transform.position = spec.Center;
        go.transform.localScale = spec.Size;

        if (!spec.Solid)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
        }

        if (!spec.Renderable)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                Object.DestroyImmediate(renderer);
            var filter = go.GetComponent<MeshFilter>();
            if (filter != null)
                Object.DestroyImmediate(filter);
            go.isStatic = true;
            return;
        }

        var meshRenderer = go.GetComponent<MeshRenderer>();
        if (!string.IsNullOrEmpty(spec.Material))
        {
            if (materials.TryGetValue(spec.Material, out var material))
                meshRenderer.sharedMaterial = material;
            else
                Debug.LogWarning("Shipment: missing material '" + spec.Material + "' for " + spec.Name);
        }

        bool castsShadow = spec.Kind == BlockKind.Floor || spec.Kind == BlockKind.Wall
            || spec.Kind == BlockKind.Container || spec.Kind == BlockKind.Step
            || spec.Kind == BlockKind.Prop || spec.Kind == BlockKind.Crane;
        if (!castsShadow)
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

        if (spec.Kind == BlockKind.Sign)
        {
            meshRenderer.receiveShadows = false;
            go.AddComponent<ShipmentHoloPulse>();
        }
        else
        {
            go.isStatic = true;
        }

        if (spec.Kind == BlockKind.Skyline || spec.Kind == BlockKind.Neon || spec.Kind == BlockKind.Water)
            meshRenderer.receiveShadows = false;
    }

    private static void CreateMoonLight()
    {
        var go = new GameObject("Moon Light");
        go.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.45f, 0.55f, 1f);
        light.intensity = 0.8f;
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;
    }

    private static void CreateReflectionProbe()
    {
        var go = new GameObject("Reflection Probe");
        go.transform.position = new Vector3(0.5f, 4f, 0f);
        var probe = go.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(60f, 24f, 42f);
        probe.resolution = 128;
        probe.hdr = true;
        probe.boxProjection = true;
        probe.intensity = 1.2f;
        probe.farClipPlane = 150f;
    }

    private static void CreateLights(ShipmentLayoutData layout)
    {
        var root = new GameObject("Lights").transform;
        foreach (var spec in layout.Lights)
        {
            var go = new GameObject(spec.Name);
            go.transform.SetParent(root, false);
            go.transform.position = spec.Position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = spec.Color;
            light.range = spec.Range;
            light.intensity = spec.Intensity;
            light.shadows = LightShadows.None;
            light.renderMode = spec.ForcePixel ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
        }
    }

    private static void CreateSpawns(ShipmentLayoutData layout)
    {
        var root = new GameObject("SpawnPoints").transform;
        SpawnSpec initial = null;

        foreach (var spec in layout.Spawns)
        {
            var go = new GameObject(spec.Name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(0f, spec.Yaw, 0f));
            go.AddComponent<ShipmentSpawnPoint>().Configure(spec.Team);

            if (spec.Name == "Spawn_A3")
                initial = spec;
        }

        if (initial == null)
            initial = layout.Spawns[0];

        // TestRangePlayerSetup.ReplaceScenePlayer positions the player at the object named PlayerSpawn.
        var playerSpawn = new GameObject("PlayerSpawn");
        playerSpawn.transform.SetPositionAndRotation(initial.Position, Quaternion.Euler(0f, initial.Yaw, 0f));
    }

    private static void CreatePlayer()
    {
        var player = TestRangePlayerSetup.ReplaceScenePlayer();
        if (player == null)
            throw new InvalidOperationException("BuildShipment: failed to spawn PlayerCharacter prefab.");

        TestRangePlayerSetup.WirePlayer(player);

        if (GameObject.Find("Player") == null)
            throw new InvalidOperationException("BuildShipment: Player is missing from the scene before save.");
    }

    private static void EnsureMovementConfig()
    {
        EnsureFolder("Assets/Configs");
        if (AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Configs/DefaultMovementConfig.asset") == null)
        {
            var config = ScriptableObject.CreateInstance<MovementConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Configs/DefaultMovementConfig.asset");
        }
    }

    private static void WarnIfWallLayersExcludeEnvironment()
    {
        int mask = 1 << EnvironmentLayer;
        foreach (string guid in AssetDatabase.FindAssets("t:MovementConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<MovementConfig>(path);
            if (config != null && (config.wallLayers.value & mask) == 0)
            {
                Debug.LogWarning("Shipment: " + path + " wallLayers excludes the Default layer; " +
                                 "wall bounce will not detect Shipment geometry.");
            }
        }
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var scene in scenes)
        {
            if (scene.path == scenePath)
            {
                if (!scene.enabled)
                    scene.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
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
}
