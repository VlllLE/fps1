using UnityEngine;

/// <summary>
/// Builds the karambit's procedural meshes at runtime and assigns them to the prefab's
/// MeshFilters, so the knife needs no imported model asset.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class KarambitModel : MonoBehaviour
{
    [SerializeField] private MeshFilter bladeFilter;
    [SerializeField] private MeshFilter handleFilter;
    [SerializeField] private MeshFilter bolsterFilter;
    [SerializeField] private MeshFilter ringFilter;

    private Mesh bladeMesh;
    private Mesh handleMesh;
    private Mesh bolsterMesh;
    private Mesh ringMesh;

    public bool IsBuilt { get; private set; }
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

        Assign(bladeFilter, ref bladeMesh, () => KarambitMeshBuilder.BuildBlade());
        Assign(handleFilter, ref handleMesh, () => KarambitMeshBuilder.BuildHandle());
        Assign(bolsterFilter, ref bolsterMesh, KarambitMeshBuilder.BuildBolster);
        Assign(ringFilter, ref ringMesh, () => KarambitMeshBuilder.BuildRing());
        IsBuilt = true;
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
