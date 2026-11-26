using System;
using UnityEngine;

/// <summary>
/// Seam that lets weapon skins reach the third-person guns soldiers carry without the Scripts assembly knowing about
/// skins. The Skins assembly implements <see cref="IHeldGunSkinProvider"/> and registers it here; the held-gun code
/// (<see cref="ThirdPersonGun"/>) only asks the provider which skin the weapon wears and hands it the gun to paint.
/// </summary>
public interface IHeldGunSkinProvider
{
    /// <summary>
    /// The skin <paramref name="weaponSource"/> wears: a spawned weapon's current skin, or a weapon prefab's default skin.
    /// Null when it is not skinned. The returned object is an opaque handle, compared by reference.
    /// </summary>
    UnityEngine.Object GetSkin(GameObject weaponSource);

    /// <summary>
    /// Paints the outer parts of <paramref name="heldGunRoot"/> with <paramref name="skin"/> and leaves optical glass
    /// alone. A null skin restores the gun's original materials.
    /// </summary>
    void Apply(GameObject heldGunRoot, UnityEngine.Object skin);
}

public static class HeldGunSkins
{
    private static IHeldGunSkinProvider provider;

    /// <summary>Raised when a spawned weapon's skin changes, so held guns that copy it can repaint.</summary>
    public static event Action<GameObject> SkinChanged;

    public static IHeldGunSkinProvider Provider => provider;

    public static void Register(IHeldGunSkinProvider newProvider)
    {
        provider = newProvider;
    }

    public static void Unregister(IHeldGunSkinProvider oldProvider)
    {
        if (provider == oldProvider)
            provider = null;
    }

    /// <summary>Called by the skin system after it changed the skin on <paramref name="weapon"/>.</summary>
    public static void NotifySkinChanged(GameObject weapon)
    {
        if (weapon != null)
            SkinChanged?.Invoke(weapon);
    }
}
