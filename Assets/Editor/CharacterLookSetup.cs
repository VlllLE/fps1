using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configures the importers of the retargeted character FBXs (<c>Tools/Blender/real_characters/retarget.py</c>) and
/// lists them in the <see cref="SoldierModelSet"/> asset. Re-run after regenerating a model.
/// </summary>
public static class CharacterLookSetup
{
    public const string LooksFolder = "Assets/Characters/Looks";
    public const string ModelSetPath = "Assets/Resources/Characters/SoldierModelSet.asset";

    private static readonly string[][] Looks =
    {
        new[] { CharacterLooks.Soldier, "Soldier" },
        new[] { CharacterLooks.Operator, "S.W.A.T. operator" },
        new[] { CharacterLooks.Ghillie, "Ghillie suit" }
    };

    [MenuItem("Tools/Characters/Setup Real Soldier Looks")]
    public static void Run()
    {
        var set = AssetDatabase.LoadAssetAtPath<SoldierModelSet>(ModelSetPath);
        if (set == null)
        {
            Debug.LogError("[CharacterLookSetup] Missing " + ModelSetPath);
            return;
        }

        var entries = new CharacterLookEntry[Looks.Length];
        for (int i = 0; i < Looks.Length; i++)
        {
            string id = Looks[i][0];
            string fbx = LooksFolder + "/" + id + "/" + id + ".fbx";
            ConfigureImporter(fbx);
            entries[i] = new CharacterLookEntry
            {
                id = id,
                displayName = Looks[i][1],
                model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx),
                height = ReadHeight(id)
            };
            Debug.Log("[CharacterLookSetup] " + id + " -> " + fbx + " height " + entries[i].height);
        }

        set.looks = entries;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureImporter(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
            return;

        importer.isReadable = true;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.optimizeGameObjects = false;
        importer.addCollider = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.SaveAndReimport();
    }

    private static float ReadHeight(string id)
    {
        string file = System.IO.Path.Combine(LooksFolder, "Source~", id + ".height.txt");
        if (File.Exists(file) && float.TryParse(File.ReadAllText(file).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
            return height;
        return 1.8f;
    }
}
