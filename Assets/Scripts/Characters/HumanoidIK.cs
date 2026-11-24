using UnityEngine;

/// <summary>Allocation-free analytic two-bone IK used for procedural legs and arms.</summary>
public static class HumanoidIK
{
    /// <summary>
    /// Solves a two-bone chain. <paramref name="mid"/> lies on the circle of valid joint positions,
    /// on the side of <paramref name="pole"/>. When the target is out of reach the chain is fully
    /// extended towards it (or folded when closer than |upper - lower|) and <paramref name="tip"/>
    /// is the reachable end position.
    /// </summary>
    /// <returns>True when the target was reachable without clamping.</returns>
    public static bool SolveTwoBone(
        Vector3 root,
        Vector3 target,
        float upperLength,
        float lowerLength,
        Vector3 pole,
        out Vector3 mid,
        out Vector3 tip)
    {
        Vector3 toTarget = target - root;
        float rawDistance = toTarget.magnitude;
        Vector3 dir = rawDistance > 1e-5f ? toTarget / rawDistance : Vector3.down;

        float minReach = Mathf.Abs(upperLength - lowerLength) + 1e-3f;
        float maxReach = upperLength + lowerLength - 1e-3f;
        float distance = Mathf.Clamp(rawDistance, minReach, maxReach);
        bool reachable = rawDistance >= minReach && rawDistance <= maxReach;

        float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));

        Vector3 bendDir = pole - dir * Vector3.Dot(pole, dir);
        if (bendDir.sqrMagnitude < 1e-8f)
        {
            bendDir = Vector3.Cross(dir, Vector3.right);
            if (bendDir.sqrMagnitude < 1e-8f)
                bendDir = Vector3.Cross(dir, Vector3.forward);
        }

        bendDir = bendDir / Mathf.Sqrt(bendDir.sqrMagnitude);
        mid = root + dir * along + bendDir * height;
        tip = root + dir * distance;
        return reachable;
    }
}
