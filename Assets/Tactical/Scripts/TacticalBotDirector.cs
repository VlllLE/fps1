using System.Collections.Generic;
using UnityEngine;

namespace FpsTactical
{
    /// <summary>
    /// Gives the tactical bots something to do besides patrolling: the attackers escort the bomb to a site (the carrier
    /// plants it) and guard it afterwards, the defenders hold the sites and, once the bomb is down, the nearest one
    /// defuses while the others cover him. A bot that meets an enemy fights first (the bot brain decides) and resumes
    /// its order afterwards. This is the simple first version of the team play; it only issues orders through
    /// <see cref="BotController.SetObjective"/>.
    /// </summary>
    public sealed class TacticalBotDirector
    {
        private const float Interval = 0.4f;
        private const float GuardRadius = 6.5f;

        private readonly TacticalMatch match;
        private readonly List<BotController> attackers = new List<BotController>();
        private readonly List<BotController> defenders = new List<BotController>();
        private readonly Dictionary<BotController, int> slots = new Dictionary<BotController, int>();
        private readonly Dictionary<BotController, Vector3> posts = new Dictionary<BotController, Vector3>();
        private float timer;
        private bool assigned;
        private int defenderOffset;

        public TacticalBotDirector(TacticalMatch match)
        {
            this.match = match;
        }

        /// <summary>The defender currently sent to defuse, or null.</summary>
        public BotController Defuser { get; private set; }

        /// <summary>The attacker sent to pick the bomb up, or null.</summary>
        public BotController Picker { get; private set; }

        public void ResetRound()
        {
            timer = 0f;
            assigned = false;
            Defuser = null;
            Picker = null;
            slots.Clear();
            posts.Clear();
            defenderOffset = Random.Range(0, 2);
        }

        public void OnBombPlanted()
        {
            timer = 0f;
        }

        public void Tick(float dt)
        {
            timer -= dt;
            if (timer > 0f)
                return;
            timer = Interval;

            attackers.Clear();
            defenders.Clear();
            match.CollectAliveBots(CombatTeam.Attackers, attackers);
            match.CollectAliveBots(CombatTeam.Defenders, defenders);

            if (!assigned)
                AssignSlots();

            TacticalBombModel bomb = match.Model.Bomb;
            DirectAttackers(bomb);
            DirectDefenders(bomb);
        }

        private void AssignSlots()
        {
            assigned = true;
            slots.Clear();
            for (int i = 0; i < attackers.Count; i++)
                slots[attackers[i]] = i;

            posts.Clear();
            IReadOnlyList<ITacticalZone> zones = match.PlantZones;
            if (zones.Count == 0)
                return;

            var perZone = new List<BotController>[zones.Count];
            for (int z = 0; z < zones.Count; z++)
                perZone[z] = new List<BotController>();
            for (int i = 0; i < defenders.Count; i++)
                perZone[(i + defenderOffset) % zones.Count].Add(defenders[i]);

            for (int z = 0; z < zones.Count; z++)
            {
                List<Vector3> points = TacticalObjectives.HoldPoints(zones[z].ZoneBounds, match.Waypoints, perZone[z].Count);
                for (int i = 0; i < perZone[z].Count; i++)
                {
                    Vector3 point = i < points.Count ? points[i] : zones[z].ZoneBounds.center;
                    posts[perZone[z][i]] = point;
                }
            }
        }

        private Vector3 SpawnCentre(CombatTeam side)
        {
            IReadOnlyList<Transform> spawns = match.SpawnsOf(side);
            if (spawns.Count == 0)
                return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < spawns.Count; i++)
                sum += spawns[i].position;
            return sum / spawns.Count;
        }

        private void DirectAttackers(TacticalBombModel bomb)
        {
            Picker = null;
            if (bomb.State == BombState.Planted)
            {
                Vector3 bombPoint = match.BombPosition;
                for (int i = 0; i < attackers.Count; i++)
                {
                    int slot = SlotOf(attackers[i]);
                    Vector3 point = TacticalObjectives.RingPoint(bombPoint, GuardRadius, slot, Mathf.Max(3, slots.Count), 40f);
                    attackers[i].SetObjective(point, 1.6f, 0.9f, TacticalObjectives.YawToward(bombPoint, point));
                }

                return;
            }

            if (bomb.State == BombState.Defused || bomb.State == BombState.Exploded)
                return;

            if (bomb.State == BombState.Dropped)
                Picker = Nearest(attackers, match.BombPosition);

            ITacticalZone site = match.TargetSite;
            if (site == null)
                return;

            Vector3 centre = site.ZoneBounds.center;
            centre.y = site.ZoneBounds.min.y + 0.05f;
            for (int i = 0; i < attackers.Count; i++)
            {
                BotController bot = attackers[i];
                if (bot == Picker)
                {
                    bot.SetObjective(match.BombPosition, 1.0f, 1f);
                }
                else if (bot == match.CarrierBot)
                {
                    bot.SetObjective(centre, 2.2f, 0.95f);
                }
                else
                {
                    int slot = SlotOf(bot);
                    Vector3 point = TacticalObjectives.RingPoint(centre, 5f, slot, Mathf.Max(3, slots.Count), 20f);
                    bot.SetObjective(point, 2f, 0.95f, TacticalObjectives.YawToward(centre, point));
                }
            }
        }

        private void DirectDefenders(TacticalBombModel bomb)
        {
            if (bomb.State == BombState.Planted)
            {
                Vector3 bombPoint = match.BombPosition;
                BotController nearest = Nearest(defenders, bombPoint);
                if (Defuser == null || Defuser.IsDead || !defenders.Contains(Defuser))
                    Defuser = nearest;
                else if (nearest != null && nearest != Defuser
                    && TacticalObjectives.Planar(nearest.transform.position, bombPoint) * 1.4f
                    < TacticalObjectives.Planar(Defuser.transform.position, bombPoint))
                    Defuser = nearest;

                int others = 0;
                for (int i = 0; i < defenders.Count; i++)
                {
                    BotController bot = defenders[i];
                    if (bot == Defuser)
                    {
                        bot.SetObjective(bombPoint, 1.2f, 1f);
                    }
                    else
                    {
                        Vector3 point = TacticalObjectives.RingPoint(bombPoint, GuardRadius + 1f, others++, Mathf.Max(3, defenders.Count - 1), 100f);
                        bot.SetObjective(point, 1.6f, 1f, TacticalObjectives.YawToward(point, bombPoint));
                    }
                }

                return;
            }

            Defuser = null;
            if (bomb.State == BombState.Defused || bomb.State == BombState.Exploded)
                return;

            Vector3 attackCentre = SpawnCentre(CombatTeam.Attackers);
            for (int i = 0; i < defenders.Count; i++)
            {
                BotController bot = defenders[i];
                if (!posts.TryGetValue(bot, out Vector3 post))
                    continue;
                bot.SetObjective(post, 1.5f, 0.9f, TacticalObjectives.YawToward(post, attackCentre));
            }
        }

        private int SlotOf(BotController bot)
        {
            if (slots.TryGetValue(bot, out int slot))
                return slot;
            slot = slots.Count;
            slots[bot] = slot;
            return slot;
        }

        private static BotController Nearest(List<BotController> bots, Vector3 point)
        {
            BotController best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < bots.Count; i++)
            {
                float distance = TacticalObjectives.Planar(bots[i].transform.position, point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = bots[i];
                }
            }

            return best;
        }
    }
}
