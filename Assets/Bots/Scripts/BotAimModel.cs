using System;
using UnityEngine;

/// <summary>
/// Pure aim model: a limited-speed aim line that chases the desired direction, plus a difficulty-scaled error cone
/// that shrinks while the bot keeps tracking the target.
/// </summary>
public sealed class BotAimModel
{
    /// <summary>Tracking error (degrees) below which the aim counts as "on target".</summary>
    public const float OnTargetDegrees = 6f;

    private readonly BotSettings settings;

    public BotAimModel(BotSettings settings)
    {
        this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        AimDirection = Vector3.forward;
    }

    public Vector3 AimDirection { get; private set; }
    /// <summary>Seconds the aim has stayed on the target.</summary>
    public float TimeOnTarget { get; private set; }
    /// <summary>Angle in degrees between the aim line and the desired direction after the last <see cref="Track"/>.</summary>
    public float TrackingError { get; private set; }

    public bool IsOnTarget => TrackingError <= OnTargetDegrees;

    /// <summary>1 right after the bot was hit, fading to 0 over the flinch time; adds to the aim error.</summary>
    public float Flinch { get; private set; }

    /// <summary>Called when the bot takes damage: its aim is thrown off for a moment.</summary>
    public void NotifyHit()
    {
        Flinch = 1f;
    }

    public void ClearFlinch()
    {
        Flinch = 0f;
    }

    public void SnapTo(Vector3 direction)
    {
        if (direction.sqrMagnitude > 1e-6f)
            AimDirection = direction.normalized;
        TrackingError = 0f;
    }

    public void ResetTracking()
    {
        TimeOnTarget = 0f;
        TrackingError = 180f;
    }

    /// <summary>Rotates the aim line towards <paramref name="desired"/> at the configured turn rate.</summary>
    public void Track(Vector3 desired, float dt)
    {
        if (Flinch > 0f)
            Flinch = Mathf.Max(0f, Flinch - Mathf.Max(0f, dt) / Mathf.Max(0.05f, settings.flinchTime));

        if (desired.sqrMagnitude < 1e-6f)
            return;

        desired.Normalize();
        float maxRadians = settings.aimTurnSpeed * Mathf.Deg2Rad * Mathf.Max(0f, dt);
        AimDirection = Vector3.RotateTowards(AimDirection, desired, maxRadians, 0f).normalized;
        TrackingError = Vector3.Angle(AimDirection, desired);

        if (TrackingError <= OnTargetDegrees)
            TimeOnTarget += dt;
        else
            TimeOnTarget = Mathf.Max(0f, TimeOnTarget - dt * 2f);
    }

    /// <summary>
    /// Radius in degrees of the cone the next shot is spread over (bot skill only; weapon spread is added separately).
    /// </summary>
    public float ComputeInaccuracy(float ownSpeed, float targetSpeed)
    {
        float settle = Mathf.Clamp01(TimeOnTarget / Mathf.Max(0.05f, settings.aimSettleTime));
        float scale = Mathf.Lerp(1f, settings.settledErrorFraction, settle);
        float error = settings.aimErrorDegrees * scale;
        if (ownSpeed > 0.5f)
            error += settings.aimErrorWhileMoving;
        error += settings.aimErrorPerTargetSpeed * Mathf.Max(0f, targetSpeed);
        error += settings.flinchError * Flinch;
        return error;
    }

    /// <summary>Deflects <paramref name="direction"/> by a random angle up to <paramref name="degrees"/> (uniform over the disc).</summary>
    public static Vector3 ApplyInaccuracy(Vector3 direction, float degrees, System.Random rng)
    {
        if (degrees <= 0f || direction.sqrMagnitude < 1e-6f)
            return direction.normalized;

        float radians = degrees * Mathf.Deg2Rad;
        float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
        float radius = Mathf.Tan(radians) * Mathf.Sqrt((float)rng.NextDouble());
        var offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 1f);
        return (Quaternion.LookRotation(direction.normalized) * offset.normalized).normalized;
    }
}
