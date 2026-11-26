using UnityEngine;

/// <summary>
/// Skins side of the <see cref="HeldGunSkins"/> seam: tells third-person held guns which skin their weapon wears and
/// paints them with it through a <see cref="WeaponSkinApplier"/>, so the held gun gets the same outer-part skinning and
/// the same untouched scope glass as the first-person weapon. The Scripts assembly never references Skins; this class
/// registers itself when the editor or player loads.
/// </summary>
public sealed class WeaponSkinHeldGunProvider : IHeldGunSkinProvider
{
    private static readonly WeaponSkinHeldGunProvider Instance = new WeaponSkinHeldGunProvider();

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterWithHeldGuns()
    {
        HeldGunSkins.Register(Instance);
    }

    public Object GetSkin(GameObject weaponSource)
    {
        if (weaponSource == null)
            return null;

        WeaponSkinApplier applier = FindApplier(weaponSource);
        if (applier == null)
            return null;

        // A spawned weapon reports the skin it wears; a weapon prefab (what bots carry) reports its authored default.
        return applier.CurrentSkin != null ? applier.CurrentSkin : applier.DefaultSkin;
    }

    public void Apply(GameObject heldGunRoot, Object skin)
    {
        if (heldGunRoot == null)
            return;

        WeaponSkinApplier applier = heldGunRoot.GetComponent<WeaponSkinApplier>();
        if (applier == null)
        {
            if (skin == null)
                return;
            applier = heldGunRoot.AddComponent<WeaponSkinApplier>();
        }

        applier.ApplySkin(skin as WeaponSkinDefinition);
    }

    private static WeaponSkinApplier FindApplier(GameObject weaponSource)
    {
        WeaponSkinApplier applier = weaponSource.GetComponent<WeaponSkinApplier>();
        return applier != null ? applier : weaponSource.GetComponentInChildren<WeaponSkinApplier>(true);
    }
}
