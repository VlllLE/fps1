using UnityEngine;

/// <summary>
/// Escape menu for TestRange: class loadout + weapon slot selection.
/// Built with IMGUI so it works without UI prefab wiring.
/// </summary>
[DefaultExecutionOrder(-50)]
public class LoadoutPauseMenu : MonoBehaviour
{
    [SerializeField] private ClassLoadoutApplier loadoutApplier;
    [SerializeField] private TestRangeClassSelector classSelector;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private FirstPersonCamera firstPersonCamera;
    [SerializeField] private bool pauseTimeScale = false;

    private bool open;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private bool stylesReady;

    public bool IsOpen => open;

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
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetOpen(!open);
    }

    public void SetOpen(bool value)
    {
        open = value;
        if (firstPersonCamera != null)
            firstPersonCamera.SetCursorLock(!open);
        else
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        if (pauseTimeScale)
            Time.timeScale = open ? 0f : 1f;
    }

    private void OnGUI()
    {
        if (!open)
            return;

        EnsureStyles();

        float w = 420f;
        float h = 460f;
        Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(panel, GUIContent.none);

        GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, w - 40f, h - 32f));
        GUILayout.Label("LOADOUT / OPTIONS", titleStyle);
        GUILayout.Space(8f);

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

        GUILayout.Space(16f);
        GUILayout.Label("Movement: Shift+W then C = slide · hold Space = bhop · hold C = crouch", labelStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Resume (Esc)", buttonStyle, GUILayout.Height(36f)))
            SetOpen(false);
        GUILayout.EndArea();
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

    private void OnDestroy()
    {
        if (pauseTimeScale)
            Time.timeScale = 1f;
    }
}
