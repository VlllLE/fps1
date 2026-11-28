using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FpsTactical
{
    /// <summary>
    /// Runs one tactical bomb match in the current scene: attackers against defenders, class select / freeze time, timed
    /// rounds without respawns, score to N with a side swap at half time, and the bomb (carry, drop, plant on the map's
    /// plant zones, defuse, fuse). There is no buying: players keep their create-a-class loadout and can change it
    /// during freeze time. The player and the bots are real combatants; the bots follow <see cref="TacticalBotDirector"/>.
    /// The pure rules live in <see cref="TacticalMatchModel"/>; this component connects them to the scene.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class TacticalMatch : MonoBehaviour
    {
        public const string DisableEnvironmentVariable = "FPS_TACTICAL";
        public const int PlayerSquad = 0;

        private readonly List<ITacticalZone> plantZones = new List<ITacticalZone>();
        private readonly List<ITacticalZone> yardZones = new List<ITacticalZone>();
        private readonly List<Transform> attackerSpawns = new List<Transform>();
        private readonly List<Transform> defenderSpawns = new List<Transform>();
        private readonly List<Vector3> waypoints = new List<Vector3>();
        private readonly List<BotController>[] squadBots = { new List<BotController>(), new List<BotController>() };

        private BotManager botManager;
        private PlayerHealth player;
        private InputHandler playerInput;
        private PlayerWeaponBridge playerWeapons;
        private WeaponLoadoutService playerLoadout;
        private TacticalBombProp bombProp;
        private TacticalBotDirector director;
        private System.Random rng = new System.Random();
        private bool started;
        private bool playerHoldingAction;
        private BotController carrierBot;
        private bool playerCarries;
        private Vector3 lastCarrierPosition;
        private Vector3 bombPosition;
        private ITacticalZone targetSite;
        private bool roundReady;

        public static TacticalMatch Instance { get; private set; }

        public TacticalRules Rules { get; private set; } = new TacticalRules();
        public TacticalMatchModel Model { get; private set; }
        public bool IsRunning => started && Model != null;
        public PlayerHealth Player => player;
        public IReadOnlyList<ITacticalZone> PlantZones => plantZones;
        public IReadOnlyList<Vector3> Waypoints => waypoints;

        /// <summary>Starts the match by itself once the bots are ready. Tests turn this off and call <see cref="Begin"/>.</summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>When true <see cref="Update"/> does nothing and tests advance the match with <see cref="Step"/>.</summary>
        public bool ManualClock { get; set; }

        /// <summary>Starts the next match as soon as one ends.</summary>
        public bool AutoRestart { get; set; } = true;

        /// <summary>True while the local player holds the plant / defuse action. Replaceable for tests.</summary>
        public Func<bool> ActionInput { get; set; } = DefaultActionInput;

        public bool PlayerIsCarrier => playerCarries;
        public BotController CarrierBot => carrierBot;
        public ITacticalZone TargetSite => targetSite;
        public Vector3 BombPosition => bombPosition;
        public TacticalBombProp BombProp => bombProp;
        public bool BombDropped => Model != null && Model.Bomb.State == BombState.Dropped;
        public bool RoundReady => roundReady;
        public IReadOnlyList<BotController> BotsOf(int squad) => squadBots[squad];

        public CombatTeam PlayerSide => Model != null ? Model.SideOf(PlayerSquad) : CombatTeam.None;

        public event Action<TacticalPhase> PhaseEntered;

        private static bool DefaultActionInput()
        {
            return Input.GetKey(KeyCode.E) && Cursor.lockState == CursorLockMode.Locked;
        }

        public static TacticalMatch Create(TacticalRules rules = null)
        {
            var go = new GameObject("TacticalMatch");
            var match = go.AddComponent<TacticalMatch>();
            if (rules != null)
                match.Rules = rules;
            return match;
        }

        public void ApplyRules(TacticalRules rules)
        {
            if (started)
                throw new InvalidOperationException("Rules cannot change while a match is running.");
            Rules = rules ?? new TacticalRules();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private IEnumerator Start()
        {
            if (Instance != this || !AutoStart)
                yield break;

            float waited = 0f;
            while (waited < 20f)
            {
                BotManager manager = BotManager.Instance;
                if (manager != null && manager.IsReady && BotNavigation.IsReady)
                    break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!started)
                Begin();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Shutdown();
        }

        private void Update()
        {
            if (!started || ManualClock)
                return;
            Step(Time.deltaTime);
        }

        #region Match setup

        /// <summary>Collects the map layout, creates the squads and starts the first round. False when the map has no layout.</summary>
        public bool Begin()
        {
            if (started)
                return true;

            botManager = BotManager.Instance != null ? BotManager.Instance : BotManager.Create();
            if (!CollectLayout())
                return false;

            botManager.KeysEnabled = false;
            botManager.AutoSpawnCount = 0;
            botManager.ClearBots();

            ResolvePlayer();
            CombatTeams.FriendlyFire = Rules.friendlyFire;

            int playerSlots = player != null ? 1 : 0;
            CreateSquad(0, Rules.playersPerSide - playerSlots);
            CreateSquad(1, Rules.playersPerSide);

            Model = new TacticalMatchModel(Rules);
            Model.PhaseChanged += OnPhaseChanged;
            Model.RoundEnded += OnRoundEnded;
            Model.Bomb.Planted += OnBombPlanted;
            Model.Bomb.Defused += OnBombDefused;
            Model.Bomb.Exploded += OnBombExploded;

            director = new TacticalBotDirector(this);
            started = true;
            Model.Start();
            return true;
        }

        private bool CollectLayout()
        {
            plantZones.Clear();
            yardZones.Clear();
            attackerSpawns.Clear();
            defenderSpawns.Clear();
            waypoints.Clear();

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(behaviour is ITacticalZone zone))
                    continue;
                if (zone.ZoneKind == TacticalZoneKind.Plant)
                    plantZones.Add(zone);
                else
                    yardZones.Add(zone);
            }

            plantZones.Sort((a, b) => string.CompareOrdinal(a.ZoneSite, b.ZoneSite));
            if (plantZones.Count == 0)
                return false;

            GameObject[] spawns = GameObject.FindGameObjectsWithTag("Respawn");
            for (int i = 0; i < spawns.Length; i++)
            {
                CombatTeam side = SideOfSpawn(spawns[i].transform);
                if (side == CombatTeam.Attackers)
                    attackerSpawns.Add(spawns[i].transform);
                else if (side == CombatTeam.Defenders)
                    defenderSpawns.Add(spawns[i].transform);
            }

            if (attackerSpawns.Count == 0 || defenderSpawns.Count == 0)
                return false;

            for (int i = 0; i < BotWaypoint.All.Count; i++)
            {
                if (BotWaypoint.All[i] != null)
                    waypoints.Add(BotWaypoint.All[i].transform.position);
            }

            return true;
        }

        private CombatTeam SideOfSpawn(Transform spawn)
        {
            for (int i = 0; i < yardZones.Count; i++)
            {
                if (!yardZones[i].Contains(spawn.position))
                    continue;
                if (string.Equals(yardZones[i].ZoneSite, "Attack", StringComparison.OrdinalIgnoreCase))
                    return CombatTeam.Attackers;
                if (string.Equals(yardZones[i].ZoneSite, "Defend", StringComparison.OrdinalIgnoreCase))
                    return CombatTeam.Defenders;
            }

            string name = spawn.name;
            if (name.StartsWith("Spawn_D", StringComparison.OrdinalIgnoreCase))
                return CombatTeam.Defenders;
            if (name.StartsWith("Spawn_A", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("PlayerSpawn", StringComparison.OrdinalIgnoreCase))
                return CombatTeam.Attackers;
            return CombatTeam.None;
        }

        private void ResolvePlayer()
        {
            player = PlayerHealth.Local;
            if (player == null)
            {
                MovementController movement = FindFirstObjectByType<MovementController>();
                if (movement != null)
                    player = movement.GetComponent<PlayerHealth>() ?? movement.gameObject.AddComponent<PlayerHealth>();
            }

            if (player == null)
                return;

            playerInput = player.GetComponent<InputHandler>();
            playerWeapons = player.GetComponent<PlayerWeaponBridge>();
            playerLoadout = player.GetComponent<WeaponLoadoutService>();
            player.AutoRespawn = false;
            player.RegenEnabled = false;
        }

        private void CreateSquad(int squad, int count)
        {
            CombatTeam side = squad == 0 ? CombatTeam.Attackers : CombatTeam.Defenders;
            List<Transform> spawns = side == CombatTeam.Attackers ? attackerSpawns : defenderSpawns;
            for (int i = 0; i < count; i++)
            {
                Transform spawn = spawns[i % spawns.Count];
                BotController bot = botManager.SpawnTeamBot(side, spawn.position, spawn.eulerAngles.y);
                bot.Frozen = true;
                squadBots[squad].Add(bot);
            }
        }

        private void Shutdown()
        {
            if (!started && Model == null)
                return;
            started = false;

            if (playerInput != null)
                playerInput.GameplayLocked = false;
            if (playerWeapons != null)
                playerWeapons.FireLocked = false;
            if (player != null)
            {
                player.Team = CombatTeam.None;
                player.AutoRespawn = true;
                player.RegenEnabled = true;
            }

            CombatTeams.FriendlyFire = false;
            if (botManager != null)
            {
                botManager.KeysEnabled = true;
                for (int squad = 0; squad < 2; squad++)
                {
                    for (int i = 0; i < squadBots[squad].Count; i++)
                    {
                        if (squadBots[squad][i] != null)
                            botManager.RemoveBot(squadBots[squad][i]);
                    }
                }
            }

            squadBots[0].Clear();
            squadBots[1].Clear();
            DestroyBombProp();
        }

        #endregion

        #region Phases

        private void OnPhaseChanged(TacticalPhase from, TacticalPhase to)
        {
            switch (to)
            {
                case TacticalPhase.ClassSelect:
                    SetupRound();
                    break;
                case TacticalPhase.Live:
                    BeginLive();
                    break;
                case TacticalPhase.RoundEnd:
                case TacticalPhase.MatchEnd:
                    FreezeCombat();
                    break;
            }

            PhaseEntered?.Invoke(to);
        }

        private void OnRoundEnded(RoundResult result)
        {
            FreezeCombat();
        }

        private void SetupRound()
        {
            roundReady = false;
            DestroyBombProp();
            carrierBot = null;
            playerCarries = false;

            CombatTeam[] sides = { Model.SideOf(0), Model.SideOf(1) };
            var attackerPool = new List<Transform>(attackerSpawns);
            var defenderPool = new List<Transform>(defenderSpawns);
            Shuffle(attackerPool);
            Shuffle(defenderPool);

            for (int squad = 0; squad < 2; squad++)
            {
                List<Transform> pool = sides[squad] == CombatTeam.Attackers ? attackerPool : defenderPool;
                List<BotController> bots = squadBots[squad];

                if (squad == PlayerSquad && player != null)
                {
                    Transform spawn = TakeSpawn(pool, sides[squad], sides[squad] == CombatTeam.Attackers ? "PlayerSpawn" : null);
                    player.Team = sides[squad];
                    player.SetSpawnPosition(spawn.position);
                    player.SetSpawnFacing(spawn.eulerAngles.y, 0f);
                    player.Respawn();
                }

                for (int i = 0; i < bots.Count; i++)
                {
                    BotController bot = bots[i];
                    if (bot == null)
                        continue;
                    Transform spawn = TakeSpawn(pool, sides[squad], null);
                    bot.Team = sides[squad];
                    bot.ClearObjective();
                    bot.Frozen = true;
                    bot.Respawn(spawn.position, spawn.eulerAngles.y);
                }
            }

            targetSite = plantZones[rng.Next(plantZones.Count)];
            UpdatePlayerLocks();
            director.ResetRound();
        }

        private Transform TakeSpawn(List<Transform> pool, CombatTeam side, string preferredName)
        {
            if (pool.Count == 0)
            {
                pool.AddRange(side == CombatTeam.Attackers ? attackerSpawns : defenderSpawns);
                Shuffle(pool);
            }

            int index = 0;
            if (!string.IsNullOrEmpty(preferredName))
            {
                int found = pool.FindIndex(t => t.name == preferredName);
                if (found >= 0)
                    index = found;
            }

            Transform spawn = pool[index];
            pool.RemoveAt(index);
            return spawn;
        }

        private void BeginLive()
        {
            roundReady = true;
            if (playerLoadout != null && player != null && !player.IsDead)
                playerLoadout.ApplySelection();

            for (int squad = 0; squad < 2; squad++)
            {
                for (int i = 0; i < squadBots[squad].Count; i++)
                {
                    if (squadBots[squad][i] != null)
                        squadBots[squad][i].Frozen = false;
                }
            }

            AssignBombCarrier();
            UpdatePlayerLocks();
        }

        private void FreezeCombat()
        {
            for (int squad = 0; squad < 2; squad++)
            {
                for (int i = 0; i < squadBots[squad].Count; i++)
                {
                    BotController bot = squadBots[squad][i];
                    if (bot == null)
                        continue;
                    bot.Frozen = true;
                    bot.ClearObjective();
                }
            }

            UpdatePlayerLocks();
        }

        private void AssignBombCarrier()
        {
            var candidates = new List<BotController>();
            List<BotController> attackers = squadBots[Model.SquadOf(CombatTeam.Attackers)];
            for (int i = 0; i < attackers.Count; i++)
            {
                if (attackers[i] != null && !attackers[i].IsDead)
                    candidates.Add(attackers[i]);
            }

            bool playerAttacks = player != null && !player.IsDead && player.Team == CombatTeam.Attackers;
            int total = candidates.Count + (playerAttacks ? 1 : 0);
            if (total == 0)
                return;

            int pick = rng.Next(total);
            if (playerAttacks && pick == candidates.Count)
            {
                playerCarries = true;
                carrierBot = null;
            }
            else
            {
                playerCarries = false;
                carrierBot = candidates[pick];
            }
        }

        /// <summary>Plants the bomb at <paramref name="position"/> right away (tests and scripted events).</summary>
        public void PlantNow(string site, Vector3 position)
        {
            if (Model == null || Model.Phase != TacticalPhase.Live)
                return;
            lastCarrierPosition = position;
            Model.Bomb.Plant(site);
        }

        /// <summary>Hands the bomb to a specific attacker (the player when <paramref name="bot"/> is null). Tests use it.</summary>
        public void GiveBomb(BotController bot)
        {
            if (Model == null || Model.Bomb.State == BombState.Planted)
                return;
            Model.Bomb.Reset();
            DestroyBombProp();
            carrierBot = bot;
            playerCarries = bot == null;
        }

        #endregion

        #region Stepping

        /// <summary>Advances the match by <paramref name="dt"/> seconds. <see cref="Update"/> calls this every frame.</summary>
        public void Step(float dt)
        {
            if (!started || Model == null)
                return;

            if (Model.Phase == TacticalPhase.Live)
                ResolveBomb();

            string site = null;
            bool planting = false;
            bool defusing = false;
            if (Model.Phase == TacticalPhase.Live)
                EvaluateBombActions(out site, out planting, out defusing);

            Model.Tick(dt, AliveCount(CombatTeam.Attackers), AliveCount(CombatTeam.Defenders), site, planting, defusing);

            if (Model.Phase == TacticalPhase.Live)
            {
                director.Tick(dt);
                if (bombProp != null && Model.Bomb.State == BombState.Planted)
                    bombProp.SetFuse01(Model.Bomb.FuseElapsed01);
            }

            UpdatePlayerLocks();

            if (Model.MatchFinished && AutoRestart)
                Model.Start();
        }

        /// <summary>Restarts the match from round one.</summary>
        public void Restart()
        {
            if (started)
                Model.Start();
        }

        public int AliveCount(CombatTeam side)
        {
            int alive = 0;
            if (player != null && !player.IsDead && player.Team == side)
                alive++;
            for (int squad = 0; squad < 2; squad++)
            {
                for (int i = 0; i < squadBots[squad].Count; i++)
                {
                    BotController bot = squadBots[squad][i];
                    if (bot != null && !bot.IsDead && bot.Team == side)
                        alive++;
                }
            }

            return alive;
        }

        public int TotalCount(CombatTeam side)
        {
            int total = player != null && player.Team == side ? 1 : 0;
            for (int squad = 0; squad < 2; squad++)
            {
                for (int i = 0; i < squadBots[squad].Count; i++)
                {
                    if (squadBots[squad][i] != null && squadBots[squad][i].Team == side)
                        total++;
                }
            }

            return total;
        }

        /// <summary>Living bots of one side.</summary>
        public void CollectAliveBots(CombatTeam side, List<BotController> into)
        {
            for (int squad = 0; squad < 2; squad++)
            {
                for (int i = 0; i < squadBots[squad].Count; i++)
                {
                    BotController bot = squadBots[squad][i];
                    if (bot != null && !bot.IsDead && bot.Team == side)
                        into.Add(bot);
                }
            }
        }

        #endregion

        #region Bomb

        private void ResolveBomb()
        {
            TacticalBombModel bomb = Model.Bomb;
            if (bomb.State == BombState.Carried)
            {
                if (!CarrierAlive())
                {
                    if (playerCarries || carrierBot != null)
                        DropBomb();
                    return;
                }

                lastCarrierPosition = CarrierPosition();
            }
            else if (bomb.State == BombState.Dropped)
            {
                TryPickUpBomb();
            }
        }

        private bool CarrierAlive()
        {
            if (playerCarries)
                return player != null && !player.IsDead;
            return carrierBot != null && !carrierBot.IsDead;
        }

        private Vector3 CarrierPosition()
        {
            if (playerCarries && player != null)
                return player.transform.position;
            return carrierBot != null ? carrierBot.transform.position : lastCarrierPosition;
        }

        private void DropBomb()
        {
            Model.Bomb.Drop();
            bombPosition = GroundPoint(lastCarrierPosition);
            playerCarries = false;
            carrierBot = null;
            DestroyBombProp();
            bombProp = TacticalBombProp.Create(bombPosition, false);
        }

        private void TryPickUpBomb()
        {
            float reach = Rules.bombReach;
            if (player != null && !player.IsDead && player.Team == CombatTeam.Attackers
                && TacticalObjectives.Planar(player.transform.position, bombPosition) <= reach
                && Mathf.Abs(player.transform.position.y - bombPosition.y) < 2.5f)
            {
                TakeBomb(null);
                return;
            }

            List<BotController> attackers = squadBots[Model.SquadOf(CombatTeam.Attackers)];
            for (int i = 0; i < attackers.Count; i++)
            {
                BotController bot = attackers[i];
                if (bot == null || bot.IsDead)
                    continue;
                if (TacticalObjectives.Planar(bot.transform.position, bombPosition) <= reach
                    && Mathf.Abs(bot.transform.position.y - bombPosition.y) < 2.5f)
                {
                    TakeBomb(bot);
                    return;
                }
            }
        }

        private void TakeBomb(BotController bot)
        {
            Model.Bomb.PickUp();
            carrierBot = bot;
            playerCarries = bot == null;
            DestroyBombProp();
        }

        private void EvaluateBombActions(out string site, out bool planting, out bool defusing)
        {
            site = null;
            planting = false;
            defusing = false;
            playerHoldingAction = false;
            TacticalBombModel bomb = Model.Bomb;

            if (bomb.State == BombState.Carried && CarrierAlive())
            {
                Vector3 position = CarrierPosition();
                ITacticalZone zone = ZoneAt(position);
                site = zone != null ? zone.ZoneSite : null;
                if (site == null)
                    return;

                if (playerCarries)
                {
                    planting = ActionInput != null && ActionInput() && player.Speed < 1f;
                    playerHoldingAction = planting;
                }
                else if (carrierBot != null)
                {
                    planting = carrierBot.Brain.State == BotState.Patrol && carrierBot.Speed < 0.6f && carrierBot.HasObjective
                        && carrierBot.AtObjective;
                }
            }
            else if (bomb.State == BombState.Planted)
            {
                if (player != null && !player.IsDead && player.Team == CombatTeam.Defenders
                    && ActionInput != null && ActionInput() && player.Speed < 1f
                    && TacticalObjectives.Planar(player.transform.position, bombPosition) <= Rules.bombReach)
                {
                    defusing = true;
                    playerHoldingAction = true;
                }

                if (!defusing)
                {
                    BotController defuser = director.Defuser;
                    if (defuser != null && !defuser.IsDead && defuser.Team == CombatTeam.Defenders
                        && defuser.Brain.State == BotState.Patrol && defuser.Speed < 0.6f
                        && TacticalObjectives.Planar(defuser.transform.position, bombPosition) <= Rules.bombReach)
                        defusing = true;
                }
            }
        }

        private void OnBombPlanted(string site)
        {
            bombPosition = GroundPoint(lastCarrierPosition);
            DestroyBombProp();
            bombProp = TacticalBombProp.Create(bombPosition, true);
            bombProp.Plant(bombPosition);
            playerCarries = false;
            carrierBot = null;
            director.OnBombPlanted();
        }

        private void OnBombDefused()
        {
            if (bombProp != null)
                bombProp.Defuse();
        }

        private void OnBombExploded()
        {
            if (bombProp != null)
                bombProp.Explode();
        }

        private void DestroyBombProp()
        {
            if (bombProp != null)
                Destroy(bombProp.gameObject);
            bombProp = null;
        }

        private static Vector3 GroundPoint(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            return position;
        }

        public ITacticalZone ZoneAt(Vector3 position)
        {
            for (int i = 0; i < plantZones.Count; i++)
            {
                if (plantZones[i].Contains(position + Vector3.up * 0.5f))
                    return plantZones[i];
            }

            return null;
        }

        public ITacticalZone ZoneBySite(string site)
        {
            for (int i = 0; i < plantZones.Count; i++)
            {
                if (string.Equals(plantZones[i].ZoneSite, site, StringComparison.OrdinalIgnoreCase))
                    return plantZones[i];
            }

            return null;
        }

        #endregion

        #region Player locks and kills

        private void UpdatePlayerLocks()
        {
            if (player == null || Model == null)
                return;

            bool freeze = Model.Phase == TacticalPhase.ClassSelect;
            bool fireOff = freeze || Model.Phase == TacticalPhase.RoundEnd || Model.Phase == TacticalPhase.MatchEnd
                || Model.Phase == TacticalPhase.Inactive;
            bool acting = playerHoldingAction && Model.Phase == TacticalPhase.Live;

            if (playerInput != null)
                playerInput.GameplayLocked = freeze || acting;
            if (playerWeapons != null)
                playerWeapons.FireLocked = fireOff || acting;
        }

        /// <summary>Spawn point the sides' bots and the player start a round from, for tests and the HUD.</summary>
        public IReadOnlyList<Transform> SpawnsOf(CombatTeam side)
        {
            return side == CombatTeam.Attackers ? attackerSpawns : defenderSpawns;
        }

        #endregion

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public void Seed(int seed)
        {
            rng = new System.Random(seed);
        }
    }
}
