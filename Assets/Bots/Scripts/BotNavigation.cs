using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum BotNavMode
{
    /// <summary>Nothing built yet.</summary>
    None = 0,
    /// <summary>A NavMesh was generated at runtime from the scene's physics colliders.</summary>
    NavMesh,
    /// <summary>No usable NavMesh: bots walk straight lines, steer around obstacles with capsule casts and follow the ground with raycasts.</summary>
    Fallback
}

/// <summary>
/// Navigation service shared by all bots. The NavMesh is generated at runtime (no baked data, no AI Navigation package) from
/// the scene's non-trigger colliders; if that fails or produces nothing walkable, a raycast based fallback keeps bots moving.
/// </summary>
public static class BotNavigation
{
    public const float AgentRadius = 0.4f;
    public const float AgentHeight = 1.9f;
    public const float StepHeight = 0.55f;
    public const float MaxDrop = 1.6f;

    private static readonly Vector3[] CornerBuffer = new Vector3[64];
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[24];

    private static NavMeshDataInstance instance;
    private static NavMeshPath pathBuffer;

    public static BotNavMode Mode { get; private set; }
    public static bool IsReady => Mode != BotNavMode.None;
    public static Bounds SceneBounds { get; private set; }

    /// <summary>Removes any generated NavMesh and returns to <see cref="BotNavMode.None"/>.</summary>
    public static void Reset()
    {
        if (instance.valid)
            instance.Remove();
        instance = default;
        Mode = BotNavMode.None;
    }

    /// <summary>Test hook: force a mode without building anything.</summary>
    public static void ForceMode(BotNavMode mode)
    {
        if (mode != BotNavMode.NavMesh && instance.valid)
        {
            instance.Remove();
            instance = default;
        }

        Mode = mode;
    }

    /// <summary>
    /// Generates the runtime NavMesh over the current scene. <paramref name="excludeRoots"/> (player, bots, targets) are kept out
    /// of the build. Ends in <see cref="BotNavMode.NavMesh"/> or <see cref="BotNavMode.Fallback"/>; never throws.
    /// </summary>
    public static IEnumerator BuildRoutine(IList<Transform> excludeRoots)
    {
        Reset();

        AsyncOperation operation = null;
        try
        {
            operation = StartBuild(excludeRoots);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[BotNavigation] NavMesh build failed, using the fallback: " + exception.Message);
        }

        if (operation != null)
        {
            while (!operation.isDone)
                yield return null;
        }

        if (operation != null && instance.valid && HasWalkableSurface())
        {
            Mode = BotNavMode.NavMesh;
        }
        else
        {
            if (instance.valid)
                instance.Remove();
            instance = default;
            Mode = BotNavMode.Fallback;
        }
    }

    private static AsyncOperation StartBuild(IList<Transform> excludeRoots)
    {
        Collider[] colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        bool any = false;
        var bounds = new Bounds();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;
            if (IsUnder(collider.transform, excludeRoots))
                continue;

            if (!any)
            {
                bounds = collider.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (!any)
            return null;

        bounds.Expand(4f);
        SceneBounds = bounds;

        var markups = new List<NavMeshBuildMarkup>();
        if (excludeRoots != null)
        {
            for (int i = 0; i < excludeRoots.Count; i++)
            {
                if (excludeRoots[i] == null)
                    continue;
                markups.Add(new NavMeshBuildMarkup
                {
                    root = excludeRoots[i],
                    ignoreFromBuild = true,
                    applyToChildren = true
                });
            }
        }

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        if (sources.Count == 0)
            return null;

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = AgentRadius;
        settings.agentHeight = AgentHeight;
        settings.agentClimb = StepHeight;

        var data = new NavMeshData(settings.agentTypeID)
        {
            position = Vector3.zero,
            rotation = Quaternion.identity
        };
        instance = NavMesh.AddNavMeshData(data);
        return NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, bounds);
    }

    private static bool HasWalkableSurface()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        return triangulation.vertices != null && triangulation.vertices.Length >= 3 && triangulation.indices.Length >= 3;
    }

    private static bool IsUnder(Transform t, IList<Transform> roots)
    {
        if (roots == null)
            return false;
        for (int i = 0; i < roots.Count; i++)
        {
            Transform root = roots[i];
            if (root != null && (t == root || t.IsChildOf(root)))
                return true;
        }

        return false;
    }

    /// <summary>True for colliders belonging to the player, a bot or a training target (never ground or cover).</summary>
    public static bool IsCharacterCollider(Collider collider)
    {
        return collider.GetComponentInParent<BotController>() != null
            || collider.GetComponentInParent<PlayerHealth>() != null
            || collider.GetComponentInParent<Target>() != null;
    }

    /// <summary>
    /// Computes the corner list from <paramref name="from"/> to <paramref name="to"/>. With a NavMesh this is a real path
    /// (<paramref name="complete"/> is false for partial paths); in fallback mode it is the straight segment.
    /// </summary>
    public static bool TryCalculatePath(Vector3 from, Vector3 to, List<Vector3> corners, out bool complete)
    {
        corners.Clear();
        complete = false;

        if (Mode == BotNavMode.NavMesh)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 2.5f, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(to, out NavMeshHit end, 2.5f, NavMesh.AllAreas))
                return false;

            if (pathBuffer == null)
                pathBuffer = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, pathBuffer))
                return false;
            if (pathBuffer.status == NavMeshPathStatus.PathInvalid)
                return false;

            int count = pathBuffer.GetCornersNonAlloc(CornerBuffer);
            for (int i = 0; i < count; i++)
                corners.Add(CornerBuffer[i]);
            complete = pathBuffer.status == NavMeshPathStatus.PathComplete;
            return count > 0;
        }

        if (Mode == BotNavMode.Fallback)
        {
            corners.Add(to);
            complete = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Picks a random point around <paramref name="center"/> that the bot can walk to. NavMesh mode requires a complete path;
    /// fallback mode requires a clear, step-free straight walk.
    /// </summary>
    public static bool TryRandomReachablePoint(Vector3 center, float radius, System.Random rng, Transform ignoreRoot, List<Vector3> scratch, out Vector3 point)
    {
        point = center;
        for (int attempt = 0; attempt < 14; attempt++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float r = radius * Mathf.Sqrt((float)rng.NextDouble());
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            if (Mode == BotNavMode.NavMesh)
            {
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    continue;
                if (!TryCalculatePath(center, hit.position, scratch, out bool complete) || !complete)
                    continue;
                point = hit.position;
                return true;
            }

            if (Mode == BotNavMode.Fallback)
            {
                if (!TryGetGround(candidate, 3f, 8f, ignoreRoot, out Vector3 ground))
                    continue;
                if (!IsWalkableStraight(center, ground, ignoreRoot))
                    continue;
                point = ground;
                return true;
            }
        }

        return false;
    }

    /// <summary>Nearest walkable position to <paramref name="position"/>, or the position itself when unknown.</summary>
    public static Vector3 Snap(Vector3 position, Transform ignoreRoot)
    {
        if (Mode == BotNavMode.NavMesh)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                return hit.position;
        }
        else if (TryGetGround(position, 1.5f, 6f, ignoreRoot, out Vector3 ground))
        {
            return ground;
        }

        return position;
    }

    /// <summary>
    /// Moves one step (<paramref name="delta"/> is planar) without leaving walkable ground or entering geometry. Returns false
    /// when the bot could not move at all.
    /// </summary>
    public static bool TryStep(Vector3 position, Vector3 delta, Transform ignoreRoot, out Vector3 result)
    {
        result = position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 1e-5f)
            return true;

        if (Mode == BotNavMode.NavMesh)
        {
            Vector3 target = position + delta;
            if (NavMesh.Raycast(position, target, out NavMeshHit edge, NavMesh.AllAreas))
            {
                target = edge.position;
                if (Vector3.Distance(position, target) < distance * 0.25f)
                {
                    // Pinned against a NavMesh border: slide along it instead of freezing.
                    Vector3 along = Vector3.ProjectOnPlane(delta, edge.normal);
                    along.y = 0f;
                    target = position + along;
                    if (NavMesh.Raycast(position, target, out NavMeshHit slideEdge, NavMesh.AllAreas))
                        target = slideEdge.position;
                }
            }
            if (!NavMesh.SamplePosition(target, out NavMeshHit sample, 1.2f, NavMesh.AllAreas))
                return false;

            result = sample.position;
            return (result - position).sqrMagnitude > 1e-8f;
        }

        Vector3 direction = delta / distance;
        if (CapsuleBlocked(position, direction, distance + 0.05f, ignoreRoot, out Vector3 normal))
        {
            Vector3 slide = Vector3.ProjectOnPlane(delta, normal);
            slide.y = 0f;
            if (slide.sqrMagnitude < 1e-6f)
                return false;
            float slideDistance = slide.magnitude;
            Vector3 slideDirection = slide / slideDistance;
            if (CapsuleBlocked(position, slideDirection, slideDistance + 0.05f, ignoreRoot, out _))
                return false;
            delta = slide;
        }

        Vector3 next = position + delta;
        if (!TryGetGround(next, StepHeight + 0.2f, MaxDrop + StepHeight + 0.2f, ignoreRoot, out Vector3 ground))
            return false;
        if (ground.y > position.y + StepHeight || ground.y < position.y - MaxDrop)
            return false;

        result = ground;
        return true;
    }

    /// <summary>Raycasts down for walkable ground, ignoring the bot itself and any character collider.</summary>
    public static bool TryGetGround(Vector3 position, float up, float down, Transform ignoreRoot, out Vector3 ground)
    {
        ground = position;
        Vector3 origin = position + Vector3.up * up;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, HitBuffer, up + down, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                continue;
            if (IsCharacterCollider(hit.collider))
                continue;
            if (hit.normal.y < 0.5f)
                continue;
            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                ground = hit.point;
                found = true;
            }
        }

        return found;
    }

    private static bool CapsuleBlocked(Vector3 position, Vector3 direction, float distance, Transform ignoreRoot, out Vector3 normal)
    {
        normal = Vector3.up;
        float radius = AgentRadius - 0.05f;
        Vector3 bottom = position + Vector3.up * (StepHeight + radius);
        Vector3 top = position + Vector3.up * (AgentHeight - radius);
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, HitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                continue;
            if (IsCharacterCollider(hit.collider))
                continue;
            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                normal = hit.normal;
                blocked = true;
            }
        }

        return blocked;
    }

    private static bool IsWalkableStraight(Vector3 from, Vector3 to, Transform ignoreRoot)
    {
        Vector3 flat = to - from;
        flat.y = 0f;
        float distance = flat.magnitude;
        if (distance < 0.1f)
            return true;

        Vector3 direction = flat / distance;
        Vector3 previous = from;
        float travelled = 0f;
        while (travelled < distance)
        {
            float step = Mathf.Min(1.5f, distance - travelled);
            if (!TryStep(previous, direction * step, ignoreRoot, out Vector3 next))
                return false;
            previous = next;
            travelled += step;
        }

        return true;
    }

    /// <summary>
    /// Looks for a nearby spot hidden from <paramref name="threatEye"/>. Candidates are random reachable points within
    /// <paramref name="radius"/>; the closest one with no line of sight to its chest and knees wins.
    /// </summary>
    public static bool TryFindCover(
        Vector3 from,
        Vector3 threatEye,
        float radius,
        int samples,
        System.Random rng,
        Transform botRoot,
        Transform threatRoot,
        List<Vector3> scratch,
        out Vector3 cover)
    {
        cover = from;
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < samples; i++)
        {
            if (!TryRandomReachablePoint(from, radius, rng, botRoot, scratch, out Vector3 candidate))
                continue;

            bool chestVisible = BotPerception.HasLineOfSight(threatEye, candidate + Vector3.up * 1.2f, threatRoot, botRoot);
            bool kneeVisible = BotPerception.HasLineOfSight(threatEye, candidate + Vector3.up * 0.6f, threatRoot, botRoot);
            if (chestVisible || kneeVisible)
                continue;

            float distance = (candidate - from).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                cover = candidate;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Looks for a walkable spot within <paramref name="radius"/> of <paramref name="center"/> that stands close to a wall or
    /// ledge (within <paramref name="maxEdgeDistance"/>), preferring spots near the centre. Needs the NavMesh; returns false
    /// in fallback mode or when nothing suitable was sampled.
    /// </summary>
    public static bool TryFindWallPoint(
        Vector3 center,
        float radius,
        int samples,
        float maxEdgeDistance,
        System.Random rng,
        Transform ignoreRoot,
        List<Vector3> scratch,
        out Vector3 point)
    {
        point = center;
        if (Mode != BotNavMode.NavMesh)
            return false;

        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < samples; i++)
        {
            if (!TryRandomReachablePoint(center, radius, rng, ignoreRoot, scratch, out Vector3 candidate))
                continue;
            if (!NavMesh.FindClosestEdge(candidate, out NavMeshHit edge, NavMesh.AllAreas))
                continue;
            if (edge.distance > maxEdgeDistance)
                continue;

            float score = edge.distance * 2f + Vector3.Distance(candidate, center) * 0.4f;
            if (score < best)
            {
                best = score;
                point = candidate;
                found = true;
            }
        }

        if (found)
        {
            if (NavMesh.FindClosestEdge(point, out NavMeshHit closest, NavMesh.AllAreas) && closest.distance > 0.05f)
            {
                Vector3 inward = point - closest.position;
                inward.y = 0f;
                const float want = 0.3f;
                if (inward.sqrMagnitude > 1e-6f && closest.distance > want)
                {
                    Vector3 shifted = closest.position + inward.normalized * want;
                    if (NavMesh.SamplePosition(shifted, out NavMeshHit shiftedHit, 0.6f, NavMesh.AllAreas))
                        point = shiftedHit.position;
                }
            }
        }

        return found;
    }
}
