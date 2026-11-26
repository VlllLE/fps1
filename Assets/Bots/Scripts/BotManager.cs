using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-level bot service. Created automatically after a scene with a player loads (TestRange, Shipment, Dome): it adds
/// <see cref="PlayerHealth"/> and the damage HUD to the player, generates the runtime NavMesh, spawns the opening bots and
/// handles the debug keys. Nothing here edits scenes or assets, so no map builder has to be re-run.
/// </summary>
[DisallowMultipleComponent]
public sealed class BotManager : MonoBehaviour
{
    public const int DefaultBotCount = 3;
    public const string DifficultyPrefKey = "FPS.Bots.Difficulty.v1";
    public const string DisableEnvironmentVariable = "FPS_BOTS";

    public static readonly KeyCode SpawnKey = KeyCode.F8;
    public static readonly KeyCode FreezeKey = KeyCode.F9;
    public static readonly KeyCode DifficultyKey = KeyCode.F10;
    public static readonly KeyCode DebugKey = KeyCode.F11;
    public static readonly KeyCode ClearKey = KeyCode.Backslash;
    public static readonly KeyCode ClearAltKey = KeyCode.F12;

    private static readonly Color[] Tints =
    {
        new Color(0.80f, 0.20f, 0.18f),
        new Color(0.85f, 0.45f, 0.15f),
        new Color(0.65f, 0.15f, 0.35f),
        new Color(0.70f, 0.60f, 0.15f)
    };

    private readonly List<BotController> bots = new List<BotController>();
    private readonly List<Vector3> scratch = new List<Vector3>(16);

    private HitDetection hitResolver;
    private BotSettings settings;
    private bool ownsSettings;
    private WeaponConfig fallbackWeapon;
    private List<WeaponConfig> weaponPool;
    private System.Random rng;
    private int spawnCounter;
    private bool frozen;
    private bool debugOverlay;
    private bool keysEnabled = true;
    private PlayerHealth playerHealth;
    private GUIStyle labelStyle;
    private GUIStyle centerStyle;
    private float noticeUntil;
    private string notice = string.Empty;

    public static BotManager Instance { get; private set; }
    public static HitDetection HitResolver => Instance != null ? Instance.hitResolver : null;

    /// <summary>When false the automatic bootstrap does nothing (tests, or a scene that manages bots itself).</summary>
    public static bool AutomaticBootstrap { get; set; } = true;

    public bool IsReady { get; private set; }
    public bool Frozen => frozen;
    public bool DebugOverlay { get => debugOverlay; set => debugOverlay = value; }
    public bool KeysEnabled { get => keysEnabled; set => keysEnabled = value; }
    public BotSettings Settings => settings;
    public BotDifficulty Difficulty => settings != null ? settings.difficulty : BotDifficulty.Normal;
    public int AutoSpawnCount { get; set; } = DefaultBotCount;
    public BotDifficulty StartDifficulty { get; set; } = BotDifficulty.Normal;
    public bool NavigateOnStart { get; set; } = true;
    public IReadOnlyList<BotController> Bots => bots;

    #region Bootstrap

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureForScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene();
    }

    private static void EnsureForScene()
    {
        if (Instance != null || !AutomaticBootstrap || IsDisabledExternally() || IsRunningUnderTestRunner())
            return;
        if (UnityEngine.Object.FindFirstObjectByType<MovementController>() == null)
            return;

        Create();
    }

    private static bool IsDisabledExternally()
    {
        string value = Environment.GetEnvironmentVariable(DisableEnvironmentVariable);
        return value == "0" || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True while the Unity Test Framework drives Play Mode. Existing PlayMode tests load the real scenes and must not get
    /// hostile bots (or a player that can die) added behind their back.
    /// </summary>
    public static bool IsRunningUnderTestRunner()
    {
        if (GameObject.Find("Code-based tests runner") != null)
            return true;

        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.name != null && active.name.StartsWith("InitTestScene", StringComparison.Ordinal))
            return true;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type runner = assembly.GetType("UnityEngine.TestTools.TestRunner.PlaymodeTestsController", false);
            if (runner != null && FindObjectsByType(runner, FindObjectsSortMode.None).Length > 0)
                return true;
        }

        return false;
    }

    /// <summary>Creates the manager for the current scene. Tests call this directly; the bootstrap calls it for real maps.</summary>
    public static BotManager Create()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("BotManager");
        return go.AddComponent<BotManager>();
    }

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this && Instance.gameObject.scene == gameObject.scene)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rng = new System.Random(Environment.TickCount);
        hitResolver = gameObject.AddComponent<HitDetection>();
        StartDifficulty = LoadSavedDifficulty();
    }

    private IEnumerator Start()
    {
        if (Instance != this)
            yield break;

        float waited = 0f;
        MovementController player = null;
        while (waited < 5f)
        {
            player = FindFirstObjectByType<MovementController>();
            if (player != null)
                break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = player.gameObject.AddComponent<PlayerHealth>();
            if (player.GetComponent<PlayerDamageIndicator>() == null)
                player.gameObject.AddComponent<PlayerDamageIndicator>();
            if (player.GetComponent<PlayerDeathPresentation>() == null)
                player.gameObject.AddComponent<PlayerDeathPresentation>();
        }

        yield return null;

        if (NavigateOnStart)
            yield return BotNavigation.BuildRoutine(CollectExcludedRoots());
        else if (!BotNavigation.IsReady)
            BotNavigation.ForceMode(BotNavMode.Fallback);

        if (settings == null)
            UseOwnedSettings(StartDifficulty);

        IsReady = true;
        for (int i = 0; i < AutoSpawnCount; i++)
            SpawnBot();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            BotNavigation.Reset();
        }

        if (ownsSettings && settings != null)
            Destroy(settings);
        if (fallbackWeapon != null)
            Destroy(fallbackWeapon);
    }

    private List<Transform> CollectExcludedRoots()
    {
        var roots = new List<Transform>();
        PlayerHealth local = PlayerHealth.Local;
        if (local != null)
            roots.Add(local.transform);
        else
        {
            MovementController player = FindFirstObjectByType<MovementController>();
            if (player != null)
                roots.Add(player.transform);
        }

        foreach (Target target in FindObjectsByType<Target>(FindObjectsSortMode.None))
            roots.Add(target.transform);
        foreach (BotController bot in BotController.All)
            roots.Add(bot.transform);
        return roots;
    }

    #region Public API

    /// <summary>Uses a designer-authored settings asset. It is never modified; difficulty changes switch back to presets.</summary>
    public void UseSettings(BotSettings asset)
    {
        if (asset == null)
            throw new ArgumentNullException(nameof(asset));

        ReleaseOwnedSettings();
        settings = asset;
        ownsSettings = false;
        RebuildBots();
    }

    public BotController SpawnBot()
    {
        return SpawnBot(null);
    }

    public BotController SpawnBot(Vector3? position)
    {
        if (settings == null)
            UseOwnedSettings(StartDifficulty);

        Vector3 anchor = playerHealth != null ? playerHealth.transform.position : Vector3.zero;
        Vector3 point = position ?? FindSpawnPosition(anchor, null, rng);
        point = BotNavigation.Snap(point, null);

        Vector3 toPlayer = anchor - point;
        toPlayer.y = 0f;
        Quaternion rotation = toPlayer.sqrMagnitude > 1f
            ? Quaternion.LookRotation(toPlayer.normalized)
            : Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
        rotation = Quaternion.Euler(0f, rotation.eulerAngles.y + (float)(rng.NextDouble() * 120.0 - 60.0), 0f);

        spawnCounter++;
        WeaponConfig weapon = PickWeapon(spawnCounter);
        Color tint = Tints[spawnCounter % Tints.Length];
        BotController bot = BotController.Create("Bot_" + spawnCounter, point, rotation, settings, weapon, tint, rng.Next());
        bot.Frozen = frozen;
        bots.Add(bot);
        return bot;
    }

    public void ClearBots()
    {
        for (int i = 0; i < bots.Count; i++)
        {
            if (bots[i] != null)
                Destroy(bots[i].gameObject);
        }

        bots.Clear();
    }

    public void SetFrozen(bool value)
    {
        frozen = value;
        for (int i = 0; i < bots.Count; i++)
        {
            if (bots[i] != null)
                bots[i].Frozen = value;
        }
    }

    public void SetDifficulty(BotDifficulty level)
    {
        StartDifficulty = level;
        if (settings != null && ownsSettings)
        {
            settings.ApplyPreset(level);
            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i] != null)
                    bots[i].RefreshFromSettings();
            }

            return;
        }

        ReleaseOwnedSettings();
        UseOwnedSettings(level);
        RebuildBots();
    }

    public void CycleDifficulty()
    {
        SetDifficulty(BotSettings.Next(Difficulty));
        PlayerPrefs.SetInt(DifficultyPrefKey, (int)Difficulty);
    }

    public int AliveCount()
    {
        int alive = 0;
        for (int i = 0; i < bots.Count; i++)
        {
            if (bots[i] != null && !bots[i].IsDead)
                alive++;
        }

        return alive;
    }

    public static bool TryGetShooterPosition(int sourceId, out Vector3 position)
    {
        if (BotController.TryGetBot(sourceId, out BotController bot))
        {
            position = bot.transform.position;
            return true;
        }

        position = default;
        return false;
    }

    /// <summary>A spawn point away from the player, preferably out of the player's sight; used for spawns and bot respawns.</summary>
    public static Vector3 FindRespawnPosition(BotController bot, Vector3 fallback)
    {
        if (Instance == null || Instance.playerHealth == null || !BotNavigation.IsReady)
            return fallback;
        return FindSpawnPosition(Instance.playerHealth.transform.position, bot != null ? bot.transform : null, Instance.rng);
    }

    public static Vector3 FindSpawnPosition(Vector3 awayFrom, Transform ignoreRoot, System.Random random)
    {
        var buffer = new List<Vector3>(16);
        Vector3 anchor = BotNavigation.Snap(awayFrom, ignoreRoot);
        Camera cam = Camera.main;
        Vector3 viewer = cam != null ? cam.transform.position : awayFrom + Vector3.up * 1.6f;

        Vector3 best = anchor;
        float bestScore = float.MinValue;
        for (int round = 0; round < 8; round++)
        {
            float minDistance = round < 3 ? 16f : round < 6 ? 9f : 4f;
            if (!BotNavigation.TryRandomReachablePoint(anchor, 55f, random, ignoreRoot, buffer, out Vector3 candidate))
                continue;

            float distance = Vector3.Distance(candidate, anchor);
            bool hidden = !BotPerception.HasLineOfSight(viewer, candidate + Vector3.up * 1.2f, ignoreRoot, PlayerRoot());
            float score = Mathf.Min(distance, 40f) + (hidden ? 25f : 0f);
            if (distance >= minDistance && score > bestScore)
            {
                bestScore = score;
                best = candidate;
                if (hidden && distance >= minDistance)
                    break;
            }
        }

        if (bestScore > float.MinValue)
            return best;

        float angle = (float)(random.NextDouble() * Math.PI * 2.0);
        return anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 12f;
    }

    #endregion

    #region Internals

    private static Transform PlayerRoot()
    {
        return PlayerHealth.Local != null ? PlayerHealth.Local.transform : null;
    }

    private void UseOwnedSettings(BotDifficulty level)
    {
        settings = BotSettings.CreatePreset(level);
        ownsSettings = true;
    }

    private void ReleaseOwnedSettings()
    {
        if (ownsSettings && settings != null)
            Destroy(settings);
        settings = null;
        ownsSettings = false;
    }

    private void RebuildBots()
    {
        int count = bots.Count;
        ClearBots();
        if (!IsReady)
            return;
        for (int i = 0; i < count; i++)
            SpawnBot();
    }

    private static BotDifficulty LoadSavedDifficulty()
    {
        int saved = PlayerPrefs.GetInt(DifficultyPrefKey, (int)BotDifficulty.Normal);
        return Enum.IsDefined(typeof(BotDifficulty), saved) ? (BotDifficulty)saved : BotDifficulty.Normal;
    }

    private WeaponConfig PickWeapon(int index)
    {
        if (weaponPool == null)
            weaponPool = BuildWeaponPool();

        if (weaponPool.Count == 0)
        {
            if (fallbackWeapon == null)
                fallbackWeapon = BotGun.CreateFallbackConfig();
            return fallbackWeapon;
        }

        return weaponPool[index % weaponPool.Count];
    }

    private static readonly Dictionary<WeaponConfig, GameObject> WeaponPrefabs = new Dictionary<WeaponConfig, GameObject>();

    /// <summary>Weapon prefab the loadout catalog lists for a config (used for the bots' third-person gun model), or null.</summary>
    public static GameObject GetWeaponPrefab(WeaponConfig config)
    {
        GameObject prefab;
        return config != null && WeaponPrefabs.TryGetValue(config, out prefab) ? prefab : null;
    }

    private static List<WeaponConfig> BuildWeaponPool()
    {
        var pool = new List<WeaponConfig>();
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        if (catalog == null || catalog.weapons == null)
            return pool;

        for (int i = 0; i < catalog.weapons.Length; i++)
        {
            LoadoutWeaponEntry entry = catalog.weapons[i];
            if (entry == null || !entry.allowPrimary)
                continue;

            WeaponConfig config = entry.config;
            if (config == null && entry.prefab != null)
            {
                WeaponController controller = entry.prefab.GetComponent<WeaponController>();
                config = controller != null ? controller.Config : null;
            }

            if (config == null || config.IsMelee || config.fireRate <= 0f)
                continue;
            if (config.weaponType != WeaponType.AssaultRifle && config.weaponType != WeaponType.SMG && config.weaponType != WeaponType.LMG)
                continue;
            if (entry.prefab != null)
                WeaponPrefabs[config] = entry.prefab;
            pool.Add(config);
        }

        return pool;
    }

    private void Update()
    {
        if (!keysEnabled || !IsReady)
            return;

        if (Input.GetKeyDown(SpawnKey))
        {
            SpawnBot();
            Notice("Bot spawned (" + bots.Count + ")");
        }

        if (Input.GetKeyDown(FreezeKey))
        {
            SetFrozen(!frozen);
            Notice(frozen ? "Bots frozen" : "Bots active");
        }

        if (Input.GetKeyDown(DifficultyKey))
        {
            CycleDifficulty();
            Notice("Bot difficulty: " + Difficulty);
        }

        if (Input.GetKeyDown(DebugKey))
        {
            debugOverlay = !debugOverlay;
            Notice("Bot debug " + (debugOverlay ? "on" : "off"));
        }

        if (IsClearPressed())
        {
            if (bots.Count > 0)
            {
                ClearBots();
                Notice("Bots removed");
            }
            else
            {
                for (int i = 0; i < DefaultBotCount; i++)
                    SpawnBot();
                Notice("Bots spawned (" + bots.Count + ")");
            }
        }

        if (debugOverlay)
            DrawSceneDebug();
    }

    /// <summary>
    /// Backslash is not KeyCode.Backslash on every layout (UK ISO keyboards report a different key), so the typed
    /// character is checked as well; F12 always works.
    /// </summary>
    private static bool IsClearPressed()
    {
        return Input.GetKeyDown(ClearKey) || Input.GetKeyDown(ClearAltKey) || Input.inputString.IndexOf('\\') >= 0;
    }

    private void Notice(string text)
    {
        notice = text;
        noticeUntil = Time.unscaledTime + 2f;
    }

    private void DrawSceneDebug()
    {
        for (int i = 0; i < bots.Count; i++)
        {
            BotController bot = bots[i];
            if (bot == null || bot.IsDead)
                continue;

            Color color = bot.Brain.State == BotState.Attack ? Color.red : bot.Brain.State == BotState.Patrol ? Color.green : Color.yellow;
            Debug.DrawRay(bot.EyePosition, bot.Aim.AimDirection * 6f, color);
            for (int w = 0; w < bot.PatrolWaypoints.Count; w++)
                Debug.DrawRay(bot.PatrolWaypoints[w], Vector3.up * 1.5f, Color.cyan);
        }
    }

    private void OnGUI()
    {
        if (!IsReady || Event.current.type != EventType.Repaint)
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperRight };
            labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            centerStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.UpperCenter };
        }

        string line = "Bots " + AliveCount() + "/" + bots.Count + "  " + Difficulty + (frozen ? "  FROZEN" : string.Empty)
            + (debugOverlay ? "  [" + BotNavigation.Mode + "]" : string.Empty);
        GUI.Label(new Rect(Screen.width - 460f, 44f, 450f, 20f), line, labelStyle);
        if (debugOverlay)
        {
            GUI.Label(new Rect(Screen.width - 460f, 62f, 450f, 20f),
                "F8 spawn  F9 freeze  F10 difficulty  F11 debug  \\ or F12 remove/add", labelStyle);
        }

        if (Time.unscaledTime < noticeUntil)
        {
            GUI.Label(new Rect(Screen.width - 460f, debugOverlay ? 80f : 62f, 450f, 20f), notice, labelStyle);
        }

        if (!debugOverlay)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        for (int i = 0; i < bots.Count; i++)
        {
            BotController bot = bots[i];
            if (bot == null)
                continue;

            Vector3 screen = cam.WorldToScreenPoint(bot.transform.position + Vector3.up * 2.2f);
            if (screen.z <= 0f)
                continue;

            string text = bot.Brain.State + " " + Mathf.CeilToInt(bot.Health) + "hp " + bot.Gun.Ammo + "/" + bot.Gun.MagazineSize;
            GUI.Label(new Rect(screen.x - 90f, Screen.height - screen.y - 10f, 180f, 20f), text, centerStyle);
        }
    }

    #endregion
}
