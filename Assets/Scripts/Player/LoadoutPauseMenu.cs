using UnityEngine;

/// <summary>
/// Escape / hotkey menu: weapon selection (via a registered <see cref="ILoadoutMenuPage"/>) and map picker.
/// Built with IMGUI so it works without UI prefab wiring. Esc toggles the menu, <see cref="loadoutHotkey"/> (L)
/// toggles it straight onto the loadout tab. Closing always restores cursor lock and time scale.
/// </summary>
[DefaultExecutionOrder(-50)]
public class LoadoutPauseMenu : MonoBehaviour
{
    public enum MenuTab
    {
        Loadout = 0,
        Map = 1,
        Movement = 2
    }

    // Toolbar order on screen; the enum keeps its original numbering so saved references stay valid.
    private static readonly MenuTab[] ToolbarOrder = { MenuTab.Loadout, MenuTab.Movement, MenuTab.Map };

    [SerializeField] private ClassLoadoutApplier loadoutApplier;
    [SerializeField] private TestRangeClassSelector classSelector;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private FirstPersonCamera firstPersonCamera;
    [SerializeField] private bool pauseTimeScale = false;
    [SerializeField] private KeyCode loadoutHotkey = KeyCode.L;
    [SerializeField] private KeyCode movementHotkey = KeyCode.M;

    private PlayerWeaponBridge weaponBridge;
    private bool open;
    private MenuTab tab = MenuTab.Loadout;
    private float previousTimeScale = 1f;
    private int pendingTab = -1;
    private ILoadoutMenuPage shownPage;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private bool stylesReady;

    public bool IsOpen => open;
    public MenuTab CurrentTab => tab;
    public KeyCode LoadoutHotkey => loadoutHotkey;
    public KeyCode MovementHotkey => movementHotkey;

    private void Awake()
    {
        if (loadoutApplier == null)
            loadoutApplier = GetComponent<ClassLoadoutApplier>() ?? FindAnyObjectByType<ClassLoadoutApplier>();
        if (classSelector == null)
            classSelector = GetComponent<TestRangeClassSelector>() ?? FindAnyObjectByType<TestRangeClassSelector>();
        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true) ?? FindAnyObjectByType<WeaponSwitcher>();
        if (firstPersonCamera == null)
            firstPersonCamera = GetComponentInChildren<FirstPersonCamera>(true) ?? FindAnyObjectByType<FirstPersonCamera>();
        if (weaponBridge == null)
            weaponBridge = GetComponent<PlayerWeaponBridge>() ?? FindAnyObjectByType<PlayerWeaponBridge>();
    }

    private void Update()
    {
        if (pendingTab >= 0)
        {
            SetTab((MenuTab)pendingTab);
            pendingTab = -1;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            SetOpen(!open);
        else if (Input.GetKeyDown(loadoutHotkey))
            ToggleTab(MenuTab.Loadout);
        else if (Input.GetKeyDown(movementHotkey))
            ToggleTab(MenuTab.Movement);
    }

    /// <summary>
    /// Opens the menu on <paramref name="target"/>, or closes it when it is already open on that tab.
    /// </summary>
    public void ToggleTab(MenuTab target)
    {
        if (open && tab == target)
        {
            SetOpen(false);
            return;
        }

        OpenTab(target);
    }

    public void OpenTab(MenuTab target)
    {
        SetTab(target);
        SetOpen(true);
    }

    public void SetOpen(bool value)
    {
        bool wasOpen = open;
        open = value;

        if (firstPersonCamera != null)
            firstPersonCamera.SetCursorLock(!open);
        else
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        if (pauseTimeScale)
        {
            if (open && !wasOpen)
                previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = open ? 0f : previousTimeScale;
        }

        SetGameplayInputSuppressed(open);
        RefreshShownPage();
    }

    private void SetTab(MenuTab target)
    {
        tab = target;
        RefreshShownPage();
    }

    private void RefreshShownPage()
    {
        ILoadoutMenuPage wanted = null;
        if (open && tab == MenuTab.Loadout)
            wanted = LoadoutMenuPages.Primary;
        else if (open && tab == MenuTab.Movement)
            wanted = LoadoutMenuPages.MovementPage;
        if (ReferenceEquals(wanted, shownPage))
            return;

        if (IsAlive(shownPage))
            shownPage.OnPageHidden();
        shownPage = wanted;
        if (IsAlive(shownPage))
            shownPage.OnPageShown();
    }

    private static bool IsAlive(ILoadoutMenuPage page)
    {
        if (page == null)
            return false;
        return !(page is Object unityObject) || unityObject != null;
    }

    // Clicking menu buttons must not fire the weapon, and the scroll wheel must not cycle weapons.
    private void SetGameplayInputSuppressed(bool suppressed)
    {
        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);

        if (weaponBridge != null)
        {
            if (suppressed && weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            {
                weaponSwitcher.CurrentWeapon.StopFiring();
                weaponSwitcher.CurrentWeapon.StopAiming();
            }

            weaponBridge.enabled = !suppressed;
        }

        if (weaponSwitcher != null)
            weaponSwitcher.SetSwitchingEnabled(!suppressed);
    }

    private void OnGUI()
    {
        if (!open)
            return;

        EnsureStyles();

        ILoadoutMenuPage page = tab == MenuTab.Movement ? LoadoutMenuPages.MovementPage : LoadoutMenuPages.Primary;
        bool wide = page != null && tab != MenuTab.Map;

        float w = wide ? Mathf.Min(1040f, Screen.width - 30f) : Mathf.Min(460f, Screen.width - 20f);
        float h = Mathf.Min(wide ? 680f : 560f, Screen.height - 20f);
        Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(panel, GUIContent.none);

        float innerW = w - 40f;
        float innerH = h - 32f;
        GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, innerW, innerH));
        GUILayout.Label("LOADOUT / OPTIONS", titleStyle);
        GUILayout.Space(6f);

        string loadoutLabel = $"LOADOUT ({loadoutHotkey})";
        string movementLabel = $"MOVEMENT ({movementHotkey})";
        int shownIndex = System.Array.IndexOf(ToolbarOrder, tab);
        int picked = GUILayout.Toolbar(shownIndex, new[] { loadoutLabel, movementLabel, "MAP" }, buttonStyle, GUILayout.Height(30f));
        if (picked != shownIndex && picked >= 0 && picked < ToolbarOrder.Length)
            pendingTab = (int)ToolbarOrder[picked];
        GUILayout.Space(8f);

        float footerH = 74f;
        float contentH = Mathf.Max(120f, innerH - footerH - 60f);
        if (tab == MenuTab.Map)
        {
            DrawMapSection();
        }
        else if (page != null)
        {
            GUILayout.BeginVertical(GUILayout.Height(contentH));
            page.DrawPage(innerW, contentH);
            GUILayout.EndVertical();
        }
        else if (tab == MenuTab.Movement)
        {
            GUILayout.Label("Movement archetypes are unavailable in this scene (no player movement found).", labelStyle);
        }
        else
        {
            DrawLegacyLoadout();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label($"Movement: Shift+W then C = slide · hold Space = bhop · hold C = crouch · {loadoutHotkey} = loadout · {movementHotkey} = movement", labelStyle);
        if (GUILayout.Button("Resume (Esc)", buttonStyle, GUILayout.Height(34f)))
            SetOpen(false);
        GUILayout.EndArea();
    }

    private void DrawLegacyLoadout()
    {
        var applied = loadoutApplier != null ? loadoutApplier.AppliedClass : null;
        string className = applied != null ? applied.className : "(none)";
        GUILayout.Label($"Current class: {className}", labelStyle);
        GUILayout.Space(6f);

        GUILayout.Label("Select class", labelStyle);
        DrawClassButton("Assault (F1)", classSelector != null ? classSelector.AssaultClass : null);
        DrawClassButton("SMG (F2)", classSelector != null ? classSelector.SmgClass : null);
        DrawClassButton("Sniper (F3)", classSelector != null ? classSelector.SniperClass : null);
        DrawClassButton("Hybrid (F4)", classSelector != null ? classSelector.HybridClass : null);

        GUILayout.Space(12f);
        GUILayout.Label("Weapons", labelStyle);
        var slots = weaponSwitcher != null ? weaponSwitcher.Slots : null;
        if (slots != null && slots.Length > 0)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var weapon = weaponSwitcher.GetWeaponInSlot(i);
                string label = weapon != null
                    ? $"{i + 1}. {weapon.name}"
                    : $"{i + 1}. (empty)";
                if (GUILayout.Button(label, buttonStyle, GUILayout.Height(32f)))
                    weaponSwitcher.SwitchToSlot(i);
            }
        }
        else
        {
            GUILayout.Label("No weapon slots yet — choose a class above or press F1–F4.", labelStyle);
        }
    }

    private void DrawMapSection()
    {
        var selector = MapSelector.EnsureInstance();
        int current = MapSelector.CurrentMapIndex;
        string currentName = current >= 0 ? MapCatalog.Maps[current].DisplayName : MapSelector.CurrentSceneName;
        GUILayout.Label($"Map: {currentName}  (F5/F6/F7, PgUp/PgDn)", labelStyle);

        GUILayout.BeginHorizontal();
        for (int i = 0; i < MapCatalog.Count; i++)
        {
            var entry = MapCatalog.Maps[i];
            bool available = selector.IsMapAvailable(i);
            bool isCurrent = i == current;
            string label = isCurrent ? $"{entry.DisplayName} (current)" : available ? entry.DisplayName : $"{entry.DisplayName} (n/a)";

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && available && !isCurrent && !selector.IsLoading;
            if (GUILayout.Button(label, buttonStyle, GUILayout.Height(32f)))
            {
                if (selector.TryLoadMap(i))
                    SetOpen(false);
            }
            GUI.enabled = wasEnabled;
        }
        GUILayout.EndHorizontal();

        string status = selector.StatusMessage;
        if (!string.IsNullOrEmpty(status))
            GUILayout.Label(status, labelStyle);
    }

    private void DrawClassButton(string label, ClassDefinition definition)
    {
        if (!GUILayout.Button(label, buttonStyle, GUILayout.Height(34f)))
            return;

        if (classSelector != null)
        {
            if (classSelector.ApplyClass(definition))
                weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);
            return;
        }

        if (definition == null || loadoutApplier == null)
            return;

        if (loadoutApplier.ApplyLoadout(definition))
        {
            var bridge = GetComponent<PlayerWeaponBridge>() ?? FindAnyObjectByType<PlayerWeaponBridge>();
            bridge?.RefreshWeaponBindings();
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);
        }
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        stylesReady = true;
    }

    private void OnDisable()
    {
        if (open)
            SetOpen(false);
    }

    private void OnDestroy()
    {
        if (pauseTimeScale && Time.timeScale == 0f)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
    }
}
