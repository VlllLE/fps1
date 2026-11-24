using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One selectable weapon: a spawnable prefab (with a WeaponController), its config and which slots it may fill.
/// The id is the prefab asset name.
/// </summary>
[Serializable]
public class LoadoutWeaponEntry
{
    public string displayName;
    public GameObject prefab;
    [Tooltip("Optional config override applied after spawn (same as ClassDefinition weapon configs).")]
    public WeaponConfig config;
    public bool allowPrimary = true;
    public bool allowSecondary = true;

    public string Id => prefab != null ? prefab.name : string.Empty;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName))
                return displayName;
            if (config != null && !string.IsNullOrEmpty(config.weaponName))
                return config.weaponName;
            return Id;
        }
    }

    public WeaponType WeaponType => config != null ? config.weaponType : WeaponType.AssaultRifle;
    public bool IsMelee => config != null && config.IsMelee;
}

/// <summary>
/// Everything the weapon selection menu can offer: weapons, skins and classes.
/// Loaded from <c>Resources/WeaponLoadoutCatalog</c>; rebuild it with Tools &gt; Loadout &gt; Rebuild Weapon Catalog.
/// </summary>
[CreateAssetMenu(fileName = "WeaponLoadoutCatalog", menuName = "Loadout/Weapon Loadout Catalog")]
public class WeaponLoadoutCatalog : ScriptableObject
{
    public const string ResourceName = "WeaponLoadoutCatalog";

    public LoadoutWeaponEntry[] weapons = Array.Empty<LoadoutWeaponEntry>();
    public WeaponSkinDefinition[] skins = Array.Empty<WeaponSkinDefinition>();
    public ClassDefinition[] classes = Array.Empty<ClassDefinition>();

    public static string ClassIdOf(ClassDefinition definition)
    {
        return definition != null ? definition.className ?? string.Empty : string.Empty;
    }

    public static string SkinIdOf(WeaponSkinDefinition skin)
    {
        return skin != null ? skin.name : string.Empty;
    }

    public static string SkinDisplayName(WeaponSkinDefinition skin)
    {
        if (skin == null)
            return "Default";
        return string.IsNullOrEmpty(skin.skinName) ? skin.name : skin.skinName;
    }

    public static WeaponLoadoutCatalog LoadFromResources()
    {
        return Resources.Load<WeaponLoadoutCatalog>(ResourceName);
    }

    public LoadoutWeaponEntry FindWeapon(string id)
    {
        if (string.IsNullOrEmpty(id) || weapons == null)
            return null;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null && weapons[i].Id == id)
                return weapons[i];
        }

        return null;
    }

    public LoadoutWeaponEntry FindWeaponByPrefab(GameObject prefab)
    {
        if (prefab == null || weapons == null)
            return null;

        for (int i = 0; i < weapons.Length; i++)
        {
            LoadoutWeaponEntry entry = weapons[i];
            if (entry != null && entry.prefab == prefab)
                return entry;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            LoadoutWeaponEntry entry = weapons[i];
            if (entry != null && entry.prefab != null && entry.prefab.name == prefab.name)
                return entry;
        }

        return null;
    }

    public WeaponSkinDefinition FindSkin(string id)
    {
        if (string.IsNullOrEmpty(id) || skins == null)
            return null;

        for (int i = 0; i < skins.Length; i++)
        {
            if (skins[i] != null && skins[i].name == id)
                return skins[i];
        }

        return null;
    }

    public ClassDefinition FindClass(string id)
    {
        if (string.IsNullOrEmpty(id) || classes == null)
            return null;

        for (int i = 0; i < classes.Length; i++)
        {
            if (classes[i] != null && ClassIdOf(classes[i]) == id)
                return classes[i];
        }

        return null;
    }

    public List<LoadoutWeaponEntry> GetPrimaryOptions()
    {
        var list = new List<LoadoutWeaponEntry>(8);
        if (weapons == null)
            return list;
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null && weapons[i].prefab != null && weapons[i].allowPrimary)
                list.Add(weapons[i]);
        }

        return list;
    }

    public List<LoadoutWeaponEntry> GetSecondaryOptions(string excludeId)
    {
        var list = new List<LoadoutWeaponEntry>(8);
        if (weapons == null)
            return list;
        for (int i = 0; i < weapons.Length; i++)
        {
            LoadoutWeaponEntry entry = weapons[i];
            if (entry != null && entry.prefab != null && entry.allowSecondary && entry.Id != excludeId)
                list.Add(entry);
        }

        return list;
    }

    /// <summary>
    /// Repairs a selection so that every id refers to something in this catalog: the primary must be a primary-capable
    /// weapon, the secondary a secondary-capable weapon different from the primary, and skin picks must exist.
    /// Missing choices fall back to the selected class's weapons, then to the first valid entry.
    /// </summary>
    public void Normalize(LoadoutSelection selection)
    {
        if (selection == null)
            return;

        if (string.IsNullOrEmpty(selection.ClassId) && classes != null)
        {
            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] != null)
                {
                    selection.ClassId = ClassIdOf(classes[i]);
                    break;
                }
            }
        }

        ClassDefinition selectedClass = FindClass(selection.ClassId);

        LoadoutWeaponEntry primary = FindWeapon(selection.PrimaryId);
        if (primary == null || !primary.allowPrimary)
        {
            primary = selectedClass != null ? FindWeaponByPrefab(selectedClass.GetPrimaryPrefab()) : null;
            if (primary == null || !primary.allowPrimary)
                primary = FirstWeapon(entry => entry.allowPrimary, null);
        }

        string primaryId = primary != null ? primary.Id : string.Empty;

        LoadoutWeaponEntry secondary = FindWeapon(selection.SecondaryId);
        if (secondary == null || !secondary.allowSecondary || secondary.Id == primaryId)
        {
            secondary = selectedClass != null ? FindWeaponByPrefab(selectedClass.GetSecondaryPrefab()) : null;
            if (secondary == null || !secondary.allowSecondary || secondary.Id == primaryId)
                secondary = FirstWeapon(entry => entry.allowSecondary, primaryId);
        }

        selection.PrimaryId = primaryId;
        selection.SecondaryId = secondary != null ? secondary.Id : string.Empty;

        selection.RemoveSkinsWhere((weaponId, skinId) => FindWeapon(weaponId) == null || FindSkin(skinId) == null);
    }

    private LoadoutWeaponEntry FirstWeapon(Func<LoadoutWeaponEntry, bool> predicate, string excludeId)
    {
        if (weapons == null)
            return null;

        for (int i = 0; i < weapons.Length; i++)
        {
            LoadoutWeaponEntry entry = weapons[i];
            if (entry == null || entry.prefab == null)
                continue;
            if (!string.IsNullOrEmpty(excludeId) && entry.Id == excludeId)
                continue;
            if (predicate(entry))
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Builds an in-memory catalog from classes and skins when the Resources asset is missing.
    /// </summary>
    public static WeaponLoadoutCatalog CreateRuntime(IEnumerable<ClassDefinition> classDefinitions, IEnumerable<WeaponSkinDefinition> skinDefinitions)
    {
        var catalog = CreateInstance<WeaponLoadoutCatalog>();
        catalog.hideFlags = HideFlags.HideAndDontSave;
        catalog.name = "RuntimeWeaponLoadoutCatalog";

        var classList = new List<ClassDefinition>();
        var weaponList = new List<LoadoutWeaponEntry>();

        if (classDefinitions != null)
        {
            foreach (ClassDefinition definition in classDefinitions)
            {
                if (definition == null || classList.Contains(definition))
                    continue;
                classList.Add(definition);
                AddRuntimeWeapon(weaponList, definition.GetPrimaryPrefab(), definition.primaryWeaponConfig);
                AddRuntimeWeapon(weaponList, definition.GetSecondaryPrefab(), definition.secondaryWeaponConfig);
            }
        }

        var skinList = new List<WeaponSkinDefinition>();
        if (skinDefinitions != null)
        {
            foreach (WeaponSkinDefinition skin in skinDefinitions)
            {
                if (skin != null && !skinList.Contains(skin))
                    skinList.Add(skin);
            }
        }

        skinList.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        catalog.classes = classList.ToArray();
        catalog.weapons = weaponList.ToArray();
        catalog.skins = skinList.ToArray();
        return catalog;
    }

    private static void AddRuntimeWeapon(List<LoadoutWeaponEntry> list, GameObject prefab, WeaponConfig config)
    {
        if (prefab == null)
            return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].prefab == prefab)
                return;
        }

        list.Add(new LoadoutWeaponEntry
        {
            prefab = prefab,
            config = config,
            allowPrimary = config == null || !config.IsMelee,
            allowSecondary = true
        });
    }
}
