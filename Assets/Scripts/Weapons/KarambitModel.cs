using UnityEngine;

/// <summary>
/// Shows the karambit's imported Blender model (Assets/Weapons/Models/Karambit/Karambit.fbx).
/// If that model is not assigned or cannot be instantiated, falls back to building the
/// procedural meshes from <see cref="KarambitMeshBuilder"/> and assigning them to the prefab's
/// MeshFilters.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class KarambitModel : MonoBehaviour
{
    [Header("Imported model (preferred)")]
    [SerializeField] private GameObject importedModelPrefab;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Material bladeMaterial;
    [SerializeField] private Material handleMaterial;
    [SerializeField] private Material accentMaterial;

    [Header("Procedural fallback")]
    [SerializeField] private MeshFilter bladeFilter;
    [SerializeField] private MeshFilter handleFilter;
    [SerializeField] private MeshFilter bolsterFilter;
    [SerializeField] private MeshFilter ringFilter;

    private Mesh bladeMesh;
    private Mesh handleMesh;
    private Mesh bolsterMesh;
    private Mesh ringMesh;

    private GameObject importedInstance;

    public bool IsBuilt { get; private set; }
    public bool UsesImportedModel => importedInstance != null;
    public GameObject ImportedInstance => importedInstance;
    public Mesh BladeMesh => bladeMesh;
    public Mesh HandleMesh => handleMesh;
    public Mesh BolsterMesh => bolsterMesh;
    public Mesh RingMesh => ringMesh;

    private void Awake()
    {
        Build();
    }

    private void OnDestroy()
    {
        DestroyMesh(ref bladeMesh);
        DestroyMesh(ref handleMesh);
        DestroyMesh(ref bolsterMesh);
        DestroyMesh(ref ringMesh);
        importedInstance = null;
        IsBuilt = false;
    }

    /// <summary>
    /// Wires the mesh filters. Intended for code that assembles the knife without the prefab.
    /// </summary>
    public void SetFilters(MeshFilter blade, MeshFilter handle, MeshFilter bolster, MeshFilter ring)
    {
        bladeFilter = blade;
        handleFilter = handle;
        bolsterFilter = bolster;
        ringFilter = ring;
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

        ShiftProceduralToGrip();
        Assign(bladeFilter, ref bladeMesh, () => KarambitMeshBuilder.BuildBlade());
        Assign(handleFilter, ref handleMesh, () => KarambitMeshBuilder.BuildHandle());
        Assign(bolsterFilter, ref bolsterMesh, KarambitMeshBuilder.BuildBolster);
        Assign(ringFilter, ref ringMesh, () => KarambitMeshBuilder.BuildRing());
        IsBuilt = true;
    }

    /// <summary>
    /// Wires the imported model. Intended for code that assembles the knife without the prefab.
    /// </summary>
    public void SetImportedModel(GameObject prefab, Transform parent)
    {
        importedModelPrefab = prefab;
        visualRoot = parent;
    }

    private bool TryBuildImported()
    {
        if (importedModelPrefab == null)
            return false;

        Transform parent = visualRoot != null ? visualRoot : transform;
        GameObject instance = null;
        try
        {
            instance = Instantiate(importedModelPrefab, parent);
            instance.name = "KarambitImported";
            Transform t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
            bool hasMesh = false;
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh != null)
                {
                    hasMesh = true;
                    break;
                }
            }

            if (!hasMesh)
            {
                DestroyObject(instance);
                return false;
            }

            ApplyMaterials(instance);
            SetProceduralVisible(false);
            importedInstance = instance;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"KarambitModel: imported model failed ({e.GetType().Name}: {e.Message}); using procedural meshes.", this);
            if (instance != null)
                DestroyObject(instance);
            return false;
        }
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
        if (materialName.IndexOf("Blade", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return bladeMaterial;
        if (materialName.IndexOf("Handle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return handleMaterial;
        if (materialName.IndexOf("Accent", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return accentMaterial;
        return null;
    }

    private void SetProceduralVisible(bool visible)
    {
        SetFilterVisible(bladeFilter, visible);
        SetFilterVisible(handleFilter, visible);
        SetFilterVisible(bolsterFilter, visible);
        SetFilterVisible(ringFilter, visible);
    }

    /// <summary>
    /// The procedural meshes are authored with the origin at the bolster; the imported model has
    /// its origin at the centre of the grip. Shifting the parts keeps the fallback gripped in the
    /// same place.
    /// </summary>
    private void ShiftProceduralToGrip()
    {
        Vector3 shift = new Vector3(0f, 0f, KarambitMeshBuilder.HandleLength * 0.5f);
        ShiftFilter(bladeFilter, shift);
        ShiftFilter(handleFilter, shift);
        ShiftFilter(bolsterFilter, shift);
        ShiftFilter(ringFilter, shift);
    }

    private static void ShiftFilter(MeshFilter filter, Vector3 shift)
    {
        if (filter != null)
            filter.transform.localPosition = shift;
    }

    private static void SetFilterVisible(MeshFilter filter, bool visible)
    {
        if (filter != null)
            filter.gameObject.SetActive(visible);
    }

    private static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    private static void Assign(MeshFilter filter, ref Mesh target, System.Func<Mesh> factory)
    {
        if (filter == null)
            return;

        target = factory();
        filter.sharedMesh = target;
    }

    private static void DestroyMesh(ref Mesh mesh)
    {
        if (mesh == null)
            return;

        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
        mesh = null;
    }
}
