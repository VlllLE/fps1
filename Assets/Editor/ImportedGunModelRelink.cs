using UnityEditor;
using UnityEngine;

/// <summary>
/// Re-assigns the imported model reference on the Blender-modelled gun prefabs. The prefabs reference the FBX root
/// by a pinned fileID (see internalIDToNameTable in the .fbx.meta); if Unity ever regenerates those ids, run
/// <b>Tools &gt; Weapons &gt; Relink Imported Gun Models</b> to fix the references without touching anything else.
/// </summary>
public static class ImportedGunModelRelink
{
    private struct Entry
    {
        public string prefab;
        public string model;
    }

    private static readonly Entry[] Entries =
    {
        new Entry
        {
            prefab = "Assets/Weapons/Prefabs/BoltActionSniper.prefab",
            model = "Assets/Weapons/Models/BoltActionSniper/BoltActionSniper.fbx",
        },
        new Entry
        {
            prefab = "Assets/Weapons/Prefabs/DMRSniper.prefab",
            model = "Assets/Weapons/Models/DMRSniper/DMRSniper.fbx",
        },
    };

    [MenuItem("Tools/Weapons/Relink Imported Gun Models")]
    public static void RelinkAll()
    {
        int relinked = 0;
        foreach (Entry entry in Entries)
        {
            if (Relink(entry.prefab, entry.model))
                relinked++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"ImportedGunModelRelink: relinked {relinked}/{Entries.Length} prefabs.");
    }

    public static bool Relink(string prefabPath, string modelPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogWarning($"ImportedGunModelRelink: model not found at {modelPath}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var component = root.GetComponent<ImportedGunModel>();
            if (component == null)
            {
                Debug.LogWarning($"ImportedGunModelRelink: {prefabPath} has no ImportedGunModel");
                return false;
            }

            var serialized = new SerializedObject(component);
            serialized.FindProperty("importedModelPrefab").objectReferenceValue = model;
            serialized.FindProperty("editorAssetPath").stringValue = modelPath;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
