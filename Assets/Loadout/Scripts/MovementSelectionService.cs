using System;
using UnityEngine;

/// <summary>
/// Applies and persists the player's movement archetype. Choosing one swaps the <see cref="MovementConfig"/> on the
/// local <see cref="MovementController"/> immediately (no restart, no scene reload) and saves the id in PlayerPrefs, and
/// the saved choice is re-applied whenever a scene with a player loads.
/// <para>
/// Default behaviour: an explicit choice that is independent of the class. Until the player picks something nothing is
/// touched, so the player keeps the scene's own config (the Default archetype). The class speed multiplier keeps coming
/// from <see cref="ClassLoadoutApplier"/> and stacks on top of whichever archetype is active.
/// </para>
/// Added to the player automatically by <see cref="LoadoutBootstrap"/>.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-40)]
public class MovementSelectionService : MonoBehaviour
{
    [SerializeField] private MovementArchetypeCatalog catalog;
    [SerializeField] private MovementController movement;
    [SerializeField] private ClassLoadoutApplier classApplier;
    [Tooltip("Re-apply the persisted choice when the scene starts.")]
    [SerializeField] private bool applySavedOnStart = true;

    private ILoadoutStore store;
    private MovementSelection selection = new MovementSelection();
    private bool ready;
    private bool hasSavedSelection;
    private bool startedApplying;

    /// <summary>Raised after the archetype changed and was applied to the player.</summary>
    public event Action Changed;

    public MovementArchetypeCatalog Catalog
    {
        get
        {
            EnsureReady();
            return catalog;
        }
    }

    public MovementController Movement
    {
        get
        {
            ResolveReferences();
            return movement;
        }
    }

    public ClassLoadoutApplier ClassApplier
    {
        get
        {
            ResolveReferences();
            return classApplier;
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

    /// <summary>The archetype the player has chosen, or the one the scene's config matches, or Default.</summary>
    public MovementArchetypeEntry SelectedEntry
    {
        get
        {
            EnsureReady();
            MovementArchetypeEntry entry = catalog != null ? catalog.Find(selection.ArchetypeId) : null;
            if (entry != null)
                return entry;

            ResolveReferences();
            if (movement != null && catalog != null)
            {
                entry = catalog.FindByConfig(movement.Config);
                if (entry != null)
                    return entry;
            }

            return catalog != null ? catalog.DefaultEntry : null;
        }
    }

    public string SelectedId
    {
        get
        {
            MovementArchetypeEntry entry = SelectedEntry;
            return entry != null ? entry.Id : string.Empty;
        }
    }

    /// <summary>The class speed multiplier currently applied by the equipped class (1 when there is none).</summary>
    public float ClassMultiplier
    {
        get
        {
            ResolveReferences();
            return movement != null ? movement.ClassSpeedMultiplier : 1f;
        }
    }

    /// <summary>Name of the equipped class, or an empty string.</summary>
    public string ClassName
    {
        get
        {
            ResolveReferences();
            ClassDefinition current = classApplier != null
                ? (classApplier.AppliedClass != null ? classApplier.AppliedClass : classApplier.ClassDefinition)
                : null;
            return current != null ? current.className : string.Empty;
        }
    }

    /// <summary>Supplies the catalog and persistence backend before the service initialises (tests, tools).</summary>
    public void Configure(MovementArchetypeCatalog newCatalog, ILoadoutStore newStore)
    {
        catalog = newCatalog;
        store = newStore;
        ready = false;
        hasSavedSelection = false;
        selection = new MovementSelection();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        EnsureReady();
        if (applySavedOnStart && hasSavedSelection && !startedApplying)
        {
            startedApplying = true;
            ApplyCurrent();
        }
    }

    // ---------------------------------------------------------------- Selection API

    public bool Select(string archetypeId)
    {
        EnsureReady();
        return Select(catalog != null ? catalog.Find(archetypeId) : null);
    }

    public bool Select(MovementArchetypeEntry entry)
    {
        EnsureReady();
        ResolveReferences();
        if (entry == null || entry.config == null || movement == null)
            return false;

        selection.ArchetypeId = entry.Id;
        movement.SetConfig(entry.config);
        selection.Save(store);
        hasSavedSelection = true;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Forgets the saved choice and returns to the Default archetype.</summary>
    public bool ResetToDefault()
    {
        EnsureReady();
        ResolveReferences();
        MovementArchetypeEntry entry = catalog != null ? catalog.DefaultEntry : null;
        if (entry == null || entry.config == null || movement == null)
            return false;

        selection = new MovementSelection();
        movement.SetConfig(entry.config);
        store.Clear();
        hasSavedSelection = false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Re-applies the saved archetype to the player. Does nothing when nothing is saved.</summary>
    public bool ApplySaved()
    {
        EnsureReady();
        if (!hasSavedSelection)
            return false;

        return ApplyCurrent();
    }

    private bool ApplyCurrent()
    {
        ResolveReferences();
        MovementArchetypeEntry entry = catalog != null ? catalog.Find(selection.ArchetypeId) : null;
        if (entry == null || entry.config == null || movement == null)
            return false;

        movement.SetConfig(entry.config);
        Changed?.Invoke();
        return true;
    }

    // ---------------------------------------------------------------- Internals

    private void EnsureReady()
    {
        if (ready)
            return;

        ready = true;
        ResolveReferences();

        if (store == null)
            store = new PlayerPrefsLoadoutStore(MovementSelection.StoreKey);

        if (catalog == null)
            catalog = MovementArchetypeCatalog.LoadFromResources();

        if (MovementSelection.TryLoad(store, out MovementSelection loaded) && catalog != null && catalog.Find(loaded.ArchetypeId) != null)
        {
            selection = loaded;
            hasSavedSelection = true;
        }
        else
        {
            selection = new MovementSelection();
            hasSavedSelection = false;
        }
    }

    private void ResolveReferences()
    {
        if (movement == null)
            movement = GetComponent<MovementController>();
        if (movement == null)
            movement = GetComponentInParent<MovementController>();
        if (classApplier == null)
            classApplier = GetComponent<ClassLoadoutApplier>();
        if (classApplier == null)
            classApplier = GetComponentInParent<ClassLoadoutApplier>();
    }
}
