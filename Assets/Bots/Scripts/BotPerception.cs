using UnityEngine;

/// <summary>View cone and line-of-sight checks for bots.</summary>
public static class BotPerception
{
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

    /// <summary>
    /// True when <paramref name="targetPoint"/> is within range and inside the cone (full angle <paramref name="fieldOfView"/>)
    /// around <paramref name="forward"/>. Targets inside <paramref name="closeRadius"/> are noticed from any direction.
    /// </summary>
    public static bool IsInViewCone(Vector3 eye, Vector3 forward, Vector3 targetPoint, float fieldOfView, float range, float closeRadius)
    {
        Vector3 to = targetPoint - eye;
        float distance = to.magnitude;
        if (distance > range)
            return false;
        if (distance <= Mathf.Max(0.01f, closeRadius))
            return true;
        if (forward.sqrMagnitude < 1e-6f)
            return false;
        if (fieldOfView >= 359f)
            return true;

        return Vector3.Angle(forward, to) <= fieldOfView * 0.5f;
    }

    /// <summary>
    /// True when nothing but the bot itself (<paramref name="ignoreRoot"/>) or the target (<paramref name="targetRoot"/>)
    /// lies between <paramref name="eye"/> and <paramref name="targetPoint"/>. Triggers are ignored.
    /// </summary>
    public static bool HasLineOfSight(Vector3 eye, Vector3 targetPoint, Transform ignoreRoot, Transform targetRoot, int layerMask = ~0)
    {
        Vector3 to = targetPoint - eye;
        float distance = to.magnitude;
        if (distance < 0.01f)
            return true;

        int count = Physics.RaycastNonAlloc(eye, to / distance, HitBuffer, distance, layerMask, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            Transform hit = HitBuffer[i].collider.transform;
            if (ignoreRoot != null && (hit == ignoreRoot || hit.IsChildOf(ignoreRoot)))
                continue;
            if (targetRoot != null && (hit == targetRoot || hit.IsChildOf(targetRoot)))
                continue;
            if (HitBuffer[i].distance < nearest)
            {
                nearest = HitBuffer[i].distance;
                blocked = true;
            }
        }

        return !blocked;
    }

    /// <summary>Radius within which a player moving at <paramref name="speed"/> m/s can be heard.</summary>
    public static float HearingRadius(float speed, float hearingScale)
    {
        float radius;
        if (speed > 6f)
            radius = 16f;
        else if (speed > 3.5f)
            radius = 10f;
        else if (speed > 0.8f)
            radius = 5f;
        else
            radius = 1.5f;
        return radius * Mathf.Max(0f, hearingScale);
    }
}
