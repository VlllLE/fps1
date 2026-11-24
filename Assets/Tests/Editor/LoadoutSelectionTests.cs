using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class LoadoutSelectionTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    // ------------------------------------------------------------ LoadoutSelection / stores

    [Test]
    public void Selection_RoundTripsThroughJson()
    {
        var selection = new LoadoutSelection { ClassId = "Sniper", PrimaryId = "SCAR", SecondaryId = "Karambit" };
        selection.SetSkinId("SCAR", "Skin_Damascus");
        selection.SetSkinId("Karambit", "Skin_Prism");

        Assert.That(LoadoutSelection.TryFromJson(selection.ToJson(), out LoadoutSelection loaded), Is.True);
        Assert.That(loaded.ClassId, Is.EqualTo("Sniper"));
        Assert.That(loaded.PrimaryId, Is.EqualTo("SCAR"));
        Assert.That(loaded.SecondaryId, Is.EqualTo("Karambit"));
        Assert.That(loaded.GetSkinId("SCAR"), Is.EqualTo("Skin_Damascus"));
        Assert.That(loaded.GetSkinId("Karambit"), Is.EqualTo("Skin_Prism"));
        Assert.That(loaded.GetSkinId("UMP"), Is.EqualTo(string.Empty));
    }

    [Test]
    public void SetSkinId_ReplacesExistingPick_AndEmptyRemovesIt()
    {
        var selection = new LoadoutSelection();
        selection.SetSkinId("M4A1", "Skin_A");
        selection.SetSkinId("M4A1", "Skin_B");

        Assert.That(selection.SkinCount, Is.EqualTo(1));
        Assert.That(selection.GetSkinId("M4A1"), Is.EqualTo("Skin_B"));

        selection.SetSkinId("M4A1", null);
        Assert.That(selection.SkinCount, Is.EqualTo(0));
        Assert.That(selection.GetSkinId("M4A1"), Is.EqualTo(string.Empty));
    }

    [Test]
    public void TryFromJson_RejectsEmptyAndGarbage()
    {
        Assert.That(LoadoutSelection.TryFromJson(null, out _), Is.False);
        Assert.That(LoadoutSelection.TryFromJson(string.Empty, out _), Is.False);
        Assert.That(LoadoutSelection.TryFromJson("{ not json", out _), Is.False);
    }

    [Test]
    public void MemoryStore_SaveLoadClear()
    {
        var store = new MemoryLoadoutStore();
        Assert.That(LoadoutSelection.TryLoad(store, out _), Is.False);

        new LoadoutSelection { ClassId = "SMG", PrimaryId = "UMP" }.Save(store);
        Assert.That(LoadoutSelection.TryLoad(store, out LoadoutSelection loaded), Is.True);
        Assert.That(loaded.ClassId, Is.EqualTo("SMG"));

        store.Clear();
        Assert.That(LoadoutSelection.TryLoad(store, out _), Is.False);
    }

    [Test]
    public void PlayerPrefsStore_PersistsAndClears_UnderACustomKey()
    {
        const string key = "FPS.Loadout.EditModeTest";
        PlayerPrefs.DeleteKey(key);
        var store = new PlayerPrefsLoadoutStore(key);
        try
        {
            Assert.That(LoadoutSelection.TryLoad(store, out _), Is.False);

            var selection = new LoadoutSelection { ClassId = "Hybrid", PrimaryId = "DMRSniper", SecondaryId = "Karambit" };
            selection.SetSkinId("DMRSniper", "Skin_NightViper");
            selection.Save(store);

            Assert.That(PlayerPrefs.HasKey(key), Is.True);
            Assert.That(LoadoutSelection.TryLoad(new PlayerPrefsLoadoutStore(key), out LoadoutSelection loaded), Is.True);
            Assert.That(loaded.PrimaryId, Is.EqualTo("DMRSniper"));
            Assert.That(loaded.GetSkinId("DMRSniper"), Is.EqualTo("Skin_NightViper"));

            store.Clear();
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
        }
        finally
        {
            PlayerPrefs.DeleteKey(key);
        }
    }

    [Test]
    public void DefaultPlayerPrefsKey_IsVersioned()
    {
        Assert.That(new PlayerPrefsLoadoutStore().Key, Is.EqualTo(PlayerPrefsLoadoutStore.DefaultKey));
        Assert.That(PlayerPrefsLoadoutStore.DefaultKey, Does.EndWith(".v1"));
    }

    // ------------------------------------------------------------ Catalog

    [Test]
    public void Normalize_FillsEmptySelectionFromFirstClass()
    {
        WeaponLoadoutCatalog catalog = CreateCatalog(out _, out _, out _, out _);
        var selection = new LoadoutSelection();

        catalog.Normalize(selection);

        Assert.That(selection.ClassId, Is.EqualTo("Assault"));
        Assert.That(selection.PrimaryId, Is.EqualTo("Rifle"));
        Assert.That(selection.SecondaryId, Is.EqualTo("Knife"));
    }

    [Test]
    public void Normalize_FallsBackToClassWeapons_WhenIdsAreUnknown()
    {
        WeaponLoadoutCatalog catalog = CreateCatalog(out _, out _, out _, out _);
        var selection = new LoadoutSelection { ClassId = "Gunner", PrimaryId = "Gone", SecondaryId = "AlsoGone" };

        catalog.Normalize(selection);

        Assert.That(selection.PrimaryId, Is.EqualTo("Smg"));
        Assert.That(selection.SecondaryId, Is.EqualTo("Knife"));
    }

    [Test]
    public void Normalize_RejectsMeleeAsPrimary()
    {
        WeaponLoadoutCatalog catalog = CreateCatalog(out _, out _, out _, out _);
        var selection = new LoadoutSelection { ClassId = "Assault", PrimaryId = "Knife", SecondaryId = "Smg" };

        catalog.Normalize(selection);

        Assert.That(selection.PrimaryId, Is.EqualTo("Rifle"));
        Assert.That(selection.SecondaryId, Is.EqualTo("Smg"));
    }

    [Test]
    public void Normalize_NeverLeavesSamePrimaryAndSecondary()
    {
        WeaponLoadoutCatalog catalog = CreateCatalog(out _, out _, out _, out _);
        var selection = new LoadoutSelection { ClassId = "Assault", PrimaryId = "Smg", SecondaryId = "Smg" };

        catalog.Normalize(selection);

        Assert.That(selection.PrimaryId, Is.EqualTo("Smg"));
        Assert.That(selection.SecondaryId, Is.Not.EqualTo("Smg"));
        Assert.That(catalog.FindWeapon(selection.SecondaryId), Is.Not.Null);
    }

    [Test]
    public void Normalize_DropsSkinPicksForUnknownWeaponsOrSkins()
    {
        WeaponLoadoutCatalog catalog = CreateCatalog(out _, out _, out _, out WeaponSkinDefinition skin);
        var selection = new LoadoutSelection { ClassId = "Assault", PrimaryId = "Rifle", SecondaryId = "Knife" };
        selection.SetSkinId("Rifle", skin.name);
        selection.SetSkinId("Rifle2", skin.name);
        selection.SetSkinId("Knife", "MissingSkin");

        catalog.Normalize(selection);

        Assert.That(selection.GetSkinId("Rifle"), Is.EqualTo(skin.name));
        Assert.That(selection.GetSkinId("Rifle2"), Is.EqualTo(string.Empty));
        Assert.That(selection.GetSkinId("Knife"), Is.EqualTo(string.Empty));
        Assert.That(selection.SkinCount, Is.EqualTo(1));
    }

    [Test]
    public void Options_PrimaryExcludesMelee_SecondaryExcludesPrimary()
    {
        WeaponLoadoutCatalog catalog = CreateCatalog(out _, out _, out _, out _);

        List<LoadoutWeaponEntry> primary = catalog.GetPrimaryOptions();
        Assert.That(primary.Count, Is.EqualTo(2));
        Assert.That(primary.Exists(e => e.Id == "Knife"), Is.False);

        List<LoadoutWeaponEntry> secondary = catalog.GetSecondaryOptions("Rifle");
        Assert.That(secondary.Exists(e => e.Id == "Rifle"), Is.False);
        Assert.That(secondary.Exists(e => e.Id == "Knife"), Is.True);
        Assert.That(secondary.Exists(e => e.Id == "Smg"), Is.True);
    }

    [Test]
    public void FindWeaponByPrefab_MatchesReference_AndFallsBackToName()
    {
        WeaponLoadoutCatalog catalog = CreateCatalog(out GameObject rifle, out _, out _, out _);

        Assert.That(catalog.FindWeaponByPrefab(rifle).Id, Is.EqualTo("Rifle"));

        var lookalike = new GameObject("Rifle");
        created.Add(lookalike);
        Assert.That(catalog.FindWeaponByPrefab(lookalike).Id, Is.EqualTo("Rifle"));
        Assert.That(catalog.FindWeaponByPrefab(null), Is.Null);
    }

    [Test]
    public void CreateRuntime_BuildsWeaponsFromClassesAndSortsSkins()
    {
        CreateCatalog(out GameObject rifle, out GameObject knife, out _, out _);
        var def = NewClass("Solo", rifle, knife, null, null);
        var skinB = NewSkin("B_Skin");
        var skinA = NewSkin("A_Skin");

        WeaponLoadoutCatalog runtime = WeaponLoadoutCatalog.CreateRuntime(new[] { def, def }, new[] { skinB, skinA });
        created.Add(runtime);

        Assert.That(runtime.classes.Length, Is.EqualTo(1));
        Assert.That(runtime.weapons.Length, Is.EqualTo(2));
        Assert.That(runtime.skins[0], Is.SameAs(skinA));
        Assert.That(runtime.skins[1], Is.SameAs(skinB));
    }

    [Test]
    public void SkinDisplayName_UsesSkinName_ThenAssetName_ThenDefault()
    {
        var skin = NewSkin("Skin_Asset");
        skin.skinName = "Pretty";
        Assert.That(WeaponLoadoutCatalog.SkinDisplayName(skin), Is.EqualTo("Pretty"));
        skin.skinName = string.Empty;
        Assert.That(WeaponLoadoutCatalog.SkinDisplayName(skin), Is.EqualTo("Skin_Asset"));
        Assert.That(WeaponLoadoutCatalog.SkinDisplayName(null), Is.EqualTo("Default"));
    }

    // ------------------------------------------------------------ Shipped catalog asset

    [Test]
    public void ShippedCatalogAsset_LoadsFromResources()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();

        Assert.That(catalog, Is.Not.Null, "Assets/Resources/WeaponLoadoutCatalog.asset must exist (Tools > Loadout > Rebuild Weapon Catalog).");
        Assert.That(catalog.weapons.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(catalog.classes.Length, Is.GreaterThanOrEqualTo(1));
        foreach (LoadoutWeaponEntry entry in catalog.weapons)
        {
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.prefab, Is.Not.Null, "Catalog weapon without prefab");
            Assert.That(entry.prefab.GetComponent<WeaponController>(), Is.Not.Null, entry.Id + " needs a WeaponController");
        }
    }

    [Test]
    public void ShippedCatalogAsset_ContainsKarambitAsSecondaryOnlyMelee()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        Assume.That(catalog, Is.Not.Null);

        LoadoutWeaponEntry karambit = catalog.FindWeapon("Karambit");
        Assert.That(karambit, Is.Not.Null);
        Assert.That(karambit.IsMelee, Is.True);
        Assert.That(karambit.allowPrimary, Is.False);
        Assert.That(karambit.allowSecondary, Is.True);
    }

    [Test]
    public void ShippedCatalogAsset_CoversEveryWeaponPrefabSkinAndClassInTheProject()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        Assume.That(catalog, Is.Not.Null);

        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Weapons/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null || prefab.GetComponent<WeaponController>() == null)
                continue;
            Assert.That(catalog.FindWeapon(prefab.name), Is.Not.Null, $"Weapon '{prefab.name}' missing from catalog; run Tools > Loadout > Rebuild Weapon Catalog.");
        }

        foreach (string guid in AssetDatabase.FindAssets("t:WeaponSkinDefinition", new[] { "Assets/Skins/Definitions" }))
        {
            var skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(catalog.FindSkin(skin.name), Is.Not.Null, $"Skin '{skin.name}' missing from catalog; run Tools > Loadout > Rebuild Weapon Catalog.");
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ClassDefinition", new[] { "Assets/Classes/Definitions" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<ClassDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(catalog.FindClass(definition.className), Is.Not.Null, $"Class '{definition.className}' missing from catalog.");
        }
    }

    [Test]
    public void ShippedCatalogAsset_EveryClassNormalizesToItsOwnWeapons()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        Assume.That(catalog, Is.Not.Null);

        foreach (ClassDefinition definition in catalog.classes)
        {
            var selection = new LoadoutSelection { ClassId = definition.className };
            catalog.Normalize(selection);

            Assert.That(selection.PrimaryId, Is.EqualTo(definition.GetPrimaryPrefab().name), definition.className);
            Assert.That(selection.SecondaryId, Is.EqualTo(definition.GetSecondaryPrefab().name), definition.className);
        }
    }

    // ------------------------------------------------------------ Fixtures

    private WeaponLoadoutCatalog CreateCatalog(out GameObject rifle, out GameObject knife, out GameObject smg, out WeaponSkinDefinition skin)
    {
        rifle = NewPrefab("Rifle");
        smg = NewPrefab("Smg");
        knife = NewPrefab("Knife");
        skin = NewSkin("Skin_Test");

        var catalog = ScriptableObject.CreateInstance<WeaponLoadoutCatalog>();
        created.Add(catalog);
        catalog.weapons = new[]
        {
            new LoadoutWeaponEntry { displayName = "Rifle", prefab = rifle, allowPrimary = true, allowSecondary = true },
            new LoadoutWeaponEntry { displayName = "Smg", prefab = smg, allowPrimary = true, allowSecondary = true },
            new LoadoutWeaponEntry { displayName = "Knife", prefab = knife, allowPrimary = false, allowSecondary = true }
        };
        catalog.skins = new[] { skin };
        catalog.classes = new[]
        {
            NewClass("Assault", rifle, knife, null, null),
            NewClass("Gunner", smg, knife, null, null)
        };
        return catalog;
    }

    private GameObject NewPrefab(string name)
    {
        var go = new GameObject(name);
        created.Add(go);
        return go;
    }

    private WeaponSkinDefinition NewSkin(string name)
    {
        var skin = ScriptableObject.CreateInstance<WeaponSkinDefinition>();
        skin.name = name;
        created.Add(skin);
        return skin;
    }

    private ClassDefinition NewClass(string className, GameObject primary, GameObject secondary, WeaponConfig primaryConfig, WeaponConfig secondaryConfig)
    {
        var def = ScriptableObject.CreateInstance<ClassDefinition>();
        def.className = className;
        def.primaryWeaponPrefab = primary;
        def.secondaryWeaponPrefab = secondary;
        def.primaryWeaponConfig = primaryConfig;
        def.secondaryWeaponConfig = secondaryConfig;
        created.Add(def);
        return def;
    }
}
