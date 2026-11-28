using System.Collections.Generic;
using UnityEngine;

namespace FpsTactical
{
    /// <summary>
    /// Team play for the tactical bots, built only from what a bot could know without talking: where its own team stands, what
    /// it sees itself and what its mates see (shared sightings). Attackers get roles (carrier, entries, supports, flankers on an
    /// alternative route); defenders hold the sites and rotate to a site where a mate made contact; once the bomb is down the
    /// defenders retake (nearest defuses when it is safe and still possible, the rest cover from different sides) while the
    /// attackers guard it from spots with a view of it and rush a defuse in progress. A bot that meets an enemy fights first (the
    /// bot brain decides) and resumes its order afterwards. Orders go out through <see cref="BotController.SetObjective"/>;
    /// <see cref="BotSettings.teamwork"/> (difficulty) scales how much of this a bot does.
    /// </summary>
    public sealed class TacticalBotDirector
    {
        private const float Interval = 0.4f;
        private const float GuardRadius = 6.5f;
        private const float SiteRing = 5f;
        private const float SupportRing = 9f;
        private const float StagingDistance = 11f;
        private const float EntryWait = 14f;
        private const float StageTimeout = 40f;
        private const float ContactMemory = 10f;
        private const float ContactRange = 32f;
        private const float ContestRadius = 12f;
        private const float SafeDefuseSlack = 12f;
        private const int MaxRotations = 2;

        private readonly TacticalMatch match;
        private readonly List<BotController> attackers = new List<BotController>();
        private readonly List<BotController> defenders = new List<BotController>();
        private readonly Dictionary<BotController, int> slots = new Dictionary<BotController, int>();
        private readonly Dictionary<BotController, Vector3> posts = new Dictionary<BotController, Vector3>();
        private readonly Dictionary<BotController, int> postZone = new Dictionary<BotController, int>();
        private readonly Dictionary<BotController, TacticalBotRole> roles = new Dictionary<BotController, TacticalBotRole>();
        private readonly Dictionary<BotController, Vector3> routeVia = new Dictionary<BotController, Vector3>();
        private readonly Dictionary<BotController, bool> routeDone = new Dictionary<BotController, bool>();
        private readonly HashSet<BotController> rotated = new HashSet<BotController>();
        private readonly List<float> contactAt = new List<float>();
        private readonly List<Vector3> pathScratch = new List<Vector3>();
        private readonly System.Random rng = new System.Random();
        private float timer;
        private float elapsed;
        private float plantedAt;
        private bool assigned;
        private bool retakeAssigned;
        private int defenderOffset;
        private BotController stagingFor;
        private bool hasStaging;
        private Vector3 staging;
        private int flankSide;

        public TacticalBotDirector(TacticalMatch match)
        {
            this.match = match;
        }

        /// <summary>The defender currently sent to defuse, or null.</summary>
        public BotController Defuser { get; private set; }

        /// <summary>The attacker sent to pick the bomb up, or null.</summary>
        public BotController Picker { get; private set; }

        /// <summary>Seconds of live play since the round started.</summary>
        public float Elapsed => elapsed;

        /// <summary>How many defenders left their site this round to help a mate in contact.</summary>
        public int Rotations { get; private set; }

        /// <summary>Attackers sent along an alternative route this round.</summary>
        public int FlankerCount { get; private set; }

        /// <summary>True when the bomb is planted and no defender can finish a defuse before it blows.</summary>
        public bool DefuseHopeless { get; private set; }

        /// <summary>True while the defuser waits because attackers still hold the bomb.</summary>
        public bool DefuseHeld { get; private set; }

        public void ResetRound()
        {
            timer = 0f;
            elapsed = 0f;
            plantedAt = 0f;
            assigned = false;
            retakeAssigned = false;
            Defuser = null;
            Picker = null;
            Rotations = 0;
            FlankerCount = 0;
            DefuseHopeless = false;
            DefuseHeld = false;
            stagingFor = null;
            hasStaging = false;
            slots.Clear();
            posts.Clear();
            postZone.Clear();
            roles.Clear();
            routeVia.Clear();
            routeDone.Clear();
            rotated.Clear();
            contactAt.Clear();
            defenderOffset = Random.Range(0, 2);
            flankSide = Random.Range(0, 2) == 0 ? 1 : -1;
        }

        public void OnBombPlanted()
        {
            timer = 0f;
            plantedAt = elapsed;
            retakeAssigned = false;
            routeVia.Clear();
            routeDone.Clear();
        }

        /// <summary>The job a bot has right now (None for bots of no side or dead bots).</summary>
        public TacticalBotRole RoleOf(BotController bot)
        {
            if (bot == null || bot.IsDead)
                return TacticalBotRole.None;

            TacticalBombModel bomb = match.Model.Bomb;
            bool planted = bomb.State == BombState.Planted;
            if (bot.Team == CombatTeam.Attackers)
            {
                if (planted)
                    return TacticalBotRole.PostPlant;
                if (bot == Picker)
                    return TacticalBotRole.Picker;
                if (bot == match.CarrierBot && bomb.State == BombState.Carried)
                    return TacticalBotRole.Carrier;
            }
            else if (bot.Team == CombatTeam.Defenders && planted)
            {
                return bot == Defuser ? TacticalBotRole.Defuser : TacticalBotRole.Cover;
            }

            return roles.TryGetValue(bot, out TacticalBotRole role) ? role : TacticalBotRole.None;
        }

        /// <summary>The waypoint a flanker or retaking defender routes through, if it has one.</summary>
        public bool TryGetRouteVia(BotController bot, out Vector3 via)
        {
            return routeVia.TryGetValue(bot, out via);
        }

        public void Tick(float dt)
        {
            elapsed += dt;
            timer -= dt;
            if (timer > 0f)
                return;
            timer = Interval;

            attackers.Clear();
            defenders.Clear();
            match.CollectAliveBots(CombatTeam.Attackers, attackers);
            match.CollectAliveBots(CombatTeam.Defenders, defenders);

            if (!assigned)
                Assign();

            TacticalBombModel bomb = match.Model.Bomb;
            DirectAttackers(bomb);
            DirectDefenders(bomb);
        }

        #region Assignment

        private void Assign()
        {
            assigned = true;
            slots.Clear();
            for (int i = 0; i < attackers.Count; i++)
                slots[attackers[i]] = i;

            AssignAttackerRoles();
            AssignDefenderPosts();
        }

        private void AssignAttackerRoles()
        {
            ITacticalZone site = match.TargetSite;
            var others = new List<BotController>();
            for (int i = 0; i < attackers.Count; i++)
            {
                if (attackers[i] == match.CarrierBot)
                    roles[attackers[i]] = TacticalBotRole.Carrier;
                else
                    others.Add(attackers[i]);
            }

            if (site == null)
                return;

            Vector3 centre = SiteCentre(site);
            float teamwork = Teamwork(attackers);
            int flankers = Mathf.Min(Mathf.Max(0, others.Count - 1), Mathf.RoundToInt(teamwork * others.Count * 0.5f));
            int side = flankSide;
            var rest = new List<BotController>();
            for (int i = 0; i < others.Count; i++)
            {
                BotController bot = others[i];
                if (flankers > 0 && CanRoute())
                {
                    int via = TacticalRoutePlanner.PickFlank(bot.transform.position, centre, match.Waypoints, NavDistance, side, rng);
                    if (via >= 0)
                    {
                        roles[bot] = TacticalBotRole.Flanker;
                        routeVia[bot] = match.Waypoints[via];
                        routeDone[bot] = false;
                        flankers--;
                        FlankerCount++;
                        side = -side;
                        continue;
                    }
                }

                rest.Add(bot);
            }

            int entries = Mathf.Max(1, (rest.Count + 1) / 2);
            for (int i = 0; i < rest.Count; i++)
                roles[rest[i]] = i < entries ? TacticalBotRole.Entry : TacticalBotRole.Support;
        }

        private void AssignDefenderPosts()
        {
            posts.Clear();
            postZone.Clear();
            IReadOnlyList<ITacticalZone> zones = match.PlantZones;
            if (zones.Count == 0)
                return;

            for (int z = 0; z < zones.Count; z++)
                contactAt.Add(float.NegativeInfinity);

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
                    BotController bot = perZone[z][i];
                    posts[bot] = i < points.Count ? points[i] : zones[z].ZoneBounds.center;
                    postZone[bot] = z;
                    roles[bot] = TacticalBotRole.Anchor;
                }
            }
        }

        #endregion

        #region Attackers

        private void DirectAttackers(TacticalBombModel bomb)
        {
            Picker = null;
            if (bomb.State == BombState.Planted)
            {
                DirectPostPlant(bomb);
                return;
            }

            if (bomb.State == BombState.Defused || bomb.State == BombState.Exploded)
                return;

            if (bomb.State == BombState.Dropped)
                Picker = Nearest(attackers, match.BombPosition);

            ITacticalZone site = match.TargetSite;
            if (site == null)
                return;

            Vector3 centre = SiteCentre(site);
            bool entriesArrived = EntriesArrived(centre);
            int count = Mathf.Max(3, slots.Count);
            for (int i = 0; i < attackers.Count; i++)
            {
                BotController bot = attackers[i];
                TacticalBotRole role = RoleOf(bot);
                if (role == TacticalBotRole.Picker)
                {
                    bot.SetObjective(match.BombPosition, 1.0f, 1f);
                }
                else if (role == TacticalBotRole.Carrier)
                {
                    if (entriesArrived || !TryStaging(bot, centre, out Vector3 stage))
                        bot.SetObjective(centre, 2.2f, 0.95f);
                    else
                        bot.SetObjective(stage, 2.5f, 0.95f, TacticalObjectives.YawToward(stage, centre), true);
                }
                else if (role == TacticalBotRole.Flanker)
                {
                    DirectFlanker(bot, centre, count);
                }
                else
                {
                    int slot = SlotOf(bot);
                    float radius = role == TacticalBotRole.Support ? SupportRing : SiteRing;
                    Vector3 point = TacticalObjectives.RingPoint(centre, radius, slot, count, 20f);
                    bot.SetObjective(point, 2f, 0.95f, TacticalObjectives.YawToward(centre, point), true);
                }
            }
        }

        private void DirectFlanker(BotController bot, Vector3 centre, int count)
        {
            if (routeVia.TryGetValue(bot, out Vector3 via) && !RouteFinished(bot, via, 0f))
            {
                bot.SetObjective(via, 2.5f, 1f);
                return;
            }

            Vector3 point = centre;
            if (routeVia.ContainsKey(bot))
            {
                Vector3 away = routeVia[bot] - centre;
                away.y = 0f;
                if (away.sqrMagnitude > 1f)
                    point = centre + away.normalized * SiteRing;
            }
            else
            {
                point = TacticalObjectives.RingPoint(centre, SiteRing, SlotOf(bot), count, 20f);
            }

            bot.SetObjective(point, 2f, 0.95f, TacticalObjectives.YawToward(centre, point), true);
        }

        private void DirectPostPlant(TacticalBombModel bomb)
        {
            Vector3 bombPoint = match.BombPosition;
            int count = Mathf.Max(3, slots.Count);
            for (int i = 0; i < attackers.Count; i++)
            {
                BotController bot = attackers[i];
                int slot = SlotOf(bot);
                if (bomb.IsDefusing && Teamwork(attackers) >= 0.3f)
                {
                    Vector3 rush = TacticalObjectives.RingPoint(bombPoint, 3.5f, slot, count, 40f);
                    bot.SetObjective(rush, 1.6f, 1f, TacticalObjectives.YawToward(rush, bombPoint));
                    continue;
                }

                Vector3 point = ViewPoint(bombPoint, GuardRadius, slot, count, 40f);
                bot.SetObjective(point, 1.6f, 0.9f, TacticalObjectives.YawToward(bombPoint, point), true);
            }
        }

        private bool EntriesArrived(Vector3 centre)
        {
            if (elapsed >= EntryWait)
                return true;

            bool any = false;
            for (int i = 0; i < attackers.Count; i++)
            {
                TacticalBotRole role = RoleOf(attackers[i]);
                if (role != TacticalBotRole.Entry && role != TacticalBotRole.Support)
                    continue;
                any = true;
                if (TacticalObjectives.Planar(attackers[i].transform.position, centre) <= SupportRing + 2.5f)
                    return true;
            }

            return !any;
        }

        private bool TryStaging(BotController carrier, Vector3 centre, out Vector3 point)
        {
            point = centre;
            if (!CanRoute())
                return false;

            if (stagingFor != carrier)
            {
                stagingFor = carrier;
                hasStaging = false;
                if (BotNavigation.TryCalculatePath(carrier.transform.position, centre, pathScratch, out bool complete) && complete
                    && TacticalRoutePlanner.PathLength(pathScratch) > StagingDistance + 6f)
                {
                    staging = TacticalRoutePlanner.StagingPoint(pathScratch, StagingDistance);
                    hasStaging = true;
                }
            }

            point = staging;
            return hasStaging;
        }

        #endregion

        #region Defenders

        private void DirectDefenders(TacticalBombModel bomb)
        {
            if (bomb.State == BombState.Planted)
            {
                DirectRetake(bomb);
                return;
            }

            Defuser = null;
            DefuseHopeless = false;
            DefuseHeld = false;
            if (bomb.State == BombState.Defused || bomb.State == BombState.Exploded)
                return;

            UpdateContacts();
            Rotate();

            Vector3 attackCentre = SpawnCentre(CombatTeam.Attackers);
            for (int i = 0; i < defenders.Count; i++)
            {
                BotController bot = defenders[i];
                if (!posts.TryGetValue(bot, out Vector3 post))
                    continue;
                bot.SetObjective(post, 1.5f, 0.9f, TacticalObjectives.YawToward(post, attackCentre), true);
            }
        }

        private void UpdateContacts()
        {
            for (int i = 0; i < defenders.Count; i++)
            {
                BotController bot = defenders[i];
                if (!bot.SeesEnemy || bot.CurrentTarget == null)
                    continue;

                ReportContact(bot.transform.position);
            }
        }

        /// <summary>
        /// Shares a sighting: a defender saw an enemy at <paramref name="position"/>. Near a site it marks that site as
        /// contested, which can pull a defender over from the other site.
        /// </summary>
        public void ReportContact(Vector3 position)
        {
            int zone = NearestZone(position, ContactRange);
            if (zone >= 0 && zone < contactAt.Count)
                contactAt[zone] = elapsed;
        }

        /// <summary>Index of the plant zone a defender is posted at (-1 when he has none).</summary>
        public int ZoneOf(BotController bot)
        {
            return bot != null && postZone.TryGetValue(bot, out int zone) ? zone : -1;
        }

        private void Rotate()
        {
            IReadOnlyList<ITacticalZone> zones = match.PlantZones;
            if (zones.Count < 2 || Rotations >= MaxRotations || contactAt.Count != zones.Count)
                return;
            if (Teamwork(defenders) < 0.3f)
                return;

            for (int z = 0; z < zones.Count; z++)
            {
                if (elapsed - contactAt[z] > ContactMemory)
                    continue;

                BotController best = null;
                float bestDistance = float.MaxValue;
                Vector3 target = zones[z].ZoneBounds.center;
                for (int i = 0; i < defenders.Count; i++)
                {
                    BotController bot = defenders[i];
                    if (rotated.Contains(bot) || !postZone.TryGetValue(bot, out int home) || home == z)
                        continue;
                    if (elapsed - contactAt[home] <= ContactMemory || ZoneHolders(home) < 2)
                        continue;

                    float distance = TacticalObjectives.Planar(bot.transform.position, target);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = bot;
                    }
                }

                if (best == null)
                    continue;

                int wanted = ZoneHolders(z) + 1;
                List<Vector3> points = TacticalObjectives.HoldPoints(zones[z].ZoneBounds, match.Waypoints, wanted);
                posts[best] = points[points.Count - 1];
                postZone[best] = z;
                roles[best] = TacticalBotRole.Rotator;
                rotated.Add(best);
                Rotations++;
                return;
            }
        }

        private int ZoneHolders(int zone)
        {
            int total = 0;
            for (int i = 0; i < defenders.Count; i++)
            {
                if (postZone.TryGetValue(defenders[i], out int z) && z == zone)
                    total++;
            }

            return total;
        }

        private void DirectRetake(TacticalBombModel bomb)
        {
            Vector3 bombPoint = match.BombPosition;
            float teamwork = Teamwork(defenders);
            float fuse = bomb.FuseRemaining;
            float remaining = Mathf.Max(0f, bomb.DefuseSeconds * (1f - bomb.DefuseProgress01));
            float reach = match.Rules.bombReach;

            BotController nearest = Nearest(defenders, bombPoint);
            DefuseHopeless = nearest != null && fuse < remaining + TravelTime(nearest, bombPoint, reach) + 0.5f;
            if (DefuseHopeless)
            {
                Defuser = null;
            }
            else if (Defuser == null || Defuser.IsDead || !defenders.Contains(Defuser))
            {
                Defuser = nearest;
            }
            else if (nearest != null && nearest != Defuser && !bomb.IsDefusing
                && TacticalObjectives.Planar(nearest.transform.position, bombPoint) * 1.4f
                < TacticalObjectives.Planar(Defuser.transform.position, bombPoint))
            {
                Defuser = nearest;
            }

            if (!retakeAssigned)
                AssignRetakeRoutes(bombPoint, teamwork);

            DefuseHeld = false;
            int others = 0;
            int coverCount = Mathf.Max(3, defenders.Count - (Defuser != null ? 1 : 0));
            for (int i = 0; i < defenders.Count; i++)
            {
                BotController bot = defenders[i];
                if (bot == Defuser)
                {
                    DirectDefuser(bot, bomb, bombPoint, fuse, remaining, reach);
                    continue;
                }

                int slot = others++;
                if (routeVia.TryGetValue(bot, out Vector3 via) && !RouteFinished(bot, via, plantedAt))
                {
                    bot.SetObjective(via, 2.5f, 1f);
                    continue;
                }

                Vector3 point = ViewPoint(bombPoint, GuardRadius + 1f, slot, coverCount, 100f);
                bot.SetObjective(point, 1.6f, 1f, TacticalObjectives.YawToward(point, bombPoint), !bomb.IsDefusing);
            }
        }

        private void DirectDefuser(BotController bot, TacticalBombModel bomb, Vector3 bombPoint, float fuse, float remaining, float reach)
        {
            bool contested = !bomb.IsDefusing && AttackerNear(bombPoint, ContestRadius);
            float slack = fuse - remaining - TravelTime(bot, bombPoint, reach);
            if (contested && slack > SafeDefuseSlack && TryHoldShort(bot, bombPoint, out Vector3 wait))
            {
                DefuseHeld = true;
                bot.SetObjective(wait, 2.5f, 1f, TacticalObjectives.YawToward(wait, bombPoint), true);
                return;
            }

            bot.SetObjective(bombPoint, 1.2f, 1f);
        }

        private bool TryHoldShort(BotController bot, Vector3 bombPoint, out Vector3 point)
        {
            point = bombPoint;
            if (!CanRoute())
                return false;
            if (!BotNavigation.TryCalculatePath(bot.transform.position, bombPoint, pathScratch, out bool complete) || !complete)
                return false;

            float length = TacticalRoutePlanner.PathLength(pathScratch);
            if (length < ContestRadius + 4f)
                return false;

            point = TacticalRoutePlanner.StagingPoint(pathScratch, ContestRadius + 3f);
            return true;
        }

        private void AssignRetakeRoutes(Vector3 bombPoint, float teamwork)
        {
            retakeAssigned = true;
            if (teamwork < 0.5f || !CanRoute())
                return;

            int side = flankSide;
            for (int i = 0; i < defenders.Count; i++)
            {
                BotController bot = defenders[i];
                if (bot == Defuser)
                    continue;

                int via = TacticalRoutePlanner.PickFlank(bot.transform.position, bombPoint, match.Waypoints, NavDistance, side, rng);
                side = -side;
                if (via < 0)
                    continue;

                routeVia[bot] = match.Waypoints[via];
                routeDone[bot] = false;
            }
        }

        private float TravelTime(BotController bot, Vector3 bombPoint, float reach)
        {
            float distance = Mathf.Max(0f, TacticalObjectives.Planar(bot.transform.position, bombPoint) - reach);
            float speed = bot.Settings != null ? Mathf.Max(1f, bot.Settings.runSpeed * 0.9f) : 4f;
            return distance / speed;
        }

        private bool AttackerNear(Vector3 point, float radius)
        {
            for (int i = 0; i < attackers.Count; i++)
            {
                if (TacticalObjectives.Planar(attackers[i].transform.position, point) <= radius)
                    return true;
            }

            return false;
        }

        #endregion

        #region Helpers

        private bool CanRoute()
        {
            return BotNavigation.Mode == BotNavMode.NavMesh && match.Waypoints.Count > 0;
        }

        private float NavDistance(Vector3 from, Vector3 to)
        {
            if (!BotNavigation.TryCalculatePath(from, to, pathScratch, out bool complete) || !complete)
                return float.MaxValue;
            return TacticalRoutePlanner.PathLength(pathScratch);
        }

        private bool RouteFinished(BotController bot, Vector3 via, float since)
        {
            if (routeDone.TryGetValue(bot, out bool done) && done)
                return true;

            bool arrived = TacticalObjectives.Planar(bot.transform.position, via) <= 3.5f;
            if (arrived || elapsed - since > StageTimeout)
            {
                routeDone[bot] = true;
                return true;
            }

            return false;
        }

        private Vector3 ViewPoint(Vector3 target, float radius, int slot, int count, float startDegrees)
        {
            Vector3 first = TacticalObjectives.RingPoint(target, radius, slot, count, startDegrees);
            Vector3 look = target + Vector3.up * 0.9f;
            for (int k = 0; k < 5; k++)
            {
                float offset = ((k + 1) / 2) * 25f * (k % 2 == 0 ? 1f : -1f);
                Vector3 point = k == 0 ? first : TacticalObjectives.RingPoint(target, radius, slot, count, startDegrees + offset);
                Vector3 snapped = BotNavigation.IsReady ? BotNavigation.Snap(point, null) : point;
                if (BotPerception.HasLineOfSight(snapped + Vector3.up * 1.5f, look, null, null))
                    return point;
            }

            return first;
        }

        private int NearestZone(Vector3 position, float maxDistance)
        {
            IReadOnlyList<ITacticalZone> zones = match.PlantZones;
            int best = -1;
            float bestDistance = maxDistance;
            for (int z = 0; z < zones.Count; z++)
            {
                float distance = TacticalObjectives.Planar(position, zones[z].ZoneBounds.center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = z;
                }
            }

            return best;
        }

        private static Vector3 SiteCentre(ITacticalZone site)
        {
            Vector3 centre = site.ZoneBounds.center;
            centre.y = site.ZoneBounds.min.y + 0.05f;
            return centre;
        }

        private static float Teamwork(List<BotController> bots)
        {
            float total = 0f;
            int count = 0;
            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i] == null || bots[i].Settings == null)
                    continue;
                total += bots[i].Settings.teamwork;
                count++;
            }

            return count > 0 ? total / count : 0f;
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

        #endregion
    }
}
