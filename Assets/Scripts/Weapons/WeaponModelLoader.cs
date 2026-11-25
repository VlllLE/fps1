using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows the imported Blender model of a gun (Assets/Weapons/Models/&lt;Gun&gt;/&lt;Gun&gt;.fbx) in place of the
/// primitive-built meshes that are still part of the prefab.
///
/// The primitives are the fallback: they are only hidden after the model has been instantiated and
/// validated. If the model is not assigned, fails to import, lacks a required part or throws while it is
/// being set up, the weapon keeps its primitive meshes and logs a warning.
///
/// Hidden fallback objects are renamed with <see cref="FallbackSuffix"/> so that name based lookups such as
/// WeaponController's recursive search for "Magazine" and "Bolt" find the model's parts instead.
/// Runs before WeaponController and WeaponSkinApplier so both see the final set of renderers.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-300)]
public class WeaponModelLoader : MonoBehaviour
{
    public const string FallbackSuffix = "_Fallback";

    [Header("Imported model (preferred)")]
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private string instanceName = "DetailedModel";
    [Tooltip("Names of model parts that must exist, otherwise the model is rejected and the fallback stays visible.")]
    [SerializeField] private string[] requiredParts = Array.Empty<string>();

    [Header("Materials (matched by slot name containing Metal / Polymer / Accent)")]
    [SerializeField] private Material metalMaterial;
    [SerializeField] private Material polymerMaterial;
    [SerializeField] private Material accentMaterial;

    [Header("Procedural fallback")]
    [Tooltip("Direct children of this weapon that are hidden once the model is shown.")]
    [SerializeField] private string[] fallbackChildren = Array.Empty<string>();

    private readonly List<Transform> hiddenFallbacks = new List<Transform>();
    private readonly List<string> hiddenFallbackNames = new List<string>();
    private GameObject modelInstance;

    public bool IsLoaded => modelInstance != null;
    public GameObject ModelInstance => modelInstance;
    public string InstanceName => instanceName;
    public IReadOnlyList<string> FallbackChildren => fallbackChildren;

    private void Awake()
    {
        Load();
    }

    /// <summary>
    /// Wires the loader without the serialized prefab fields. Intended for code that assembles a weapon and for tests.
    /// </summary>
    public void Configure(GameObject prefab, string[] required, string[] fallbacks, Material metal, Material polymer, Material accent)
    {
        modelPrefab = prefab;
        requiredParts = required ?? Array.Empty<string>();
        fallbackChildren = fallbacks ?? Array.Empty<string>();
        metalMaterial = metal;
        polymerMaterial = polymer;
        accentMaterial = accent;
    }

    /// <summary>
    /// Instantiates the model and hides the fallback meshes. Returns false (and leaves the fallback visible)
    /// when the model cannot be used.
    /// </summary>
    public bool Load()
    {
        if (IsLoaded)
            return true;
        if (modelPrefab == null)
            return false;

        GameObject instance = null;
        try
        {
            Transform parent = visualRoot != null ? visualRoot : transform;
            instance = Instantiate(modelPrefab, parent);
            instance.name = instanceName;
            Transform t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            if (!HasMesh(instance))
            {
                DestroyObject(instance);
                return false;
            }

            for (int i = 0; i < requiredParts.Length; i++)
            {
                if (FindDescendantByName(t, requiredParts[i]) == null)
                {
                    Debug.LogWarning($"WeaponModelLoader: model '{modelPrefab.name}' has no part '{requiredParts[i]}'; keeping the fallback meshes.", this);
                    DestroyObject(instance);
                    return false;
                }
            }

            ApplyMaterials(instance);
            HideFallback();
            modelInstance = instance;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"WeaponModelLoader: imported model failed ({e.GetType().Name}: {e.Message}); using the fallback meshes.", this);
            RestoreFallback();
            if (instance != null)
                DestroyObject(instance);
            modelInstance = null;
            return false;
        }
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
        if (materialName.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0)
            return metalMaterial;
        if (materialName.IndexOf("Polymer", StringComparison.OrdinalIgnoreCase) >= 0)
            return polymerMaterial;
        if (materialName.IndexOf("Accent", StringComparison.OrdinalIgnoreCase) >= 0)
            return accentMaterial;
        return null;
    }

    private void HideFallback()
    {
        hiddenFallbacks.Clear();
        hiddenFallbackNames.Clear();
        for (int i = 0; i < fallbackChildren.Length; i++)
        {
            Transform child = transform.Find(fallbackChildren[i]);
            if (child == null)
                continue;

            hiddenFallbacks.Add(child);
            hiddenFallbackNames.Add(child.name);
            child.name = child.name + FallbackSuffix;
            child.gameObject.SetActive(false);
        }
    }

    private void RestoreFallback()
    {
        for (int i = 0; i < hiddenFallbacks.Count; i++)
        {
            if (hiddenFallbacks[i] == null)
                continue;
            hiddenFallbacks[i].name = hiddenFallbackNames[i];
            hiddenFallbacks[i].gameObject.SetActive(true);
        }

        hiddenFallbacks.Clear();
        hiddenFallbackNames.Clear();
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        foreach (Transform child in root)
        {
            if (child.name == targetName)
                return child;
            Transform nested = FindDescendantByName(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void DestroyObject(GameObject go)
    {
        if (go == null)
            return;
        go.transform.SetParent(null);
        if (Application.isPlaying)
        {
            go.SetActive(false);
            Destroy(go);
        }
        else
            DestroyImmediate(go);
    }
}
