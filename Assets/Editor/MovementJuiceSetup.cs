using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Creates movement archetype configs and wires MovementFeedback onto the player.
/// Does not change DefaultMovementConfig physics defaults.
/// </summary>
public static class MovementJuiceSetup
{
    private const string ConfigsFolder = "Assets/Configs/Movement";
    private const string AudioFolder = "Assets/Audio/Movement";

    [MenuItem("Tools/Movement/Ensure Character Movement Configs")]
    public static void EnsureCharacterConfigs()
    {
        EnsureFolder("Assets/Configs");
        EnsureFolder(ConfigsFolder);

        // Only create missing assets — hand-authored YAML may already exist.
        CreateConfigIfMissing("MovementConfig_Scout", ApplyScout);
        CreateConfigIfMissing("MovementConfig_Heavy", ApplyHeavy);
        CreateConfigIfMissing("MovementConfig_Acrobat", ApplyAcrobat);
        CreateConfigIfMissing("MovementConfig_Marksman", ApplyMarksman);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Movement character configs ensured under " + ConfigsFolder);
    }

    [MenuItem("Tools/Movement/Add Feedback To Scene Player")]
    public static void AddFeedbackToScenePlayer()
    {
        var movement = Object.FindAnyObjectByType<MovementController>();
        if (movement == null)
        {
            Debug.LogError("No MovementController in the open scene.");
            return;
        }

        var feedback = movement.GetComponent<MovementFeedback>();
        if (feedback == null)
            feedback = movement.gameObject.AddComponent<MovementFeedback>();

        var so = new SerializedObject(feedback);
        Assign(so, "movement", movement);

        var cam = movement.GetComponentInChildren<Camera>();
        if (cam != null)
            Assign(so, "cameraEffectsTarget", cam.transform);

        var audio = movement.GetComponent<AudioSource>();
        if (audio == null)
            audio = movement.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        Assign(so, "audioSource", audio);

        AssignClip(so, "jumpClip", "sfx_jump");
        AssignClip(so, "landClip", "sfx_land");
        AssignClip(so, "slideStartClip", "sfx_slide_start");
        AssignClip(so, "slideLoopClip", "sfx_slide_loop");
        AssignClip(so, "wallBounceClip", "sfx_wall_bounce");
        AssignClip(so, "diveClip", "sfx_dive");
        AssignClip(so, "slideCancelClip", "sfx_slide_cancel");
        AssignClip(so, "bunnyHopClip", "sfx_bhop");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(movement.gameObject);
        Debug.Log("MovementFeedback wired on " + movement.gameObject.name);
    }

    [MenuItem("Tools/Movement/Apply Scout Config To Scene Player")]
    public static void ApplyScoutToPlayer() => ApplyConfigToPlayer("MovementConfig_Scout");

    [MenuItem("Tools/Movement/Apply Heavy Config To Scene Player")]
    public static void ApplyHeavyToPlayer() => ApplyConfigToPlayer("MovementConfig_Heavy");

    [MenuItem("Tools/Movement/Apply Acrobat Config To Scene Player")]
    public static void ApplyAcrobatToPlayer() => ApplyConfigToPlayer("MovementConfig_Acrobat");

    [MenuItem("Tools/Movement/Apply Marksman Config To Scene Player")]
    public static void ApplyMarksmanToPlayer() => ApplyConfigToPlayer("MovementConfig_Marksman");

    [MenuItem("Tools/Movement/Apply Default Config To Scene Player")]
    public static void ApplyDefaultToPlayer()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Configs/DefaultMovementConfig.asset");
        ApplyConfigAsset(cfg);
    }

    private static void ApplyConfigToPlayer(string assetName)
    {
        var cfg = AssetDatabase.LoadAssetAtPath<MovementConfig>($"{ConfigsFolder}/{assetName}.asset");
        ApplyConfigAsset(cfg);
    }

    private static void ApplyConfigAsset(MovementConfig cfg)
    {
        if (cfg == null)
        {
            Debug.LogError("MovementConfig asset not found.");
            return;
        }

        var movement = Object.FindAnyObjectByType<MovementController>();
        if (movement == null)
        {
            Debug.LogError("No MovementController in the open scene.");
            return;
        }

        movement.SetConfig(cfg);
        var so = new SerializedObject(movement);
        var prop = so.FindProperty("config");
        if (prop != null)
            prop.objectReferenceValue = cfg;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(movement);
        Debug.Log($"Applied {cfg.name} to {movement.gameObject.name}");
    }

    private static void CreateConfigIfMissing(string name, System.Action<MovementConfig> apply)
    {
        string path = $"{ConfigsFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<MovementConfig>(path);
        if (existing != null)
            return;

        var cfg = ScriptableObject.CreateInstance<MovementConfig>();
        apply(cfg);
        AssetDatabase.CreateAsset(cfg, path);
    }

    private static void ApplyScout(MovementConfig c)
    {
        c.walkSpeed = 7.2f;
        c.sprintMultiplier = 1.75f;
        c.groundAcceleration = 16f;
        c.groundFriction = 7f;
        c.jumpForce = 8.5f;
        c.airAcceleration = 8f;
        c.maxAirSpeed = 0.95f;
        c.airControl = 0.4f;
        c.airFriction = 2.2f;
        c.slideBoostSpeed = 11f;
        c.minSlideSpeed = 3.5f;
        c.slideDuration = 0.95f;
        c.slideFriction = 0.45f;
        c.slideCancelBoost = 1.35f;
        c.bhopSpeedRetention = 0.94f;
        c.bhopMaxSpeed = 13f;
        c.bhopBonus = 0.2f;
        c.diveForwardSpeed = 15.5f;
        c.wallBounceForce = 11f;
        c.wallBounceUpForce = 6.5f;
        c.enableWallRun = true;
        c.wallRunSpeed = 11f;
        c.wallLayers = ~0;
        c.showDebugGizmos = false;
    }

    private static void ApplyHeavy(MovementConfig c)
    {
        c.walkSpeed = 4.8f;
        c.sprintMultiplier = 1.35f;
        c.groundAcceleration = 10f;
        c.groundFriction = 10f;
        c.idleFrictionMultiplier = 3f;
        c.jumpForce = 7f;
        c.gravityMultiplier = 2.8f;
        c.airAcceleration = 4f;
        c.maxAirSpeed = 0.5f;
        c.airControl = 0.18f;
        c.airFriction = 4f;
        c.slideBoostSpeed = 7.5f;
        c.minSlideSpeed = 5f;
        c.slideDuration = 0.55f;
        c.slideFriction = 0.85f;
        c.slideCancelBoost = 1.15f;
        c.crouchSpeedMultiplier = 0.45f;
        c.bhopSpeedRetention = 0.8f;
        c.bhopMaxSpeed = 8.5f;
        c.bhopBonus = 0.08f;
        c.diveForwardSpeed = 11f;
        c.diveUpwardForce = 2.2f;
        c.minDiveSpeed = 6f;
        c.wallBounceForce = 7f;
        c.wallBounceUpForce = 4.5f;
        c.minWallBounceSpeed = 7f;
        c.enableWallRun = false;
        c.maxMantleHeight = 1.6f;
        c.wallLayers = ~0;
        c.showDebugGizmos = false;
    }

    private static void ApplyAcrobat(MovementConfig c)
    {
        c.walkSpeed = 6.4f;
        c.sprintMultiplier = 1.55f;
        c.groundAcceleration = 15f;
        c.jumpForce = 9f;
        c.gravityMultiplier = 2.3f;
        c.coyoteTime = 0.16f;
        c.airAcceleration = 9f;
        c.maxAirSpeed = 1.1f;
        c.airControl = 0.45f;
        c.slideBoostSpeed = 12f;
        c.minSlideSpeed = 3.2f;
        c.slideDuration = 1f;
        c.slideFriction = 0.4f;
        c.slideDownhillBoost = 11f;
        c.slideCancelBoost = 1.4f;
        c.slideCancelWindow = 0.35f;
        c.bhopWindow = 0.18f;
        c.bhopSpeedRetention = 0.96f;
        c.bhopMaxSpeed = 14f;
        c.bhopBonus = 0.25f;
        c.diveForwardSpeed = 16f;
        c.diveUpwardForce = 4f;
        c.diveRecoveryTime = 0.3f;
        c.wallBounceForce = 12f;
        c.wallBounceUpForce = 7.5f;
        c.wallBounceCooldown = 0.3f;
        c.enableWallRun = true;
        c.wallRunSpeed = 12f;
        c.maxWallRunTime = 2f;
        c.wallRunGravityReduction = 0.85f;
        c.mantleDuration = 0.25f;
        c.wallLayers = ~0;
        c.showDebugGizmos = false;
    }

    private static void ApplyMarksman(MovementConfig c)
    {
        c.walkSpeed = 5.4f;
        c.sprintMultiplier = 1.4f;
        c.groundAcceleration = 12f;
        c.groundFriction = 9f;
        c.jumpForce = 7.5f;
        c.airAcceleration = 5f;
        c.maxAirSpeed = 0.6f;
        c.airControl = 0.22f;
        c.airFriction = 3.5f;
        c.slideBoostSpeed = 8f;
        c.minSlideSpeed = 4.5f;
        c.slideDuration = 0.65f;
        c.slideFriction = 0.7f;
        c.slideCancelBoost = 1.2f;
        c.crouchSpeedMultiplier = 0.5f;
        c.bhopSpeedRetention = 0.85f;
        c.bhopMaxSpeed = 9.5f;
        c.bhopBonus = 0.1f;
        c.diveForwardSpeed = 12f;
        c.diveCooldown = 1.5f;
        c.wallBounceForce = 8f;
        c.wallBounceUpForce = 5f;
        c.minWallBounceSpeed = 6.5f;
        c.enableWallRun = false;
        c.wallLayers = ~0;
        c.showDebugGizmos = false;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string folder = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void Assign(SerializedObject so, string name, Object value)
    {
        var prop = so.FindProperty(name);
        if (prop != null)
            prop.objectReferenceValue = value;
    }

    private static void AssignClip(SerializedObject so, string field, string clipName)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{clipName}.wav");
        Assign(so, field, clip);
    }
}
