using System;
using System.Collections.Generic;

namespace FpsTactical
{
    /// <summary>One fighter of a tactical match as the scoreboard sees it.</summary>
    public sealed class TacticalParticipant
    {
        public TacticalParticipant(string name, int squad, bool isPlayer, BotController bot)
        {
            Name = name;
            Squad = squad;
            IsPlayer = isPlayer;
            Bot = bot;
        }

        public string Name { get; }
        public int Squad { get; }
        public bool IsPlayer { get; }
        public BotController Bot { get; }
        public int Kills { get; internal set; }
        public int Deaths { get; internal set; }
    }

    public readonly struct KillFeedEntry
    {
        public KillFeedEntry(string killer, int killerSquad, string victim, int victimSquad, bool headshot, float time)
        {
            Killer = killer;
            KillerSquad = killerSquad;
            Victim = victim;
            VictimSquad = victimSquad;
            Headshot = headshot;
            Time = time;
        }

        /// <summary>Name of the killer, or null when nobody is to blame (a direct damage call, the bomb).</summary>
        public string Killer { get; }
        public int KillerSquad { get; }
        public string Victim { get; }
        public int VictimSquad { get; }
        public bool Headshot { get; }
        public float Time { get; }
    }

    /// <summary>Kills and deaths per fighter plus a short kill feed. Pure data, so it is unit-testable.</summary>
    public sealed class TacticalStats
    {
        public const int MaxFeedEntries = 6;

        private readonly List<TacticalParticipant> participants = new List<TacticalParticipant>();
        private readonly List<KillFeedEntry> feed = new List<KillFeedEntry>();

        public IReadOnlyList<TacticalParticipant> Participants => participants;
        public IReadOnlyList<KillFeedEntry> KillFeed => feed;

        public event Action<KillFeedEntry> KillRecorded;

        public void Clear()
        {
            participants.Clear();
            feed.Clear();
        }

        public TacticalParticipant Add(string name, int squad, bool isPlayer, BotController bot)
        {
            var participant = new TacticalParticipant(name, squad, isPlayer, bot);
            participants.Add(participant);
            return participant;
        }

        /// <summary>Zeroes every score and the kill feed (a new match), keeping the fighters.</summary>
        public void ResetScores()
        {
            for (int i = 0; i < participants.Count; i++)
            {
                participants[i].Kills = 0;
                participants[i].Deaths = 0;
            }

            feed.Clear();
        }

        public TacticalParticipant FindBot(BotController bot)
        {
            if (bot == null)
                return null;
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].Bot == bot)
                    return participants[i];
            }

            return null;
        }

        public TacticalParticipant FindPlayer()
        {
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].IsPlayer)
                    return participants[i];
            }

            return null;
        }

        /// <summary>Counts a death and, when there is a killer, a kill (a team kill takes one away).</summary>
        public void RecordDeath(TacticalParticipant victim, TacticalParticipant killer, bool headshot, float time)
        {
            if (victim == null)
                return;

            victim.Deaths++;
            if (killer != null && killer != victim)
                killer.Kills += killer.Squad == victim.Squad ? -1 : 1;

            var entry = new KillFeedEntry(killer != null && killer != victim ? killer.Name : null,
                killer != null ? killer.Squad : -1, victim.Name, victim.Squad, headshot, time);
            feed.Add(entry);
            while (feed.Count > MaxFeedEntries)
                feed.RemoveAt(0);
            KillRecorded?.Invoke(entry);
        }

        /// <summary>The fighters of one squad, best score first.</summary>
        public void CollectSquad(int squad, List<TacticalParticipant> into)
        {
            into.Clear();
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].Squad == squad)
                    into.Add(participants[i]);
            }

            into.Sort((a, b) =>
            {
                int byKills = b.Kills.CompareTo(a.Kills);
                if (byKills != 0)
                    return byKills;
                int byDeaths = a.Deaths.CompareTo(b.Deaths);
                return byDeaths != 0 ? byDeaths : string.CompareOrdinal(a.Name, b.Name);
            });
        }
    }
}
