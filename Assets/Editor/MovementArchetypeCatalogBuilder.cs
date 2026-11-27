using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds <c>Assets/Resources/MovementArchetypeCatalog.asset</c>, the list behind the MOVEMENT tab, from the movement
/// config assets. Run it after adding or renaming an archetype config.
/// </summary>
public static class MovementArchetypeCatalogBuilder
{
    public const string CatalogPath = "Assets/Resources/MovementArchetypeCatalog.asset";
    private const string DefaultConfigPath = "Assets/Configs/DefaultMovementConfig.asset";
    private const string ArchetypeFolder = "Assets/Configs/Movement";

    private struct Spec
    {
        public string id;
        public string assetName;
        public string description;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { id = "Default", assetName = null, description = "The assault baseline: balanced speed, slides and bunny-hops." },
        new Spec { id = "Scout", assetName = "MovementConfig_Scout", description = "Fast and floaty: high walk and air control, wall-run on." },
        new Spec { id = "Heavy", assetName = "MovementConfig_Heavy", description = "Slow and grounded: lower speeds, sticky slides, weaker wall bounce." },
        new Spec { id = "Acrobat", assetName = "MovementConfig_Acrobat", description = "Mobility focused: long slides, strong bunny-hops, wall-run and extra coyote time." },
        new Spec { id = "Marksman", assetName = "MovementConfig_Marksman", description = "Deliberate: slower sprint and slide, less air aggression." }
    };

    [MenuItem("Tools/Movement/Rebuild Movement Catalog")]
    public static void RebuildMenu()
    {
        MovementArchetypeCatalog catalog = Rebuild();
        Debug.Log($"Movement archetype catalog rebuilt: {catalog.archetypes.Length} archetypes.", catalog);
        Selection.activeObject = catalog;
    }

    public static MovementArchetypeCatalog Rebuild()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MovementArchetypeCatalog>(CatalogPath);
        bool created = catalog == null;
        if (created)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            catalog = ScriptableObject.CreateInstance<MovementArchetypeCatalog>();
        }

        var entries = new List<MovementArchetypeEntry>();
        foreach (Spec spec in Specs)
        {
            string path = spec.assetName == null ? DefaultConfigPath : $"{ArchetypeFolder}/{spec.assetName}.asset";
            var config = AssetDatabase.LoadAssetAtPath<MovementConfig>(path);
            if (config == null)
            {
                Debug.LogWarning($"Movement catalog: '{path}' not found, skipping {spec.id}.");
                continue;
            }

            entries.Add(new MovementArchetypeEntry
            {
                id = spec.id,
                displayName = spec.id,
                description = spec.description,
                config = config
            });
        }

        catalog.archetypes = entries.ToArray();

        if (created)
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        else
            EditorUtility.SetDirty(catalog);

        AssetDatabase.SaveAssets();
        return catalog;
    }
}
