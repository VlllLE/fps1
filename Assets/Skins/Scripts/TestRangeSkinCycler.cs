using UnityEngine;

/// <summary>
/// TestRange helper: cycle camo skins on the active weapon with [ and ].
/// Each weapon keeps its own prefab default skin until a bracket key is pressed
/// (or <c>applyOnStart</c> is enabled), so class loadouts show their per-weapon skins.
/// </summary>
public class TestRangeSkinCycler : MonoBehaviour
{
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private WeaponSkinDefinition[] skins;
    [SerializeField] private int startingSkinIndex;
    [Tooltip("When false (default), prefab default skins stay until [ or ] is pressed.")]
    [SerializeField] private bool applyOnStart;

    private int currentSkinIndex;
    private bool overrideActive;

    private void Awake()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
    }

    private void Start()
    {
        currentSkinIndex = Mathf.Clamp(startingSkinIndex, 0, Mathf.Max(0, (skins != null ? skins.Length : 0) - 1));
        if (weaponSwitcher != null)
            weaponSwitcher.OnWeaponChanged += OnWeaponChanged;

        overrideActive = applyOnStart;
        if (overrideActive)
            ApplySkinToCurrentWeapon();
    }

    private void OnDestroy()
    {
        if (weaponSwitcher != null)
            weaponSwitcher.OnWeaponChanged -= OnWeaponChanged;
    }

    private void Update()
    {
        if (skins == null || skins.Length == 0)
            return;

        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            currentSkinIndex--;
            if (currentSkinIndex < 0)
                currentSkinIndex = skins.Length - 1;
            overrideActive = true;
            ApplySkinToCurrentWeapon();
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            currentSkinIndex = (currentSkinIndex + 1) % skins.Length;
            overrideActive = true;
            ApplySkinToCurrentWeapon();
        }
    }

    private void OnWeaponChanged(WeaponController weapon)
    {
        if (overrideActive)
            ApplySkinToCurrentWeapon();
    }

    private void ApplySkinToCurrentWeapon()
    {
        if (skins == null || skins.Length == 0)
            return;

        WeaponController weapon = weaponSwitcher != null
            ? weaponSwitcher.CurrentWeapon
            : GetComponentInChildren<WeaponController>();

        if (weapon == null)
            return;

        var applier = weapon.GetComponent<WeaponSkinApplier>();
        if (applier == null)
            return;

        var skin = skins[currentSkinIndex];
        if (skin != null)
            applier.ApplySkin(skin);
    }
}