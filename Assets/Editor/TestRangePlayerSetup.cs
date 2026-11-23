using CodMovementShooter.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Swaps TestRange to PlayerCharacter, class loadouts, HUD, and supporting services.
/// </summary>
public static class TestRangePlayerSetup
{
    private const string ScenePath = "Assets/Scenes/TestRange.unity";
    private const string PlayerCharacterPath = "Assets/Characters/Player/PlayerCharacter.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string SpawnName = "PlayerSpawn";
    private static readonly string[] ClassAssetNames =
    {
        "Class_Assault",
        "Class_SMG",
        "Class_Sniper",
        "Class_Hybrid",
    };

    [MenuItem("Tools/Sync Test Range Player (Character + Classes)")]
    public static void SyncFromMenu()
    {
        SyncSceneAndPrefab(openScene: true);
    }

    /// <summary>CLI: -executeMethod TestRangePlayerSetup.SyncBatch</summary>
    public static void SyncBatch()
    {
        ClassLoadoutMenu.CreateDefaultClassDefinitions();
        TestRangeWeaponLoadout.EnsureWeaponConfigsOnly();
        SyncSceneAndPrefab(openScene: true);
        EditorApplication.Exit(0);
    }

    public static void SyncSceneAndPrefab(bool openScene)
    {
        ClassLoadoutMenu.CreateDefaultClassDefinitions();
        TestRangeWeaponLoadout.EnsureWeaponConfigsOnly();

        if (openScene && !Application.isPlaying)
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var player = ReplaceScenePlayer();
        if (player != null)
        {
            WirePlayer(player);
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
        }

        SavePlayerPrefabFromCharacter();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TestRange player synced: PlayerCharacter + class loadouts + HUD.");
    }

    public static GameObject ReplaceScenePlayer()
    {
        Vector3 spawnPos = new Vector3(0f, 0.55f, -5f);
        Quaternion spawnRot = Quaternion.identity;
        var spawn = GameObject.Find(SpawnName);
        if (spawn != null)
        {
            spawnPos = spawn.transform.position;
            spawnRot = spawn.transform.rotation;
        }

        // CharacterController feet sit at transform.y; keep the TestRange default stance.
        if (spawnPos.y > 0.9f)
            spawnPos.y = 0.55f;

        var existing = GameObject.Find("Player");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterPath);
        if (characterPrefab == null)
        {
            Debug.LogError($"Missing {PlayerCharacterPath}");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
        instance.name = "Player";
        instance.transform.SetPositionAndRotation(spawnPos, spawnRot);
        return instance;
    }

    public static void WirePlayer(GameObject player)
    {
        var cameraHolder = player.transform.Find("CameraHolder");
        var mainCam = player.transform.Find("CameraHolder/MainCamera");
        var movement = player.GetComponent<MovementController>();
        if (movement != null && cameraHolder != null)
        {
            var mSo = new SerializedObject(movement);
            var camProp = mSo.FindProperty("cameraTransform");
            if (camProp != null)
                camProp.objectReferenceValue = cameraHolder;
            mSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var bridge = player.GetComponent<PlayerWeaponBridge>();
        if (bridge != null && mainCam != null)
        {
            var bSo = new SerializedObject(bridge);
            var camT = bSo.FindProperty("cameraTransform");
            if (camT != null)
                camT.objectReferenceValue = mainCam;
            bSo.ApplyModifiedPropertiesWithoutUndo();
        }

        ClassLoadoutApplier applier = EnsureClassApplier(player);
        EnsureClassSelector(player, applier);
        TestRangeWeaponLoadout.EnsureSkinCyclerOnPlayer(player);
        TestRangeWeaponLoadout.EnsureTestRangeHudAndHitDetection(player);
    }

    private static ClassLoadoutApplier EnsureClassApplier(GameObject player)
    {
        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher == null)
            switcher = EnsureWeaponSwitcher(player);

        var applier = player.GetComponent<ClassLoadoutApplier>();
        if (applier == null)
            applier = player.AddComponent<ClassLoadoutApplier>();

        var assault = AssetDatabase.LoadAssetAtPath<ClassDefinition>(
            "Assets/Classes/Definitions/Class_Assault.asset");

        var so = new SerializedObject(applier);
        so.FindProperty("classDefinition").objectReferenceValue = assault;
        so.FindProperty("weaponSwitcher").objectReferenceValue = switcher;
        so.FindProperty("movementController").objectReferenceValue = player.GetComponent<MovementController>();
        so.FindProperty("weaponBridge").objectReferenceValue = player.GetComponent<PlayerWeaponBridge>();
        so.FindProperty("weaponParent").objectReferenceValue = switcher != null ? switcher.transform : null;
        so.FindProperty("applyOnAwake").boolValue = true;
        so.FindProperty("destroyReplacedWeapons").boolValue = true;
        so.FindProperty("startingSlot").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        var swSo = new SerializedObject(switcher);
        swSo.FindProperty("useDirectSlotIndexKeys").boolValue = true;
        swSo.FindProperty("allowScrollSwitching").boolValue = true;
        swSo.ApplyModifiedPropertiesWithoutUndo();

        return applier;
    }

    private static void EnsureClassSelector(GameObject player, ClassLoadoutApplier applier)
    {
        var selector = player.GetComponent<TestRangeClassSelector>();
        if (selector == null)
            selector = player.AddComponent<TestRangeClassSelector>();

        var so = new SerializedObject(selector);
        so.FindProperty("loadoutApplier").objectReferenceValue = applier;
        so.FindProperty("weaponBridge").objectReferenceValue = player.GetComponent<PlayerWeaponBridge>();
        so.FindProperty("assaultClass").objectReferenceValue =
            LoadClass("Class_Assault");
        so.FindProperty("smgClass").objectReferenceValue = LoadClass("Class_SMG");
        so.FindProperty("sniperClass").objectReferenceValue = LoadClass("Class_Sniper");
        so.FindProperty("hybridClass").objectReferenceValue = LoadClass("Class_Hybrid");
        so.FindProperty("allowRuntimeSwap").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ClassDefinition LoadClass(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<ClassDefinition>(
            $"Assets/Classes/Definitions/{assetName}.asset");
    }

    private static WeaponSwitcher EnsureWeaponSwitcher(GameObject player)
    {
        var cam = player.transform.Find("CameraHolder/MainCamera");
        if (cam == null)
            cam = player.GetComponentInChildren<Camera>()?.transform;
        if (cam == null)
            return null;

        var holder = cam.Find("WeaponHolder");
        if (holder == null)
        {
            var holderGo = new GameObject("WeaponHolder");
            holderGo.transform.SetParent(cam, false);
            holderGo.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
            holder = holderGo.transform;
        }

        var switcher = holder.GetComponent<WeaponSwitcher>();
        if (switcher == null)
            switcher = holder.gameObject.AddComponent<WeaponSwitcher>();
        return switcher;
    }

    private static void SavePlayerPrefabFromCharacter()
    {
        var scenePlayer = GameObject.Find("Player");
        if (scenePlayer == null)
            return;

        var root = Object.Instantiate(scenePlayer);
        root.name = "Player";
        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}