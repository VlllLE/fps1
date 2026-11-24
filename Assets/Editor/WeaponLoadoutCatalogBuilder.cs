using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds <c>Assets/Resources/WeaponLoadoutCatalog.asset</c> (weapons, skins, classes) from the project assets.
/// Run it after adding a weapon prefab, skin or class definition.
/// </summary>
public static class WeaponLoadoutCatalogBuilder
{
    public const string CatalogPath = "Assets/Resources/WeaponLoadoutCatalog.asset";
    private const string WeaponPrefabFolder = "Assets/Weapons/Prefabs";
    private const string SkinFolder = "Assets/Skins/Definitions";
    private const string ClassFolder = "Assets/Classes/Definitions";

    [MenuItem("Tools/Loadout/Rebuild Weapon Catalog")]
    public static void RebuildMenu()
    {
        WeaponLoadoutCatalog catalog = Rebuild();
        Debug.Log($"Weapon loadout catalog rebuilt: {catalog.weapons.Length} weapons, {catalog.skins.Length} skins, {catalog.classes.Length} classes.", catalog);
        Selection.activeObject = catalog;
    }

    public static WeaponLoadoutCatalog Rebuild()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WeaponLoadoutCatalog>(CatalogPath);
        bool created = catalog == null;
        if (created)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            catalog = ScriptableObject.CreateInstance<WeaponLoadoutCatalog>();
        }

        var weapons = new List<LoadoutWeaponEntry>();
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { WeaponPrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            var controller = prefab.GetComponent<WeaponController>();
            if (controller == null)
                continue;

            WeaponConfig config = controller.Config;
            weapons.Add(new LoadoutWeaponEntry
            {
                displayName = config != null && !string.IsNullOrEmpty(config.weaponName) ? config.weaponName : prefab.name,
                prefab = prefab,
                config = config,
                allowPrimary = config == null || !config.IsMelee,
                allowSecondary = true
            });
        }

        // Guns first (primary-capable), melee last; stable by name otherwise.
        weapons.Sort((a, b) =>
        {
            int melee = a.allowPrimary == b.allowPrimary ? 0 : (a.allowPrimary ? -1 : 1);
            return melee != 0 ? melee : string.CompareOrdinal(a.Id, b.Id);
        });

        var skins = new List<WeaponSkinDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponSkinDefinition", new[] { SkinFolder }))
        {
            var skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skin != null)
                skins.Add(skin);
        }
        skins.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        var classes = new List<ClassDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:ClassDefinition", new[] { ClassFolder }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<ClassDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null)
                classes.Add(definition);
        }
        // Same order as the F1-F4 class hotkeys, then any extra classes by name.
        string[] preferredOrder = { "Assault", "SMG", "Sniper", "Hybrid" };
        classes.Sort((a, b) =>
        {
            int ia = System.Array.IndexOf(preferredOrder, a.className);
            int ib = System.Array.IndexOf(preferredOrder, b.className);
            if (ia < 0) ia = preferredOrder.Length;
            if (ib < 0) ib = preferredOrder.Length;
            return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a.className, b.className);
        });

        catalog.weapons = weapons.ToArray();
        catalog.skins = skins.ToArray();
        catalog.classes = classes.ToArray();

        if (created)
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        else
            EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        return catalog;
    }
}
