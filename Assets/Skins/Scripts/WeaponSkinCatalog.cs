using System;
using UnityEngine;

/// <summary>
/// Every weapon skin in the project, loaded from <c>Resources/WeaponSkinCatalog</c>.
/// Lives in the Skins assembly so skin-only tools (such as <see cref="TestRangeSkinCycler"/>) can enumerate all skins
/// without a hand-maintained list. Rebuild with Tools &gt; Skins &gt; Rebuild Weapon Skin Catalog.
/// </summary>
[CreateAssetMenu(fileName = "WeaponSkinCatalog", menuName = "Skins/Weapon Skin Catalog")]
public class WeaponSkinCatalog : ScriptableObject
{
    public const string ResourceName = "WeaponSkinCatalog";

    public WeaponSkinDefinition[] skins = Array.Empty<WeaponSkinDefinition>();

    public static WeaponSkinCatalog LoadFromResources()
    {
        return Resources.Load<WeaponSkinCatalog>(ResourceName);
    }
}
