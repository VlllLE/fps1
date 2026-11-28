using UnityEngine;

namespace FpsTactical
{
    /// <summary>Wording and sizing rules of the tactical HUD, kept free of IMGUI so they can be unit-tested.</summary>
    public static class TacticalHudText
    {
        public const float MinUiWidth = 520f;
        public const float BaseUiHeight = 720f;

        /// <summary>
        /// Scale of the IMGUI HUD: 1 at 720p landscape, bigger on tall or high-resolution screens, and small enough that
        /// a narrow portrait phone screen still has at least <see cref="MinUiWidth"/> virtual pixels across.
        /// </summary>
        public static float ResolveScale(float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
                return 1f;
            return Mathf.Max(0.5f, Mathf.Min(screenHeight / BaseUiHeight, screenWidth / MinUiWidth));
        }

        /// <summary>Whole seconds, rounded up, as m:ss.</summary>
        public static string Clock(float seconds)
        {
            int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        public static string SideName(CombatTeam side)
        {
            switch (side)
            {
                case CombatTeam.Attackers: return "ATTACK";
                case CombatTeam.Defenders: return "DEFEND";
                default: return string.Empty;
            }
        }

        public static string SquadName(int squad)
        {
            return squad == 0 ? "ALPHA" : "BRAVO";
        }

        public static string PhaseLabel(TacticalPhase phase)
        {
            switch (phase)
            {
                case TacticalPhase.ClassSelect: return "CLASS SELECT";
                case TacticalPhase.Live: return "LIVE";
                case TacticalPhase.RoundEnd: return "ROUND OVER";
                case TacticalPhase.MatchEnd: return "MATCH OVER";
                default: return string.Empty;
            }
        }

        public static string RoleHint(CombatTeam side)
        {
            if (side == CombatTeam.Attackers)
                return "Plant the bomb at site A or B";
            if (side == CombatTeam.Defenders)
                return "Stop the bomb being planted, defuse it if it is";
            return string.Empty;
        }

        public static string BombLine(BombState state, string site, bool playerCarries, string carrierName, bool playerAttacks, float fuseRemaining)
        {
            switch (state)
            {
                case BombState.Carried:
                    if (playerCarries)
                        return "YOU HAVE THE BOMB";
                    if (playerAttacks)
                        return string.IsNullOrEmpty(carrierName) ? "BOMB CARRIED" : carrierName + " HAS THE BOMB";
                    return "BOMB NOT PLANTED";
                case BombState.Dropped:
                    return "BOMB DROPPED";
                case BombState.Planted:
                    return "BOMB PLANTED " + site + "  " + Clock(fuseRemaining);
                case BombState.Defused:
                    return "BOMB DEFUSED";
                case BombState.Exploded:
                    return "BOMB EXPLODED";
                default:
                    return string.Empty;
            }
        }

        public static string RoundTitle(bool won)
        {
            return won ? "ROUND WON" : "ROUND LOST";
        }

        public static string MatchTitle(bool won)
        {
            return won ? "VICTORY" : "DEFEAT";
        }

        public static string ActionHint(bool plantable, bool defusable)
        {
            if (plantable)
                return "HOLD E TO PLANT";
            if (defusable)
                return "HOLD E TO DEFUSE";
            return null;
        }

        public static string ProgressLabel(bool planting, bool byPlayer)
        {
            if (planting)
                return byPlayer ? "PLANTING" : "BOMB BEING PLANTED";
            return byPlayer ? "DEFUSING" : "BOMB BEING DEFUSED";
        }
    }
}
