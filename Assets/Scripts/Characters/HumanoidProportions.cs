using UnityEngine;

public enum HumanoidHitboxShape
{
    Sphere,
    Box,
    CapsuleY
}

/// <summary>Authoring data for one hit volume of the humanoid (BodyRoot space, unscaled).</summary>
public readonly struct HumanoidHitboxSpec
{
    public readonly string Name;
    public readonly HitZoneType Zone;
    public readonly HumanoidHitboxShape Shape;
    public readonly Vector3 Center;
    /// <summary>Sphere: x = radius. Box: full size. CapsuleY: x = radius, y = total height.</summary>
    public readonly Vector3 Size;

    public HumanoidHitboxSpec(string name, HitZoneType zone, HumanoidHitboxShape shape, Vector3 center, Vector3 size)
    {
        Name = name;
        Zone = zone;
        Shape = shape;
        Center = center;
        Size = size;
    }
}

/// <summary>
/// Rest-pose proportions of the stylised humanoid, expressed for the project's standard
/// CharacterController capsule (height 2, radius 0.4, centre y = 1, feet at the root origin).
/// Every value is scaled uniformly by (controller height / 2) at build time.
/// </summary>
public static class HumanoidProportions
{
    public const float NominalControllerHeight = 2f;
    public const float NominalControllerRadius = 0.4f;
    /// <summary>Camera eye height owned by the movement setup (CameraHolder local Y).</summary>
    public const float EyeHeight = 1.6f;

    // Joint heights above the ground (BodyRoot space).
    public const float AnkleY = 0.10f;
    public const float KneeY = 0.52f;
    public const float HipJointY = 0.94f;
    public const float HipsY = 0.98f;
    public const float WaistY = 1.04f;
    public const float ChestY = 1.22f;
    public const float ShoulderY = 1.41f;
    public const float NeckY = 1.44f;
    public const float HeadY = 1.52f;

    public const float HipHalfWidth = 0.11f;
    public const float ShoulderHalfWidth = 0.215f;

    public const float ThighLength = HipJointY - KneeY;
    public const float ShinLength = KneeY - AnkleY;
    public const float UpperArmLength = 0.29f;
    public const float ForearmLength = 0.27f;

    /// <summary>Head centre relative to the head bone.</summary>
    public static readonly Vector3 HeadCenterLocal = new Vector3(0f, 0.17f, 0.005f);
    public const float TopOfHeadY = 1.825f;

    /// <summary>
    /// Hit volumes used when the body doubles as a target/remote pawn. They are intentionally
    /// static (not animated) so cosmetic animation can never change hit results, and every volume
    /// sits inside the standard CharacterController capsule.
    /// </summary>
    public static readonly HumanoidHitboxSpec[] Hitboxes =
    {
        new HumanoidHitboxSpec("Head", HitZoneType.Head, HumanoidHitboxShape.Sphere,
            new Vector3(0f, HeadY + 0.17f, 0.005f), new Vector3(0.15f, 0f, 0f)),
        new HumanoidHitboxSpec("Body", HitZoneType.Body, HumanoidHitboxShape.Box,
            new Vector3(0f, 1.19f, 0f), new Vector3(0.42f, 0.60f, 0.27f)),
        new HumanoidHitboxSpec("Arm_L", HitZoneType.Limb, HumanoidHitboxShape.CapsuleY,
            new Vector3(-0.255f, 1.12f, 0f), new Vector3(0.07f, 0.66f, 0f)),
        new HumanoidHitboxSpec("Arm_R", HitZoneType.Limb, HumanoidHitboxShape.CapsuleY,
            new Vector3(0.255f, 1.12f, 0f), new Vector3(0.07f, 0.66f, 0f)),
        new HumanoidHitboxSpec("Leg_L", HitZoneType.Limb, HumanoidHitboxShape.CapsuleY,
            new Vector3(-HipHalfWidth, 0.515f, 0f), new Vector3(0.09f, 0.85f, 0f)),
        new HumanoidHitboxSpec("Leg_R", HitZoneType.Limb, HumanoidHitboxShape.CapsuleY,
            new Vector3(HipHalfWidth, 0.515f, 0f), new Vector3(0.09f, 0.85f, 0f)),
    };

    /// <summary>Uniform body scale for a controller of the given height.</summary>
    public static float ScaleForControllerHeight(float controllerHeight)
    {
        return Mathf.Max(0.1f, controllerHeight / NominalControllerHeight);
    }
}
