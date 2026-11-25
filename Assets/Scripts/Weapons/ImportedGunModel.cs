using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shows a gun's imported Blender model (for example Assets/Weapons/Models/BoltActionSniper/BoltActionSniper.fbx).
/// The prefab keeps its primitive-built parts under a "ProceduralFallback" child; when the imported model is
/// instantiated successfully that group is hidden, otherwise the primitives stay visible and the gun still works.
/// Runs before the other weapon components so WeaponController finds the model's Magazine / Bolt parts and
/// WeaponSkinApplier sees the model's renderers.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class ImportedGunModel : MonoBehaviour
{
    public const string FallbackSuffix = "_Fallback";

    [Header("Imported model (preferred)")]
    [SerializeField] private GameObject importedModelPrefab;
    [Tooltip("Editor-only safety net: loaded from this path when importedModelPrefab did not resolve.")]
    [SerializeField] private string editorAssetPath;
    [Tooltip("Parent for the model instance. Defaults to this transform.")]
    [SerializeField] private Transform visualRoot;

    [Header("Material remap (by FBX material name)")]
    [SerializeField] private Material metalMaterial;
    [SerializeField] private Material polymerMaterial;
    [SerializeField] private Material glassMaterial;
    [SerializeField] private Material accentMaterial;

    [Header("Procedural fallback")]
    [SerializeField] private GameObject proceduralFallback;

    private GameObject importedInstance;

    public bool IsBuilt { get; private set; }
    public bool UsesImportedModel => importedInstance != null;
    public GameObject ImportedInstance => importedInstance;
    public GameObject ProceduralFallback => proceduralFallback;

    private void Awake()
    {
        Build();
    }

    private void OnDestroy()
    {
        importedInstance = null;
        IsBuilt = false;
    }

    /// <summary>Wires the component from code (tests and tools that assemble a gun without the prefab).</summary>
    public void Configure(GameObject model, Transform parent, GameObject fallback)
    {
        importedModelPrefab = model;
        visualRoot = parent;
        proceduralFallback = fallback;
    }

    public void Build()
    {
        if (IsBuilt)
            return;

        if (TryBuildImported())
        {
            IsBuilt = true;
            return;
        }

        SetFallbackVisible(true);
        IsBuilt = true;
    }

    /// <summary>Finds a part of the active model (imported if present, otherwise the fallback) by name.</summary>
    public Transform FindPart(string partName)
    {
        Transform root = importedInstance != null ? importedInstance.transform
            : proceduralFallback != null ? proceduralFallback.transform : transform;
        return FindDescendant(root, partName);
    }

    private bool TryBuildImported()
    {
        GameObject source = ResolveSource();
        if (source == null)
            return false;

        Transform parent = visualRoot != null ? visualRoot : transform;
        GameObject instance = null;
        try
        {
            instance = Instantiate(source, parent);
            instance.name = "ImportedModel";
            Transform t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            if (!HasMesh(instance))
            {
                DestroyObject(instance);
                return false;
            }

            ApplyMaterials(instance);
            RenameFallbackDuplicates(instance.transform);
            SetFallbackVisible(false);
            importedInstance = instance;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ImportedGunModel: imported model failed ({e.GetType().Name}: {e.Message}); using the procedural fallback.", this);
            if (instance != null)
                DestroyObject(instance);
            return false;
        }
    }

    private GameObject ResolveSource()
    {
        if (importedModelPrefab != null)
            return importedModelPrefab;

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(editorAssetPath))
        {
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(editorAssetPath);
            if (loaded != null)
                return loaded;
        }
#endif
        return null;
    }

    private static bool HasMesh(GameObject instance)
    {
        MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh != null)
                return true;
        }
        return false;
    }

    private void ApplyMaterials(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Material[] materials = renderers[r].sharedMaterials;
            bool changed = false;
            for (int m = 0; m < materials.Length; m++)
            {
                Material replacement = ResolveMaterial(materials[m] != null ? materials[m].name : string.Empty);
                if (replacement != null && replacement != materials[m])
                {
                    materials[m] = replacement;
                    changed = true;
                }
            }

            if (changed)
                renderers[r].sharedMaterials = materials;
        }
    }

    private Material ResolveMaterial(string materialName)
    {
        if (materialName.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return glassMaterial;
        if (materialName.IndexOf("Accent", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return accentMaterial;
        if (materialName.IndexOf("Polymer", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return polymerMaterial;
        if (materialName.IndexOf("Metal", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return metalMaterial;
        return null;
    }

    /// <summary>
    /// WeaponController looks up "Magazine" and "Bolt" by name (first match, depth first). The hidden primitive
    /// parts must not shadow the model's parts, so any fallback transform that shares a name gets a suffix.
    /// </summary>
    private void RenameFallbackDuplicates(Transform modelRoot)
    {
        if (proceduralFallback == null)
            return;

        var modelNames = new HashSet<string>();
        foreach (Transform t in modelRoot.GetComponentsInChildren<Transform>(true))
            modelNames.Add(t.name);

        foreach (Transform t in proceduralFallback.GetComponentsInChildren<Transform>(true))
        {
            if (t != proceduralFallback.transform && modelNames.Contains(t.name))
                t.name += FallbackSuffix;
        }
    }

    private void SetFallbackVisible(bool visible)
    {
        if (proceduralFallback != null)
            proceduralFallback.SetActive(visible);
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        foreach (Transform child in root)
        {
            if (child.name == targetName)
                return child;
            Transform nested = FindDescendant(child, targetName);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }
}
