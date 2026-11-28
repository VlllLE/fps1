using UnityEngine;

public enum TacticalZoneKind
{
    /// <summary>A bomb site the attackers can plant on.</summary>
    Plant,
    /// <summary>A team's spawn yard.</summary>
    Buy
}

/// <summary>
/// Marker implemented by map components that label a plant zone or a team yard. The tactical match finds zones through
/// this interface so maps do not have to reference the mode.
/// </summary>
public interface ITacticalZone
{
    TacticalZoneKind ZoneKind { get; }

    /// <summary>"A" or "B" for plant zones, "Attack" or "Defend" for yards.</summary>
    string ZoneSite { get; }

    Bounds ZoneBounds { get; }

    bool Contains(Vector3 worldPoint);
}
