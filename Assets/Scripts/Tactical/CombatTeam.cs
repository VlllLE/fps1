using UnityEngine;

/// <summary>
/// Side a combatant fights on in a team mode. <see cref="None"/> is the classic practice behaviour: bots only ever
/// target the local player and nobody has teammates.
/// </summary>
public enum CombatTeam
{
    None = 0,
    Attackers = 1,
    Defenders = 2
}

/// <summary>Team rules shared by the player, the bots and the tactical match.</summary>
public static class CombatTeams
{
    /// <summary>When false (the default) shots from a teammate never hurt.</summary>
    public static bool FriendlyFire { get; set; }

    public static CombatTeam Opposite(CombatTeam team)
    {
        switch (team)
        {
            case CombatTeam.Attackers: return CombatTeam.Defenders;
            case CombatTeam.Defenders: return CombatTeam.Attackers;
            default: return CombatTeam.None;
        }
    }

    /// <summary>True when both sides are real teams and they differ.</summary>
    public static bool AreEnemies(CombatTeam a, CombatTeam b)
    {
        return a != CombatTeam.None && b != CombatTeam.None && a != b;
    }

    /// <summary>
    /// Whether a hit from <paramref name="attacker"/> may hurt <paramref name="victim"/>. Anything involving
    /// <see cref="CombatTeam.None"/> keeps the practice rules (the callers handle bot against bot there).
    /// </summary>
    public static bool CanDamage(CombatTeam attacker, CombatTeam victim)
    {
        if (attacker == CombatTeam.None || victim == CombatTeam.None)
            return true;
        return attacker != victim || FriendlyFire;
    }

    public static Color ColorOf(CombatTeam team)
    {
        switch (team)
        {
            case CombatTeam.Attackers: return new Color(1f, 0.55f, 0.15f);
            case CombatTeam.Defenders: return new Color(0.25f, 0.6f, 1f);
            default: return Color.white;
        }
    }
}
