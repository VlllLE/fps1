using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders a slowly rotating weapon (with an optional skin) into a RenderTexture for the loadout menu.
/// The weapon prefab is instantiated under an inactive holder so none of its gameplay scripts ever run; only its
/// meshes are drawn, with <see cref="Graphics.DrawMesh(Mesh, Matrix4x4, Material, int, Camera, int, MaterialPropertyBlock)"/>
/// by a dedicated camera. Skins go through <see cref="WeaponSkinApplier"/>, exactly like the in-hand weapon.
/// </summary>
public sealed class WeaponPreviewRig : IDisposable
{
    public const int PreviewLayer = 31;

    private static readonly Vector3 Origin = new Vector3(0f, -4000f, 0f);

    private sealed class Part
    {
        public Renderer renderer;
        public Mesh mesh;
        public Matrix4x4 local;
        public Material[] materials;
        public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    }

    private readonly GameObject root;
    private readonly Camera previewCamera;
    private readonly RenderTexture texture;
    private readonly GameObject holder;
    private readonly List<Part> parts = new List<Part>(8);

    private GameObject currentPrefab;
    private GameObject instance;
    private WeaponSkinApplier skinApplier;
    private WeaponSkinDefinition shownSkin;
    private bool hasShownSkin;
    private Vector3 center;
    private float radius = 0.5f;
    private float yaw = 55f;
    private bool disposed;

    public WeaponPreviewRig(int width = 640, int height = 360)
    {
        root = new GameObject("WeaponPreviewRig");
        root.transform.position = Origin;

        var cameraObject = new GameObject("PreviewCamera");
        cameraObject.transform.SetParent(root.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.09f, 0.1f, 0.13f, 1f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.fieldOfView = 28f;
        previewCamera.nearClipPlane = 0.02f;
        previewCamera.farClipPlane = 60f;
        previewCamera.allowHDR = false;
        previewCamera.allowMSAA = false;
        previewCamera.useOcclusionCulling = false;
        previewCamera.depth = -50f;

        texture = new RenderTexture(Mathf.Max(64, width), Mathf.Max(64, height), 24, RenderTextureFormat.ARGB32)
        {
            name = "WeaponPreviewTexture",
            antiAliasing = 4
        };
        previewCamera.targetTexture = texture;
        previewCamera.enabled = false;

        CreateLight("PreviewKeyLight", Quaternion.Euler(40f, -35f, 0f), 1.25f, new Color(1f, 0.97f, 0.92f));
        CreateLight("PreviewFillLight", Quaternion.Euler(-20f, 150f, 0f), 0.6f, new Color(0.7f, 0.8f, 1f));

        holder = new GameObject("PreviewHolder");
        holder.transform.SetParent(root.transform, false);
        holder.SetActive(false);
    }

    public RenderTexture Texture => texture;
    public GameObject CurrentPrefab => currentPrefab;
    public WeaponSkinDefinition ShownSkin => shownSkin;
    public int PartCount => parts.Count;
    public Camera PreviewCamera => previewCamera;
    public bool IsActive => previewCamera != null && previewCamera.enabled;

    /// <summary>Material currently shown on the first mesh part (null when nothing is loaded). For tests/diagnostics.</summary>
    public Material FirstPartMaterial
    {
        get
        {
            if (parts.Count == 0 || parts[0].materials == null || parts[0].materials.Length == 0)
                return null;
            return parts[0].materials[0];
        }
    }

    public void SetActive(bool active)
    {
        if (previewCamera != null)
            previewCamera.enabled = active && !disposed;
    }

    /// <summary>Shows <paramref name="prefab"/> (with its default skin). Null clears the preview.</summary>
    public void SetPrefab(GameObject prefab)
    {
        if (disposed || prefab == currentPrefab)
            return;

        ClearInstance();
        currentPrefab = prefab;
        if (prefab == null)
            return;

        instance = UnityEngine.Object.Instantiate(prefab, holder.transform);
        instance.name = prefab.name + "_Preview";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        // Procedural meshes (karambit) are normally built in Awake, which never runs on the inactive preview copy.
        KarambitModel karambit = instance.GetComponentInChildren<KarambitModel>(true);
        if (karambit != null)
            karambit.Build();

        skinApplier = instance.GetComponent<WeaponSkinApplier>();
        if (skinApplier == null)
            skinApplier = instance.GetComponentInChildren<WeaponSkinApplier>(true);
        if (skinApplier == null)
            skinApplier = instance.AddComponent<WeaponSkinApplier>();

        BuildParts();

        hasShownSkin = false;
        SetSkin(null);
    }

    /// <summary>Shows <paramref name="skin"/> on the preview weapon; null shows the weapon's default skin.</summary>
    public void SetSkin(WeaponSkinDefinition skin)
    {
        if (disposed || instance == null || skinApplier == null)
            return;
        if (hasShownSkin && skin == shownSkin)
            return;

        hasShownSkin = true;
        shownSkin = skin;
        skinApplier.ApplySkin(skin != null ? skin : skinApplier.DefaultSkin);
        RefreshMaterials();
    }

    /// <summary>Advances the turntable and queues the weapon meshes for the preview camera. Call once per frame.</summary>
    public void Tick(float deltaTime)
    {
        if (disposed || instance == null || parts.Count == 0)
            return;

        yaw = Mathf.Repeat(yaw + deltaTime * 28f, 360f);

        float halfFov = previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float distance = radius / Mathf.Sin(halfFov) * 1.12f;
        previewCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
        previewCamera.transform.localRotation = Quaternion.identity;

        Matrix4x4 pivot = Matrix4x4.TRS(Origin, Quaternion.Euler(12f, yaw, 0f), Vector3.one)
                          * Matrix4x4.Translate(-center);

        for (int i = 0; i < parts.Count; i++)
        {
            Part part = parts[i];
            if (part.mesh == null)
                continue;

            Matrix4x4 matrix = pivot * part.local;
            int subMeshes = part.mesh.subMeshCount;
            Material[] materials = part.materials;
            if (materials == null || materials.Length == 0)
                continue;

            for (int sub = 0; sub < subMeshes; sub++)
            {
                Material material = materials[Mathf.Min(sub, materials.Length - 1)];
                if (material == null)
                    continue;
                Graphics.DrawMesh(part.mesh, matrix, material, PreviewLayer, previewCamera, sub, part.block);
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        ClearInstance();

        if (previewCamera != null)
            previewCamera.targetTexture = null;
        if (texture != null)
        {
            texture.Release();
            DestroyObject(texture);
        }

        if (root != null)
            DestroyObject(root);
    }

    private void CreateLight(string lightName, Quaternion rotation, float intensity, Color color)
    {
        var lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localRotation = rotation;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << PreviewLayer;
    }

    private void BuildParts()
    {
        parts.Clear();
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        Matrix4x4 worldToRoot = instance.transform.worldToLocalMatrix;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !ActiveWithin(renderer.transform, instance.transform))
                continue;

            Mesh mesh = null;
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null)
                    mesh = filter.sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }

            if (mesh == null)
                continue;

            var part = new Part
            {
                renderer = renderer,
                mesh = mesh,
                local = worldToRoot * renderer.transform.localToWorldMatrix
            };
            parts.Add(part);

            Bounds meshBounds = mesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 transformed = part.local.MultiplyPoint3x4(point);
                if (!hasBounds)
                {
                    bounds = new Bounds(transformed, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(transformed);
                }
            }
        }

        center = hasBounds ? bounds.center : Vector3.zero;
        radius = hasBounds ? Mathf.Max(0.05f, bounds.extents.magnitude) : 0.5f;
        RefreshMaterials();
    }

    private void RefreshMaterials()
    {
        for (int i = 0; i < parts.Count; i++)
        {
            Part part = parts[i];
            if (part.renderer == null)
                continue;
            part.materials = part.renderer.sharedMaterials;
            part.renderer.GetPropertyBlock(part.block);
        }
    }

    private static bool ActiveWithin(Transform child, Transform rootTransform)
    {
        Transform current = child;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                return false;
            if (current == rootTransform)
                return true;
            current = current.parent;
        }

        return true;
    }

    private void ClearInstance()
    {
        parts.Clear();
        skinApplier = null;
        shownSkin = null;
        hasShownSkin = false;
        currentPrefab = null;

        if (instance == null)
            return;

        // The inactive copy never ran OnDestroy, so release the knife's procedural meshes explicitly.
        KarambitModel karambit = instance.GetComponentInChildren<KarambitModel>(true);
        if (karambit != null)
        {
            DestroyObject(karambit.BladeMesh);
            DestroyObject(karambit.HandleMesh);
            DestroyObject(karambit.BolsterMesh);
            DestroyObject(karambit.RingMesh);
        }

        DestroyObject(instance);
        instance = null;
    }

    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }
}
