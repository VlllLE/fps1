using UnityEngine;

/// <summary>Anything a bot can see, hear and shoot at: the local player or another team's bot.</summary>
public interface IBotTarget
{
    Transform TargetTransform { get; }

    /// <summary>Chest height point the bots aim at.</summary>
    Vector3 AimPoint { get; }

    /// <summary>Second line-of-sight probe.</summary>
    Vector3 HeadPoint { get; }

    /// <summary>Horizontal speed in m/s; fast movers are heard from further away.</summary>
    float Speed { get; }

    bool IsDead { get; }

    CombatTeam Team { get; }
}
