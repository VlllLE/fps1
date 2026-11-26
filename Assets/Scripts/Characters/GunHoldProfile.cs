using UnityEngine;

public enum HoldStance
{
    /// <summary>Muzzle down, weapon carried against the body (patrolling, idle).</summary>
    Lowered = 0,
    /// <summary>Low ready: weapon up in front of the chest, muzzle roughly towards the look direction.</summary>
    Ready,
    /// <summary>Shouldered / extended: the muzzle points exactly along the look direction.</summary>
    Aim
}

public enum HoldCategory
{
    Rifle,
    Smg,
    Lmg,
    Shotgun,
    Sniper,
    Pistol,
    Melee
}

/// <summary>
/// Where the weapon sits on the upper body for one stance. <see cref="anchor"/> is the profile's reference point
/// (stock butt for shouldered weapons, right palm otherwise) in Chest-bone space (unscaled metres).
/// </summary>
[System.Serializable]
public struct HoldStancePose
{
    public Vector3 anchor;
    /// <summary>Constant downward pitch of the weapon in degrees.</summary>
    public float pitchBase;
    /// <summary>Fraction of the look pitch that the weapon follows (1 = muzzle follows the view exactly).</summary>
    public float pitchLook;
    /// <summary>Weapon yaw relative to the look direction, in degrees (positive = right).</summary>
    public float yawOffset;
    /// <summary>Chest yaw towards the shooting hand's side, in degrees (a bladed stance brings the support arm onto the gun).</summary>
    public float blade;
    /// <summary>Extra head pitch in degrees (positive = chin down towards the sights).</summary>
    public float headNod;
    /// <summary>Extra head roll in degrees (positive = tilts the head towards the shooting shoulder, cheek towards the stock).</summary>
    public float headRoll;
    /// <summary>Elbow pole of the shooting arm in chest space: how far it points outwards and backwards (0 = tucked at the ribs).</summary>
    public Vector2 rightElbow;
    /// <summary>Elbow pole of the support arm: outwards (negative = in under the weapon) and backwards.</summary>
    public Vector2 leftElbow;

    public static readonly Vector2 FlaredElbow = new Vector2(0.6f, 0.2f);

    public HoldStancePose(Vector3 anchor, float pitchBase, float pitchLook, float yawOffset, float blade)
    {
        this.anchor = anchor;
        this.pitchBase = pitchBase;
        this.pitchLook = pitchLook;
        this.yawOffset = yawOffset;
        this.blade = blade;
        headNod = 0f;
        headRoll = 0f;
        rightElbow = FlaredElbow;
        leftElbow = FlaredElbow;
    }

    public static HoldStancePose Lerp(HoldStancePose a, HoldStancePose b, float t)
    {
        HoldStancePose pose = new HoldStancePose(
            Vector3.Lerp(a.anchor, b.anchor, t),
            Mathf.Lerp(a.pitchBase, b.pitchBase, t),
            Mathf.Lerp(a.pitchLook, b.pitchLook, t),
            Mathf.Lerp(a.yawOffset, b.yawOffset, t),
            Mathf.Lerp(a.blade, b.blade, t));
        pose.headNod = Mathf.Lerp(a.headNod, b.headNod, t);
        pose.headRoll = Mathf.Lerp(a.headRoll, b.headRoll, t);
        pose.rightElbow = new Vector2(Mathf.Lerp(a.rightElbow.x, b.rightElbow.x, t), Mathf.Lerp(a.rightElbow.y, b.rightElbow.y, t));
        pose.leftElbow = new Vector2(Mathf.Lerp(a.leftElbow.x, b.leftElbow.x, t), Mathf.Lerp(a.leftElbow.y, b.leftElbow.y, t));
        return pose;
    }
}

/// <summary>
/// Data describing how a weapon is held by a third-person body. All points are in weapon space
/// (+Z muzzle, +Y up, +X weapon right, metres) and name the centre of the palm, not the wrist.
/// The weapon FBXs ship no grip empties, so the table in <see cref="GunHoldCatalog"/> was measured from the
/// meshes; a prefab may override any point with named children (see <see cref="GunHoldCatalog.OverrideNames"/>).
/// </summary>
public sealed class GunHoldProfile
{
    public string name = "Generic";
    public HoldCategory category = HoldCategory.Rifle;

    /// <summary>The support hand also holds the weapon (false for knives and one-handed pistols when lowered).</summary>
    public bool twoHanded = true;
    /// <summary>The weapon has a shoulder stock; the stock butt is the reference anchor.</summary>
    public bool stocked = true;
    /// <summary>Heavy weapon with a bipod: leaned-in, lower carry.</summary>
    public bool bipodStance;
    /// <summary>Support hand is free (hangs relaxed) while the weapon is lowered.</summary>
    public bool supportHandFreeWhenLowered;
    /// <summary>The weapon is reloaded one shell at a time.</summary>
    public bool shellReload;
    /// <summary>The right hand cycles a bolt handle during the reload.</summary>
    public bool boltRack;

    public Vector3 stock;
    public Vector3 rightGrip;
    public Vector3 leftGrip;
    /// <summary>Nearest acceptable support-hand point towards the receiver when <see cref="leftGrip"/> is out of reach.</summary>
    public Vector3 leftGripRear;
    public Vector3 magwell;
    public Vector3 boltHandle;
    public Vector3 muzzle;

    /// <summary>Hand frame in weapon space: direction the fingers point and the direction the palm faces.</summary>
    public Vector3 rightFingers = new Vector3(0f, -0.25f, 1f);
    public Vector3 rightPalmNormal = Vector3.left;
    public Vector3 leftFingers = Vector3.forward;
    public Vector3 leftPalmNormal = new Vector3(0.3f, 0.95f, 0f);
    public Vector3 magFingers = new Vector3(0f, -0.4f, 0.9f);
    public Vector3 magPalmNormal = Vector3.right;

    /// <summary>Third-person recoil scale: 1 = rifle round. Muzzle rise, push back and torso rock all scale with it.</summary>
    public float recoilKick = 1f;
    /// <summary>Extra forward lean (degrees) and knee bend (0..1) while aiming, for a braced bipod stance.</summary>
    public float aimLean;
    public float aimCrouch;

    public HoldStancePose lowered;
    public HoldStancePose ready;
    public HoldStancePose aim;

    public HoldStancePose GetStance(HoldStance stance)
    {
        switch (stance)
        {
            case HoldStance.Aim: return aim;
            case HoldStance.Ready: return ready;
            default: return lowered;
        }
    }

    /// <summary>Blends the three stances with a continuous parameter (0 = lowered, 1 = ready, 2 = aim).</summary>
    public HoldStancePose Evaluate(float stanceT)
    {
        stanceT = Mathf.Clamp(stanceT, 0f, 2f);
        return stanceT <= 1f
            ? HoldStancePose.Lerp(lowered, ready, stanceT)
            : HoldStancePose.Lerp(ready, aim, stanceT - 1f);
    }

    /// <summary>The weapon point that is pinned to <see cref="HoldStancePose.anchor"/>.</summary>
    public Vector3 ReferencePoint => stocked ? stock : rightGrip;

    public GunHoldProfile Clone()
    {
        return (GunHoldProfile)MemberwiseClone();
    }
}

/// <summary>Per-weapon hold data, selected by weapon type and name.</summary>
public static class GunHoldCatalog
{
    /// <summary>Optional child names a weapon prefab can add to override measured points (weapon-space local position).</summary>
    public static readonly string[] OverrideNames = { "RightGrip", "LeftGrip", "Stock", "MuzzlePoint" };

    public const string M4A1 = "M4A1";
    public const string Scar = "SCAR";
    public const string Ump = "UMP";
    public const string Lmg = "LMG";
    public const string ShotgunName = "Shotgun";
    public const string PistolName = "Pistol";
    public const string BoltSniper = "BoltActionSniper";
    public const string Dmr = "DMRSniper";
    public const string Karambit = "Karambit";

    /// <summary>Picks a profile from the weapon type and a weapon/prefab name. Always returns a new, editable instance.</summary>
    public static GunHoldProfile Get(WeaponType type, string weaponName)
    {
        if (type == WeaponType.Melee)
            return Build(Karambit);
        string match = MatchName(weaponName);
        if (match != null)
            return Build(match);

        switch (type)
        {
            case WeaponType.SMG: return Build(Ump);
            case WeaponType.LMG: return Build(Lmg);
            case WeaponType.Launcher:
            case WeaponType.Shotgun: return Build(ShotgunName);
            case WeaponType.Sniper: return Build(BoltSniper);
            case WeaponType.Pistol: return Build(PistolName);
            default: return Build(M4A1);
        }
    }

    /// <summary>The profile name a weapon/prefab name maps to, or null when the name says nothing about the weapon.</summary>
    public static string MatchName(string weaponName)
    {
        string key = Normalize(weaponName);
        if (key.Length == 0)
            return null;
        if (key.Contains("karambit") || key.Contains("knife"))
            return Karambit;
        if (key.Contains("bolt"))
            return BoltSniper;
        if (key.Contains("dmr") || key.Contains("marksman"))
            return Dmr;
        if (key.Contains("scar"))
            return Scar;
        if (key.Contains("m4a1") || key.Contains("m4"))
            return M4A1;
        if (key.Contains("ump"))
            return Ump;
        if (key.Contains("lmg") || key.Contains("machine"))
            return Lmg;
        if (key.Contains("shotgun"))
            return ShotgunName;
        if (key.Contains("pistol"))
            return PistolName;
        return null;
    }

    /// <summary>
    /// Profile for a weapon instance. A scene instance is often named after its inventory slot ("Assault_Primary"), so the
    /// instance name is tried first and the config's prefab, asset and display names after it, before the weapon type.
    /// </summary>
    public static GunHoldProfile Get(WeaponConfig config, GameObject instance)
    {
        if (config == null)
            return Get(WeaponType.AssaultRifle, instance != null ? instance.name : null);
        if (config.weaponType == WeaponType.Melee)
            return Build(Karambit);

        string match = MatchName(instance != null ? instance.name : null)
            ?? MatchName(config.weaponPrefab != null ? config.weaponPrefab.name : null)
            ?? MatchName(config.name)
            ?? MatchName(config.weaponName);
        if (match != null)
            return Build(match);
        return Get(config.weaponType, null);
    }

    public static GunHoldProfile Get(WeaponConfig config)
    {
        if (config == null)
            return Build(M4A1);
        string name = config.weaponPrefab != null ? config.weaponPrefab.name : config.weaponName;
        if (string.IsNullOrEmpty(name))
            name = config.weaponName;
        GunHoldProfile profile = Get(config.weaponType, name);
        if (string.IsNullOrEmpty(profile.name))
            profile.name = name;
        return profile;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static Vector3 V(float x, float y, float z)
    {
        return new Vector3(x, y, z);
    }

    /// <summary>Builds the named profile from the measured table.</summary>
    public static GunHoldProfile Build(string weapon)
    {
        GunHoldProfile p = new GunHoldProfile { name = weapon };
        switch (weapon)
        {
            case Scar:
                p.category = HoldCategory.Rifle;
                p.stock = V(0f, 0.017f, -0.360f);
                p.rightGrip = V(0.030f, -0.062f, -0.032f);
                p.leftGrip = V(0f, 0.000f, 0.300f);
                p.leftGripRear = V(0f, -0.010f, 0.160f);
                p.magwell = V(-0.035f, -0.100f, 0.090f);
                p.boltHandle = V(0f, 0.060f, -0.050f);
                p.muzzle = V(0f, 0.050f, 0.590f);
                ApplyStocked(p);
                p.recoilKick = 1.1f;
                break;

            case Ump:
                p.category = HoldCategory.Smg;
                p.stock = V(0f, 0.010f, -0.343f);
                p.rightGrip = V(0.030f, -0.064f, -0.033f);
                p.leftGrip = V(0f, 0.000f, 0.260f);
                p.leftGripRear = V(0f, -0.010f, 0.140f);
                p.magwell = V(-0.035f, -0.100f, 0.065f);
                p.boltHandle = V(0f, 0.060f, -0.040f);
                p.muzzle = V(0f, 0.030f, 0.398f);
                ApplyStocked(p);
                p.recoilKick = 0.65f;
                break;

            case Lmg:
                p.category = HoldCategory.Lmg;
                p.bipodStance = true;
                p.stock = V(0f, -0.020f, -0.417f);
                p.rightGrip = V(0.030f, -0.070f, -0.050f);
                p.leftGrip = V(0f, -0.010f, 0.300f);
                p.leftGripRear = V(0f, -0.005f, 0.170f);
                p.magwell = V(-0.060f, -0.080f, 0.090f);
                p.boltHandle = V(0.04f, 0.050f, 0.050f);
                p.muzzle = V(0f, 0.040f, 0.740f);
                ApplyStocked(p);
                p.recoilKick = 0.85f;
                p.aimLean = 15f;
                p.aimCrouch = 0.45f;
                p.aim.anchor = V(0.15f, 0.17f, 0.08f);
                p.aim.blade = 32f;
                p.aim.headNod = 6f;
                p.aim.headRoll = -7f;
                p.ready.anchor = V(0.14f, 0.00f, 0.12f);
                p.lowered.anchor = V(0.17f, -0.11f, 0.09f);
                break;

            case ShotgunName:
                p.category = HoldCategory.Shotgun;
                p.shellReload = true;
                p.stock = V(0f, 0.030f, -0.340f);
                p.rightGrip = V(0.030f, -0.070f, -0.065f);
                p.leftGrip = V(0f, -0.010f, 0.250f);
                p.leftGripRear = V(0f, -0.005f, 0.170f);
                p.magwell = V(-0.030f, -0.040f, 0.030f);
                p.boltHandle = V(0f, 0.04f, 0.2f);
                p.muzzle = V(0f, 0.030f, 0.570f);
                ApplyStocked(p);
                p.recoilKick = 2.3f;
                break;

            case PistolName:
                p.category = HoldCategory.Pistol;
                p.recoilKick = 1.4f;
                p.stocked = false;
                p.twoHanded = true;
                p.supportHandFreeWhenLowered = true;
                p.rightGrip = V(0.025f, -0.062f, -0.046f);
                p.leftGrip = V(-0.025f, -0.070f, -0.040f);
                p.leftGripRear = V(-0.025f, -0.070f, -0.040f);
                p.magwell = V(-0.030f, -0.130f, -0.075f);
                p.boltHandle = V(0f, 0.03f, -0.07f);
                p.muzzle = V(0f, 0.030f, 0.300f);
                p.rightFingers = V(0f, -0.2f, 1f);
                p.leftFingers = V(0f, -0.2f, 1f);
                p.leftPalmNormal = Vector3.right;
                p.lowered = new HoldStancePose(V(0.16f, -0.20f, 0.26f), 38f, 0.2f, -4f, 8f);
                p.ready = new HoldStancePose(V(0.07f, 0.02f, 0.40f), 22f, 0.5f, 0f, 14f);
                p.aim = new HoldStancePose(V(0.05f, 0.16f, 0.50f), 0f, 1f, 0f, 10f);
                break;

            case BoltSniper:
                p.category = HoldCategory.Sniper;
                p.boltRack = true;
                p.stock = V(0f, 0.000f, -0.378f);
                p.rightGrip = V(0.030f, -0.055f, -0.050f);
                p.leftGrip = V(0f, -0.045f, 0.190f);
                p.leftGripRear = V(0f, -0.040f, 0.120f);
                p.magwell = V(-0.050f, -0.080f, 0.030f);
                p.boltHandle = V(0.050f, 0.022f, -0.030f);
                p.muzzle = V(0f, 0.030f, 0.660f);
                ApplyStocked(p);
                p.recoilKick = 2.7f;
                p.aim.headRoll = -13f;
                p.aim.headNod = 15f;
                p.aim.anchor = V(0.15f, 0.27f, 0.05f);
                break;

            case Dmr:
                p.category = HoldCategory.Sniper;
                p.stock = V(0f, 0.006f, -0.287f);
                p.rightGrip = V(0.030f, -0.060f, -0.065f);
                p.leftGrip = V(0f, -0.025f, 0.220f);
                p.leftGripRear = V(0f, -0.020f, 0.140f);
                p.magwell = V(-0.035f, -0.100f, 0.035f);
                p.boltHandle = V(0f, 0.060f, -0.030f);
                p.muzzle = V(0f, 0.045f, 0.580f);
                ApplyStocked(p);
                p.recoilKick = 1.7f;
                p.aim.headRoll = -12f;
                p.aim.headNod = 14f;
                p.aim.anchor = V(0.15f, 0.27f, 0.05f);
                break;

            case Karambit:
                p.category = HoldCategory.Melee;
                p.recoilKick = 0.6f;
                p.stocked = false;
                p.twoHanded = false;
                p.supportHandFreeWhenLowered = true;
                p.rightGrip = V(0f, 0f, -0.005f);
                p.leftGrip = p.rightGrip;
                p.leftGripRear = p.rightGrip;
                p.magwell = p.rightGrip;
                p.muzzle = V(0f, -0.030f, 0.100f);
                p.rightFingers = V(0f, -0.55f, 0.83f);
                p.rightPalmNormal = Vector3.left;
                p.lowered = new HoldStancePose(V(0.17f, -0.26f, 0.20f), 20f, 0.2f, 0f, 8f);
                p.ready = new HoldStancePose(V(0.17f, -0.05f, 0.34f), 5f, 0.4f, 0f, 14f);
                p.aim = new HoldStancePose(V(0.14f, 0.02f, 0.40f), 0f, 0.8f, 0f, 14f);
                break;

            default:
                p.name = M4A1;
                p.category = HoldCategory.Rifle;
                p.stock = V(0f, 0.000f, -0.314f);
                p.rightGrip = V(0.030f, -0.060f, -0.040f);
                p.leftGrip = V(0f, -0.012f, 0.270f);
                p.leftGripRear = V(0f, -0.010f, 0.150f);
                p.magwell = V(-0.035f, -0.100f, 0.090f);
                p.boltHandle = V(0f, 0.060f, -0.060f);
                p.muzzle = V(0f, 0.040f, 0.544f);
                ApplyStocked(p);
                p.recoilKick = 1f;
                break;
        }

        return p;
    }

    /// <summary>Default stance table for a shoulder-stocked weapon.</summary>
    private static void ApplyStocked(GunHoldProfile p)
    {
        p.stocked = true;
        p.twoHanded = true;
        p.aim = new HoldStancePose(V(0.15f, 0.25f, 0.05f), 0f, 1f, 0f, 35f);
        p.aim.headNod = 13f;
        p.aim.headRoll = -9f;
        p.aim.rightElbow = new Vector2(0.06f, 0.55f);
        p.aim.leftElbow = new Vector2(-0.10f, 0.05f);
        p.ready = new HoldStancePose(V(0.14f, 0.05f, 0.12f), 12f, 0.6f, -4f, 26f);
        p.ready.rightElbow = new Vector2(0.30f, 0.35f);
        p.ready.leftElbow = new Vector2(0.15f, 0.10f);
        p.lowered = new HoldStancePose(V(0.17f, -0.08f, 0.08f), 32f, 0.2f, -8f, 12f);
    }

    /// <summary>
    /// Builds the world-independent hand rotation for a palm normal / finger direction pair. A hand bone at rest has
    /// fingers along -Y and, for the right hand, the palm facing -X (mirrored for the left hand); the thumb is +Z.
    /// </summary>
    public static Quaternion HandRotation(bool rightHand, Vector3 fingers, Vector3 palmNormal)
    {
        Vector3 y = -fingers;
        if (y.sqrMagnitude < 1e-8f)
            y = Vector3.down;
        y = y.normalized;
        Vector3 n = palmNormal - y * Vector3.Dot(palmNormal, y);
        if (n.sqrMagnitude < 1e-8f)
            n = Vector3.Cross(y, Vector3.forward);
        n = n.normalized;
        Vector3 x = rightHand ? -n : n;
        Vector3 z = Vector3.Cross(x, y);
        return Quaternion.LookRotation(z, y);
    }
}
