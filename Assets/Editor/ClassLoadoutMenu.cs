using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor helpers for class definitions and applying a loadout to the TestRange player
/// without hand-editing scene YAML.
/// </summary>
public static class ClassLoadoutMenu
{
    private const string ClassesFolder = "Assets/Classes";
    private const string DefinitionsFolder = "Assets/Classes/Definitions";
    private const string ScenePath = "Assets/Scenes/TestRange.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string KarambitPrefab = "Assets/Weapons/Prefabs/Karambit.prefab";
    private const string KarambitConfig = "Assets/Configs/Weapons/KarambitConfig.asset";

    private struct ClassSpec
    {
        public string assetName;
        public string className;
        public string description;
        public string primaryPrefab;
        public string primaryConfig;
        public string secondaryPrefab;
        public string secondaryConfig;
        public float moveSpeedMultiplier;
    }

    private static readonly ClassSpec[] DefaultClasses =
    {
        new ClassSpec
        {
            assetName = "Class_Assault",
            className = "Assault",
            description = "Balanced rifle class. M4A1 primary with Karambit knife secondary.",
            primaryPrefab = "Assets/Weapons/Prefabs/M4A1.prefab",
            primaryConfig = "Assets/Configs/Weapons/M4A1Config.asset",
            secondaryPrefab = KarambitPrefab,
            secondaryConfig = KarambitConfig,
            moveSpeedMultiplier = 1f,
        },
        new ClassSpec
        {
            assetName = "Class_SMG",
            className = "SMG",
            description = "Close-quarters class. UMP primary with Karambit knife secondary.",
            primaryPrefab = "Assets/Weapons/Prefabs/UMP.prefab",
            primaryConfig = "Assets/Configs/Weapons/UMPConfig.asset",
            secondaryPrefab = KarambitPrefab,
            secondaryConfig = KarambitConfig,
            moveSpeedMultiplier = 1.08f,
        },
        new ClassSpec
        {
            assetName = "Class_Sniper",
            className = "Sniper",
            description = "Long-range class. Bolt-action primary with Karambit knife secondary.",
            primaryPrefab = "Assets/Weapons/Prefabs/BoltActionSniper.prefab",
            primaryConfig = "Assets/Configs/Weapons/BoltActionSniperConfig.asset",
            secondaryPrefab = KarambitPrefab,
            secondaryConfig = KarambitConfig,
            moveSpeedMultiplier = 0.94f,
        },
        new ClassSpec
        {
            assetName = "Class_Hybrid",
            className = "Hybrid",
            description = "Marksman hybrid. DMR primary with Karambit knife secondary.",
            primaryPrefab = "Assets/Weapons/Prefabs/DMRSniper.prefab",
            primaryConfig = "Assets/Configs/Weapons/DMRSniperConfig.asset",
            secondaryPrefab = KarambitPrefab,
            secondaryConfig = KarambitConfig,
            moveSpeedMultiplier = 1f,
        },
    };

    [MenuItem("Tools/Classes/Create Default Class Definitions")]
    public static void CreateDefaultClassDefinitions()
    {
        EnsureFolders();
        int created = 0;
        foreach (var spec in DefaultClasses)
        {
            string path = $"{DefinitionsFolder}/{spec.assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ClassDefinition>(path);
            ClassDefinition def = existing != null ? existing : ScriptableObject.CreateInstance<ClassDefinition>();
            if (existing == null)
            {
                AssetDatabase.CreateAsset(def, path);
                created++;
            }

            ApplySpec(def, spec);
            EditorUtility.SetDirty(def);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Class definitions ready under {DefinitionsFolder} ({created} created).");
    }

    [MenuItem("Tools/Classes/Add ClassLoadoutApplier To Player (Scene)")]
    public static void AddApplierToScenePlayer()
    {
        EnsureTestRangeOpen();
        var player = FindPlayer();
        if (player == null)
        {
            Debug.LogError("No Player found in the open scene.");
            return;
        }

        EnsureApplier(player, loadDefaultAssault: true);
        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("ClassLoadoutApplier added/updated on scene Player (Assault by default).");
    }

    [MenuItem("Tools/Classes/Add ClassLoadoutApplier To Player Prefab")]
    public static void AddApplierToPlayerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            EnsureApplier(root, loadDefaultAssault: true);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("ClassLoadoutApplier added/updated on Player prefab (Assault by default).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Classes/Apply To Scene Player/Assault")]
    public static void ApplyAssault() => ApplyClassToScenePlayer("Class_Assault");

    [MenuItem("Tools/Classes/Apply To Scene Player/SMG")]
    public static void ApplySmg() => ApplyClassToScenePlayer("Class_SMG");

    [MenuItem("Tools/Classes/Apply To Scene Player/Sniper")]
    public static void ApplySniper() => ApplyClassToScenePlayer("Class_Sniper");

    [MenuItem("Tools/Classes/Apply To Scene Player/Hybrid")]
    public static void ApplyHybrid() => ApplyClassToScenePlayer("Class_Hybrid");

    private static void ApplyClassToScenePlayer(string assetName)
    {
        CreateDefaultClassDefinitions();
        EnsureTestRangeOpen();

        var player = FindPlayer();
        if (player == null)
        {
            Debug.LogError("No Player found in the open scene.");
            return;
        }

        var def = AssetDatabase.LoadAssetAtPath<ClassDefinition>($"{DefinitionsFolder}/{assetName}.asset");
        if (def == null)
        {
            Debug.LogError($"Missing class asset: {DefinitionsFolder}/{assetName}.asset");
            return;
        }

        var applier = EnsureApplier(player, loadDefaultAssault: false);
        var so = new SerializedObject(applier);
        so.FindProperty("classDefinition").objectReferenceValue = def;
        so.FindProperty("applyOnAwake").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Edit-mode preview: clear holder children and leave applier to spawn at play.
        // Also stamp the chosen class so entering Play Mode uses it.
        EditorUtility.SetDirty(applier);
        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log($"Scene Player will use class '{def.className}' on play (ClassLoadoutApplier). Enter Play Mode to spawn weapons.");
    }

    private static ClassLoadoutApplier EnsureApplier(GameObject player, bool loadDefaultAssault)
    {
        EnsureWeaponSwitcher(player);

        var applier = player.GetComponent<ClassLoadoutApplier>();
        if (applier == null)
            applier = player.AddComponent<ClassLoadoutApplier>();

        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        var movement = player.GetComponent<MovementController>();
        var bridge = player.GetComponent<PlayerWeaponBridge>();

        var so = new SerializedObject(applier);
        so.FindProperty("weaponSwitcher").objectReferenceValue = switcher;
        so.FindProperty("movementController").objectReferenceValue = movement;
        so.FindProperty("weaponBridge").objectReferenceValue = bridge;
        so.FindProperty("weaponParent").objectReferenceValue = switcher != null ? switcher.transform : null;
        so.FindProperty("applyOnAwake").boolValue = true;
        so.FindProperty("destroyReplacedWeapons").boolValue = true;
        so.FindProperty("startingSlot").intValue = 0;

        if (loadDefaultAssault)
        {
            CreateDefaultClassDefinitions();
            var assault = AssetDatabase.LoadAssetAtPath<ClassDefinition>($"{DefinitionsFolder}/Class_Assault.asset");
            so.FindProperty("classDefinition").objectReferenceValue = assault;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(applier);
        return applier;
    }

    private static void EnsureWeaponSwitcher(GameObject player)
    {
        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null)
            return;

        var cam = player.transform.Find("CameraHolder/MainCamera");
        if (cam == null)
            cam = player.GetComponentInChildren<Camera>()?.transform;
        if (cam == null)
        {
            Debug.LogWarning("Could not find MainCamera to host WeaponSwitcher.");
            return;
        }

        var holder = cam.Find("WeaponHolder");
        if (holder == null)
        {
            var holderGo = new GameObject("WeaponHolder");
            holderGo.transform.SetParent(cam, false);
            holderGo.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
            holder = holderGo.transform;
        }

        holder.gameObject.AddComponent<WeaponSwitcher>();
    }

    private static void ApplySpec(ClassDefinition def, ClassSpec spec)
    {
        var so = new SerializedObject(def);
        so.FindProperty("className").stringValue = spec.className;
        so.FindProperty("description").stringValue = spec.description;
        so.FindProperty("primaryWeaponPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(spec.primaryPrefab);
        so.FindProperty("primaryWeaponConfig").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<WeaponConfig>(spec.primaryConfig);
        so.FindProperty("secondaryWeaponPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(spec.secondaryPrefab);
        so.FindProperty("secondaryWeaponConfig").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<WeaponConfig>(spec.secondaryConfig);
        so.FindProperty("meleeWeaponPrefab").objectReferenceValue = null;
        so.FindProperty("meleeWeaponConfig").objectReferenceValue = null;
        so.FindProperty("moveSpeedMultiplier").floatValue = spec.moveSpeedMultiplier;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(ClassesFolder))
            AssetDatabase.CreateFolder("Assets", "Classes");
        if (!AssetDatabase.IsValidFolder(DefinitionsFolder))
            AssetDatabase.CreateFolder(ClassesFolder, "Definitions");
    }

    private static void EnsureTestRangeOpen()
    {
        if (Application.isPlaying)
            return;
        if (SceneManager.GetActiveScene().path != ScenePath && File.Exists(ScenePath))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static GameObject FindPlayer()
    {
        var player = GameObject.Find("Player");
        if (player != null)
            return player;
        var movement = Object.FindAnyObjectByType<MovementController>();
        return movement != null ? movement.gameObject : null;
    }
}
