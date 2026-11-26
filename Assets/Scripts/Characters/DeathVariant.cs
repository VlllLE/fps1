using UnityEngine;

/// <summary>How a humanoid body goes down. Each variant has its own topple direction and limb pose table.</summary>
public enum DeathVariant
{
    /// <summary>Shot from the front: the torso arches and the body falls onto its back, arms flung out.</summary>
    FallBackward = 0,
    /// <summary>Shot from behind or while sprinting: the body pitches onto its chest, arms reaching ahead.</summary>
    ForwardCollapse,
    /// <summary>Frontal head shot: the head whips back, the arms fly up and the body goes down hard on its back.</summary>
    HeadshotSnapBack,
    /// <summary>Pushed to the soldier's left (shot travelling towards -X).</summary>
    SideFallLeft,
    /// <summary>Pushed to the soldier's right (shot travelling towards +X).</summary>
    SideFallRight,
    /// <summary>The knees buckle first, then the torso folds forward.</summary>
    KneelCollapse
}

/// <summary>Everything the variant choice depends on. All directions are in the body's local space (+Z forward).</summary>
public struct DeathContext
{
    /// <summary>Direction the killing shot travelled, in body space. A shot hitting the front travels towards -Z.</summary>
    public Vector3 localHitDirection;
    public bool headshot;
    public float damage;
    /// <summary>Planar speed along the facing direction (m/s).</summary>
    public float forwardSpeed;
    public bool crouched;
    /// <summary>Uniform random value in [0, 1] used to vary otherwise equal situations; supplied by the caller so tests stay deterministic.</summary>
    public float roll;
}

/// <summary>Chooses a <see cref="DeathVariant"/> from where and how the soldier was hit. Pure and deterministic.</summary>
public static class DeathVariantSelector
{
    public const float SprintSpeed = 4.5f;
    private const float SideDominance = 0.6f;

    public static DeathVariant Select(DeathContext context)
    {
        Vector3 d = context.localHitDirection;
        d.y = 0f;
        float length = Mathf.Sqrt(d.x * d.x + d.z * d.z);
        if (length < 1e-3f)
            return DeathVariant.FallBackward;

        float x = d.x / length;
        float z = d.z / length;
        bool fromFront = z < 0f;
        bool sideways = Mathf.Abs(x) >= SideDominance && Mathf.Abs(x) > Mathf.Abs(z) * 1.1f;

        if (context.headshot)
        {
            if (sideways)
                return x > 0f ? DeathVariant.SideFallRight : DeathVariant.SideFallLeft;
            return fromFront ? DeathVariant.HeadshotSnapBack : DeathVariant.ForwardCollapse;
        }

        if (sideways)
            return x > 0f ? DeathVariant.SideFallRight : DeathVariant.SideFallLeft;

        if (context.forwardSpeed > SprintSpeed)
            return DeathVariant.ForwardCollapse;

        bool buckle = context.crouched || context.roll < 0.25f;
        if (buckle && context.damage < 120f)
            return DeathVariant.KneelCollapse;

        return fromFront ? DeathVariant.FallBackward : DeathVariant.ForwardCollapse;
    }

    /// <summary>Horizontal direction (body space) the torso topples towards.</summary>
    public static Vector3 FallDirection(DeathVariant variant)
    {
        switch (variant)
        {
            case DeathVariant.FallBackward:
            case DeathVariant.HeadshotSnapBack:
                return Vector3.back;
            case DeathVariant.SideFallLeft:
                return Vector3.left;
            case DeathVariant.SideFallRight:
                return Vector3.right;
            default:
                return Vector3.forward;
        }
    }
}
