using UnityEngine;

/// <summary>
/// ScriptableObject describing a player class loadout (primary + secondary, optional melee).
/// Create via Assets &gt; Create &gt; Classes &gt; Class Definition
/// </summary>
[CreateAssetMenu(fileName = "NewClassDefinition", menuName = "Classes/Class Definition", order = 10)]
public class ClassDefinition : ScriptableObject
{
    [Header("Identity")]
    public string className = "Class";
    [TextArea(2, 4)]
    public string description;

    [Header("Primary")]
    [Tooltip("Weapon prefab with a WeaponController. Preferred over config.weaponPrefab.")]
    public GameObject primaryWeaponPrefab;
    [Tooltip("Optional config override applied after spawn. Also used as prefab fallback via weaponPrefab.")]
    public WeaponConfig primaryWeaponConfig;

    [Header("Secondary")]
    public GameObject secondaryWeaponPrefab;
    public WeaponConfig secondaryWeaponConfig;

    [Header("Melee (optional)")]
    [Tooltip("Reserved for a future melee slot. Leave empty for now.")]
    public GameObject meleeWeaponPrefab;
    public WeaponConfig meleeWeaponConfig;

    [Header("Movement")]
    [Tooltip("Multiplies walk/sprint target speed when applied via ClassLoadoutApplier. 1 = unchanged.")]
    [Range(0.5f, 1.5f)]
    public float moveSpeedMultiplier = 1f;

    /// <summary>
    /// Resolves the prefab for a slot: explicit prefab wins, else <see cref="WeaponConfig.weaponPrefab"/>.
    /// </summary>
    public static GameObject ResolvePrefab(GameObject prefab, WeaponConfig config)
    {
        if (prefab != null)
            return prefab;
        return config != null ? config.weaponPrefab : null;
    }

    public GameObject GetPrimaryPrefab() => ResolvePrefab(primaryWeaponPrefab, primaryWeaponConfig);
    public GameObject GetSecondaryPrefab() => ResolvePrefab(secondaryWeaponPrefab, secondaryWeaponConfig);
    public GameObject GetMeleePrefab() => ResolvePrefab(meleeWeaponPrefab, meleeWeaponConfig);

    public bool HasPrimary => GetPrimaryPrefab() != null;
    public bool HasSecondary => GetSecondaryPrefab() != null;
    public bool HasMelee => GetMeleePrefab() != null;
}
