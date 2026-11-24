using UnityEngine;

/// <summary>
/// Resolves a short-range melee slash through the existing <see cref="HitDetection"/>.
/// A centre ray is fired first; if it does not damage anything, a small fan of rays
/// (left/right/up/down by <see cref="WeaponConfig.meleeRadius"/>) gives close-range forgiveness.
/// Stops at the first ray that applies damage, so a single swing never hurts a target twice.
/// </summary>
public static class MeleeAttackResolver
{
    public const int RayCount = 5;

    /// <summary>
    /// Builds the swing ray directions. Index 0 is always the centre ray.
    /// </summary>
    public static Vector3[] BuildDirections(Vector3 forward, Vector3 up, float range, float radius)
    {
        Vector3 f = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(up, f);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.Cross(Vector3.up, f);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.right;
        right.Normalize();
        Vector3 trueUp = Vector3.Cross(f, right).normalized;

        float reach = Mathf.Max(0.01f, range);
        float spread = Mathf.Max(0f, radius);

        return new[]
        {
            f,
            (f * reach + right * spread).normalized,
            (f * reach - right * spread).normalized,
            (f * reach + trueUp * spread).normalized,
            (f * reach - trueUp * spread).normalized
        };
    }

    /// <summary>
    /// Fires the swing rays and returns the result that damaged something, else the first
    /// result that struck any collider, else the (empty) centre-ray result.
    /// </summary>
    public static HitResult Resolve(
        HitDetection hitDetection,
        WeaponConfig config,
        Vector3 origin,
        Vector3 forward,
        Vector3 up,
        Transform ignoreRoot,
        int sourceId,
        long shotId,
        bool authoritative)
    {
        if (hitDetection == null || config == null)
            return null;

        float range = Mathf.Max(0.1f, config.meleeRange);
        Vector3[] directions = BuildDirections(forward, up, range, config.meleeRadius);

        HitResult firstContact = null;
        HitResult centre = null;

        for (int i = 0; i < directions.Length; i++)
        {
            HitRequest request = HitRequest.Create(origin, directions[i], range, config.damage);
            request.ShotId = shotId;
            request.SourceId = sourceId;
            request.IgnoreRoot = ignoreRoot;
            request.IsAuthoritative = authoritative;
            request.HeadMultiplier = config.headshotMultiplier;
            request.BodyMultiplier = 1f;
            request.LimbMultiplier = config.limbMultiplier;
            request.AllowPenetration = false;
            request.MaxPenetrations = 0;
            request.InvokeSurfaceResponses = i == 0;

            HitResult result = hitDetection.Resolve(request);
            if (i == 0)
                centre = result;

            if (result.TotalDamageApplied > 0f)
                return result;

            if (firstContact == null && result.DidHit)
                firstContact = result;
        }

        return firstContact ?? centre;
    }
}
