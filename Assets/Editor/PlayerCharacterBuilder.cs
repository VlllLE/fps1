using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Builds / rebuilds Assets/Characters/Player/PlayerCharacter.prefab from Unity primitives.
/// Safe to re-run; does not modify MovementConfig defaults or class systems.
/// </summary>
public static class PlayerCharacterBuilder
{
    private const string PrefabPath = "Assets/Characters/Player/PlayerCharacter.prefab";
    private const string SuitMatPath = "Assets/Characters/Player/Materials/Mat_PlayerSuit.mat";
    private const string SkinMatPath = "Assets/Characters/Player/Materials/Mat_PlayerSkin.mat";
    private const string ArmsMatPath = "Assets/Characters/Player/Materials/Mat_PlayerArms.mat";
    private const string AccentMatPath = "Assets/Characters/Player/Materials/Mat_PlayerAccent.mat";
    private const string AnimatorPath = "Assets/Characters/Player/Animations/PlayerAnimator.controller";
    private const string HideLayerName = "PlayerLocalHide";

    [MenuItem("Tools/Build Player Character Prefab")]
    public static void BuildPlayerCharacterPrefab()
    {
        EnsureFolders();
        EnsureMaterials();
        EnsureHideLayer();

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(PrefabPath);

        var root = BuildHierarchy();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"PlayerCharacter prefab saved to {PrefabPath}");
    }

    private static void EnsureFolders()
    {
        string[] dirs =
        {
            "Assets/Characters",
            "Assets/Characters/Player",
            "Assets/Characters/Player/Materials",
            "Assets/Characters/Player/Animations"
        };

        foreach (string dir in dirs)
        {
            if (AssetDatabase.IsValidFolder(dir))
                continue;
            string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
            string folder = Path.GetFileName(dir);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static void EnsureMaterials()
    {
        EnsureMaterial(SuitMatPath, new Color(0.18f, 0.28f, 0.38f), 0.25f, 0.35f);
        EnsureMaterial(SkinMatPath, new Color(0.78f, 0.62f, 0.52f), 0f, 0.25f);
        EnsureMaterial(ArmsMatPath, new Color(0.22f, 0.32f, 0.42f), 0.15f, 0.3f);
        EnsureMaterial(AccentMatPath, new Color(0.55f, 0.45f, 0.22f), 0.4f, 0.4f);
    }

    private static void EnsureMaterial(string path, Color color, float metallic, float gloss)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", gloss);
        EditorUtility.SetDirty(mat);
    }

    private static void EnsureHideLayer()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        if (layers == null)
            return;

        // User layers start at index 8
        for (int i = 8; i < layers.arraySize; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (sp.stringValue == HideLayerName)
                return;
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = HideLayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }
    }

    private static GameObject BuildHierarchy()
    {
        var suit = AssetDatabase.LoadAssetAtPath<Material>(SuitMatPath);
        var skin = AssetDatabase.LoadAssetAtPath<Material>(SkinMatPath);
        var arms = AssetDatabase.LoadAssetAtPath<Material>(ArmsMatPath);
        var accent = AssetDatabase.LoadAssetAtPath<Material>(AccentMatPath);
        var animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);

        var player = new GameObject("PlayerCharacter");

        // --- Physics / movement (same defaults as Assets/Prefabs/Player.prefab) ---
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 1f, 0f);
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.08f;
        cc.minMoveDistance = 0f;

        var inputHandler = player.AddComponent<InputHandler>();
        var movement = player.AddComponent<MovementController>();
        var weaponBridge = player.AddComponent<PlayerWeaponBridge>();
        var bodyVisibility = player.AddComponent<LocalPlayerBodyVisibility>();

        var animator = player.AddComponent<Animator>();
        if (animatorController != null)
            animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // --- Camera (eye height matches existing TestRange player) ---
        var cameraHolder = new GameObject("CameraHolder");
        cameraHolder.transform.SetParent(player.transform, false);
        cameraHolder.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        var camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(cameraHolder.transform, false);
        var camera = camGo.AddComponent<Camera>();
        camera.nearClipPlane = 0.1f;
        camera.fieldOfView = 90f;
        camGo.AddComponent<AudioListener>();

        int hideLayer = LayerMask.NameToLayer(HideLayerName);
        if (hideLayer >= 0)
            camera.cullingMask &= ~(1 << hideLayer);

        var fpsCam = cameraHolder.AddComponent<FirstPersonCamera>();
        SetObjectRef(fpsCam, "playerBody", player.transform);
        SetObjectRef(fpsCam, "inputHandler", inputHandler);

        SetObjectRef(movement, "config",
            AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Configs/DefaultMovementConfig.asset"));
        SetObjectRef(movement, "input", inputHandler);
        SetObjectRef(movement, "cameraTransform", camGo.transform);

        // --- First-person arms + weapon under camera ---
        var weaponHolder = new GameObject("WeaponHolder");
        weaponHolder.transform.SetParent(camGo.transform, false);
        weaponHolder.transform.localPosition = new Vector3(0.3f, -0.2f, 0.5f);

        var fpArms = BuildFirstPersonArms(camGo.transform, arms, skin);
        var weapon = CreatePrimitiveGun(weaponHolder.transform);
        SetObjectRef(weaponBridge, "weapon", weapon);
        SetObjectRef(weaponBridge, "cameraTransform", camGo.transform);
        SetObjectRef(weaponBridge, "inputHandler", inputHandler);

        // --- Third-person / shadow body (replaces capsule PlayerBody) ---
        var bodyRoot = new GameObject("BodyRoot");
        bodyRoot.transform.SetParent(player.transform, false);
        bodyRoot.transform.localPosition = Vector3.zero;

        var hips = new GameObject("Hips");
        hips.transform.SetParent(bodyRoot.transform, false);
        hips.transform.localPosition = new Vector3(0f, 0.95f, 0f);

        // Torso (capsule, slightly shorter than full body capsule)
        var torso = CreateLimb("Torso", hips.transform, PrimitiveType.Capsule,
            new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.35f, 0.28f), suit);

        var head = CreateLimb("Head", hips.transform, PrimitiveType.Sphere,
            new Vector3(0f, 0.72f, 0f), new Vector3(0.28f, 0.28f, 0.28f), skin);

        // Neck accent ring
        CreateLimb("Neck", hips.transform, PrimitiveType.Cylinder,
            new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 0.04f, 0.12f), accent);

        // Arms
        var leftShoulder = CreateEmpty("LeftShoulder", hips.transform, new Vector3(-0.32f, 0.35f, 0f));
        CreateLimb("LeftUpperArm", leftShoulder, PrimitiveType.Capsule,
            new Vector3(-0.18f, -0.05f, 0f), new Vector3(0.12f, 0.22f, 0.12f), suit);
        CreateLimb("LeftForearm", leftShoulder, PrimitiveType.Capsule,
            new Vector3(-0.38f, -0.28f, 0.05f), new Vector3(0.1f, 0.2f, 0.1f), suit);

        var rightShoulder = CreateEmpty("RightShoulder", hips.transform, new Vector3(0.32f, 0.35f, 0f));
        CreateLimb("RightUpperArm", rightShoulder, PrimitiveType.Capsule,
            new Vector3(0.18f, -0.05f, 0f), new Vector3(0.12f, 0.22f, 0.12f), suit);
        CreateLimb("RightForearm", rightShoulder, PrimitiveType.Capsule,
            new Vector3(0.38f, -0.28f, 0.05f), new Vector3(0.1f, 0.2f, 0.1f), suit);

        // Legs
        var leftHip = CreateEmpty("LeftHip", hips.transform, new Vector3(-0.12f, -0.15f, 0f));
        CreateLimb("LeftThigh", leftHip, PrimitiveType.Capsule,
            new Vector3(0f, -0.28f, 0f), new Vector3(0.16f, 0.28f, 0.16f), suit);
        CreateLimb("LeftShin", leftHip, PrimitiveType.Capsule,
            new Vector3(0f, -0.72f, 0f), new Vector3(0.13f, 0.26f, 0.13f), suit);
        CreateLimb("LeftBoot", leftHip, PrimitiveType.Cube,
            new Vector3(0f, -1.05f, 0.04f), new Vector3(0.16f, 0.1f, 0.26f), accent);

        var rightHip = CreateEmpty("RightHip", hips.transform, new Vector3(0.12f, -0.15f, 0f));
        CreateLimb("RightThigh", rightHip, PrimitiveType.Capsule,
            new Vector3(0f, -0.28f, 0f), new Vector3(0.16f, 0.28f, 0.16f), suit);
        CreateLimb("RightShin", rightHip, PrimitiveType.Capsule,
            new Vector3(0f, -0.72f, 0f), new Vector3(0.13f, 0.26f, 0.13f), suit);
        CreateLimb("RightBoot", rightHip, PrimitiveType.Cube,
            new Vector3(0f, -1.05f, 0.04f), new Vector3(0.16f, 0.1f, 0.26f), accent);

        // Wire visibility: hide head (+ neck) from local FP camera
        var neck = hips.transform.Find("Neck");
        var hideTargets = neck != null
            ? new Object[] { head.transform, neck }
            : new Object[] { head.transform };
        SetObjectRef(bodyVisibility, "hideFromLocalCamera", hideTargets);
        SetObjectRef(bodyVisibility, "playerCamera", camera);

        // Spawn pose matches TestRange player
        player.transform.position = new Vector3(0f, 0.55f, -5f);

        MovementJuiceSetup.EnsureFeedbackOnPlayer(player);

        _ = fpArms;
        _ = torso;

        return player;
    }

    private static GameObject BuildFirstPersonArms(Transform cameraTransform, Material armsMat, Material skinMat)
    {
        var root = new GameObject("FirstPersonArms");
        root.transform.SetParent(cameraTransform, false);
        root.transform.localPosition = new Vector3(0f, -0.35f, 0.35f);
        root.transform.localRotation = Quaternion.identity;

        // Right arm (weapon side)
        CreateLimb("FP_RightUpper", root.transform, PrimitiveType.Capsule,
            new Vector3(0.22f, -0.05f, 0.15f), new Vector3(0.08f, 0.14f, 0.08f), armsMat);
        CreateLimb("FP_RightForearm", root.transform, PrimitiveType.Capsule,
            new Vector3(0.28f, -0.12f, 0.35f), new Vector3(0.07f, 0.12f, 0.07f), armsMat);
        CreateLimb("FP_RightHand", root.transform, PrimitiveType.Sphere,
            new Vector3(0.3f, -0.15f, 0.48f), new Vector3(0.08f, 0.08f, 0.08f), skinMat);

        // Left support arm
        CreateLimb("FP_LeftUpper", root.transform, PrimitiveType.Capsule,
            new Vector3(-0.05f, -0.08f, 0.2f), new Vector3(0.07f, 0.12f, 0.07f), armsMat);
        CreateLimb("FP_LeftForearm", root.transform, PrimitiveType.Capsule,
            new Vector3(0.05f, -0.1f, 0.38f), new Vector3(0.06f, 0.1f, 0.06f), armsMat);
        CreateLimb("FP_LeftHand", root.transform, PrimitiveType.Sphere,
            new Vector3(0.12f, -0.12f, 0.5f), new Vector3(0.07f, 0.07f, 0.07f), skinMat);

        return root;
    }

    private static Transform CreateEmpty(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    private static GameObject CreateLimb(
        string name,
        Transform parent,
        PrimitiveType type,
        Vector3 localPos,
        Vector3 localScale,
        Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        var col = go.GetComponent<Collider>();
        if (col != null)
            Object.DestroyImmediate(col);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return go;
    }

    private static WeaponController CreatePrimitiveGun(Transform parent)
    {
        var gun = new GameObject("TestPistol");
        gun.transform.SetParent(parent, false);

        var accent = AssetDatabase.LoadAssetAtPath<Material>(AccentMatPath);
        CreateLimb("Barrel", gun.transform, PrimitiveType.Cube,
            new Vector3(0f, 0f, 0.15f), new Vector3(0.05f, 0.05f, 0.3f), accent);
        CreateLimb("GunBody", gun.transform, PrimitiveType.Cube,
            new Vector3(0f, 0f, -0.02f), new Vector3(0.06f, 0.12f, 0.15f), accent);
        var grip = CreateLimb("Grip", gun.transform, PrimitiveType.Cube,
            new Vector3(0f, -0.08f, -0.05f), new Vector3(0.04f, 0.1f, 0.06f), accent);
        grip.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

        var muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.SetParent(gun.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0f, 0.3f);

        var weapon = gun.AddComponent<WeaponController>();
        SetObjectRef(weapon, "config",
            AssetDatabase.LoadAssetAtPath<WeaponConfig>("Assets/Configs/TestPistolConfig.asset"));
        SetObjectRef(weapon, "muzzlePoint", muzzle.transform);
        return weapon;
    }

    private static void SetObjectRef(Object target, string propertyName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null)
            return;
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectRef(Object target, string propertyName, Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || !prop.isArray)
            return;
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
