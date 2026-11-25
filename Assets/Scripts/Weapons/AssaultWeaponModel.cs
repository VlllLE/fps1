using UnityEngine;

/// <summary>
/// Shows the imported Blender model of an assault weapon (M4A1, SCAR-H, UMP45; see Tools/Blender and
/// Assets/Weapons/Models). If no model is assigned or it cannot be instantiated, the prefab's primitive-built
/// "Procedural" group stays visible instead and the muzzle / fire / eject points return to their fallback positions.
///
/// The imported FBX contains three objects: Body, Magazine (pivot at the top of the magazine) and Bolt
/// (the charging handle). <see cref="WeaponController"/> animates the first objects named "Magazine" and "Bolt" it
/// finds, so the model is parented under a "Model" child that comes before the procedural group in the hierarchy,
/// and the procedural reload parts are renamed when the import succeeds.
///
/// Runs before <see cref="WeaponController"/> and the weapon skin applier so both see the final renderers.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class AssaultWeaponModel : MonoBehaviour
{
    private const string MagazineName = "Magazine";
    private const string BoltName = "Bolt";
    private const string ProceduralSuffix = "_Procedural";

    [Header("Imported model (preferred)")]
    [SerializeField] private GameObject importedModelPrefab;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Material metalMaterial;
    [SerializeField] private Material polymerMaterial;
    [SerializeField] private Material accentMaterial;

    [Header("Procedural fallback")]
    [SerializeField] private GameObject proceduralRoot;

    [Header("Weapon points (weapon-root space)")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform shellEjectPoint;
    [SerializeField] private Vector3 fallbackMuzzleLocal;
    [SerializeField] private Vector3 fallbackEjectLocal;

    private GameObject importedInstance;

    public bool IsBuilt { get; private set; }
    public bool UsesImportedModel => importedInstance != null;
    public GameObject ImportedInstance => importedInstance;
    public GameObject ProceduralRoot => proceduralRoot;
    public Transform VisualRoot => visualRoot != null ? visualRoot : transform;

    private void Awake()
    {
        Build();
    }

    private void OnDestroy()
    {
        importedInstance = null;
        IsBuilt = false;
    }

    /// <summary>Wires the model. Intended for code that assembles the weapon without the prefab.</summary>
    public void SetImportedModel(GameObject prefab, Transform parent)
    {
        importedModelPrefab = prefab;
        visualRoot = parent;
    }

    /// <summary>Instantiates the imported model, or falls back to the procedural group. Safe to call twice.</summary>
    public void Build()
    {
        if (IsBuilt)
            return;

        if (TryBuildImported())
        {
            IsBuilt = true;
            return;
        }

        UseProcedural();
        IsBuilt = true;
    }

    private bool TryBuildImported()
    {
        if (importedModelPrefab == null)
            return false;

        GameObject instance = null;
        try
        {
            instance = Instantiate(importedModelPrefab, VisualRoot);
            instance.name = importedModelPrefab.name + "Imported";
            Transform t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            if (!HasMesh(instance) || FindDescendant(t, MagazineName) == null || FindDescendant(t, BoltName) == null)
            {
                DestroyObject(instance);
                return false;
            }

            ApplyMaterials(instance);
            RenameProceduralReloadParts();
            if (proceduralRoot != null)
                proceduralRoot.SetActive(false);
            importedInstance = instance;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"AssaultWeaponModel: imported model failed ({e.GetType().Name}: {e.Message}); using procedural meshes.", this);
            if (instance != null)
                DestroyObject(instance);
            return false;
        }
    }

    private void UseProcedural()
    {
        if (proceduralRoot != null)
            proceduralRoot.SetActive(true);
        if (muzzlePoint != null)
            muzzlePoint.localPosition = fallbackMuzzleLocal;
        if (firePoint != null)
            firePoint.localPosition = fallbackMuzzleLocal;
        if (shellEjectPoint != null)
            shellEjectPoint.localPosition = fallbackEjectLocal;
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

    private void RenameProceduralReloadParts()
    {
        if (proceduralRoot == null)
            return;

        RenameDescendant(proceduralRoot.transform, MagazineName);
        RenameDescendant(proceduralRoot.transform, BoltName);
    }

    private static void RenameDescendant(Transform root, string targetName)
    {
        Transform found = FindDescendant(root, targetName);
        if (found != null)
            found.name = targetName + ProceduralSuffix;
    }

    /// <summary>Depth-first, first match, the same lookup <see cref="WeaponController"/> uses.</summary>
    public static Transform FindDescendant(Transform root, string targetName)
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
        if (materialName.IndexOf("Metal", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return metalMaterial;
        if (materialName.IndexOf("Polymer", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return polymerMaterial;
        if (materialName.IndexOf("Accent", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return accentMaterial;
        return null;
    }

    private static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying)
        {
            go.SetActive(false);
            go.transform.SetParent(null, false);
            Destroy(go);
        }
        else
            DestroyImmediate(go);
    }
}
