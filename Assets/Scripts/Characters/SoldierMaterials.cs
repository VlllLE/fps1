using UnityEngine;

/// <summary>
/// Runtime material instances for the imported soldier. The five <see cref="HumanoidPalette"/> colours
/// drive the soldier slots (Cloth = suit, Armor = suitDark, Skin, Accent, Lens = visor) so team/target
/// tinting works exactly like it does for the procedural body; Gear is derived and the eye stays white.
/// </summary>
public static class SoldierMaterials
{
    private static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    /// <summary>Creates one instance per <see cref="SoldierMaterialSlot"/> from the set's templates and tints them.</summary>
    public static Material[] Create(SoldierModelSet set, HumanoidPalette palette, Texture occlusion = null)
    {
        var materials = new Material[SoldierModelSet.MaterialSlotCount];
        for (int i = 0; i < materials.Length; i++)
        {
            Material template = set != null ? set.GetTemplate((SoldierMaterialSlot)i) : null;
            materials[i] = template != null ? new Material(template) : CreateFallback((SoldierMaterialSlot)i);
            materials[i].name = "Soldier_" + (SoldierMaterialSlot)i;
            if (occlusion != null && materials[i].HasProperty(OcclusionMapId))
                materials[i].SetTexture(OcclusionMapId, occlusion);
        }

        ApplyPalette(materials, palette);
        return materials;
    }

    /// <summary>The soldier slot that renders the given <see cref="HumanoidSlot"/> colour.</summary>
    public static SoldierMaterialSlot ForHumanoidSlot(HumanoidSlot slot)
    {
        switch (slot)
        {
            case HumanoidSlot.Suit: return SoldierMaterialSlot.Cloth;
            case HumanoidSlot.SuitDark: return SoldierMaterialSlot.Armor;
            case HumanoidSlot.Skin: return SoldierMaterialSlot.Skin;
            case HumanoidSlot.Accent: return SoldierMaterialSlot.Accent;
            default: return SoldierMaterialSlot.Lens;
        }
    }

    /// <summary>Colour the Gear (webbing, pouches, straps) takes for a palette: between the dark and the main suit colour.</summary>
    public static Color DeriveGear(HumanoidPalette palette)
    {
        return Color.Lerp(palette.suitDark, palette.suit, 0.55f);
    }

    public static void ApplyPalette(Material[] materials, HumanoidPalette palette)
    {
        if (materials == null || materials.Length < SoldierModelSet.MaterialSlotCount)
            return;

        SetColor(materials, SoldierMaterialSlot.Cloth, palette.suit);
        SetColor(materials, SoldierMaterialSlot.Armor, palette.suitDark);
        SetColor(materials, SoldierMaterialSlot.Skin, palette.skin);
        SetColor(materials, SoldierMaterialSlot.Accent, palette.accent);
        SetColor(materials, SoldierMaterialSlot.Gear, DeriveGear(palette));
        SetColor(materials, SoldierMaterialSlot.Lens, palette.visor);
        SetColor(materials, SoldierMaterialSlot.Eye, Color.white);

        Material lens = materials[(int)SoldierMaterialSlot.Lens];
        if (lens != null && lens.HasProperty(EmissionColorId))
        {
            lens.EnableKeyword("_EMISSION");
            lens.SetColor(EmissionColorId, palette.visor * 0.5f);
        }
    }

    /// <summary>Maps an imported material (by name) to its slot, or null when the name is unknown.</summary>
    public static bool TryGetSlot(Material material, out SoldierMaterialSlot slot)
    {
        slot = SoldierMaterialSlot.Armor;
        if (material == null)
            return false;

        string name = material.name;
        for (int i = 0; i < SoldierModelSet.MaterialSlotCount; i++)
        {
            var candidate = (SoldierMaterialSlot)i;
            if (name.IndexOf(candidate.ToString(), System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private static void SetColor(Material[] materials, SoldierMaterialSlot slot, Color color)
    {
        Material m = materials[(int)slot];
        if (m != null)
            m.color = color;
    }

    private static Material CreateFallback(SoldierMaterialSlot slot)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        var material = new Material(shader);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", slot == SoldierMaterialSlot.Lens ? 0.85f : 0.3f);
        return material;
    }
}
