using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies a <see cref="WeaponSkinDefinition"/> to every Renderer under this weapon
/// (or an explicit target root). Works with any mesh: skins use object-space triplanar
/// materials and do not depend on weapon UVs.
///
/// Optical glass (scope and sight lenses, see <see cref="WeaponGlass"/>) is never skinned or restored: those
/// material slots keep their transparent glass material.
///
/// Attach beside a WeaponController, or call <see cref="ApplySkin"/> from UI / loadout code.
/// The skin is re-applied whenever the weapon is enabled, so holster/equip cycles and
/// loadout swaps never fall back to the default grey materials.
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
    private readonly Dictionary<Material, Material> fallbackMaterials = new Dictionary<Material, Material>();
    private MaterialPropertyBlock propertyBlock;
    private MaterialPropertyBlock PropertyBlock => propertyBlock ??= new MaterialPropertyBlock();

    private WeaponSkinDefinition currentSkin;
    private bool initialized;

    public WeaponSkinDefinition CurrentSkin => currentSkin;
    public WeaponSkinDefinition DefaultSkin => defaultSkin;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        if (currentSkin != null)
            ApplySkin(currentSkin);
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<Material, Material> pair in fallbackMaterials)
        {
            if (pair.Value != null)
                Destroy(pair.Value);
        }
        fallbackMaterials.Clear();
    }

    /// <summary>
    /// Assigns the default skin used on Awake. Intended for loadout code and tests.
    /// </summary>
    public void SetDefaultSkin(WeaponSkinDefinition skin, bool applyNow)
    {
        defaultSkin = skin;
        if (applyNow)
            ApplySkin(skin);
    }

    /// <summary>
    /// Applies the given skin material to every cached renderer. Pass null to restore originals.
    /// </summary>
    public void ApplySkin(WeaponSkinDefinition skin)
    {
        ApplySkinToRenderers(skin);
        HeldGunSkins.NotifySkinChanged(gameObject);
    }

    private void ApplySkinToRenderers(WeaponSkinDefinition skin)
    {
        if (!initialized)
            Initialize(applyDefault: false);

        // Weapons such as the Karambit build their meshes in Awake, after this component may have cached
        // its renderers, so pick up any renderers added since the last call.
        CacheRenderers();
        CaptureOriginalMaterials();

        if (skin == null || skin.material == null)
        {
            RestoreOriginalMaterials();
            currentSkin = null;
            return;
        }

        currentSkin = skin;
        Material material = ResolveRenderableMaterial(skin);

        var block = PropertyBlock;
        block.Clear();
        if (skin.applyParameterOverrides)
            skin.WritePropertyBlock(block);

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
                continue;

            Material[] current = renderer.sharedMaterials;
            int slotCount = current != null ? current.Length : 1;
            if (slotCount <= 0)
                slotCount = 1;

            originalMaterials.TryGetValue(renderer, out Material[] originals);
            Material[] materials = new Material[slotCount];
            bool[] glass = new bool[slotCount];
            for (int slot = 0; slot < slotCount; slot++)
            {
                glass[slot] = IsGlassSlot(renderer, originals, current, slot);
                materials[slot] = glass[slot] ? GlassMaterialAt(originals, current, slot) : material;
            }

            renderer.sharedMaterials = materials;

            // The block is written per slot: a skin's _Color / _Glossiness overrides must never reach a lens.
            renderer.SetPropertyBlock(null);
            if (skin.applyParameterOverrides)
            {
                for (int slot = 0; slot < slotCount; slot++)
                {
                    if (!glass[slot])
                        renderer.SetPropertyBlock(block, slot);
                }
            }
        }
    }

    /// <summary>
    /// Number of material slots on this weapon that are optical glass and therefore never skinned (for diagnostics and tests).
    /// </summary>
    public int GlassSlotCount
    {
        get
        {
            if (!initialized)
                Initialize(applyDefault: false);
            CacheRenderers();
            CaptureOriginalMaterials();

            int count = 0;
            for (int i = 0; i < cachedRenderers.Count; i++)
            {
                Renderer renderer = cachedRenderers[i];
                if (renderer == null)
                    continue;
                Material[] current = renderer.sharedMaterials;
                originalMaterials.TryGetValue(renderer, out Material[] originals);
                for (int slot = 0; slot < current.Length; slot++)
                {
                    if (IsGlassSlot(renderer, originals, current, slot))
                        count++;
                }
            }

            return count;
        }
    }

    private static bool IsGlassSlot(Renderer renderer, Material[] originals, Material[] current, int slot)
    {
        Material original = originals != null && slot < originals.Length ? originals[slot] : null;
        if (original == null && current != null && slot < current.Length)
            original = current[slot];
        return WeaponGlass.IsGlassSlot(renderer, original);
    }

    private static Material GlassMaterialAt(Material[] originals, Material[] current, int slot)
    {
        if (originals != null && slot < originals.Length && originals[slot] != null)
            return originals[slot];
        return current != null && slot < current.Length ? current[slot] : null;
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

    /// <summary>
    /// Number of renderers this applier currently skins (for diagnostics and tests).
    /// </summary>
    public int RendererCount
    {
        get
        {
            if (!initialized)
                Initialize(applyDefault: false);
            return cachedRenderers.Count;
        }
    }

    /// <summary>
    /// Returns the skin material, or a Standard-shader stand-in carrying the skin texture
    /// and tint when the skin's custom shader cannot render on this machine / pipeline.
    /// </summary>
    public Material ResolveRenderableMaterial(WeaponSkinDefinition skin)
    {
        if (skin == null || skin.material == null)
            return null;

        Material source = skin.material;
        if (IsShaderUsable(source))
            return source;

        if (fallbackMaterials.TryGetValue(source, out Material cached) && cached != null)
            return cached;

        Shader standard = Shader.Find("Standard");
        if (standard == null)
            return source;

        var fallback = new Material(standard) { name = source.name + "_Fallback" };
        Color tint = Color.white;
        if (source.HasProperty("_SkinColor"))
            tint = source.GetColor("_SkinColor");
        else if (source.HasProperty("_Color"))
            tint = source.GetColor("_Color");
        fallback.color = tint;

        Texture albedo = null;
        if (source.HasProperty("_MainTex"))
            albedo = source.GetTexture("_MainTex");
        if (albedo == null && source.HasProperty("_NoiseTex"))
            albedo = source.GetTexture("_NoiseTex");
        if (albedo != null)
            fallback.mainTexture = albedo;

        fallback.SetFloat("_Metallic", source.HasProperty("_Metallic") ? Mathf.Min(source.GetFloat("_Metallic"), 0.5f) : 0.2f);
        fallback.SetFloat("_Glossiness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.4f);

        fallbackMaterials[source] = fallback;
        return fallback;
    }

    private static bool IsShaderUsable(Material material)
    {
        Shader shader = material.shader;
        return shader != null && shader.isSupported;
    }

    private void Initialize(bool applyDefault = true)
    {
        if (initialized)
            return;

        initialized = true;
        CacheRenderers();
        CaptureOriginalMaterials();

        if (applyDefault && applyDefaultOnAwake && defaultSkin != null)
            ApplySkin(defaultSkin);
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
        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || originalMaterials.ContainsKey(renderer))
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

            // Glass slots were never skinned, so whatever material they hold now (including a tuned lens) stays.
            Material[] restored = (Material[])pair.Value.Clone();
            Material[] current = pair.Key.sharedMaterials;
            for (int slot = 0; slot < restored.Length && slot < current.Length; slot++)
            {
                if (WeaponGlass.IsGlassSlot(pair.Key, restored[slot]))
                    restored[slot] = current[slot];
            }

            pair.Key.sharedMaterials = restored;
            pair.Key.SetPropertyBlock(null);
        }
    }
}
