using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies a <see cref="WeaponSkinDefinition"/> to every Renderer under this weapon
/// (or an explicit target root). Works with any mesh: skins use object-space triplanar
/// materials and do not depend on weapon UVs.
///
/// Attach beside a WeaponController, or call <see cref="ApplySkin"/> from UI / loadout code.
/// </summary>
[DisallowMultipleComponent]
public class WeaponSkinApplier : MonoBehaviour
{
    [SerializeField] private WeaponSkinDefinition defaultSkin;
    [SerializeField] private bool applyDefaultOnAwake = true;
    [Tooltip("If set, only renderers under this transform are skinned. Defaults to this object.")]
    [SerializeField] private Transform rendererRoot;
    [Tooltip("When true, child renderers are included.")]
    [SerializeField] private bool includeInactiveRenderers = true;

    private readonly List<Renderer> cachedRenderers = new List<Renderer>(8);
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

    private WeaponSkinDefinition currentSkin;

    public WeaponSkinDefinition CurrentSkin => currentSkin;
    public WeaponSkinDefinition DefaultSkin => defaultSkin;

    private void Awake()
    {
        CacheRenderers();
        CaptureOriginalMaterials();

        if (applyDefaultOnAwake && defaultSkin != null)
            ApplySkin(defaultSkin);
    }

    /// <summary>
    /// Applies the given skin material to every cached renderer. Pass null to restore originals.
    /// </summary>
    public void ApplySkin(WeaponSkinDefinition skin)
    {
        if (cachedRenderers.Count == 0)
            CacheRenderers();

        if (skin == null || skin.material == null)
        {
            RestoreOriginalMaterials();
            currentSkin = null;
            return;
        }

        currentSkin = skin;
        propertyBlock.Clear();
        if (skin.applyParameterOverrides)
            skin.WritePropertyBlock(propertyBlock);

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
                continue;

            int slotCount = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 1;
            if (slotCount <= 0)
                slotCount = 1;

            Material[] materials = new Material[slotCount];
            for (int slot = 0; slot < slotCount; slot++)
                materials[slot] = skin.material;

            renderer.sharedMaterials = materials;

            if (skin.applyParameterOverrides)
                renderer.SetPropertyBlock(propertyBlock);
            else
                renderer.SetPropertyBlock(null);
        }
    }

    public void ClearSkin()
    {
        ApplySkin(null);
    }

    public void ReapplyCurrentSkin()
    {
        ApplySkin(currentSkin);
    }

    /// <summary>
    /// Rebuilds the renderer list. Call after weapon mesh children change at runtime.
    /// </summary>
    public void RefreshRenderers()
    {
        CacheRenderers();
        CaptureOriginalMaterials();
        if (currentSkin != null)
            ApplySkin(currentSkin);
    }

    private void CacheRenderers()
    {
        cachedRenderers.Clear();
        Transform root = rendererRoot != null ? rendererRoot : transform;
        Renderer[] found = root.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
        for (int i = 0; i < found.Length; i++)
        {
            // Skip particle / trail systems — they are VFX, not weapon body meshes.
            if (found[i] is ParticleSystemRenderer || found[i] is TrailRenderer || found[i] is LineRenderer)
                continue;
            cachedRenderers.Add(found[i]);
        }
    }

    private void CaptureOriginalMaterials()
    {
        originalMaterials.Clear();
        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
                continue;
            Material[] shared = renderer.sharedMaterials;
            Material[] copy = new Material[shared.Length];
            for (int slot = 0; slot < shared.Length; slot++)
                copy[slot] = shared[slot];
            originalMaterials[renderer] = copy;
        }
    }

    private void RestoreOriginalMaterials()
    {
        foreach (KeyValuePair<Renderer, Material[]> pair in originalMaterials)
        {
            if (pair.Key == null)
                continue;
            pair.Key.sharedMaterials = pair.Value;
            pair.Key.SetPropertyBlock(null);
        }
    }
}
