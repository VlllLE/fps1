using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One enemy soldier. Perception, aim, weapon and the state machine live in plain classes (<see cref="BotBrain"/>,
/// <see cref="BotAimModel"/>, <see cref="BotGun"/>); this component senses the world, moves along <see cref="BotNavigation"/>
/// paths, fires real hitscan shots through <see cref="HitDetection"/> and owns health, death and respawn.
/// Build bots with <see cref="Create"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class BotController : MonoBehaviour, IHitDamageReceiver, IDamageable, IBotTarget
{
    private const float ArriveDistance = 0.9f;
    private const float CornerDistance = 0.45f;
    private const float FallDuration = 0.55f;

    private static readonly List<BotController> Registry = new List<BotController>();

    private readonly List<Vector3> corners = new List<Vector3>(16);
    private readonly List<Vector3> scratchPath = new List<Vector3>(16);
    private readonly List<Vector3> waypoints = new List<Vector3>(8);

    private BotSettings settings;
    private BotBrain brain;
    private BotAimModel aim;
    private BotGun gun;
    private System.Random rng;
    private HumanoidBody body;
    private Transform eye;
    private bool bodyDeath;
    private float health;
    private bool frozen;
    private bool damagedPending;
    private long shotCounter;

    private int pathIndex;
    private Vector3 pathDestination;
    private bool hasPath;
    private float repathTimer;
    private float stuckTimer;
    private int patrolGiveUps;
    private const int MaxPatrolGiveUps = 3;
    private float currentSpeed;
    private Vector3 lastMoveDirection = Vector3.forward;

    private BotStuckDetector stuck;
    private BotEngagementWatchdog fireWatchdog;
    private bool wantsMove;
    private float steeringUntil;
    private float repositionUntil;
    private Vector3 repositionPoint;
    private float probeSign = 1f;
    private Vector3 separation;
    private readonly List<Vector3> neighbourPositions = new List<Vector3>(8);
    private static readonly float[] ProbeAngles = { 0f, 35f, 70f, 110f, 150f };

    private int waypointIndex;
    private float patrolPause;
    private bool waypointsBuilt;
    private Vector3 spawnPoint;
    private Quaternion spawnRotation;

    private float strafeSign = 1f;
    private float strafeTimer;
    private float scanTimer;
    private float scanYaw;

    private Vector3 coverPoint;
    private bool coverValid;
    private float coverSearchTimer;

    private float deathTime;
    private Quaternion fallFrom;
    private Quaternion fallTo;
    private bool bodyHidden;
    private float lastPlanarSpeed;

    private IBotTarget currentTarget;
    private float targetScanTimer;
    private bool seesCurrentTarget;
    private bool hasObjective;
    private Vector3 objective;
    private float objectiveRadius = 1.5f;
    private float objectiveSpeedFactor = 0.9f;
    private float? objectiveLookYaw;
    private bool objectiveHugWalls;
    private bool holdResolved;
    private bool holdValid;
    private Vector3 holdPoint;
    private BotPeekTimer peekTimer;
    private float peekHideRoll = 1f;
    private const float HoldSearchRadius = 3.5f;
    private const float BotVersusBotDamage = 0.6f;

    /// <summary>How many times the bot ducked behind cover to peek (not counting health or reload cover).</summary>
    public int Peeks { get; private set; }

    /// <summary>Seconds the bot has been in the open since its last peek (see <see cref="BotPeekTimer"/>).</summary>
    public float PeekExposure => peekTimer != null ? peekTimer.Exposed : 0f;

    /// <summary>True when the bot found a wall to stand against while holding its objective.</summary>
    public bool HasHoldPoint => holdValid;

    /// <summary>The wall-hugging spot the bot holds at (valid while <see cref="HasHoldPoint"/>).</summary>
    public Vector3 HoldPoint => holdPoint;

    public static IReadOnlyList<BotController> All => Registry;

    /// <summary>Side the bot fights on; <see cref="CombatTeam.None"/> keeps the classic practice behaviour.</summary>
    public CombatTeam Team { get; set; }

    /// <summary>Source id of the shooter whose hit last damaged this bot (valid while <see cref="HasLastAttacker"/>).</summary>
    public int LastAttackerId { get; private set; }

    /// <summary>True when the last damage came from a hit with a known shooter (not a direct <c>TakeDamage</c> call).</summary>
    public bool HasLastAttacker { get; private set; }

    /// <summary>True when the last damaging hit was a headshot.</summary>
    public bool LastHitWasHeadshot { get; private set; }

    private bool applyingHit;

    /// <summary>When false a dead bot stays dead until a mode calls <see cref="Respawn(Vector3)"/> (tactical rounds).</summary>
    public bool AutoRespawn { get; set; } = true;

    public Transform TargetTransform => transform;
    public Vector3 AimPoint => transform.position + Vector3.up * 1.2f;
    public Vector3 HeadPoint => transform.position + Vector3.up * 1.65f;
    public float Speed => lastPlanarSpeed;

    /// <summary>Enemy the bot is currently tracking (seen or merely heard), or null.</summary>
    public IBotTarget CurrentTarget => currentTarget;

    /// <summary>True while the tracked enemy is in sight this frame.</summary>
    public bool SeesEnemy => seesCurrentTarget;

    public bool HasObjective => hasObjective;
    public Vector3 ObjectivePosition => objective;

    /// <summary>True when an objective is set and the bot stands within its radius.</summary>
    public bool AtObjective => hasObjective && PlanarDistance(transform.position, objective) <= objectiveRadius;

    /// <summary>
    /// Sends the bot to <paramref name="position"/> whenever it is not busy with an enemy; on arrival it holds there and
    /// scans around <paramref name="lookYaw"/> if given. Replaces patrolling until <see cref="ClearObjective"/>.
    /// </summary>
    public void SetObjective(Vector3 position, float radius = 1.5f, float speedFactor = 0.9f, float? lookYaw = null, bool hugWalls = false)
    {
        Vector3 snapped = BotNavigation.IsReady ? BotNavigation.Snap(position, transform) : position;
        if (!hasObjective || PlanarDistance(objective, snapped) > 0.75f)
        {
            hasPath = false;
            holdResolved = false;
            holdValid = false;
        }

        hasObjective = true;
        objective = snapped;
        objectiveRadius = Mathf.Max(ArriveDistance + 0.1f, radius);
        objectiveSpeedFactor = speedFactor;
        objectiveLookYaw = lookYaw;
        objectiveHugWalls = hugWalls;
    }

    public void ClearObjective()
    {
        hasObjective = false;
        objectiveLookYaw = null;
        objectiveHugWalls = false;
        holdResolved = false;
        holdValid = false;
        hasPath = false;
    }

    public BotBrain Brain => brain;
    public BotAimModel Aim => aim;
    public BotGun Gun => gun;
    public BotSettings Settings => settings;
    public HumanoidBody Body => body;
    public float Health => health;
    public float MaxHealth => settings != null ? settings.maxHealth : 0f;
    public float Health01 => settings != null && settings.maxHealth > 0f ? Mathf.Clamp01(health / settings.maxHealth) : 0f;
    public bool IsDead => brain != null && brain.State == BotState.Dead;
    public bool Frozen { get => frozen; set => frozen = value; }
    public int SourceId => gameObject.GetHashCode();
    public int StuckLevel => stuck != null ? stuck.Level : 0;
    public int StuckRecoveries { get; private set; }
    public bool IsRepositioning => Time.time < repositionUntil;
    public bool IsSteering => Time.time < steeringUntil;
    public Vector3 Separation => separation;
    public int Deaths { get; private set; }
    public int ShotsFired { get; private set; }
    public float DamageDealt { get; private set; }
    public Vector3 EyePosition => eye != null ? eye.position : transform.position + Vector3.up * (settings != null ? settings.eyeHeight : 1.6f);
    public Vector3 CoverPoint => coverPoint;
    public bool HasCoverPoint => coverValid;
    public IReadOnlyList<Vector3> PatrolWaypoints => waypoints;

    public event Action<BotController, float> Damaged;
    public event Action<BotController> Died;
    public event Action<BotController> Respawned;

    public static bool IsBotSource(int sourceId)
    {
        for (int i = 0; i < Registry.Count; i++)
        {
            if (Registry[i] != null && Registry[i].SourceId == sourceId)
                return true;
        }

        return false;
    }

    public static bool TryGetBot(int sourceId, out BotController bot)
    {
        for (int i = 0; i < Registry.Count; i++)
        {
            if (Registry[i] != null && Registry[i].SourceId == sourceId)
            {
                bot = Registry[i];
                return true;
            }
        }

        bot = null;
        return false;
    }

    /// <summary>
    /// Creates a bot standing at <paramref name="position"/> (its feet). The humanoid body, hit boxes, weapon prop
    /// are generated at runtime; nothing is saved as an asset.
    /// </summary>
    public static BotController Create(
        string objectName,
        Vector3 position,
        Quaternion rotation,
        BotSettings settings,
        WeaponConfig weapon,
        Color tint,
        int seed)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        if (weapon == null)
            throw new ArgumentNullException(nameof(weapon));

        var go = new GameObject(objectName);
        go.transform.SetPositionAndRotation(position, rotation);

        var rigidbody = go.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        var bodySettings = new HumanoidBodySettings
        {
            palette = HumanoidPalette.ForTarget(tint),
            scale = 1f,
            feetOffset = Vector3.zero,
            useFeetOffset = true,
            createHitboxes = true,
            armPose = HumanoidArmPose.Relaxed,
            look = CharacterLooks.ForBot(weapon, seed),
            lookTint = 0.3f
        };
        HumanoidBody humanoid = HumanoidBody.AddTo(go, bodySettings);

        var bot = go.AddComponent<BotController>();
        bot.Initialize(humanoid, settings, weapon, seed);
        return bot;
    }

    private void Initialize(HumanoidBody humanoid, BotSettings botSettings, WeaponConfig weapon, int seed)
    {
        body = humanoid;
        settings = botSettings;
        rng = new System.Random(seed);
        brain = new BotBrain(settings);
        stuck = new BotStuckDetector();
        stuck.Reset(transform.position);
        fireWatchdog = new BotEngagementWatchdog();
        aim = new BotAimModel(settings);
        peekTimer = new BotPeekTimer(settings, rng);
        peekHideRoll = peekTimer.RollHideTime();
        gun = new BotGun(weapon, settings, rng);
        health = settings.maxHealth;
        spawnPoint = transform.position;
        spawnRotation = transform.rotation;
        aim.SnapTo(transform.forward);
        strafeSign = rng.NextDouble() < 0.5 ? -1f : 1f;
        strafeTimer = NextStrafeInterval();

        eye = new GameObject("BotEye").transform;
        eye.SetParent(transform, false);
        eye.localPosition = Vector3.up * settings.eyeHeight;
        if (body != null)
            body.LookSource = eye;

        EquipBodyWeapon(weapon);
        brain.StateChanged += OnStateChanged;
        UpdateArmPose();
    }

    private void OnEnable()
    {
        if (!Registry.Contains(this))
            Registry.Add(this);
    }

    private void OnDisable()
    {
        Registry.Remove(this);
    }

    private void OnDestroy()
    {
        Registry.Remove(this);
    }

    private void Update()
    {
        if (brain == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        float now = Time.time;

        if (IsDead)
        {
            UpdateDead(now);
            return;
        }

        if (frozen)
        {
            lastPlanarSpeed = 0f;
            return;
        }

        gun.Tick(now, dt);

        peekTimer.Tick(dt, brain.State == BotState.Attack && seesCurrentTarget);
        BotSenses senses = GatherSenses();
        senses.peekDue = peekTimer.Due;
        senses.peekHideTime = peekHideRoll;
        senses.atCoverPoint = coverValid && PlanarDistance(transform.position, coverPoint) <= ArriveDistance + 0.3f;
        brain.Tick(dt, senses);
        if (IsDead)
            return;

        if (gun.NeedsReload && brain.State != BotState.Attack)
            gun.TryStartReload(now);

        UpdateCoverSearch(dt, senses);
        Act(dt, now, senses);
        UpdateEyePitch();
    }

    #region Senses

    private BotSenses GatherSenses()
    {
        var senses = new BotSenses
        {
            health01 = Health01,
            needsReload = gun.NeedsReload,
            isReloading = gun.IsReloading,
            hasCoverPoint = coverValid
        };

        IBotTarget target = SelectTarget(Time.deltaTime);
        currentTarget = target;
        seesCurrentTarget = false;
        senses.targetValid = target != null && !target.IsDead;
        if (brain.HasLastKnownPosition)
            senses.atDestination = PlanarDistance(transform.position, brain.LastKnownPosition) < ArriveDistance + 0.4f;

        if (!senses.targetValid)
        {
            damagedPending = false;
            return senses;
        }

        Vector3 eyePosition = EyePosition;
        Vector3 aimPoint = target.AimPoint;
        senses.targetPosition = target.TargetTransform.position;
        senses.targetDistance = Vector3.Distance(eyePosition, aimPoint);

        bool inCone = BotPerception.IsInViewCone(eyePosition, transform.forward, aimPoint,
            settings.fieldOfView, settings.viewDistance, settings.closeAwarenessRadius);
        senses.seesTarget = inCone
            && (BotPerception.HasLineOfSight(eyePosition, aimPoint, transform, target.TargetTransform)
                || BotPerception.HasLineOfSight(eyePosition, target.HeadPoint, transform, target.TargetTransform));
        seesCurrentTarget = senses.seesTarget;

        if (!senses.seesTarget)
        {
            float heardRadius = BotPerception.HearingRadius(target.Speed, settings.hearingScale);
            if (senses.targetDistance <= heardRadius)
            {
                senses.heardSomething = true;
                senses.heardPosition = target.TargetTransform.position;
            }
        }

        if (damagedPending)
        {
            damagedPending = false;
            if (!senses.seesTarget)
            {
                senses.heardSomething = true;
                senses.heardPosition = target.TargetTransform.position;
            }
        }

        return senses;
    }

    private IBotTarget SelectTarget(float dt)
    {
        if (Team == CombatTeam.None)
        {
            PlayerHealth local = PlayerHealth.Local;
            return local != null && !local.IsDead ? local : null;
        }

        targetScanTimer -= dt;
        if (currentTarget != null && !currentTarget.IsDead && targetScanTimer > 0f)
            return currentTarget;

        targetScanTimer = 0.25f;
        IBotTarget best = null;
        bool bestVisible = false;
        float bestDistance = float.MaxValue;
        Vector3 eyePosition = EyePosition;

        PlayerHealth player = PlayerHealth.Local;
        if (player != null && !player.IsDead && CombatTeams.AreEnemies(Team, player.Team))
            ConsiderTarget(player, eyePosition, ref best, ref bestVisible, ref bestDistance);

        for (int i = 0; i < Registry.Count; i++)
        {
            BotController other = Registry[i];
            if (other == null || other == this || other.brain == null || other.IsDead || !CombatTeams.AreEnemies(Team, other.Team))
                continue;
            ConsiderTarget(other, eyePosition, ref best, ref bestVisible, ref bestDistance);
        }

        return best;
    }

    private void ConsiderTarget(IBotTarget candidate, Vector3 eyePosition, ref IBotTarget best, ref bool bestVisible, ref float bestDistance)
    {
        float distance = Vector3.Distance(eyePosition, candidate.AimPoint);
        bool visible = BotPerception.IsInViewCone(eyePosition, transform.forward, candidate.AimPoint,
                settings.fieldOfView, settings.viewDistance, settings.closeAwarenessRadius)
            && (BotPerception.HasLineOfSight(eyePosition, candidate.AimPoint, transform, candidate.TargetTransform)
                || BotPerception.HasLineOfSight(eyePosition, candidate.HeadPoint, transform, candidate.TargetTransform));

        bool better;
        if (best == null)
            better = true;
        else if (visible != bestVisible)
            better = visible;
        else
            better = distance < bestDistance * (candidate == currentTarget ? 1.25f : 1f);

        if (!better)
            return;
        best = candidate;
        bestVisible = visible;
        bestDistance = distance;
    }

    #endregion

    #region Behaviour

    private void Act(float dt, float now, BotSenses senses)
    {
        IBotTarget target = currentTarget;
        bool sees = senses.targetValid && senses.seesTarget;
        Vector3 desiredAim = sees
            ? target.AimPoint - EyePosition
            : Flatten(transform.forward);

        aim.Track(desiredAim, dt);

        stuck.Window = settings.stuckWindow;
        stuck.MinProgress = settings.stuckMinProgress;
        fireWatchdog.Limit = settings.noFireRepositionTime;
        UpdateSeparation();

        wantsMove = false;
        bool moved = false;
        if (now < repositionUntil && brain.State != BotState.Patrol && brain.State != BotState.TakeCover)
        {
            moved = DoReposition(dt);
        }
        else
        {
            switch (brain.State)
            {
                case BotState.Patrol:
                    moved = DoPatrol(dt);
                    break;
                case BotState.Investigate:
                    moved = DoInvestigate(dt, senses);
                    break;
                case BotState.Chase:
                    moved = DoChase(dt, sees);
                    break;
                case BotState.Attack:
                    moved = DoAttack(dt, target, sees);
                    break;
                case BotState.TakeCover:
                    moved = DoTakeCover(dt, sees);
                    break;
            }
        }

        if (IsDead)
            return;

        bool trackLong = brain.State == BotState.Patrol || brain.State == BotState.Investigate || brain.State == BotState.Chase;
        BotStuckAction stuckAction = stuck.Update(dt, transform.position, wantsMove, trackLong);
        if (stuckAction != BotStuckAction.None)
        {
            HandleStuck(stuckAction, now);
            if (IsDead)
                return;
        }

        if (!moved && !frozen)
            DriftApart(dt);

        if (!moved)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, 20f * dt);
            lastPlanarSpeed = 0f;
        }

        bool engaged = brain.IsEngaged;
        Vector3 facing = engaged && sees ? Flatten(aim.AimDirection) : lastMoveDirection;
        if (brain.State == BotState.Investigate && senses.atDestination)
            facing = Flatten(Quaternion.Euler(0f, scanYaw, 0f) * Vector3.forward);
        else if (brain.State == BotState.Patrol && hasObjective && !moved && objectiveLookYaw.HasValue && AtHoldPosition)
        {
            scanTimer -= dt;
            if (scanTimer <= 0f)
            {
                scanTimer = 1f + (float)rng.NextDouble();
                scanYaw = objectiveLookYaw.Value + (float)(rng.NextDouble() * 80.0 - 40.0);
            }

            facing = Flatten(Quaternion.Euler(0f, scanYaw, 0f) * Vector3.forward);
        }

        FaceToward(facing, dt);

        bool fired = false;
        if (brain.State == BotState.Attack && sees && brain.CanFire && aim.TrackingError <= 12f && !gun.IsReloading)
            fired = TryShoot(now, target);

        if (fireWatchdog.Update(dt, brain.State == BotState.Attack && !fired && !gun.IsReloading))
            StartReposition(now);

        UpdateArmPose();
    }

    private bool AtHoldPosition => holdValid
        ? PlanarDistance(transform.position, holdPoint) <= ArriveDistance + 0.2f
        : AtObjective;

    private void ResolveHoldPoint()
    {
        holdResolved = true;
        holdValid = false;
        if (!objectiveHugWalls || !settings.preferWalls || !BotNavigation.IsReady)
            return;

        if (BotNavigation.TryFindWallPoint(objective, HoldSearchRadius, 10, 1.1f, rng, transform, scratchPath, out Vector3 wall)
            && PlanarDistance(wall, objective) <= HoldSearchRadius + 0.5f)
        {
            holdPoint = wall;
            holdValid = true;
        }
    }

    private bool DoObjective(float dt)
    {
        if (holdValid)
        {
            if (AtHoldPosition)
            {
                hasPath = false;
                return false;
            }

            bool reached = MoveAlongPath(holdPoint, settings.walkSpeed * 1.2f, dt, out bool holdGaveUp);
            if (holdGaveUp)
            {
                hasPath = false;
                holdValid = false;
            }

            return !reached && !holdGaveUp;
        }

        if (AtObjective)
        {
            hasPath = false;
            if (objectiveHugWalls && !holdResolved)
                ResolveHoldPoint();
            return false;
        }

        bool arrived = MoveAlongPath(objective, settings.runSpeed * objectiveSpeedFactor, dt, out bool gaveUp);
        if (gaveUp)
            hasPath = false;
        return !arrived && !gaveUp;
    }

    private bool DoPatrol(float dt)
    {
        if (hasObjective)
            return DoObjective(dt);

        EnsureWaypoints();
        if (waypoints.Count == 0)
            return false;

        if (patrolPause > 0f)
        {
            patrolPause -= dt;
            return false;
        }

        Vector3 destination = waypoints[waypointIndex % waypoints.Count];
        bool arrived = MoveAlongPath(destination, settings.walkSpeed, dt, out bool gaveUp);
        if (arrived)
            patrolGiveUps = 0;
        else if (gaveUp && ++patrolGiveUps >= MaxPatrolGiveUps)
        {
            patrolGiveUps = 0;
            if (Team == CombatTeam.None)
                Respawn(BotManager.FindRespawnPosition(this, spawnPoint));
            return false;
        }

        if (arrived || gaveUp)
        {
            waypointIndex = (waypointIndex + 1) % waypoints.Count;
            patrolPause = Mathf.Lerp(settings.patrolPauseMin, settings.patrolPauseMax, (float)rng.NextDouble());
            hasPath = false;
        }

        return !arrived;
    }

    private bool DoInvestigate(float dt, BotSenses senses)
    {
        if (!brain.HasLastKnownPosition)
            return false;

        if (senses.atDestination)
        {
            scanTimer -= dt;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.8f;
                scanYaw = transform.eulerAngles.y + (float)(rng.NextDouble() * 140.0 - 70.0);
            }

            return false;
        }

        scanTimer = 0f;
        bool arrived = MoveAlongPath(brain.LastKnownPosition, settings.walkSpeed * 1.4f, dt, out bool gaveUp);
        return !arrived && !gaveUp;
    }

    private bool DoChase(float dt, bool sees)
    {
        if (!brain.HasLastKnownPosition)
            return false;

        bool arrived = MoveAlongPath(brain.LastKnownPosition, settings.runSpeed, dt, out bool gaveUp);
        return !arrived && !gaveUp;
    }

    private bool DoAttack(float dt, IBotTarget target, bool sees)
    {
        if (target == null)
            return false;

        if (!sees)
        {
            if (!brain.HasLastKnownPosition)
                return false;
            bool peeking = MoveAlongPath(brain.LastKnownPosition, settings.runSpeed * 0.7f, dt, out bool peekGaveUp);
            return !peeking && !peekGaveUp;
        }

        Vector3 toTarget = Flatten(target.TargetTransform.position - transform.position);
        float distance = toTarget.magnitude;
        if (distance < 0.01f)
            return false;

        Vector3 forward = toTarget / distance;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 move = Vector3.zero;
        float speed = settings.strafeSpeed;

        if (distance > settings.preferredRangeMax)
        {
            move += forward;
            speed = settings.runSpeed * 0.75f;
        }
        else if (distance < settings.preferredRangeMin)
        {
            move -= forward;
        }

        if (settings.strafe)
        {
            strafeTimer -= dt;
            if (strafeTimer <= 0f)
            {
                strafeSign = -strafeSign;
                strafeTimer = NextStrafeInterval();
            }

            move += right * strafeSign * 0.9f;
        }

        if (move.sqrMagnitude < 0.01f)
            return false;

        move.Normalize();
        wantsMove = true;
        bool stepped = StepDirection(move, speed, dt);
        if (!stepped && settings.strafe)
        {
            strafeSign = -strafeSign;
            strafeTimer = NextStrafeInterval();
        }

        return stepped;
    }

    private bool DoTakeCover(float dt, bool sees)
    {
        if (!coverValid)
            return false;
        if (PlanarDistance(transform.position, coverPoint) < ArriveDistance)
            return false;

        bool arrived = MoveAlongPath(coverPoint, settings.runSpeed, dt, out bool gaveUp);
        if (gaveUp)
            coverValid = false;
        return !arrived && !gaveUp;
    }

    private void UpdateCoverSearch(float dt, BotSenses senses)
    {
        if (brain.State == BotState.Patrol || brain.State == BotState.Investigate || !senses.targetValid)
        {
            coverValid = false;
            return;
        }

        coverSearchTimer -= dt;
        bool needsCover = settings.useCover
            && (senses.health01 <= settings.coverHealthFraction || senses.needsReload);
        bool wantsPeek = settings.usePeek && peekTimer.Due;
        if (!(needsCover || wantsPeek) || brain.State == BotState.TakeCover || coverSearchTimer > 0f)
            return;

        coverSearchTimer = 1f;
        IBotTarget target = currentTarget;
        if (target == null)
            return;

        float searchRadius = needsCover ? 14f : 9f;
        coverValid = BotNavigation.TryFindCover(transform.position, target.AimPoint, searchRadius, 10, rng,
            transform, target.TargetTransform, scratchPath, out coverPoint);
    }

    private void EnsureWaypoints()
    {
        if (waypointsBuilt)
            return;
        waypointsBuilt = true;

        var markers = new List<BotWaypoint>();
        for (int i = 0; i < BotWaypoint.All.Count; i++)
        {
            BotWaypoint marker = BotWaypoint.All[i];
            if (marker != null && PlanarDistance(marker.transform.position, spawnPoint) <= settings.patrolRadius * 1.5f)
                markers.Add(marker);
        }

        if (markers.Count > 0)
        {
            int count = Mathf.Min(settings.patrolWaypointCount, markers.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = rng.Next(i, markers.Count);
                (markers[i], markers[pick]) = (markers[pick], markers[i]);
                waypoints.Add(markers[i].transform.position);
            }

            return;
        }

        Vector3 origin = BotNavigation.Snap(spawnPoint, transform);
        for (int i = 0; i < settings.patrolWaypointCount; i++)
        {
            if (BotNavigation.TryRandomReachablePoint(origin, settings.patrolRadius, rng, transform, scratchPath, out Vector3 point))
                waypoints.Add(point);
        }
    }

    #endregion

    #region Movement

    private bool MoveAlongPath(Vector3 destination, float speed, float dt, out bool gaveUp)
    {
        gaveUp = false;
        Vector3 position = transform.position;
        if (PlanarDistance(position, destination) < ArriveDistance && Mathf.Abs(position.y - destination.y) < 2.5f)
            return true;

        wantsMove = true;
        if (Time.time < steeringUntil)
            return SteerTowards(destination, speed, dt);

        repathTimer -= dt;
        if (!hasPath || repathTimer <= 0f || PlanarDistance(pathDestination, destination) > 1.5f)
        {
            hasPath = BotNavigation.TryCalculatePath(position, destination, corners, out _);
            pathDestination = destination;
            repathTimer = 0.8f + (float)rng.NextDouble() * 0.4f;
            pathIndex = corners.Count > 1 ? 1 : 0;
            if (!hasPath)
            {
                gaveUp = true;
                return false;
            }
        }

        while (pathIndex < corners.Count && PlanarDistance(position, corners[pathIndex]) < CornerDistance)
            pathIndex++;
        if (pathIndex >= corners.Count)
        {
            hasPath = false;
            return PlanarDistance(position, destination) < ArriveDistance + 0.6f;
        }

        Vector3 toCorner = Flatten(corners[pathIndex] - position);
        float distance = toCorner.magnitude;
        if (distance < 1e-4f)
        {
            pathIndex++;
            return false;
        }

        Vector3 direction = toCorner / distance;
        bool moved = StepDirection(direction, speed, dt);
        if (moved)
        {
            stuckTimer = 0f;
        }
        else
        {
            stuckTimer += dt;
            if (stuckTimer > 0.5f)
                hasPath = false;
            if (stuckTimer > 2.5f)
            {
                stuckTimer = 0f;
                gaveUp = true;
            }
        }

        return false;
    }

    private void UpdateSeparation()
    {
        neighbourPositions.Clear();
        for (int i = 0; i < Registry.Count; i++)
        {
            BotController other = Registry[i];
            if (other == null || other == this || other.IsDead || other.brain == null)
                continue;
            neighbourPositions.Add(other.transform.position);
        }

        separation = neighbourPositions.Count == 0
            ? Vector3.zero
            : BotSeparation.Compute(transform.position, SourceId, neighbourPositions, settings.separationRadius);
    }

    private void DriftApart(float dt)
    {
        float strength = separation.magnitude;
        if (strength < 0.02f)
            return;

        Vector3 from = transform.position;
        Vector3 step = separation / strength * (1.6f * Mathf.Min(1f, strength * 1.5f) * dt);
        if (BotNavigation.TryStep(from, step, transform, out Vector3 result))
            transform.position = result;
    }

    private void HandleStuck(BotStuckAction action, float now)
    {
        StuckRecoveries++;
        switch (action)
        {
            case BotStuckAction.Repath:
                hasPath = false;
                repathTimer = 0f;
                break;

            case BotStuckAction.NewGoal:
                hasPath = false;
                coverValid = false;
                if (brain.State == BotState.Patrol)
                {
                    if (waypoints.Count < 2)
                    {
                        waypoints.Clear();
                        waypointsBuilt = false;
                    }

                    waypointIndex++;
                    patrolPause = 0f;
                }
                else
                {
                    StartReposition(now);
                }

                break;

            case BotStuckAction.Steering:
                hasPath = false;
                steeringUntil = now + 3f;
                break;

            case BotStuckAction.Respawn:
                if (Team == CombatTeam.None)
                    Respawn(BotManager.FindRespawnPosition(this, spawnPoint));
                else
                    StartReposition(now);
                break;
        }
    }

    private void StartReposition(float now)
    {
        if (BotNavigation.TryRandomReachablePoint(transform.position, 10f, rng, transform, scratchPath, out Vector3 point))
        {
            repositionPoint = point;
            repositionUntil = now + 4f;
            hasPath = false;
        }
    }

    private bool DoReposition(float dt)
    {
        bool arrived = MoveAlongPath(repositionPoint, settings.runSpeed, dt, out bool gaveUp);
        if (arrived || gaveUp)
        {
            repositionUntil = 0f;
            return false;
        }

        return true;
    }

    private bool SteerTowards(Vector3 destination, float speed, float dt)
    {
        Vector3 direction = Flatten(destination - transform.position);
        if (direction.sqrMagnitude < 1e-4f)
            return false;
        direction.Normalize();

        for (int i = 0; i < ProbeAngles.Length; i++)
        {
            float angle = ProbeAngles[i];
            for (int side = 0; side < (angle == 0f ? 1 : 2); side++)
            {
                float signed = angle * (side == 0 ? probeSign : -probeSign);
                if (StepDirection(Quaternion.Euler(0f, signed, 0f) * direction, speed, dt))
                {
                    if (angle != 0f)
                        probeSign = side == 0 ? probeSign : -probeSign;
                    return false;
                }
            }
        }

        return false;
    }

    private bool StepDirection(Vector3 direction, float speed, float dt)
    {
        direction = BotSeparation.Blend(direction, separation, settings.separationStrength);
        if (direction.sqrMagnitude < 1e-6f)
            return false;
        currentSpeed = Mathf.MoveTowards(currentSpeed, speed, 14f * dt);
        float step = currentSpeed * dt;
        Vector3 from = transform.position;
        if (!BotNavigation.TryStep(from, direction * step, transform, out Vector3 result))
        {
            lastPlanarSpeed = 0f;
            return false;
        }

        float actual = PlanarDistance(from, result);
        transform.position = result;
        lastPlanarSpeed = dt > 0f ? actual / dt : 0f;
        if (actual > 1e-4f)
            lastMoveDirection = Flatten(result - from).normalized;
        return actual > step * 0.15f;
    }

    private void FaceToward(Vector3 direction, float dt)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-4f)
            return;

        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, settings.turnSpeed * dt);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void UpdateEyePitch()
    {
        if (eye == null)
            return;

        float pitch = -Mathf.Asin(Mathf.Clamp(aim.AimDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
        eye.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -60f, 60f), 0f, 0f);
    }

    private void UpdateArmPose()
    {
        if (body == null)
            return;
        bool ready = brain.State != BotState.Patrol && brain.State != BotState.Dead;
        body.ArmPose = ready ? HumanoidArmPose.WeaponReady : HumanoidArmPose.Relaxed;
        body.Stance = StanceFor(brain.State);
        body.SetReloadState(gun.IsReloading, gun.ReloadProgress01(Time.time),
            gun.Config != null && gun.Config.perShellReload ? Mathf.Clamp(gun.MagazineSize - gun.Ammo, 1, 12) : 1);
    }

    /// <summary>Lowered carry while patrolling, shouldered rifle while shooting, ready position in between.</summary>
    public static HoldStance StanceFor(BotState state)
    {
        switch (state)
        {
            case BotState.Attack: return HoldStance.Aim;
            case BotState.Patrol:
            case BotState.Dead: return HoldStance.Lowered;
            default: return HoldStance.Ready;
        }
    }

    private void EquipBodyWeapon(WeaponConfig weapon)
    {
        if (body != null)
            body.EquipWeapon(weapon, BotManager.GetWeaponPrefab(weapon));
    }

    private float NextStrafeInterval()
    {
        return Mathf.Lerp(settings.strafeIntervalMin, settings.strafeIntervalMax, (float)rng.NextDouble());
    }

    #endregion

    #region Shooting

    private bool TryShoot(float now, IBotTarget target)
    {
        float weaponSpread = gun.CurrentSpreadDegrees;
        if (!gun.TryFire(now))
            return false;

        ShotsFired++;
        WeaponConfig config = gun.Config;
        Vector3 origin = EyePosition;
        float skillError = aim.ComputeInaccuracy(lastPlanarSpeed, target != null ? target.Speed : 0f);
        Vector3 shotDirection = BotAimModel.ApplyInaccuracy(aim.AimDirection, skillError, rng);

        HitDetection resolver = BotManager.HitResolver;
        Transform muzzle = body != null ? body.Muzzle : null;
        Vector3 muzzlePosition = muzzle != null ? muzzle.position : origin;
        if (resolver != null)
        {
            int pellets = Mathf.Max(1, config.pelletCount);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 pelletDirection = pellets > 1 || weaponSpread > 0f
                    ? BotAimModel.ApplyInaccuracy(shotDirection, weaponSpread, rng)
                    : shotDirection;
                shotCounter++;
                float damageScale = target is BotController ? settings.damageMultiplier * BotVersusBotDamage : settings.damageMultiplier;
                HitRequest request = BotGun.CreateHitRequest(config, origin, pelletDirection, shotCounter, SourceId, transform,
                    damageScale);
                HitResult result = resolver.Resolve(request);
                DamageDealt += result.TotalDamageApplied;
                ShotEffects.PlayHitscan(muzzlePosition, result, config);
            }
        }

        PlayShotEffects(config);
        return true;
    }

    private void PlayShotEffects(WeaponConfig config)
    {
        if (body != null && body.Animator != null)
            body.Animator.AddFlinch(0.1f, Vector3.back);
        if (body != null)
            body.NotifyFired(true);

        if (config.fireSound != null)
            AudioSource.PlayClipAtPoint(config.fireSound, transform.position + Vector3.up * 1.4f, config.fireSoundVolume * 0.8f);
    }

    #endregion

    #region Damage, death, respawn

    public void ApplyHitDamage(HitDamageInfo damage)
    {
        if (IsDead)
            return;

        if (TryGetBot(damage.SourceId, out BotController shooter))
        {
            if (shooter == this || (Team == CombatTeam.None && shooter.Team == CombatTeam.None))
                return;
            if (!CombatTeams.CanDamage(shooter.Team, Team))
                return;
        }
        else
        {
            PlayerHealth local = PlayerHealth.Local;
            if (local != null && !CombatTeams.CanDamage(local.Team, Team))
                return;
        }

        applyingHit = true;
        LastAttackerId = damage.SourceId;
        HasLastAttacker = true;
        try
        {
            TakeDamage(damage.FinalDamage, damage.Direction, damage.Zone == HitZoneType.Head);
        }
        finally
        {
            applyingHit = false;
        }
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead)
            return;
        TakeDamage(damageInfo.damage, damageInfo.hitDirection, damageInfo.isHeadshot);
    }

    /// <summary>Applies damage, plays a flinch, alerts the bot and kills it at zero health.</summary>
    public void TakeDamage(float amount, Vector3 direction, bool headshot)
    {
        if (IsDead || amount <= 0f || brain == null)
            return;

        if (!applyingHit)
            HasLastAttacker = false;
        LastHitWasHeadshot = headshot;
        health = Mathf.Max(0f, health - amount);
        damagedPending = true;
        aim.NotifyHit();
        Damaged?.Invoke(this, amount);

        if (body != null && body.Animator != null)
        {
            float intensity = 0.35f + Mathf.Clamp01(amount / 50f) * 0.65f + (headshot ? 0.3f : 0f);
            body.Animator.AddFlinch(intensity, transform.InverseTransformDirection(direction));
        }

        if (health <= 0f)
            Die(direction, headshot, amount);
    }

    public float GetHealth() => health;

    bool IDamageable.IsDead() => IsDead;

    private void Die(Vector3 shotDirection, bool headshot, float amount)
    {
        health = 0f;
        Deaths++;
        deathTime = Time.time;
        brain.Kill();
        SetCollidersEnabled(false);
        float deathSpeed = lastPlanarSpeed;
        currentSpeed = 0f;
        lastPlanarSpeed = 0f;

        Vector3 flat = Flatten(shotDirection);
        if (flat.sqrMagnitude < 1e-4f)
            flat = transform.forward;
        flat.Normalize();
        Vector3 axis = Vector3.Cross(Vector3.up, flat);
        fallFrom = transform.rotation;
        fallTo = Quaternion.AngleAxis(88f, axis) * fallFrom;

        bodyDeath = body != null && body.IsBuilt && body.Animator != null;
        if (bodyDeath)
        {
            Vector3 planarVelocity = lastMoveDirection * deathSpeed;
            var context = new DeathContext
            {
                localHitDirection = transform.InverseTransformDirection(flat),
                headshot = headshot,
                damage = amount,
                forwardSpeed = Vector3.Dot(planarVelocity, transform.forward),
                crouched = false,
                roll = (float)rng.NextDouble()
            };
            float impulse = Mathf.Clamp(0.8f + amount / 100f + (headshot ? 0.3f : 0f), 0.8f, 1.7f);
            body.BeginDeath(context, impulse);
        }

        Died?.Invoke(this);
    }

    private void UpdateDead(float now)
    {
        float elapsed = now - deathTime;
        if (!bodyDeath)
            transform.rotation = Quaternion.Slerp(fallFrom, fallTo, Mathf.Clamp01(elapsed / FallDuration));

        if (!bodyHidden && elapsed >= settings.corpseTime)
            SetBodyVisible(false);

        if (AutoRespawn && elapsed >= Mathf.Max(settings.respawnDelay, settings.corpseTime))
        {
            Vector3 position = BotManager.FindRespawnPosition(this, spawnPoint);
            Respawn(position);
        }
    }

    /// <summary>Brings the bot back with full health and ammo at <paramref name="position"/>.</summary>
    public void Respawn(Vector3 position)
    {
        Respawn(position, (float)(rng.NextDouble() * 360.0));
    }

    /// <summary>Brings the bot back with full health and ammo at <paramref name="position"/>, facing <paramref name="yaw"/>.</summary>
    public void Respawn(Vector3 position, float yaw)
    {
        if (body != null)
            body.EndDeath();
        bodyDeath = false;
        transform.position = BotNavigation.Snap(position, transform);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        spawnPoint = transform.position;
        spawnRotation = transform.rotation;
        health = settings.maxHealth;
        gun.Refill();
        aim.SnapTo(transform.forward);
        aim.ResetTracking();
        aim.ClearFlinch();
        peekTimer.Restart();
        peekHideRoll = peekTimer.RollHideTime();
        holdResolved = false;
        holdValid = false;
        waypoints.Clear();
        waypointsBuilt = false;
        waypointIndex = 0;
        hasPath = false;
        coverValid = false;
        currentTarget = null;
        targetScanTimer = 0f;
        seesCurrentTarget = false;
        damagedPending = false;
        stuckTimer = 0f;
        patrolPause = 0f;
        repositionUntil = 0f;
        steeringUntil = 0f;
        stuck.Reset(transform.position);
        fireWatchdog.Reset();
        brain.Revive();
        SetCollidersEnabled(true);
        SetBodyVisible(true);
        Respawned?.Invoke(this);
    }

    /// <summary>Refills health after the shared settings changed (difficulty switch).</summary>
    public void RefreshFromSettings()
    {
        if (!IsDead)
            health = settings.maxHealth;
    }

    private void OnStateChanged(BotState from, BotState to)
    {
        if (to != BotState.TakeCover)
            coverValid = to == BotState.Attack && coverValid;
        hasPath = false;
        if (to == BotState.TakeCover && brain.CoverIsPeek)
            Peeks++;
        if (from == BotState.TakeCover && peekTimer != null)
        {
            peekTimer.Restart();
            peekHideRoll = peekTimer.RollHideTime();
        }

        if ((from == BotState.Patrol || from == BotState.Investigate) && (to == BotState.Chase || to == BotState.Attack))
            brain.ReactionScale = 1f + settings.reactionJitter * (float)rng.NextDouble();
        if (to == BotState.Attack)
            aim.ResetTracking();
        if (to == BotState.Investigate)
        {
            scanTimer = 0f;
            scanYaw = transform.eulerAngles.y;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = enabled;
    }

    private void SetBodyVisible(bool visible)
    {
        bodyHidden = !visible;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;
    }

    #endregion

    #region Visual props

    #endregion

    #region Helpers

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void OnDrawGizmosSelected()
    {
        if (settings == null)
            return;

        Vector3 eyePosition = EyePosition;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
        float half = settings.fieldOfView * 0.5f;
        Gizmos.DrawRay(eyePosition, Quaternion.Euler(0f, half, 0f) * transform.forward * 8f);
        Gizmos.DrawRay(eyePosition, Quaternion.Euler(0f, -half, 0f) * transform.forward * 8f);

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Count; i++)
            Gizmos.DrawWireSphere(waypoints[i], 0.3f);
        Gizmos.color = Color.green;
        for (int i = pathIndex; i + 1 < corners.Count; i++)
            Gizmos.DrawLine(corners[i], corners[i + 1]);
        if (coverValid)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(coverPoint, 0.5f);
        }
    }

    #endregion
}
