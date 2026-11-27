using UnityEngine;

/// <summary>
/// First-person weapon kick for one shot at a time: an under-damped spring snaps the gun back and muzzle-up, then it
/// settles with a small overshoot, plus a smaller random sideways twist. Amplitudes come from the weapon config
/// (<see cref="WeaponConfig.gunKickback"/>, <see cref="WeaponConfig.gunRotationKick"/>) scaled by the weapon type's
/// <see cref="WeaponFxProfile"/>. Purely visual: it offsets the weapon model and never feeds back into aim or hit logic.
/// </summary>
public sealed class WeaponFireKick
{
    private const float Damping = 0.55f;
    private const float AimScale = 0.6f;
    private const float MaxDisplacement = 2.2f;

    private float displacement;
    private float velocity;
    private float side;
    private float sideVelocity;
    private float omega = 14f;
    private float kickback;
    private Vector3 rotationKick;

    /// <summary>Normalised spring displacement: 0 at rest, about 1 at the peak of a single shot.</summary>
    public float Displacement => displacement;

    public bool IsActive => Mathf.Abs(displacement) > 0.001f || Mathf.Abs(velocity) > 0.01f;

    public Vector3 PositionOffset => new Vector3(side * kickback * 0.15f, 0.12f * kickback * displacement, -kickback * displacement);

    public Quaternion RotationOffset => Quaternion.Euler(
        rotationKick.x * displacement,
        rotationKick.y * side,
        rotationKick.z * side);

    public void Reset()
    {
        displacement = 0f;
        velocity = 0f;
        side = 0f;
        sideVelocity = 0f;
    }

    /// <summary>Adds the kick of one shot. <paramref name="sideRoll"/> is a random value in [-1, 1] (a parameter so tests are deterministic).</summary>
    public void Kick(WeaponConfig config, bool aiming, float sideRoll)
    {
        if (config == null || config.IsMelee)
            return;

        WeaponFxProfile profile = WeaponFxProfile.For(config);
        float strength = profile.kickStrength * (aiming ? AimScale : 1f);
        if (strength <= 0f)
            return;

        kickback = config.gunKickback;
        rotationKick = config.gunRotationKick;
        omega = Mathf.Max(6f, config.gunRecoilRecoverySpeed * 0.95f * profile.kickRecovery);
        velocity += strength * omega * 1.7f;
        sideVelocity += Mathf.Clamp(sideRoll, -1f, 1f) * strength * omega * 0.9f;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f || !IsMoving)
            return;

        int steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / 0.004f));
        float h = deltaTime / steps;
        for (int i = 0; i < steps; i++)
        {
            velocity += (-omega * omega * displacement - 2f * Damping * omega * velocity) * h;
            displacement += velocity * h;
            sideVelocity += (-omega * omega * side - 2f * 0.8f * omega * sideVelocity) * h;
            side += sideVelocity * h;
        }

        displacement = Mathf.Clamp(displacement, -0.4f, MaxDisplacement);
        side = Mathf.Clamp(side, -1f, 1f);
    }

    /// <summary>True while any part of the spring (back kick or sideways twist) is still moving.</summary>
    public bool IsMoving => IsActive || Mathf.Abs(side) > 0.001f || Mathf.Abs(sideVelocity) > 0.01f;
}
