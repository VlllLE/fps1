using System;

namespace FpsTactical
{
    /// <summary>
    /// Tunable numbers of one tactical match. The defaults are a short best-of-seven (first to four rounds, sides swap
    /// after three) with four players a side and no economy.
    /// </summary>
    [Serializable]
    public sealed class TacticalRules
    {
        /// <summary>Rounds a squad needs to win the match. Sides swap after <see cref="SwapAfterRounds"/> rounds.</summary>
        public int roundsToWin = 4;

        /// <summary>Seconds of class select / freeze time before every round.</summary>
        public float freezeSeconds = 15f;

        /// <summary>Seconds a live round lasts when the bomb is not planted.</summary>
        public float roundSeconds = 100f;

        /// <summary>Seconds the result is shown before the next round.</summary>
        public float roundEndSeconds = 5f;

        /// <summary>Seconds the final result is shown before a new match starts.</summary>
        public float matchEndSeconds = 10f;

        /// <summary>Seconds the bomb carrier must hold the plant action inside a site.</summary>
        public float plantSeconds = 3.2f;

        /// <summary>Seconds a defender must hold the defuse action next to the bomb.</summary>
        public float defuseSeconds = 7f;

        /// <summary>Seconds between the plant and the explosion.</summary>
        public float fuseSeconds = 40f;

        /// <summary>Combatants per side, the player included in the player's squad.</summary>
        public int playersPerSide = 4;

        /// <summary>Radius around the bomb in which a defender can defuse it and an attacker can pick it up.</summary>
        public float bombReach = 2.2f;

        public bool friendlyFire;

        /// <summary>The player's squad attacks in round one (otherwise it defends and swaps to attack at half time).</summary>
        public bool playerAttacksFirst = true;

        public int SwapAfterRounds => Math.Max(1, roundsToWin - 1);

        /// <summary>Largest number of rounds a match can last.</summary>
        public int MaxRounds => Math.Max(1, roundsToWin * 2 - 1);

        public TacticalRules Clone()
        {
            return (TacticalRules)MemberwiseClone();
        }
    }
}
