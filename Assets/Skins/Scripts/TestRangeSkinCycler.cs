using UnityEngine;

/// <summary>
/// TestRange helper: cycle camo skins on the active weapon with [ and ].
/// </summary>
public class TestRangeSkinCycler : MonoBehaviour
{
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private WeaponSkinDefinition[] skins;
    [SerializeField] private int startingSkinIndex;

    private int currentSkinIndex;

    private void Awake()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
    }

    private void Start()
    {
        currentSkinIndex = Mathf.Clamp(startingSkinIndex, 0, Mathf.Max(0, skins.Length - 1));
        if (weaponSwitcher != null)
            weaponSwitcher.OnWeaponChanged += OnWeaponChanged;
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
            ApplySkinToCurrentWeapon();
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            currentSkinIndex = (currentSkinIndex + 1) % skins.Length;
            ApplySkinToCurrentWeapon();
        }
    }

    private void OnWeaponChanged(WeaponController weapon)
    {
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