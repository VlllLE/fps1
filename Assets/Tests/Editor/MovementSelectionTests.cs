using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class MovementSelectionTests
{
    private static readonly string[] ExpectedIds = { "Default", "Scout", "Heavy", "Acrobat", "Marksman" };

    private const string CatalogPath = "Assets/Resources/MovementArchetypeCatalog.asset";

    private static MovementArchetypeCatalog LoadCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MovementArchetypeCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null, "Assets/Resources/MovementArchetypeCatalog.asset must exist (Tools > Movement > Rebuild Movement Catalog).");
        return catalog;
    }

    [Test]
    public void Catalog_ListsTheFiveArchetypesInOrder_EachWithAConfigAndText()
    {
        MovementArchetypeCatalog catalog = LoadCatalog();
        CollectionAssert.AreEqual(ExpectedIds, catalog.archetypes.Select(a => a.Id).ToArray());
        foreach (MovementArchetypeEntry entry in catalog.archetypes)
        {
            Assert.That(entry.config, Is.Not.Null, entry.Id);
            Assert.That(entry.displayName, Is.Not.Empty, entry.Id);
            Assert.That(entry.description, Is.Not.Empty, entry.Id);
        }
    }

    [Test]
    public void Catalog_DefaultIsTheConfigEveryPlayerPrefabAlreadyUses()
    {
        MovementArchetypeCatalog catalog = LoadCatalog();
        var defaultConfig = AssetDatabase.LoadAssetAtPath<MovementConfig>("Assets/Configs/DefaultMovementConfig.asset");
        Assert.That(catalog.DefaultEntry.Id, Is.EqualTo("Default"));
        Assert.That(catalog.DefaultEntry.config, Is.SameAs(defaultConfig));
        Assert.That(catalog.FindByConfig(defaultConfig).Id, Is.EqualTo("Default"));
    }

    [Test]
    public void Catalog_IsNotStale_EveryArchetypeConfigAssetIsListed()
    {
        MovementArchetypeCatalog catalog = LoadCatalog();
        foreach (string guid in AssetDatabase.FindAssets("t:MovementConfig", new[] { "Assets/Configs/Movement" }))
        {
            var config = AssetDatabase.LoadAssetAtPath<MovementConfig>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(catalog.FindByConfig(config), Is.Not.Null,
                $"{config.name} is missing from the movement catalog; run Tools > Movement > Rebuild Movement Catalog.");
        }
    }

    [Test]
    public void Catalog_FindIsExact_AndUnknownIdsAreNull()
    {
        MovementArchetypeCatalog catalog = LoadCatalog();
        Assert.That(catalog.Find("Scout").config.name, Is.EqualTo("MovementConfig_Scout"));
        Assert.That(catalog.Find("Heavy").config.name, Is.EqualTo("MovementConfig_Heavy"));
        Assert.That(catalog.Find("Acrobat").config.name, Is.EqualTo("MovementConfig_Acrobat"));
        Assert.That(catalog.Find("Marksman").config.name, Is.EqualTo("MovementConfig_Marksman"));
        Assert.That(catalog.Find("scout"), Is.Null);
        Assert.That(catalog.Find(string.Empty), Is.Null);
        Assert.That(catalog.Find(null), Is.Null);
    }

    [Test]
    public void StatSheet_ReadsTheConfig_AndStacksTheClassMultiplierOnSpeedsOnly()
    {
        MovementConfig scout = LoadCatalog().Find("Scout").config;
        var plain = new MovementStatSheet(scout, 1f);
        var smg = new MovementStatSheet(scout, 1.08f);

        Assert.That(plain.WalkSpeed, Is.EqualTo(scout.walkSpeed));
        Assert.That(plain.SprintSpeed, Is.EqualTo(scout.walkSpeed * scout.sprintMultiplier).Within(1e-4f));
        Assert.That(smg.EffectiveWalkSpeed, Is.EqualTo(scout.walkSpeed * 1.08f).Within(1e-4f));
        Assert.That(smg.EffectiveSprintSpeed, Is.EqualTo(scout.walkSpeed * scout.sprintMultiplier * 1.08f).Within(1e-4f));
        Assert.That(smg.EffectiveBhopCap, Is.EqualTo(scout.bhopMaxSpeed * 1.08f).Within(1e-4f));
        Assert.That(smg.JumpHeight, Is.EqualTo(plain.JumpHeight), "jump height is not a speed, the class multiplier must not touch it");
        Assert.That(smg.AirControl, Is.EqualTo(plain.AirControl));
        Assert.That(smg.WallRun, Is.True);
        Assert.That(smg.ClassMultiplier, Is.EqualTo(1.08f));
    }

    [Test]
    public void StatSheet_JumpHeightMatchesTheBallisticFormula()
    {
        MovementConfig config = LoadCatalog().DefaultEntry.config;
        float gravity = Physics.gravity.magnitude * config.gravityMultiplier;
        Assert.That(new MovementStatSheet(config).JumpHeight, Is.EqualTo(config.jumpForce * config.jumpForce / (2f * gravity)).Within(1e-4f));
    }

    [Test]
    public void StatSheet_ArchetypesActuallyDiffer_ScoutQuickHeavySlowAcrobatSlides()
    {
        MovementArchetypeCatalog catalog = LoadCatalog();
        var def = new MovementStatSheet(catalog.Find("Default").config);
        var scout = new MovementStatSheet(catalog.Find("Scout").config);
        var heavy = new MovementStatSheet(catalog.Find("Heavy").config);
        var acrobat = new MovementStatSheet(catalog.Find("Acrobat").config);
        var marksman = new MovementStatSheet(catalog.Find("Marksman").config);

        Assert.That(scout.WalkSpeed, Is.GreaterThan(def.WalkSpeed));
        Assert.That(heavy.WalkSpeed, Is.LessThan(def.WalkSpeed));
        Assert.That(marksman.SprintSpeed, Is.LessThan(def.SprintSpeed));
        Assert.That(acrobat.SlideDuration, Is.GreaterThan(def.SlideDuration));
        Assert.That(acrobat.BhopCap, Is.GreaterThan(def.BhopCap));
        Assert.That(scout.AirControl, Is.GreaterThan(heavy.AirControl));
        Assert.That(def.WallRun, Is.False);
        Assert.That(scout.WallRun && acrobat.WallRun, Is.True);
    }

    [Test]
    public void Selection_RoundTripsThroughJson_AndTheStore()
    {
        var selection = new MovementSelection { ArchetypeId = "Acrobat" };
        Assert.That(MovementSelection.TryFromJson(selection.ToJson(), out MovementSelection parsed), Is.True);
        Assert.That(parsed.ArchetypeId, Is.EqualTo("Acrobat"));

        var store = new MemoryLoadoutStore();
        Assert.That(MovementSelection.TryLoad(store, out _), Is.False, "nothing stored yet");
        selection.Save(store);
        Assert.That(MovementSelection.TryLoad(store, out MovementSelection loaded), Is.True);
        Assert.That(loaded.ArchetypeId, Is.EqualTo("Acrobat"));
        Assert.That(selection.Clone().ArchetypeId, Is.EqualTo("Acrobat"));
    }

    [Test]
    public void Selection_RejectsGarbageAndEmptyIds()
    {
        Assert.That(MovementSelection.TryFromJson("not json{", out _), Is.False);
        Assert.That(MovementSelection.TryFromJson(string.Empty, out _), Is.False);

        var store = new MemoryLoadoutStore();
        store.Write(new MovementSelection().ToJson());
        Assert.That(MovementSelection.TryLoad(store, out _), Is.False, "an empty id is not a saved choice");
    }

    [Test]
    public void Selection_UsesItsOwnPlayerPrefsKey_NotTheWeaponLoadoutKey()
    {
        Assert.That(MovementSelection.StoreKey, Is.EqualTo("FPS.Movement.v1"));
        Assert.That(MovementSelection.StoreKey, Is.Not.EqualTo(PlayerPrefsLoadoutStore.DefaultKey));

        var store = new PlayerPrefsLoadoutStore(MovementSelection.StoreKey);
        try
        {
            store.Clear();
            new MovementSelection { ArchetypeId = "Heavy" }.Save(store);
            Assert.That(PlayerPrefs.HasKey(MovementSelection.StoreKey), Is.True);
            Assert.That(MovementSelection.TryLoad(new PlayerPrefsLoadoutStore(MovementSelection.StoreKey), out MovementSelection loaded), Is.True);
            Assert.That(loaded.ArchetypeId, Is.EqualTo("Heavy"));
        }
        finally
        {
            store.Clear();
        }
    }

    [Test]
    public void RuntimeCatalog_DropsEntriesWithoutAConfig()
    {
        MovementConfig config = LoadCatalog().Find("Scout").config;
        MovementArchetypeCatalog runtime = MovementArchetypeCatalog.CreateRuntime(new[]
        {
            new MovementArchetypeEntry { id = "Default", displayName = "Default", config = config },
            new MovementArchetypeEntry { id = "Broken", displayName = "Broken", config = null },
            null
        });
        try
        {
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(runtime.DefaultEntry.Id, Is.EqualTo("Default"));
        }
        finally
        {
            Object.DestroyImmediate(runtime);
        }
    }

    [Test]
    public void PauseMenu_MovementTabIsANewTabAndKeepsTheOldNumbering()
    {
        Assert.That((int)LoadoutPauseMenu.MenuTab.Loadout, Is.EqualTo(0));
        Assert.That((int)LoadoutPauseMenu.MenuTab.Map, Is.EqualTo(1));
        Assert.That((int)LoadoutPauseMenu.MenuTab.Movement, Is.EqualTo(2));
    }
}
