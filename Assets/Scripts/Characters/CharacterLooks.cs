using UnityEngine;

/// <summary>One downloaded character model (see <c>Assets/Characters/Looks/CREDITS.md</c>) retargeted to the 19 humanoid bones.</summary>
[System.Serializable]
public sealed class CharacterLookEntry
{
    public string id;
    public string displayName;
    [Tooltip("Imported <Look>.fbx: skinned meshes on the 19 humanoid bones in the game's rest pose, own materials/textures.")]
    public GameObject model;
    [Tooltip("Height of the model in metres (feet to top), used to validate and normalise the import scale.")]
    public float height = 1.8f;
}

/// <summary>
/// The selectable character looks and which one a weapon (class) gets. The classic Blender soldier is the fallback
/// whenever a look is missing or fails validation.
/// </summary>
public static class CharacterLooks
{
    public const string Operator = "Operator";
    public const string Soldier = "Soldier";
    public const string Ghillie = "Ghillie";

    public static readonly string[] AllIds = { Soldier, Operator, Ghillie };

    /// <summary>Look for a primary weapon: assault rifle and LMG = soldier, sniper = ghillie suit, SMG and shotgun = S.W.A.T. operator.</summary>
    public static string ForWeapon(WeaponConfig weapon)
    {
        return weapon != null ? ForWeaponType(weapon.weaponType) : Soldier;
    }

    public static string ForWeaponType(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Sniper: return Ghillie;
            case WeaponType.SMG:
            case WeaponType.Shotgun: return Operator;
            default: return Soldier;
        }
    }

    /// <summary>Bots with assault rifles are dealt a deterministic mix of all three looks so a firefight is not one uniform.</summary>
    public static string ForBot(WeaponConfig weapon, int seed)
    {
        if (weapon != null && weapon.weaponType != WeaponType.AssaultRifle)
            return ForWeapon(weapon);
        return AllIds[(int)((uint)seed % (uint)AllIds.Length)];
    }

    /// <summary>How far a training dummy's colour cue (red, green, blue...) pulls its look's own colours.</summary>
    public const float DummyLookTint = 0.3f;

    /// <summary>The look of the <paramref name="index"/>th training dummy: the three looks in turn, so every map shows a mix.</summary>
    public static string ForDummy(int index)
    {
        return AllIds[(int)((uint)index % (uint)AllIds.Length)];
    }

    /// <summary>Sleeve and glove colours of the first-person arms for a look, so the arms match the body in view.</summary>
    public static HumanoidPalette GetArmsPalette(string id)
    {
        switch (id)
        {
            case Operator:
                return new HumanoidPalette
                {
                    suit = new Color(0.12f, 0.13f, 0.16f),
                    suitDark = new Color(0.06f, 0.06f, 0.08f),
                    skin = new Color(0.72f, 0.56f, 0.44f),
                    accent = new Color(0.1f, 0.1f, 0.11f),
                    visor = new Color(0.2f, 0.2f, 0.22f)
                };
            case Soldier:
                return new HumanoidPalette
                {
                    suit = new Color(0.30f, 0.33f, 0.22f),
                    suitDark = new Color(0.14f, 0.15f, 0.11f),
                    skin = new Color(0.84f, 0.64f, 0.52f),
                    accent = new Color(0.12f, 0.12f, 0.12f),
                    visor = new Color(0.2f, 0.2f, 0.22f)
                };
            case Ghillie:
                return new HumanoidPalette
                {
                    suit = new Color(0.22f, 0.26f, 0.12f),
                    suitDark = new Color(0.11f, 0.15f, 0.07f),
                    skin = new Color(0.84f, 0.64f, 0.52f),
                    accent = new Color(0.30f, 0.22f, 0.11f),
                    visor = new Color(0.2f, 0.2f, 0.22f)
                };
            default:
                return HumanoidPalette.Player;
        }
    }
}
