using UnityEngine;

/// <summary>
/// Minimal FPS body hook: hides selected third-person meshes from the local
/// first-person camera (typically the head) via layer culling so they do not
/// clip the view, while remaining visible to other cameras and third-person views.
/// Shadows come from <see cref="LocalBodyShadowProxies"/>, because culled layers cast no shadows. Does not touch MovementController or CharacterController.
/// </summary>
public class LocalPlayerBodyVisibility : MonoBehaviour
{
    [Header("Local player")]
    [SerializeField] private bool isLocalPlayer = true;
    [SerializeField] private Camera playerCamera;
    [Tooltip("Layer name used to hide parts from the local camera. Must exist in Tags & Layers.")]
    [SerializeField] private string hideLayerName = "PlayerLocalHide";

    [Header("Parts to hide from local FP camera")]
    [SerializeField] private Transform[] hideFromLocalCamera;

    [Header("Fallback if layer missing")]
    [Tooltip("If the hide layer is not defined, disable MeshRenderers on hide targets instead.")]
    [SerializeField] private bool disableRenderersWhenLayerMissing = true;

    private int hideLayer = -1;
    private bool awoken;

    public Transform[] HideTargets => hideFromLocalCamera;

    private void Awake()
    {
        awoken = true;
        if (!isLocalPlayer)
            return;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        hideLayer = LayerMask.NameToLayer(hideLayerName);
        ApplyLocalVisibility();
    }

    /// <summary>
    /// Replaces the set of parts hidden from the local camera (used when the body is generated at runtime).
    /// Applies immediately when this component has already initialised; otherwise Awake applies it.
    /// </summary>
    public void SetHideTargets(Transform[] targets)
    {
        hideFromLocalCamera = targets;
        if (awoken)
            ApplyLocalVisibility();
    }

    /// <summary>
    /// Call after swapping body meshes at runtime if needed.
    /// </summary>
    public void ApplyLocalVisibility()
    {
        if (!isLocalPlayer)
            return;

        if (hideLayer >= 0)
        {
            ApplyLayerToTargets(hideLayer);
            CullHideLayerFromLocalCamera();
            EnsureShadowProxies();
            return;
        }

        Debug.LogWarning(
            $"[LocalPlayerBodyVisibility] Layer '{hideLayerName}' not found. " +
            "Add it under Edit > Project Settings > Tags and Layers (user layer 8+).",
            this);

        if (disableRenderersWhenLayerMissing)
            SetTargetRenderersEnabled(false);
    }

    /// <summary>
    /// For remote / third-person players: show full body to every camera.
    /// </summary>
    public void ConfigureAsRemotePlayer()
    {
        isLocalPlayer = false;
        ApplyLayerToTargets(0); // Default
        SetTargetRenderersEnabled(true);
    }

    private void ApplyLayerToTargets(int layer)
    {
        if (hideFromLocalCamera == null)
            return;

        foreach (var t in hideFromLocalCamera)
        {
            if (t == null)
                continue;
            SetLayerRecursive(t.gameObject, layer);
        }
    }

    // The camera's culling mask also culls shadow casters, so hidden parts get shadows-only twins.
    private void EnsureShadowProxies()
    {
        if (!Application.isPlaying)
            return;

        var proxies = GetComponent<LocalBodyShadowProxies>();
        if (proxies == null)
            proxies = gameObject.AddComponent<LocalBodyShadowProxies>();
        proxies.Refresh();
    }

    private void CullHideLayerFromLocalCamera()
    {
        if (playerCamera == null || hideLayer < 0)
            return;

        playerCamera.cullingMask &= ~(1 << hideLayer);
    }

    private void SetTargetRenderersEnabled(bool enabled)
    {
        if (hideFromLocalCamera == null)
            return;

        foreach (var t in hideFromLocalCamera)
        {
            if (t == null)
                continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                r.enabled = enabled;
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        var transform = go.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.name == LocalBodyShadowProxies.ProxyName)
                continue;
            SetLayerRecursive(child, layer);
        }
    }
}
