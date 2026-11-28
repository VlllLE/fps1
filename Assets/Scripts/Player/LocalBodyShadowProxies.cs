using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps the local player's own body (and the gun it holds) casting a shadow in first person.
/// The body sits on the hidden-from-first-person layer, but the built-in render pipeline culls shadow
/// casters with the camera's culling mask, so anything on that layer casts no shadow at all. Every
/// shadow-casting renderer on that layer therefore gets a shadows-only twin on the default layer: it
/// shares the source's mesh, materials and bones, is never drawn to any camera, and is destroyed
/// together with its source. The first-person viewmodel gun stops casting its own shadow once the held
/// gun's twin does, so the gun is not shadowed twice.
/// </summary>
public sealed class LocalBodyShadowProxies : MonoBehaviour
{
    public const string ProxyName = "ShadowProxy";

    private const float RescanInterval = 0.2f;

    [SerializeField] private string hideLayerName = "PlayerLocalHide";

    private readonly Dictionary<Renderer, Renderer> proxies = new Dictionary<Renderer, Renderer>();
    private readonly List<Renderer> scratch = new List<Renderer>();
    private readonly List<Renderer> stale = new List<Renderer>();
    private int hideLayer = -1;
    private float nextScan;

    public int ProxyCount => proxies.Count;

    public static bool IsShadowProxy(Renderer renderer)
    {
        return renderer != null && renderer.name == ProxyName && renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly;
    }

    private void Awake()
    {
        hideLayer = LayerMask.NameToLayer(hideLayerName);
    }

    private void OnEnable()
    {
        nextScan = 0f;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + RescanInterval;
        Refresh();
    }

    /// <summary>Creates, syncs and prunes the shadow twins. Safe to call at any time.</summary>
    public void Refresh()
    {
        if (hideLayer < 0)
            hideLayer = LayerMask.NameToLayer(hideLayerName);
        if (hideLayer < 0)
            return;

        stale.Clear();
        foreach (var pair in proxies)
        {
            if (pair.Key == null || pair.Value == null)
                stale.Add(pair.Key);
        }
        for (int i = 0; i < stale.Count; i++)
        {
            Renderer key = stale[i];
            if (key != null && proxies[key] != null)
                Destroy(proxies[key].gameObject);
            proxies.Remove(key);
        }

        bool gunHasTwin = false;
        GetComponentsInChildren(true, scratch);
        for (int i = 0; i < scratch.Count; i++)
        {
            Renderer source = scratch[i];
            if (source.gameObject.layer != hideLayer || IsShadowProxy(source))
                continue;
            if (!(source is MeshRenderer) && !(source is SkinnedMeshRenderer))
                continue;

            if (!proxies.TryGetValue(source, out Renderer proxy) || proxy == null)
            {
                if (source.shadowCastingMode == ShadowCastingMode.Off)
                    continue;
                proxy = CreateProxy(source);
                if (proxy == null)
                    continue;
                proxies[source] = proxy;
            }

            Sync(source, proxy);
            if (proxy.gameObject.layer != 0)
                proxy.gameObject.layer = 0;
            if (IsUnderHeldGun(source.transform))
                gunHasTwin = true;
        }

        if (gunHasTwin)
            SilenceViewmodelShadows();
    }

    private static Renderer CreateProxy(Renderer source)
    {
        var go = new GameObject(ProxyName);
        go.transform.SetParent(source.transform, false);
        go.layer = 0;

        Renderer proxy;
        if (source is SkinnedMeshRenderer skinned)
        {
            if (skinned.sharedMesh == null)
            {
                Destroy(go);
                return null;
            }

            var twin = go.AddComponent<SkinnedMeshRenderer>();
            twin.sharedMesh = skinned.sharedMesh;
            twin.bones = skinned.bones;
            twin.rootBone = skinned.rootBone;
            twin.localBounds = skinned.localBounds;
            twin.updateWhenOffscreen = true;
            proxy = twin;
        }
        else
        {
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                Destroy(go);
                return null;
            }

            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            proxy = go.AddComponent<MeshRenderer>();
        }

        proxy.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        proxy.receiveShadows = false;
        proxy.lightProbeUsage = LightProbeUsage.Off;
        proxy.reflectionProbeUsage = ReflectionProbeUsage.Off;
        proxy.sharedMaterials = source.sharedMaterials;
        return proxy;
    }

    private static void Sync(Renderer source, Renderer proxy)
    {
        proxy.enabled = source.enabled && source.shadowCastingMode != ShadowCastingMode.Off;

        if (source is MeshRenderer && proxy.TryGetComponent(out MeshFilter proxyFilter)
            && source.TryGetComponent(out MeshFilter sourceFilter) && proxyFilter.sharedMesh != sourceFilter.sharedMesh)
            proxyFilter.sharedMesh = sourceFilter.sharedMesh;

        if (proxy.sharedMaterial != source.sharedMaterial)
            proxy.sharedMaterials = source.sharedMaterials;
    }

    private static bool IsUnderHeldGun(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name == ThirdPersonGun.RootName)
                return true;
        }

        return false;
    }

    private void SilenceViewmodelShadows()
    {
        var weapons = GetComponentsInChildren<WeaponController>(true);
        for (int w = 0; w < weapons.Length; w++)
        {
            weapons[w].GetComponentsInChildren(true, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                Renderer r = scratch[i];
                if (r.gameObject.layer != hideLayer && !(r is ParticleSystemRenderer) && r.shadowCastingMode == ShadowCastingMode.On)
                    r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
