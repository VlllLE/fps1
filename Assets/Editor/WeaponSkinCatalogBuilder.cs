using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds <c>Assets/Skins/Resources/WeaponSkinCatalog.asset</c> from every <see cref="WeaponSkinDefinition"/>
/// under <c>Assets/Skins/Definitions</c>. Run it after adding a skin.
/// </summary>
public static class WeaponSkinCatalogBuilder
{
    public const string CatalogPath = "Assets/Skins/Resources/WeaponSkinCatalog.asset";
    public const string SkinFolder = "Assets/Skins/Definitions";

    [MenuItem("Tools/Skins/Rebuild Weapon Skin Catalog")]
    public static void RebuildMenu()
    {
        WeaponSkinCatalog catalog = Rebuild();
        Debug.Log($"Weapon skin catalog rebuilt: {catalog.skins.Length} skins.", catalog);
        Selection.activeObject = catalog;
    }

    public static WeaponSkinCatalog Rebuild()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<WeaponSkinCatalog>(CatalogPath);
        if (catalog == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            catalog = ScriptableObject.CreateInstance<WeaponSkinCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.skins = FindAllSkins().ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    public static List<WeaponSkinDefinition> FindAllSkins()
    {
        var skins = new List<WeaponSkinDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponSkinDefinition", new[] { SkinFolder }))
        {
            var skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skin != null)
                skins.Add(skin);
        }

        skins.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return skins;
    }
}
