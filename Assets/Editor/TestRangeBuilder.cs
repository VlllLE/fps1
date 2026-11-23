using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public class TestRangeBuilder : MonoBehaviour
{
    [MenuItem("Tools/Build Test Range")]
    public static void BuildTestRange()
    {
        CreateDirectories();
        CreateConfigs();
        TestRangeVisuals.EnsureAssets();
        CreateScene();
        CreatePlayerPrefab();
        AddSceneToBuildSettings();
        EnsurePlayerPresentBeforeSave();
        
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, "Assets/Scenes/TestRange.unity"))
            throw new System.InvalidOperationException("BuildTestRange: failed to save TestRange.unity");
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("Test Range build complete (player saved in scene).");
    }
    
    private static void EnsurePlayerPresentBeforeSave()
    {
        if (GameObject.Find("Player") != null)
            return;
        
        var movement = Object.FindAnyObjectByType<MovementController>();
        if (movement != null)
        {
            movement.gameObject.name = "Player";
            return;
        }
        
        throw new System.InvalidOperationException(
            "BuildTestRange: Player is missing from the active scene. CreatePlayerPrefab must run before SaveScene.");
    }
    
    private static void CreateDirectories()
    {
        string[] dirs = {
            "Assets/Scenes",
            "Assets/Prefabs",
            "Assets/Configs",
            "Assets/Scripts/Targets"
        };
        
        foreach (string dir in dirs)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
                string folder = Path.GetFileName(dir);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
        AssetDatabase.Refresh();
    }
    
    private static void CreateConfigs()
    {
        if (!AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Configs/DefaultMovementConfig.asset"))
        {
            var movementConfig = ScriptableObject.CreateInstance<MovementConfig>();
            AssetDatabase.CreateAsset(movementConfig, "Assets/Configs/DefaultMovementConfig.asset");
        }
        
        var weaponConfig = AssetDatabase.LoadAssetAtPath<WeaponConfig>("Assets/Configs/TestPistolConfig.asset");
        if (weaponConfig == null)
        {
            weaponConfig = ScriptableObject.CreateInstance<WeaponConfig>();
            AssetDatabase.CreateAsset(weaponConfig, "Assets/Configs/TestPistolConfig.asset");
        }
        SetupTestPistol(weaponConfig);
        EditorUtility.SetDirty(weaponConfig);
        
        AssetDatabase.SaveAssets();
    }
    
    private static void SetupTestPistol(WeaponConfig config)
    {
        var so = new SerializedObject(config);
        
        SetField(so, "weaponName", "Test Pistol");
        SetField(so, "damage", 25f);
        SetField(so, "headshotMultiplier", 2f);
        SetField(so, "limbMultiplier", 0.75f);
        SetField(so, "fireRate", 400f);
        SetField(so, "range", 100f);
        SetField(so, "magazineSize", 12);
        SetField(so, "maxReserveAmmo", 48);
        SetField(so, "startingReserveAmmo", 48);
        SetField(so, "reloadTime", 1.5f);
        SetField(so, "baseSpread", 0.5f);
        SetField(so, "adsSpread", 0.15f);
        SetField(so, "adsSpeed", 0.2f);
        
        var fireModes = so.FindProperty("availableFireModes");
        if (fireModes != null)
        {
            fireModes.arraySize = 1;
            fireModes.GetArrayElementAtIndex(0).enumValueIndex = (int)FireMode.Semi;
        }
        
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    
    private static void SetField(SerializedObject so, string name, object value)
    {
        var prop = so.FindProperty(name);
        if (prop == null) return;
        
        switch (value)
        {
            case string s: prop.stringValue = s; break;
            case float f: prop.floatValue = f; break;
            case int i: prop.intValue = i; break;
            case bool b: prop.boolValue = b; break;
            case System.Enum e: prop.enumValueIndex = System.Convert.ToInt32(e); break;
        }
    }
    
    private static void CreateScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        var defaultCam = GameObject.Find("Main Camera");
        if (defaultCam != null)
            Object.DestroyImmediate(defaultCam);
        
        TestRangeVisuals.ApplyScenePresentation();
        
        CreateGround();
        CreateWalls();
        CreateRamps();
        CreateTargets();
        CreateSpawnPoint();
        TestRangeVisuals.CreateProps();
    }
    
    private static void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(50, 1, 50);
        ground.isStatic = true;
        
        TestRangeVisuals.ApplyMaterial(ground.GetComponent<Renderer>(), TestRangeVisuals.Ground);
    }
    
    private static void CreateWalls()
    {
        CreateWall("Wall_Left", new Vector3(-8, 2.5f, 5), new Vector3(0.5f, 5, 10));
        CreateWall("Wall_Right", new Vector3(8, 2.5f, 5), new Vector3(0.5f, 5, 10));
        CreateWall("Wall_Corner_A", new Vector3(-5, 2.5f, 12), new Vector3(6, 5, 0.5f));
        CreateWall("Wall_Corner_B", new Vector3(5, 2.5f, 12), new Vector3(6, 5, 0.5f));
        CreateWall("Wall_Low", new Vector3(-10, 1f, 0), new Vector3(4, 2, 0.5f));
    }
    
    private static void CreateWall(string name, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.isStatic = true;
        
        TestRangeVisuals.ApplyMaterial(wall.GetComponent<Renderer>(), TestRangeVisuals.Wall);
    }
    
    private static void CreateRamps()
    {
        CreateRamp("Ramp_A", new Vector3(-4, 0.75f, -3), new Vector3(4, 1.5f, 6), 15f);
        CreateRamp("Ramp_B", new Vector3(4, 0.75f, -3), new Vector3(4, 1.5f, 6), -15f);
        CreateRamp("Ramp_High", new Vector3(0, 1.5f, -8), new Vector3(6, 3, 8), 20f);
    }
    
    private static void CreateRamp(string name, Vector3 position, Vector3 scale, float angle)
    {
        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = name;
        ramp.transform.position = position;
        ramp.transform.localScale = scale;
        ramp.transform.rotation = Quaternion.Euler(angle, 0, 0);
        ramp.isStatic = true;
        
        TestRangeVisuals.ApplyMaterial(ramp.GetComponent<Renderer>(), TestRangeVisuals.Ramp);
    }
    
    private static void CreateTargets()
    {
        CreateTarget("Target_Near", new Vector3(0, 1.5f, 10), Color.red);
        CreateTarget("Target_Left", new Vector3(-6, 1.5f, 8), Color.green);
        CreateTarget("Target_Right", new Vector3(6, 1.5f, 8), Color.blue);
        CreateTarget("Target_Far", new Vector3(0, 2f, 18), Color.yellow, 1.5f);
        CreateTarget("Target_High", new Vector3(3, 4f, 12), Color.magenta);
    }
    
    private static void CreateTarget(string name, Vector3 position, Color color, float scale = 1f)
    {
        var target = new GameObject(name);
        target.transform.position = position;
        target.layer = LayerMask.NameToLayer("Default");
        
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(target.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.8f * scale, 1f * scale, 0.8f * scale);
        
        var bodyZone = body.AddComponent<HitZone>();
        bodyZone.Configure(HitZoneType.Body);
        
        var bodyRenderer = body.GetComponent<Renderer>();
        var bodyMat = new Material(TestRangeVisuals.TargetBodyMaterial);
        bodyMat.color = color;
        bodyRenderer.sharedMaterial = bodyMat;
        
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(target.transform);
        head.transform.localPosition = new Vector3(0, 1.3f * scale, 0);
        head.transform.localScale = Vector3.one * 0.5f * scale;
        
        var headZone = head.AddComponent<HitZone>();
        headZone.Configure(HitZoneType.Head);
        
        TestRangeVisuals.ApplyMaterial(head.GetComponent<Renderer>(), TestRangeVisuals.TargetHeadMaterial);
        
        target.AddComponent<Target>();
    }
    
    private static void CreateSpawnPoint()
    {
        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.position = new Vector3(0, 1.5f, -5);
        spawn.transform.rotation = Quaternion.identity;
        
        var icon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        icon.name = "SpawnIcon";
        icon.transform.SetParent(spawn.transform);
        icon.transform.localPosition = Vector3.zero;
        icon.transform.localScale = Vector3.one * 0.3f;
        Object.DestroyImmediate(icon.GetComponent<Collider>());
        
        var renderer = icon.GetComponent<Renderer>();
        renderer.sharedMaterial = new Material(Shader.Find("Standard"));
        renderer.sharedMaterial.color = Color.cyan;
    }
    
    private static void CreatePlayerPrefab()
    {
        ClassLoadoutMenu.CreateDefaultClassDefinitions();
        TestRangeWeaponLoadout.EnsureWeaponConfigsOnly();

        var player = TestRangePlayerSetup.ReplaceScenePlayer();
        if (player == null)
            throw new System.InvalidOperationException("BuildTestRange: failed to spawn PlayerCharacter.");

        TestRangePlayerSetup.WirePlayer(player);
        PrefabUtility.SaveAsPrefabAssetAndConnect(player, "Assets/Prefabs/Player.prefab", InteractionMode.AutomatedAction);
        Debug.Log($"PlayerCharacter wired in scene at {player.transform.position}");
    }
    
    private static WeaponController CreatePrimitiveGun(Transform parent)
    {
        var gun = new GameObject("TestPistol");
        gun.transform.SetParent(parent);
        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.identity;
        
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = "Barrel";
        barrel.transform.SetParent(gun.transform);
        barrel.transform.localPosition = new Vector3(0, 0, 0.15f);
        barrel.transform.localScale = new Vector3(0.05f, 0.05f, 0.3f);
        Object.DestroyImmediate(barrel.GetComponent<Collider>());
        
        var barrelRenderer = barrel.GetComponent<Renderer>();
        barrelRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
        barrelRenderer.sharedMaterial.color = new Color(0.1f, 0.1f, 0.1f);
        
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "GunBody";
        body.transform.SetParent(gun.transform);
        body.transform.localPosition = new Vector3(0, 0, -0.02f);
        body.transform.localScale = new Vector3(0.06f, 0.12f, 0.15f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        
        var bodyRenderer = body.GetComponent<Renderer>();
        bodyRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
        bodyRenderer.sharedMaterial.color = new Color(0.2f, 0.2f, 0.2f);
        
        var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.name = "Grip";
        grip.transform.SetParent(gun.transform);
        grip.transform.localPosition = new Vector3(0, -0.08f, -0.05f);
        grip.transform.localRotation = Quaternion.Euler(15, 0, 0);
        grip.transform.localScale = new Vector3(0.04f, 0.1f, 0.06f);
        Object.DestroyImmediate(grip.GetComponent<Collider>());
        
        var gripRenderer = grip.GetComponent<Renderer>();
        gripRenderer.sharedMaterial = new Material(Shader.Find("Standard"));
        gripRenderer.sharedMaterial.color = new Color(0.15f, 0.1f, 0.08f);
        
        var muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.SetParent(gun.transform);
        muzzle.transform.localPosition = new Vector3(0, 0, 0.3f);
        
        var weapon = gun.AddComponent<WeaponController>();
        var weaponSO = new SerializedObject(weapon);
        var weaponConfigProp = weaponSO.FindProperty("config");
        if (weaponConfigProp != null)
        {
            var weaponConfig = AssetDatabase.LoadAssetAtPath<WeaponConfig>("Assets/Configs/TestPistolConfig.asset");
            weaponConfigProp.objectReferenceValue = weaponConfig;
        }
        var muzzleProp = weaponSO.FindProperty("muzzlePoint");
        if (muzzleProp != null)
        {
            muzzleProp.objectReferenceValue = muzzle.transform;
        }
        weaponSO.ApplyModifiedPropertiesWithoutUndo();
        return weapon;
    }
    
    private static void AddSceneToBuildSettings()
    {
        string scenePath = "Assets/Scenes/TestRange.unity";
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        
        bool found = false;
        foreach (var scene in scenes)
        {
            if (scene.path == scenePath)
            {
                found = true;
                break;
            }
        }
        
        if (!found)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
