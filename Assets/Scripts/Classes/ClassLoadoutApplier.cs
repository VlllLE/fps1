using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies a <see cref="ClassDefinition"/> to a <see cref="WeaponSwitcher"/> on spawn or on demand.
/// Attach to the player (or any object that owns the switcher). Does not require scene YAML edits —
/// assign a class in the inspector, or use Tools &gt; Classes menu items in the editor.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class ClassLoadoutApplier : MonoBehaviour
{
    [Header("Class")]
    [SerializeField] private ClassDefinition classDefinition;

    [Header("References")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private MovementController movementController;
    [SerializeField] private PlayerWeaponBridge weaponBridge;
    [Tooltip("Parent for spawned weapons. Defaults to the WeaponSwitcher transform.")]
    [SerializeField] private Transform weaponParent;

    [Header("Behaviour")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool destroyReplacedWeapons = true;
    [SerializeField] private int startingSlot = 0;

    private ClassDefinition appliedClass;
    private readonly List<GameObject> spawnedWeapons = new List<GameObject>(4);

    public ClassDefinition ClassDefinition
    {
        get => classDefinition;
        set => classDefinition = value;
    }

    public ClassDefinition AppliedClass => appliedClass;
    public WeaponSwitcher WeaponSwitcher => weaponSwitcher;

    public event Action<ClassDefinition> OnLoadoutApplied;

    private void Awake()
    {
        ResolveReferences();
        if (applyOnAwake && classDefinition != null)
            ApplyLoadout(classDefinition);
    }

    /// <summary>
    /// Applies the inspector-assigned class definition.
    /// </summary>
    public bool ApplyLoadout()
    {
        return ApplyLoadout(classDefinition);
    }

    /// <summary>
    /// Spawns primary/secondary/(optional melee) from <paramref name="definition"/> into the WeaponSwitcher.
    /// </summary>
    public bool ApplyLoadout(ClassDefinition definition)
    {
        ResolveReferences();

        if (definition == null)
        {
            Debug.LogWarning($"{nameof(ClassLoadoutApplier)} on {name}: no ClassDefinition assigned.", this);
            return false;
        }

        if (weaponSwitcher == null)
        {
            Debug.LogError($"{nameof(ClassLoadoutApplier)} on {name}: no WeaponSwitcher found.", this);
            return false;
        }

        Transform parent = weaponParent != null ? weaponParent : weaponSwitcher.transform;
        ClearExistingLoadout(parent);

        var slots = new List<WeaponSlot>(3);

        WeaponController primary = SpawnSlotWeapon(
            definition.GetPrimaryPrefab(),
            definition.primaryWeaponConfig,
            parent,
            definition.className + "_Primary");
        if (primary != null)
        {
            slots.Add(new WeaponSlot
            {
                slotName = "Primary",
                slotType = SlotType.Primary,
                weapon = primary
            });
        }

        WeaponController secondary = SpawnSlotWeapon(
            definition.GetSecondaryPrefab(),
            definition.secondaryWeaponConfig,
            parent,
            definition.className + "_Secondary");
        if (secondary != null)
        {
            slots.Add(new WeaponSlot
            {
                slotName = "Secondary",
                slotType = SlotType.Secondary,
                weapon = secondary
            });
        }

        WeaponController melee = SpawnSlotWeapon(
            definition.GetMeleePrefab(),
            definition.meleeWeaponConfig,
            parent,
            definition.className + "_Melee");
        if (melee != null)
        {
            slots.Add(new WeaponSlot
            {
                slotName = "Melee",
                slotType = SlotType.Melee,
                weapon = melee
            });
        }

        if (slots.Count == 0)
        {
            Debug.LogError(
                $"{nameof(ClassLoadoutApplier)}: class '{definition.className}' has no resolvable weapon prefabs.",
                definition);
            return false;
        }

        int start = Mathf.Clamp(startingSlot, 0, slots.Count - 1);
        weaponSwitcher.ConfigureSlots(slots.ToArray(), start);

        if (movementController != null)
            movementController.ClassSpeedMultiplier = definition.moveSpeedMultiplier;

        if (weaponBridge != null)
            weaponBridge.BindSwitcher(weaponSwitcher);

        appliedClass = definition;
        classDefinition = definition;
        OnLoadoutApplied?.Invoke(definition);
        return true;
    }

    private void ResolveReferences()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);
        if (movementController == null)
            movementController = GetComponent<MovementController>();
        if (weaponBridge == null)
            weaponBridge = GetComponent<PlayerWeaponBridge>();
        if (weaponParent == null && weaponSwitcher != null)
            weaponParent = weaponSwitcher.transform;
    }

    private WeaponController SpawnSlotWeapon(GameObject prefab, WeaponConfig config, Transform parent, string objectName)
    {
        if (prefab == null)
            return null;

        GameObject instance = Instantiate(prefab, parent);
        instance.name = objectName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        WeaponController weapon = instance.GetComponent<WeaponController>();
        if (weapon == null)
            weapon = instance.GetComponentInChildren<WeaponController>(true);

        if (weapon == null)
        {
            Debug.LogError($"{nameof(ClassLoadoutApplier)}: prefab '{prefab.name}' has no WeaponController.", prefab);
            DestroyWeaponObject(instance);
            return null;
        }

        if (config != null)
            weapon.Initialize(config);

        spawnedWeapons.Add(instance);
        return weapon;
    }

    private void ClearExistingLoadout(Transform parent)
    {
        if (weaponSwitcher != null)
        {
            WeaponSlot[] existing = weaponSwitcher.Slots;
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i].weapon == null)
                        continue;
                    DestroyWeaponObject(existing[i].weapon.gameObject);
                }
            }

            weaponSwitcher.ConfigureSlots(Array.Empty<WeaponSlot>(), 0);
        }

        for (int i = spawnedWeapons.Count - 1; i >= 0; i--)
        {
            if (spawnedWeapons[i] != null)
                DestroyWeaponObject(spawnedWeapons[i]);
        }
        spawnedWeapons.Clear();

        if (parent == null || !destroyReplacedWeapons)
            return;

        // Clear leftover children under the weapon holder (e.g. scene-wired guns).
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.GetComponent<WeaponController>() != null || child.GetComponentInChildren<WeaponController>(true) != null)
                DestroyWeaponObject(child.gameObject);
        }
    }

    private void DestroyWeaponObject(GameObject go)
    {
        if (go == null)
            return;
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }
}
