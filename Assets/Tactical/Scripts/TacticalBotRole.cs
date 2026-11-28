namespace FpsTactical
{
    /// <summary>The job the <see cref="TacticalBotDirector"/> gave a bot for the current round or phase.</summary>
    public enum TacticalBotRole
    {
        None = 0,
        /// <summary>Attacker holding the bomb; trails the entries and plants.</summary>
        Carrier,
        /// <summary>Attacker sent to pick up a dropped bomb.</summary>
        Picker,
        /// <summary>Attacker that pushes the site first.</summary>
        Entry,
        /// <summary>Attacker that follows the entries and holds a flank of the site.</summary>
        Support,
        /// <summary>Attacker taking an alternative route to the site.</summary>
        Flanker,
        /// <summary>Attacker guarding the planted bomb.</summary>
        PostPlant,
        /// <summary>Defender holding a site.</summary>
        Anchor,
        /// <summary>Defender that left his site to help where the contact is.</summary>
        Rotator,
        /// <summary>Defender sent to defuse the planted bomb.</summary>
        Defuser,
        /// <summary>Defender covering the defuser (retake).</summary>
        Cover
    }
}
