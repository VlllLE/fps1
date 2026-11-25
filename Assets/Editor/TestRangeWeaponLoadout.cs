using System.Collections.Generic;
using CodMovementShooter.UI;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates WeaponConfigs, wires gun prefabs, and equips the TestRange player loadout.
/// </summary>
public static class TestRangeWeaponLoadout
{
    private const string ConfigFolder = "Assets/Configs/Weapons";
    private const string ScenePath = "Assets/Scenes/TestRange.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string HudPrefabPath = "Assets/UI/Prefabs/FpsHudCanvas.prefab";

    private struct WeaponEntry
    {
        public string slotName;
        public string prefabPath;
        public string configPath;
        public string skinPath;
        public WeaponType weaponType;
        public string displayName;
        public float damage;
        public float fireRate;
        public FireMode fireMode;
        public int magazineSize;
        public int reserveAmmo;
        public float baseSpread;
        public float adsSpread;
        public float range;
        public float reloadTime;
        /// <summary>Config asset is authored by hand (pellets, per-shell reload, recoil); only link the prefab and name.</summary>
        public bool keepAuthoredStats;
    }

    private static readonly WeaponEntry[] Loadout =
    {
        new WeaponEntry
        {
            slotName = "M4A1",
            prefabPath = "Assets/Weapons/Prefabs/M4A1.prefab",
            configPath = ConfigFolder + "/M4A1Config.asset",
            skinPath = "Assets/Skins/Definitions/Skin_WoodlandCamo.asset",
            weaponType = WeaponType.AssaultRifle,
            displayName = "M4A1",
            damage = 28f, fireRate = 750f, fireMode = FireMode.Auto,
            magazineSize = 30, reserveAmmo = 120,
            baseSpread = 1.1f, adsSpread = 0.35f, range = 120f, reloadTime = 2.1f,
        },
        new WeaponEntry
        {
            slotName = "SCAR",
            prefabPath = "Assets/Weapons/Prefabs/SCAR.prefab",
            configPath = ConfigFolder + "/SCARConfig.asset",
            skinPath = "Assets/Skins/Definitions/Skin_DesertTiger.asset",
            weaponType = WeaponType.AssaultRifle,
            displayName = "SCAR-H",
            damage = 32f, fireRate = 625f, fireMode = FireMode.Auto,
            magazineSize = 20, reserveAmmo = 80,
            baseSpread = 1f, adsSpread = 0.3f, range = 115f, reloadTime = 2.3f,
        },
        new WeaponEntry
        {
            slotName = "UMP",
            prefabPath = "Assets/Weapons/Prefabs/UMP.prefab",
            configPath = ConfigFolder + "/UMPConfig.asset",
            skinPath = "Assets/Skins/Definitions/Skin_UrbanHex.asset",
            weaponType = WeaponType.SMG,
            displayName = "UMP45",
            damage = 22f, fireRate = 900f, fireMode = FireMode.Auto,
            magazineSize = 30, reserveAmmo = 150,
            baseSpread = 1.6f, adsSpread = 0.5f, range = 65f, reloadTime = 1.9f,
        },
        new WeaponEntry
        {
            slotName = "Bolt Sniper",
            prefabPath = "Assets/Weapons/Prefabs/BoltActionSniper.prefab",
            configPath = ConfigFolder + "/BoltActionSniperConfig.asset",
            skinPath = "Assets/Skins/Definitions/Skin_KryptekFracture.asset",
            weaponType = WeaponType.Sniper,
            displayName = "Bolt-Action Sniper",
            damage = 95f, fireRate = 55f, fireMode = FireMode.Semi,
            magazineSize = 5, reserveAmmo = 25,
            baseSpread = 0.08f, adsSpread = 0.02f, range = 280f, reloadTime = 3.2f,
        },
        new WeaponEntry
        {
            slotName = "DMR",
            prefabPath = "Assets/Weapons/Prefabs/DMRSniper.prefab",
            configPath = ConfigFolder + "/DMRSniperConfig.asset",
            skinPath = "Assets/Skins/Definitions/Animated/Skin_StormArc.asset",
            weaponType = WeaponType.Sniper,
            displayName = "DMR",
            damage = 55f, fireRate = 200f, fireMode = FireMode.Semi,
            magazineSize = 10, reserveAmmo = 50,
            baseSpread = 0.25f, adsSpread = 0.08f, range = 190f, reloadTime = 2.6f,
        },
        new WeaponEntry
        {
            slotName = "Shotgun",
            prefabPath = "Assets/Weapons/Prefabs/Shotgun.prefab",
            configPath = ConfigFolder + "/ShotgunConfig.asset",
            skinPath = "Assets/Skins/Definitions/Skin_Damascus.asset",
            weaponType = WeaponType.Shotgun,
            displayName = "Pump Shotgun",
            keepAuthoredStats = true,
        },
        new WeaponEntry
        {
            slotName = "LMG",
            prefabPath = "Assets/Weapons/Prefabs/LMG.prefab",
            configPath = ConfigFolder + "/LMGConfig.asset",
            skinPath = "Assets/Skins/Definitions/Skin_NightViper.asset",
            weaponType = WeaponType.LMG,
            displayName = "LMG",
            keepAuthoredStats = true,
        },
    };

    private static readonly string[] SkinCyclePaths =
    {
        "Assets/Skins/Definitions/Skin_CarbonFiber.asset",
        "Assets/Skins/Definitions/Skin_CopperPatina.asset",
        "Assets/Skins/Definitions/Skin_DesertTiger.asset",
        "Assets/Skins/Definitions/Skin_KryptekFracture.asset",
        "Assets/Skins/Definitions/Skin_NightViper.asset",
        "Assets/Skins/Definitions/Skin_UrbanHex.asset",
        "Assets/Skins/Definitions/Animated/Skin_ChromaticShift.asset",
        "Assets/Skins/Definitions/Animated/Skin_EmberAsh.asset",
        "Assets/Skins/Definitions/Animated/Skin_NeonRain.asset",
        "Assets/Skins/Definitions/Animated/Skin_StormArc.asset",
    };

    [MenuItem("Tools/Sync Test Range Weapon Loadout")]
    public static void SyncFromMenu()
    {
        SyncAndSave(openScene: true);
    }

    /// <summary>CLI: -batchmode -executeMethod TestRangeWeaponLoadout.SyncAndSaveBatch</summary>
    public static void SyncAndSaveBatch()
    {
        SyncAndSave(openScene: true);
        EditorApplication.Exit(0);
    }

    public static void SyncAndSave(bool openScene)
    {
        TestRangePlayerSetup.SyncSceneAndPrefab(openScene);
    }

    /// <summary>Weapon configs + prefab wiring only (no scene player swap).</summary>
    public static void EnsureWeaponConfigsOnly()
    {
        EnsureConfigFolder();
        EnsureWeaponConfigs();
        WireWeaponPrefabs();
    }

    /// <summary>Called from TestRangeBuilder after the player hierarchy is created.</summary>
    public static void WirePlayerGameObject(GameObject player)
    {
        EnsureConfigFolder();
        EnsureWeaponConfigs();
        WireWeaponPrefabs();

        var cam = player.transform.Find("CameraHolder/MainCamera");
        if (cam == null)
            cam = player.GetComponentInChildren<Camera>()?.transform;
        if (cam == null)
        {
            Debug.LogWarning("TestRangeWeaponLoadout: MainCamera not found on player.");
            return;
        }

        var holder = cam.Find("WeaponHolder");
        if (holder == null)
        {
            var holderGo = new GameObject("WeaponHolder");
            holderGo.transform.SetParent(cam, false);
            holderGo.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);
            holderGo.transform.localRotation = Quaternion.identity;
            holder = holderGo.transform;
        }

        ClearChildren(holder);

        var switcher = holder.GetComponent<WeaponSwitcher>();
        if (switcher == null)
            switcher = holder.gameObject.AddComponent<WeaponSwitcher>();

        var spawnedWeapons = new List<WeaponController>(Loadout.Length);
        foreach (var entry in Loadout)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing weapon prefab: {entry.prefabPath}");
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            instance.name = entry.slotName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            var weapon = instance.GetComponent<WeaponController>();
            if (weapon != null)
            {
                var config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(entry.configPath);
                ApplyConfigToRuntimeWeapon(weapon, config);
                spawnedWeapons.Add(weapon);
            }
        }

        var gameplayCamera = cam != null ? cam.GetComponent<Camera>() : null;
        var sceneHitDetection = Object.FindAnyObjectByType<HitDetection>();
        foreach (var weapon in spawnedWeapons)
        {
            if (gameplayCamera != null)
            {
                var wSo = new SerializedObject(weapon);
                var camProp = wSo.FindProperty("playerCamera");
                if (camProp != null)
                    camProp.objectReferenceValue = gameplayCamera;
                wSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (sceneHitDetection != null)
            {
                var wSo = new SerializedObject(weapon);
                var hitProp = wSo.FindProperty("hitDetection");
                if (hitProp != null)
                    hitProp.objectReferenceValue = sceneHitDetection;
                wSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        ConfigureSwitcher(switcher, spawnedWeapons);
        ConfigureBridge(player, switcher);
        ConfigureSkinCycler(player, switcher);
        EnsureTestRangeHudAndHitDetection(player);
    }

    private static void EnsureConfigFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Configs/Weapons"))
            AssetDatabase.CreateFolder("Assets/Configs", "Weapons");
    }

    private static void EnsureWeaponConfigs()
    {
        foreach (var entry in Loadout)
        {
            var config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(entry.configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<WeaponConfig>();
                AssetDatabase.CreateAsset(config, entry.configPath);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
            var so = new SerializedObject(config);
            SetField(so, "weaponName", entry.displayName);
            SetEnum(so, "weaponType", entry.weaponType);
            if (prefab != null)
            {
                var prefabProp = so.FindProperty("weaponPrefab");
                if (prefabProp != null)
                    prefabProp.objectReferenceValue = prefab;
            }
            if (entry.keepAuthoredStats)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                continue;
            }
            SetField(so, "damage", entry.damage);
            SetField(so, "fireRate", entry.fireRate);
            SetField(so, "magazineSize", entry.magazineSize);
            SetField(so, "maxReserveAmmo", entry.reserveAmmo);
            SetField(so, "startingReserveAmmo", entry.reserveAmmo);
            SetField(so, "baseSpread", entry.baseSpread);
            SetField(so, "adsSpread", entry.adsSpread);
            SetField(so, "range", entry.range);
            SetField(so, "reloadTime", entry.reloadTime);
            SetField(so, "tacticalReloadTime", entry.reloadTime * 0.85f);
            SetField(so, "reloadAmmoTime", Mathf.Max(0.5f, entry.reloadTime * 0.65f));
            SetField(so, "adsSpeed", 0.2f);

            var fireModes = so.FindProperty("availableFireModes");
            if (fireModes != null)
            {
                fireModes.arraySize = 1;
                fireModes.GetArrayElementAtIndex(0).enumValueIndex = (int)entry.fireMode;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }
    }

    private static void WireWeaponPrefabs()
    {
        foreach (var entry in Loadout)
        {
            var config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(entry.configPath);
            var skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>(entry.skinPath);
            if (!System.IO.File.Exists(entry.prefabPath))
                continue;

            var root = PrefabUtility.LoadPrefabContents(entry.prefabPath);
            var weapon = root.GetComponent<WeaponController>();
            if (weapon != null && config != null)
            {
                var wSo = new SerializedObject(weapon);
                var configProp = wSo.FindProperty("config");
                if (configProp != null)
                    configProp.objectReferenceValue = config;
                wSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var applier = root.GetComponent<WeaponSkinApplier>();
            if (applier != null && skin != null)
            {
                var aSo = new SerializedObject(applier);
                var skinProp = aSo.FindProperty("defaultSkin");
                if (skinProp != null)
                    skinProp.objectReferenceValue = skin;
                var awakeProp = aSo.FindProperty("applyDefaultOnAwake");
                if (awakeProp != null)
                    awakeProp.boolValue = true;
                aSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, entry.prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureSwitcher(WeaponSwitcher switcher, List<WeaponController> weapons)
    {
        var so = new SerializedObject(switcher);
        var slotsProp = so.FindProperty("weaponSlots");
        if (slotsProp == null)
            return;

        slotsProp.arraySize = weapons.Count;
        for (int i = 0; i < weapons.Count; i++)
        {
            var slot = slotsProp.GetArrayElementAtIndex(i);
            var nameProp = slot.FindPropertyRelative("slotName");
            var typeProp = slot.FindPropertyRelative("slotType");
            var weaponProp = slot.FindPropertyRelative("weapon");
            if (nameProp != null)
                nameProp.stringValue = Loadout[i].slotName;
            if (typeProp != null)
                typeProp.enumValueIndex = i == 0 ? (int)SlotType.Primary : (int)SlotType.Secondary;
            if (weaponProp != null)
                weaponProp.objectReferenceValue = weapons[i];
        }

        SetField(so, "startingSlot", 0);
        SetField(so, "useDirectSlotIndexKeys", true);
        SetField(so, "allowScrollSwitching", true);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBridge(GameObject player, WeaponSwitcher switcher)
    {
        var bridge = player.GetComponent<PlayerWeaponBridge>();
        if (bridge == null)
            bridge = player.AddComponent<PlayerWeaponBridge>();

        var so = new SerializedObject(bridge);
        var switcherProp = so.FindProperty("weaponSwitcher");
        if (switcherProp != null)
            switcherProp.objectReferenceValue = switcher;
        var weaponProp = so.FindProperty("weapon");
        if (weaponProp != null && switcher.Slots.Length > 0 && switcher.Slots[0].weapon != null)
            weaponProp.objectReferenceValue = switcher.Slots[0].weapon;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void EnsureSkinCyclerOnPlayer(GameObject player)
    {
        var switcher = player.GetComponentInChildren<WeaponSwitcher>(true);
        if (switcher != null)
            ConfigureSkinCycler(player, switcher);
    }

    private static void ConfigureSkinCycler(GameObject player, WeaponSwitcher switcher)
    {
        var cycler = player.GetComponent<TestRangeSkinCycler>();
        if (cycler == null)
            cycler = player.AddComponent<TestRangeSkinCycler>();

        var skins = new List<WeaponSkinDefinition>();
        foreach (var path in SkinCyclePaths)
        {
            var skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>(path);
            if (skin != null)
                skins.Add(skin);
        }

        var so = new SerializedObject(cycler);
        var switcherProp = so.FindProperty("weaponSwitcher");
        if (switcherProp != null)
            switcherProp.objectReferenceValue = switcher;
        var skinsProp = so.FindProperty("skins");
        if (skinsProp != null)
        {
            skinsProp.arraySize = skins.Count;
            for (int i = 0; i < skins.Count; i++)
                skinsProp.GetArrayElementAtIndex(i).objectReferenceValue = skins[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyConfigToRuntimeWeapon(WeaponController weapon, WeaponConfig config)
    {
        if (config == null)
            return;
        var so = new SerializedObject(weapon);
        var configProp = so.FindProperty("config");
        if (configProp != null)
            configProp.objectReferenceValue = config;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject FindPlayer()
    {
        var player = GameObject.Find("Player");
        if (player != null)
            return player;
        var movement = Object.FindAnyObjectByType<MovementController>();
        return movement != null ? movement.gameObject : null;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    private static void SetField(SerializedObject so, string name, object value)
    {
        var prop = so.FindProperty(name);
        if (prop == null)
            return;
        switch (value)
        {
            case string s: prop.stringValue = s; break;
            case float f: prop.floatValue = f; break;
            case int i: prop.intValue = i; break;
            case bool b: prop.boolValue = b; break;
        }
    }

    private static void SetEnum(SerializedObject so, string name, System.Enum value)
    {
        var prop = so.FindProperty(name);
        if (prop != null)
            prop.enumValueIndex = System.Convert.ToInt32(value);
    }

    public static void EnsureTestRangeHudAndHitDetection(GameObject player)
    {
        var scene = player.scene;
        HitDetection hitDetection = Object.FindAnyObjectByType<HitDetection>();
        if (hitDetection == null)
        {
            var go = new GameObject("HitDetection");
            hitDetection = go.AddComponent<HitDetection>();
            if (scene.IsValid())
                EditorSceneManager.MoveGameObjectToScene(go, scene);
        }

        FpsHudController hudController = Object.FindAnyObjectByType<FpsHudController>();
        if (hudController == null)
        {
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (hudPrefab != null)
            {
                var hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
                hudController = hudInstance.GetComponent<FpsHudController>();
                if (scene.IsValid())
                    EditorSceneManager.MoveGameObjectToScene(hudInstance, scene);
            }
        }

        if (hudController == null)
            return;

        WeaponHudBridge bridge = hudController.GetComponent<WeaponHudBridge>();
        if (bridge == null)
            return;

        var movement = player.GetComponent<MovementController>();
        var bridgeSo = new SerializedObject(bridge);
        var weaponSource = bridgeSo.FindProperty("weaponSource");
        if (weaponSource != null && movement != null)
            weaponSource.objectReferenceValue = movement;
        var movementSource = bridgeSo.FindProperty("movementSource");
        if (movementSource != null && movement != null)
            movementSource.objectReferenceValue = movement;
        var hitSrc = bridgeSo.FindProperty("hitDetectionSource");
        if (hitSrc != null && hitDetection != null)
            hitSrc.objectReferenceValue = hitDetection;
        bridgeSo.ApplyModifiedPropertiesWithoutUndo();
    }
}