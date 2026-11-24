using UnityEngine;

/// <summary>Colour set for the stylised humanoid. Slots map one-to-one to <see cref="HumanoidSlot"/>.</summary>
[System.Serializable]
public struct HumanoidPalette
{
    public Color suit;
    public Color suitDark;
    public Color skin;
    public Color accent;
    public Color visor;

    public static HumanoidPalette Player
    {
        get
        {
            return new HumanoidPalette
            {
                suit = new Color(0.18f, 0.28f, 0.38f),
                suitDark = new Color(0.09f, 0.13f, 0.18f),
                skin = new Color(0.78f, 0.62f, 0.52f),
                accent = new Color(0.55f, 0.45f, 0.22f),
                visor = new Color(0.25f, 0.75f, 0.95f)
            };
        }
    }

    /// <summary>Training-dummy palette: the suit takes the legacy target colour so targets stay distinguishable.</summary>
    public static HumanoidPalette ForTarget(Color tint)
    {
        Color.RGBToHSV(tint, out float h, out float s, out float v);
        Color suit = Color.HSVToRGB(h, Mathf.Clamp(s, 0.35f, 0.9f), Mathf.Clamp(v, 0.45f, 0.9f));
        Color dark = Color.HSVToRGB(h, Mathf.Clamp(s, 0.35f, 0.9f), Mathf.Clamp(v * 0.45f, 0.12f, 0.45f));
        return new HumanoidPalette
        {
            suit = suit,
            suitDark = dark,
            skin = new Color(0.82f, 0.74f, 0.64f),
            accent = new Color(0.92f, 0.9f, 0.82f),
            visor = new Color(1f, 0.85f, 0.35f)
        };
    }

    public Color Get(HumanoidSlot slot)
    {
        switch (slot)
        {
            case HumanoidSlot.Suit: return suit;
            case HumanoidSlot.SuitDark: return suitDark;
            case HumanoidSlot.Skin: return skin;
            case HumanoidSlot.Accent: return accent;
            default: return visor;
        }
    }
}

/// <summary>Creates the runtime materials used by procedural humanoid bodies (nothing is saved as an asset).</summary>
public static class HumanoidMaterials
{
    /// <summary>Returns one material per <see cref="HumanoidSlot"/>, indexed by slot value.</summary>
    public static Material[] Create(HumanoidPalette palette)
    {
        var materials = new Material[HumanoidMeshBuilder.SlotCount];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = CreateSlotMaterial((HumanoidSlot)i, palette.Get((HumanoidSlot)i));
        return materials;
    }

    public static Material CreateSlotMaterial(HumanoidSlot slot, Color color)
    {
        var material = new Material(FindShader()) { name = "Humanoid_" + slot, color = color };
        float metallic = 0f;
        float gloss = 0.3f;
        switch (slot)
        {
            case HumanoidSlot.Suit: metallic = 0.1f; gloss = 0.3f; break;
            case HumanoidSlot.SuitDark: metallic = 0.2f; gloss = 0.35f; break;
            case HumanoidSlot.Skin: metallic = 0f; gloss = 0.2f; break;
            case HumanoidSlot.Accent: metallic = 0.45f; gloss = 0.5f; break;
            case HumanoidSlot.Visor: metallic = 0.6f; gloss = 0.85f; break;
        }

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", gloss);

        if (slot == HumanoidSlot.Visor && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.6f);
        }

        return material;
    }

    private static Shader FindShader()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        return shader;
    }
}
