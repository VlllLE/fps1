using System;

namespace FpsTactical
{
    public enum TacticalPhase
    {
        /// <summary>Not started.</summary>
        Inactive,
        /// <summary>Freeze time: nobody moves, players pick their class.</summary>
        ClassSelect,
        Live,
        /// <summary>The result of the round is on screen.</summary>
        RoundEnd,
        /// <summary>The match is decided.</summary>
        MatchEnd
    }

    public enum RoundEndReason
    {
        None,
        AttackersEliminated,
        DefendersEliminated,
        BombExploded,
        BombDefused,
        TimeExpired
    }

    public readonly struct RoundResult
    {
        public RoundResult(int round, CombatTeam winnerSide, int winnerSquad, RoundEndReason reason)
        {
            Round = round;
            WinnerSide = winnerSide;
            WinnerSquad = winnerSquad;
            Reason = reason;
        }

        public int Round { get; }
        public CombatTeam WinnerSide { get; }
        public int WinnerSquad { get; }
        public RoundEndReason Reason { get; }
    }

    /// <summary>
    /// The rules of a tactical match as a plain state machine: freeze time, a timed live round, a short result pause,
    /// score to N, and a side swap at half time. Two squads fight; squad 0 (the player's) attacks first unless the rules say otherwise. Win conditions are
    /// elimination, bomb explosion, defuse and time out (a planted bomb switches the round clock off).
    /// </summary>
    public sealed class TacticalMatchModel
    {
        private readonly TacticalRules rules;
        private readonly int[] scores = new int[2];
        private bool endedThisRound;

        public TacticalMatchModel(TacticalRules rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Bomb = new TacticalBombModel(rules.plantSeconds, rules.defuseSeconds, rules.fuseSeconds);
        }

        public TacticalRules Rules => rules;
        public TacticalBombModel Bomb { get; }

        public TacticalPhase Phase { get; private set; }

        /// <summary>1-based number of the current round.</summary>
        public int RoundNumber { get; private set; }

        /// <summary>Seconds left in the current phase (for Live: the round clock; frozen while the bomb is planted).</summary>
        public float PhaseRemaining { get; private set; }

        /// <summary>Squad that attacks in the current round (squad 0 is the player's).</summary>
        public int AttackerSquad { get; private set; }

        public bool SidesSwapped { get; private set; }
        public RoundResult LastResult { get; private set; }
        public int MatchWinnerSquad { get; private set; } = -1;
        public bool MatchFinished => Phase == TacticalPhase.MatchEnd && PhaseRemaining <= 0f;
        public int RoundsPlayed => scores[0] + scores[1];

        public int Score(int squad) => scores[squad];

        public CombatTeam SideOf(int squad) => squad == AttackerSquad ? CombatTeam.Attackers : CombatTeam.Defenders;

        public int SquadOf(CombatTeam side) => side == CombatTeam.Attackers ? AttackerSquad : 1 - AttackerSquad;

        public event Action<TacticalPhase, TacticalPhase> PhaseChanged;
        public event Action<RoundResult> RoundEnded;
        public event Action HalfTimeSwap;

        /// <summary>Starts a fresh match: scores cleared, squad 0 attacking, freeze time of round one.</summary>
        public void Start()
        {
            scores[0] = 0;
            scores[1] = 0;
            RoundNumber = 1;
            AttackerSquad = rules.playerAttacksFirst ? 0 : 1;
            SidesSwapped = false;
            MatchWinnerSquad = -1;
            LastResult = default;
            BeginFreeze();
        }

        /// <summary>Ends the freeze time at once (every human is ready).</summary>
        public void SkipFreeze()
        {
            if (Phase == TacticalPhase.ClassSelect)
                BeginLive();
        }

        /// <summary>
        /// Advances the match. <paramref name="aliveAttackers"/> / <paramref name="aliveDefenders"/> are the living
        /// combatants of each side, <paramref name="plantSite"/> the site the bomb carrier stands in (or null) and
        /// <paramref name="planting"/> / <paramref name="defusing"/> whether those actions are held.
        /// </summary>
        public void Tick(float dt, int aliveAttackers, int aliveDefenders, string plantSite, bool planting, bool defusing)
        {
            dt = Math.Max(0f, dt);
            switch (Phase)
            {
                case TacticalPhase.ClassSelect:
                    PhaseRemaining -= dt;
                    if (PhaseRemaining <= 0f)
                        BeginLive();
                    break;

                case TacticalPhase.Live:
                    TickLive(dt, aliveAttackers, aliveDefenders, plantSite, planting, defusing);
                    break;

                case TacticalPhase.RoundEnd:
                    PhaseRemaining -= dt;
                    if (PhaseRemaining <= 0f)
                        FinishRoundEnd();
                    break;

                case TacticalPhase.MatchEnd:
                    PhaseRemaining = Math.Max(0f, PhaseRemaining - dt);
                    break;
            }
        }

        private void TickLive(float dt, int aliveAttackers, int aliveDefenders, string plantSite, bool planting, bool defusing)
        {
            Bomb.Tick(dt, plantSite, planting, defusing);

            if (Bomb.State == BombState.Exploded)
            {
                EndRound(CombatTeam.Attackers, RoundEndReason.BombExploded);
                return;
            }

            if (Bomb.State == BombState.Defused)
            {
                EndRound(CombatTeam.Defenders, RoundEndReason.BombDefused);
                return;
            }

            bool planted = Bomb.State == BombState.Planted;
            if (aliveDefenders <= 0 && (aliveAttackers > 0 || planted))
            {
                EndRound(CombatTeam.Attackers, RoundEndReason.DefendersEliminated);
                return;
            }

            if (aliveAttackers <= 0 && !planted)
            {
                EndRound(CombatTeam.Defenders, RoundEndReason.AttackersEliminated);
                return;
            }

            if (!planted)
            {
                PhaseRemaining -= dt;
                if (PhaseRemaining <= 0f)
                {
                    PhaseRemaining = 0f;
                    EndRound(CombatTeam.Defenders, RoundEndReason.TimeExpired);
                }
            }
        }

        private void EndRound(CombatTeam winnerSide, RoundEndReason reason)
        {
            if (Phase != TacticalPhase.Live)
                return;

            int squad = SquadOf(winnerSide);
            scores[squad]++;
            LastResult = new RoundResult(RoundNumber, winnerSide, squad, reason);
            endedThisRound = true;
            SetPhase(TacticalPhase.RoundEnd, rules.roundEndSeconds);
            RoundEnded?.Invoke(LastResult);
        }

        private void FinishRoundEnd()
        {
            if (!endedThisRound)
                return;
            endedThisRound = false;

            for (int squad = 0; squad < 2; squad++)
            {
                if (scores[squad] >= rules.roundsToWin)
                {
                    MatchWinnerSquad = squad;
                    SetPhase(TacticalPhase.MatchEnd, rules.matchEndSeconds);
                    return;
                }
            }

            RoundNumber++;
            if (RoundsPlayed == rules.SwapAfterRounds && rules.roundsToWin > 1)
            {
                AttackerSquad = 1 - AttackerSquad;
                SidesSwapped = !SidesSwapped;
                HalfTimeSwap?.Invoke();
            }

            BeginFreeze();
        }

        private void BeginFreeze()
        {
            Bomb.Reset();
            SetPhase(TacticalPhase.ClassSelect, rules.freezeSeconds);
        }

        private void BeginLive()
        {
            Bomb.Reset();
            SetPhase(TacticalPhase.Live, rules.roundSeconds);
        }

        private void SetPhase(TacticalPhase next, float seconds)
        {
            TacticalPhase previous = Phase;
            Phase = next;
            PhaseRemaining = seconds;
            PhaseChanged?.Invoke(previous, next);
        }

        public static string Describe(RoundEndReason reason)
        {
            switch (reason)
            {
                case RoundEndReason.AttackersEliminated: return "Attackers eliminated";
                case RoundEndReason.DefendersEliminated: return "Defenders eliminated";
                case RoundEndReason.BombExploded: return "Bomb exploded";
                case RoundEndReason.BombDefused: return "Bomb defused";
                case RoundEndReason.TimeExpired: return "Time ran out";
                default: return string.Empty;
            }
        }
    }
}
