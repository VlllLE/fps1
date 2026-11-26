using System;
using UnityEngine;

/// <summary>
/// Tiny ballistic simulation for a gun that falls from a dying soldier: gravity, a bouncing ground contact, rolling
/// friction and a blend to a resting orientation (lying on its side, muzzle direction preserved). Pure so it can be
/// unit tested; the caller supplies the ground height under the gun as a delegate (a raycast in-game).
/// </summary>
public sealed class DroppedGunPhysics
{
    private const float SubStep = 1f / 90f;
    private const float Gravity = 9.81f;

    private readonly Func<Vector3, float> groundHeight;
    private readonly float halfThickness;
    private float accumulator;
    private int bounces;

    public DroppedGunPhysics(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocityDegrees,
        Func<Vector3, float> groundHeight, float halfThickness = 0.045f)
    {
        Position = position;
        Rotation = rotation;
        Velocity = velocity;
        AngularVelocity = angularVelocityDegrees;
        this.groundHeight = groundHeight ?? (_ => 0f);
        this.halfThickness = halfThickness;
    }

    public Vector3 Position { get; private set; }

    public Quaternion Rotation { get; private set; }

    public Vector3 Velocity { get; private set; }

    /// <summary>World-space angular velocity in degrees per second.</summary>
    public Vector3 AngularVelocity { get; private set; }

    public bool Grounded { get; private set; }

    public bool AtRest { get; private set; }

    public int Bounces => bounces;

    /// <summary>Orientation the gun relaxes to on the floor: horizontal barrel (same heading), right side down.</summary>
    public static Quaternion RestRotation(Quaternion current)
    {
        Vector3 forward = current * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f)
            forward = current * Vector3.up;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.forward;
        forward.Normalize();
        return Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, -90f);
    }

    public void Tick(float deltaTime)
    {
        if (AtRest)
            return;

        accumulator += Mathf.Clamp(deltaTime, 0f, 0.1f);
        while (accumulator >= SubStep)
        {
            accumulator -= SubStep;
            Step(SubStep);
        }
    }

    private void Step(float dt)
    {
        Vector3 velocity = Velocity;
        Vector3 position = Position;
        velocity.y -= Gravity * dt;
        position += velocity * dt;

        float floor = groundHeight(position) + halfThickness;
        Grounded = false;
        if (position.y <= floor)
        {
            position.y = floor;
            Grounded = true;
            if (velocity.y < -0.8f && bounces < 4)
            {
                velocity.y = -velocity.y * 0.28f;
                velocity.x *= 0.65f;
                velocity.z *= 0.65f;
                AngularVelocity *= 0.6f;
                bounces++;
            }
            else
            {
                velocity.y = 0f;
            }

            float friction = Mathf.Exp(-7f * dt);
            velocity.x *= friction;
            velocity.z *= friction;
        }

        Velocity = velocity;
        Position = position;

        Quaternion rotation = Rotation;
        Vector3 spin = AngularVelocity;
        float spinMagnitude = spin.magnitude;
        if (spinMagnitude > 1e-3f)
            rotation = Quaternion.AngleAxis(spinMagnitude * dt, spin / spinMagnitude) * rotation;

        if (Grounded)
        {
            rotation = Quaternion.Slerp(rotation, RestRotation(rotation), 1f - Mathf.Exp(-9f * dt));
            AngularVelocity *= Mathf.Exp(-6f * dt);
        }

        Rotation = rotation;

        if (Grounded && velocity.sqrMagnitude < 0.0025f && AngularVelocity.sqrMagnitude < 25f)
        {
            Quaternion rest = RestRotation(Rotation);
            if (Quaternion.Angle(Rotation, rest) < 2f)
            {
                Rotation = rest;
                Velocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                AtRest = true;
            }
        }
    }
}
