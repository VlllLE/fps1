using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies and persists the weapon selection (class, primary, secondary, skin per weapon) on the player.
/// Weapons are spawned through <see cref="ClassLoadoutApplier"/> / <see cref="WeaponSwitcher"/> and skins through
/// <see cref="WeaponSkinApplier"/>; this component only decides what to spawn and stores the choice.
/// Added to the player automatically by <see cref="LoadoutBootstrap"/>, so no scene or prefab edits are required.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-40)]
public class WeaponLoadoutService : MonoBehaviour
{
    [SerializeField] private WeaponLoadoutCatalog catalog;
    [SerializeField] private ClassLoadoutApplier loadoutApplier;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private PlayerWeaponBridge weaponBridge;
    [Tooltip("Re-apply the persisted selection when the scene starts.")]
    [SerializeField] private bool applySavedOnStart = true;

    private ILoadoutStore store;
    private LoadoutSelection selection = new LoadoutSelection();
    private ClassDefinition composedDefinition;
    private bool ready;
    private bool hasSavedSelection;
    private bool applying;
    private bool subscribed;
    private bool startedApplying;

    /// <summary>Raised after the selection changed and was applied to the player.</summary>
    public event Action Changed;

    public WeaponLoadoutCatalog Catalog
    {
        get
        {
            EnsureReady();
            return catalog;
        }
    }

    public LoadoutSelection Selection
    {
        get
        {
            EnsureReady();
            return selection;
        }
    }

    public ILoadoutStore Store
    {
        get
        {
            EnsureReady();
            return store;
        }
    }

    public bool HasSavedSelection => hasSavedSelection;
    public ClassLoadoutApplier LoadoutApplier => loadoutApplier;
    public WeaponSwitcher WeaponSwitcher => weaponSwitcher;

    /// <summary>
    /// Supplies the catalog and persistence backend before the service initialises. Intended for tests and tools.
    /// </summary>
    public void Configure(WeaponLoadoutCatalog newCatalog, ILoadoutStore newStore)
    {
        catalog = newCatalog;
        store = newStore;
        ready = false;
        hasSavedSelection = false;
        selection = new LoadoutSelection();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        EnsureReady();
        if (applySavedOnStart && hasSavedSelection && !startedApplying)
        {
            startedApplying = true;
            ApplySelection();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (composedDefinition != null)
        {
            if (Application.isPlaying)
                Destroy(composedDefinition);
            else
                DestroyImmediate(composedDefinition);
            composedDefinition = null;
        }
    }

    // ---------------------------------------------------------------- Selection API

    public bool SelectClass(ClassDefinition definition)
    {
        EnsureReady();
        if (definition == null)
            return false;

        selection.ClassId = WeaponLoadoutCatalog.ClassIdOf(definition);
        selection.PrimaryId = IdOfPrefab(definition.GetPrimaryPrefab());
        selection.SecondaryId = IdOfPrefab(definition.GetSecondaryPrefab());
        return ApplySelection();
    }

    public bool SelectPrimary(LoadoutWeaponEntry entry)
    {
        EnsureReady();
        if (entry == null || entry.prefab == null || !entry.allowPrimary)
            return false;

        string previousPrimary = selection.PrimaryId;
        if (selection.SecondaryId == entry.Id)
            selection.SecondaryId = previousPrimary;
        selection.PrimaryId = entry.Id;
        return ApplySelection(0);
    }

    public bool SelectSecondary(LoadoutWeaponEntry entry)
    {
        EnsureReady();
        if (entry == null || entry.prefab == null || !entry.allowSecondary)
            return false;

        string previousSecondary = selection.SecondaryId;
        if (selection.PrimaryId == entry.Id)
            selection.PrimaryId = previousSecondary;
        selection.SecondaryId = entry.Id;
        return ApplySelection(1);
    }

    /// <summary>
    /// Picks <paramref name="skin"/> for <paramref name="weaponId"/> (null = the weapon's default skin) and applies it
    /// to the equipped copy of that weapon immediately.
    /// </summary>
    public bool SelectSkin(string weaponId, WeaponSkinDefinition skin)
    {
        EnsureReady();
        if (catalog.FindWeapon(weaponId) == null)
            return false;

        selection.SetSkinId(weaponId, skin != null ? WeaponLoadoutCatalog.SkinIdOf(skin) : null);
        catalog.Normalize(selection);
        selection.Save(store);
        hasSavedSelection = true;
        ApplySkinsToLoadout();
        Changed?.Invoke();
        return true;
    }

    /// <summary>The chosen skin for a weapon, or null when it uses its default skin.</summary>
    public WeaponSkinDefinition GetSelectedSkin(string weaponId)
    {
        EnsureReady();
        return catalog.FindSkin(selection.GetSkinId(weaponId));
    }

    /// <summary>Clears the saved selection and returns to the first catalog class with default skins.</summary>
    public bool ResetToDefaults()
    {
        EnsureReady();
        selection = new LoadoutSelection();
        catalog.Normalize(selection);
        bool applied = ApplySelection();
        store.Clear();
        hasSavedSelection = false;
        return applied;
    }

    /// <summary>
    /// Spawns the selected weapons through <see cref="ClassLoadoutApplier"/>, applies the chosen skins and persists
    /// the selection. <paramref name="equipSlot"/> &lt; 0 keeps the slot the player is holding.
    /// </summary>
    public bool ApplySelection(int equipSlot = -1)
    {
        EnsureReady();
        ResolveReferences();
        if (loadoutApplier == null)
            return false;

        catalog.Normalize(selection);

        ClassDefinition baseClass = catalog.FindClass(selection.ClassId);
        if (baseClass == null)
            baseClass = loadoutApplier.ClassDefinition;

        LoadoutWeaponEntry primary = catalog.FindWeapon(selection.PrimaryId);
        LoadoutWeaponEntry secondary = catalog.FindWeapon(selection.SecondaryId);
        ClassDefinition definition = ComposeDefinition(baseClass, primary, secondary);
        if (definition == null)
            return false;

        int slot = equipSlot;
        if (slot < 0)
            slot = weaponSwitcher != null && weaponSwitcher.CurrentSlotIndex >= 0 ? weaponSwitcher.CurrentSlotIndex : 0;

        bool applied;
        applying = true;
        try
        {
            applied = loadoutApplier.ApplyLoadout(definition, slot);
        }
        finally
        {
            applying = false;
        }

        if (!applied)
            return false;

        if (weaponBridge != null)
            weaponBridge.RefreshWeaponBindings();

        ApplySkinsToLoadout();
        selection.Save(store);
        hasSavedSelection = true;
        Changed?.Invoke();
        return true;
    }

    /// <summary>The weapon currently spawned for a slot type, or null.</summary>
    public WeaponController GetEquippedWeapon(SlotType slotType)
    {
        ResolveReferences();
        return weaponSwitcher != null ? weaponSwitcher.GetWeaponOfType(slotType) : null;
    }

    // ---------------------------------------------------------------- Internals

    private void EnsureReady()
    {
        if (ready)
            return;

        ready = true;
        ResolveReferences();

        if (store == null)
            store = new PlayerPrefsLoadoutStore();

        if (catalog == null)
            catalog = WeaponLoadoutCatalog.LoadFromResources();
        if (catalog == null)
            catalog = BuildFallbackCatalog();

        if (LoadoutSelection.TryLoad(store, out LoadoutSelection loaded))
        {
            selection = loaded;
            hasSavedSelection = true;
        }
        else
        {
            selection = new LoadoutSelection();
            hasSavedSelection = false;
            SyncSelectionFromApplier();
        }

        catalog.Normalize(selection);
    }

    private void ResolveReferences()
    {
        if (loadoutApplier == null)
            loadoutApplier = GetComponent<ClassLoadoutApplier>();
        if (loadoutApplier == null)
            loadoutApplier = GetComponentInParent<ClassLoadoutApplier>();
        if (weaponSwitcher == null && loadoutApplier != null)
            weaponSwitcher = loadoutApplier.WeaponSwitcher;
        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);
        if (weaponBridge == null)
            weaponBridge = GetComponent<PlayerWeaponBridge>();
    }

    private WeaponLoadoutCatalog BuildFallbackCatalog()
    {
        var classList = new List<ClassDefinition>();
        var selector = GetComponent<TestRangeClassSelector>();
        if (selector != null)
        {
            classList.Add(selector.AssaultClass);
            classList.Add(selector.SmgClass);
            classList.Add(selector.SniperClass);
            classList.Add(selector.HybridClass);
        }

        if (loadoutApplier != null)
        {
            classList.Add(loadoutApplier.ClassDefinition);
            classList.Add(loadoutApplier.AppliedClass);
        }

        var skins = Resources.FindObjectsOfTypeAll<WeaponSkinDefinition>();
        return WeaponLoadoutCatalog.CreateRuntime(classList, skins);
    }

    private void SyncSelectionFromApplier()
    {
        if (loadoutApplier == null)
            return;

        ClassDefinition current = loadoutApplier.AppliedClass != null
            ? loadoutApplier.AppliedClass
            : loadoutApplier.ClassDefinition;
        if (current == null)
            return;

        selection.ClassId = WeaponLoadoutCatalog.ClassIdOf(current);
        selection.PrimaryId = IdOfPrefab(current.GetPrimaryPrefab());
        selection.SecondaryId = IdOfPrefab(current.GetSecondaryPrefab());
    }

    private string IdOfPrefab(GameObject prefab)
    {
        LoadoutWeaponEntry entry = catalog != null ? catalog.FindWeaponByPrefab(prefab) : null;
        return entry != null ? entry.Id : string.Empty;
    }

    private void Subscribe()
    {
        if (subscribed || loadoutApplier == null)
            return;

        loadoutApplier.OnLoadoutApplied += HandleExternalLoadout;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (loadoutApplier != null)
            loadoutApplier.OnLoadoutApplied -= HandleExternalLoadout;
        subscribed = false;
    }

    // Class picked elsewhere (F1-F4, legacy buttons): adopt and persist it, then restore the chosen skins.
    private void HandleExternalLoadout(ClassDefinition definition)
    {
        if (applying || definition == null)
            return;

        EnsureReady();
        selection.ClassId = WeaponLoadoutCatalog.ClassIdOf(definition);
        selection.PrimaryId = IdOfPrefab(definition.GetPrimaryPrefab());
        selection.SecondaryId = IdOfPrefab(definition.GetSecondaryPrefab());
        catalog.Normalize(selection);
        selection.Save(store);
        hasSavedSelection = true;
        ApplySkinsToLoadout();
        Changed?.Invoke();
    }

    private ClassDefinition ComposeDefinition(ClassDefinition baseClass, LoadoutWeaponEntry primary, LoadoutWeaponEntry secondary)
    {
        GameObject primaryPrefab = primary != null ? primary.prefab : baseClass != null ? baseClass.GetPrimaryPrefab() : null;
        GameObject secondaryPrefab = secondary != null ? secondary.prefab : baseClass != null ? baseClass.GetSecondaryPrefab() : null;

        if (baseClass != null
            && primaryPrefab == baseClass.GetPrimaryPrefab()
            && secondaryPrefab == baseClass.GetSecondaryPrefab())
        {
            return baseClass;
        }

        if (primaryPrefab == null && secondaryPrefab == null)
            return null;

        if (composedDefinition == null)
        {
            composedDefinition = ScriptableObject.CreateInstance<ClassDefinition>();
            composedDefinition.hideFlags = HideFlags.HideAndDontSave;
            composedDefinition.name = "CustomLoadout";
        }

        ClassDefinition custom = composedDefinition;
        custom.className = baseClass != null ? baseClass.className : "Custom";
        custom.description = baseClass != null ? baseClass.description : string.Empty;
        custom.primaryWeaponPrefab = primaryPrefab;
        custom.primaryWeaponConfig = primary != null ? primary.config : baseClass != null ? baseClass.primaryWeaponConfig : null;
        custom.secondaryWeaponPrefab = secondaryPrefab;
        custom.secondaryWeaponConfig = secondary != null ? secondary.config : baseClass != null ? baseClass.secondaryWeaponConfig : null;
        custom.meleeWeaponPrefab = baseClass != null ? baseClass.meleeWeaponPrefab : null;
        custom.meleeWeaponConfig = baseClass != null ? baseClass.meleeWeaponConfig : null;
        custom.moveSpeedMultiplier = baseClass != null ? baseClass.moveSpeedMultiplier : 1f;
        return custom;
    }

    private void ApplySkinsToLoadout()
    {
        if (weaponSwitcher == null || catalog == null)
            return;

        WeaponSlot[] slots = weaponSwitcher.Slots;
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null || slot.weapon == null)
                continue;

            string weaponId;
            switch (slot.slotType)
            {
                case SlotType.Primary:
                    weaponId = selection.PrimaryId;
                    break;
                case SlotType.Secondary:
                    weaponId = selection.SecondaryId;
                    break;
                default:
                    continue;
            }

            if (string.IsNullOrEmpty(weaponId))
                continue;

            ApplySkinToWeapon(slot.weapon, catalog.FindSkin(selection.GetSkinId(weaponId)));
        }
    }

    /// <summary>Applies <paramref name="skin"/> (null = the weapon's default skin) to a spawned weapon.</summary>
    public static void ApplySkinToWeapon(WeaponController weapon, WeaponSkinDefinition skin)
    {
        if (weapon == null)
            return;

        WeaponSkinApplier skinApplier = weapon.GetComponent<WeaponSkinApplier>();
        if (skinApplier == null)
            skinApplier = weapon.GetComponentInChildren<WeaponSkinApplier>(true);
        if (skinApplier == null)
            skinApplier = weapon.gameObject.AddComponent<WeaponSkinApplier>();

        skinApplier.ApplySkin(skin != null ? skin : skinApplier.DefaultSkin);
    }
}
