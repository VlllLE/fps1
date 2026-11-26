using UnityEngine;

/// <summary>
/// Decides which weapon material slots are optical glass (scope and sight lenses). Skins are painted on the
/// weapon body, scope tube and mounts, but a lens has to stay transparent, so <see cref="WeaponSkinApplier"/>
/// leaves these slots alone when it applies or restores a skin.
/// </summary>
public static class WeaponGlass
{
    public const string ShaderName = "Weapons/ScopeGlass";

    /// <summary>
    /// A material is glass when it uses the scope glass shader, or (for imported models that have not been remapped yet)
    /// when its name says so, such as the FBX slot "SniperGlass" or a lens material.
    /// </summary>
    public static bool IsGlassMaterial(Material material)
    {
        if (material == null)
            return false;

        Shader shader = material.shader;
        if (shader != null && shader.name == ShaderName)
            return true;

        return NameSaysGlass(material.name);
    }

    /// <summary>
    /// A slot is excluded from skinning when its material is glass, or when the renderer itself is a lens mesh
    /// (an object named like "ScopeLensFront" or "RedDotGlass").
    /// </summary>
    public static bool IsGlassSlot(Renderer renderer, Material material)
    {
        if (IsGlassMaterial(material))
            return true;

        return renderer != null && NameSaysGlass(renderer.name);
    }

    private static bool NameSaysGlass(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Lens", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
