using UnityEngine;

/// <summary>
/// Describes a weapon skin: display data plus the material applied to renderers.
/// Create via Assets &gt; Create &gt; Skins &gt; Weapon Skin.
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponSkin", menuName = "Skins/Weapon Skin")]
public class WeaponSkinDefinition : ScriptableObject
{
    public enum SkinRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    [Header("Identity")]
    public string skinName = "Skin";
    [TextArea(2, 4)]
    public string description;
    public SkinRarity rarity = SkinRarity.Common;
    public Sprite icon;

    [Header("Appearance")]
    [Tooltip("Material used when this skin is applied. Static skins use TriplanarStatic; animated skins use one of the Skins/* shaders.")]
    public Material material;

    [Tooltip("If true, runtime parameter overrides below are written into a MaterialPropertyBlock each time the skin is applied.")]
    public bool applyParameterOverrides;

    [Header("Shader Parameter Overrides")]
    [Tooltip("Maps to _SkinColor / tint on the skin shaders.")]
    public Color skinColor = Color.white;

    [Tooltip("Maps to _EnergyColor / _EmissionColor / accent colour.")]
    public Color accentColor = Color.white;

    [Range(0f, 8f)]
    [Tooltip("Maps to _Speed on animated skin shaders.")]
    public float speed = 1f;

    [Range(0f, 8f)]
    [Tooltip("Maps to _Intensity on animated skin shaders.")]
    public float intensity = 1f;

    [Range(0.05f, 16f)]
    [Tooltip("Maps to _Scale (triplanar tiling).")]
    public float scale = 1f;

    /// <summary>
    /// Writes this skin's override values into a property block for the Skins/* shaders.
    /// </summary>
    public void WritePropertyBlock(MaterialPropertyBlock block)
    {
        if (block == null)
            return;

        block.SetColor("_SkinColor", skinColor);
        block.SetColor("_Color", skinColor);
        block.SetColor("_EnergyColor", accentColor);
        block.SetColor("_EmissionColor", accentColor);
        block.SetColor("_AccentColor", accentColor);
        block.SetFloat("_Speed", speed);
        block.SetFloat("_Intensity", intensity);
        block.SetFloat("_Scale", scale);
    }
}
