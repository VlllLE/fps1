using UnityEngine;

public enum BotStuckAction
{
    None = 0,
    Repath,
    NewGoal,
    Steering,
    Respawn
}

/// <summary>
/// Detects a bot that wants to move but is not getting anywhere and escalates: repath, new goal, direct steering,
/// respawn. Progress means getting <see cref="MinProgress"/> metres away from an anchor position; moving back and forth
/// in place therefore still counts as stuck. A second, slower check catches bots that keep wandering in a small area.
/// </summary>
public sealed class BotStuckDetector
{
    private Vector3 anchor;
    private Vector3 longAnchor;
    private float elapsed;
    private float longElapsed;
    private int longLevel;
    private bool hasAnchor;

    public float Window { get; set; } = 2f;
    public float MinProgress { get; set; } = 0.6f;
    public float LongWindow { get; set; } = 14f;
    public float LongMinProgress { get; set; } = 3f;

    /// <summary>Escalation step reached so far (0 = moving fine).</summary>
    public int Level { get; private set; }
    public float TimeWithoutProgress => elapsed;

    public void Reset(Vector3 position)
    {
        anchor = position;
        longAnchor = position;
        elapsed = 0f;
        longElapsed = 0f;
        Level = 0;
        longLevel = 0;
        hasAnchor = true;
    }

    /// <param name="shouldMove">The bot currently intends to move (independent of whether it succeeds).</param>
    /// <param name="trackLongTerm">Enable the slow wander check (disable while fighting, where strafing is expected).</param>
    public BotStuckAction Update(float dt, Vector3 position, bool shouldMove, bool trackLongTerm = true)
    {
        if (!hasAnchor)
            Reset(position);

        if (!shouldMove)
        {
            anchor = position;
            elapsed = 0f;
            if (!trackLongTerm)
            {
                longAnchor = position;
                longElapsed = 0f;
            }

            return BotStuckAction.None;
        }

        dt = Mathf.Max(0f, dt);

        if (Planar(anchor, position) >= MinProgress)
        {
            anchor = position;
            elapsed = 0f;
            Level = 0;
        }
        else
        {
            elapsed += dt;
        }

        if (trackLongTerm)
        {
            if (Planar(longAnchor, position) >= LongMinProgress)
            {
                longAnchor = position;
                longElapsed = 0f;
                longLevel = 0;
            }
            else
            {
                longElapsed += dt;
            }
        }

        if (trackLongTerm && longElapsed >= LongWindow)
        {
            longElapsed = 0f;
            longLevel++;
            if (longLevel >= 2)
            {
                Reset(position);
                return BotStuckAction.Respawn;
            }

            return BotStuckAction.NewGoal;
        }

        if (elapsed >= Window)
        {
            elapsed = 0f;
            Level++;
            switch (Level)
            {
                case 1: return BotStuckAction.Repath;
                case 2: return BotStuckAction.NewGoal;
                case 3: return BotStuckAction.Steering;
                default:
                    Reset(position);
                    return BotStuckAction.Respawn;
            }
        }

        return BotStuckAction.None;
    }

    private static float Planar(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}

/// <summary>Fires once when an attacking bot has been unable to shoot for too long (no clear line of fire at a corner).</summary>
public sealed class BotEngagementWatchdog
{
    private float timer;

    public float Limit { get; set; } = 3f;
    public float Timer => timer;

    public void Reset()
    {
        timer = 0f;
    }

    /// <returns>True once per <see cref="Limit"/> seconds of <paramref name="blocked"/> time; the bot should reposition.</returns>
    public bool Update(float dt, bool blocked)
    {
        if (!blocked)
        {
            timer = 0f;
            return false;
        }

        timer += Mathf.Max(0f, dt);
        if (timer < Limit)
            return false;

        timer = 0f;
        return true;
    }
}

/// <summary>Pushes bots apart so they never share a spot.</summary>
public static class BotSeparation
{
    private const float GoldenAngle = 2.39996323f;

    /// <summary>
    /// Planar push away from every neighbour closer than <paramref name="radius"/>, strongest when overlapping, with a
    /// magnitude of at most 1. <paramref name="selfIndex"/> breaks ties when two bots occupy exactly the same point.
    /// </summary>
    public static Vector3 Compute(Vector3 self, int selfIndex, System.Collections.Generic.IList<Vector3> others, float radius)
    {
        if (others == null || radius <= 0f)
            return Vector3.zero;

        Vector3 push = Vector3.zero;
        for (int i = 0; i < others.Count; i++)
        {
            float dx = self.x - others[i].x;
            float dz = self.z - others[i].z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance >= radius)
                continue;

            Vector3 direction;
            if (distance < 0.05f)
            {
                float angle = (selfIndex & 0xFFFF) * GoldenAngle;
                direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
            else
            {
                direction = new Vector3(dx / distance, 0f, dz / distance);
            }

            push += direction * (1f - distance / radius);
        }

        return push.magnitude > 1f ? push.normalized : push;
    }

    /// <summary>Blends the separation push into a desired move direction; returns a unit vector (or zero).</summary>
    public static Vector3 Blend(Vector3 desired, Vector3 separation, float strength)
    {
        desired.y = 0f;
        Vector3 blended = desired + separation * strength;
        blended.y = 0f;
        return blended.sqrMagnitude < 1e-6f ? Vector3.zero : blended.normalized;
    }
}
