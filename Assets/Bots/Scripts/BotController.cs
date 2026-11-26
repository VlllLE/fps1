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
public sealed class BotController : MonoBehaviour, IHitDamageReceiver, IDamageable
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
    private Transform muzzle;
    private LineRenderer tracer;
    private Material propMaterial;
    private Material tracerMaterial;
    private readonly List<GameObject> props = new List<GameObject>();

    private float health;
    private bool frozen;
    private bool damagedPending;
    private long shotCounter;

    private int pathIndex;
    private Vector3 pathDestination;
    private bool hasPath;
    private float repathTimer;
    private float stuckTimer;
    private float currentSpeed;
    private Vector3 lastMoveDirection = Vector3.forward;

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
    private float tracerHideTime;
    private Quaternion fallFrom;
    private Quaternion fallTo;
    private bool bodyHidden;
    private float lastPlanarSpeed;

    public static IReadOnlyList<BotController> All => Registry;

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
    /// Creates a bot standing at <paramref name="position"/> (its feet). The humanoid body, hit boxes, weapon prop and tracer
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
            armPose = HumanoidArmPose.Relaxed
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
        aim = new BotAimModel(settings);
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

        BuildWeaponProp();
        BuildTracer();
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
        if (propMaterial != null)
            Destroy(propMaterial);
        if (tracerMaterial != null)
            Destroy(tracerMaterial);
    }

    private void Update()
    {
        if (brain == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        float now = Time.time;
        if (tracer != null && tracer.enabled && now >= tracerHideTime)
            tracer.enabled = false;

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

        BotSenses senses = GatherSenses();
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

        PlayerHealth target = PlayerHealth.Local;
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
        senses.targetPosition = target.transform.position;
        senses.targetDistance = Vector3.Distance(eyePosition, aimPoint);

        bool inCone = BotPerception.IsInViewCone(eyePosition, transform.forward, aimPoint,
            settings.fieldOfView, settings.viewDistance, settings.closeAwarenessRadius);
        senses.seesTarget = inCone
            && (BotPerception.HasLineOfSight(eyePosition, aimPoint, transform, target.transform)
                || BotPerception.HasLineOfSight(eyePosition, target.HeadPoint, transform, target.transform));

        if (!senses.seesTarget)
        {
            float heardRadius = BotPerception.HearingRadius(target.Speed, settings.hearingScale);
            if (senses.targetDistance <= heardRadius)
            {
                senses.heardSomething = true;
                senses.heardPosition = target.transform.position;
            }
        }

        if (damagedPending)
        {
            damagedPending = false;
            if (!senses.seesTarget)
            {
                senses.heardSomething = true;
                senses.heardPosition = target.transform.position;
            }
        }

        return senses;
    }

    #endregion

    #region Behaviour

    private void Act(float dt, float now, BotSenses senses)
    {
        PlayerHealth target = PlayerHealth.Local;
        bool sees = senses.targetValid && senses.seesTarget;
        Vector3 desiredAim = sees
            ? target.AimPoint - EyePosition
            : Flatten(transform.forward);

        aim.Track(desiredAim, dt);

        bool moved = false;
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

        if (!moved)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, 20f * dt);
            lastPlanarSpeed = 0f;
        }

        bool engaged = brain.IsEngaged;
        Vector3 facing = engaged && sees ? Flatten(aim.AimDirection) : lastMoveDirection;
        if (brain.State == BotState.Investigate && senses.atDestination)
            facing = Flatten(Quaternion.Euler(0f, scanYaw, 0f) * Vector3.forward);
        FaceToward(facing, dt);

        if (brain.State == BotState.Attack && sees && brain.CanFire && aim.TrackingError <= 12f && !gun.IsReloading)
            TryShoot(now, target);

        UpdateArmPose();
    }

    private bool DoPatrol(float dt)
    {
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

    private bool DoAttack(float dt, PlayerHealth target, bool sees)
    {
        if (target == null || !sees)
            return false;

        Vector3 toTarget = Flatten(target.transform.position - transform.position);
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
        bool wantsCover = settings.useCover
            && (senses.health01 <= settings.coverHealthFraction || senses.needsReload);
        if (!wantsCover || brain.State == BotState.TakeCover || coverSearchTimer > 0f)
            return;

        coverSearchTimer = 1f;
        PlayerHealth target = PlayerHealth.Local;
        if (target == null)
            return;

        coverValid = BotNavigation.TryFindCover(transform.position, target.AimPoint, 14f, 10, rng,
            transform, target.transform, scratchPath, out coverPoint);
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

    private bool StepDirection(Vector3 direction, float speed, float dt)
    {
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
    }

    private float NextStrafeInterval()
    {
        return Mathf.Lerp(settings.strafeIntervalMin, settings.strafeIntervalMax, (float)rng.NextDouble());
    }

    #endregion

    #region Shooting

    private void TryShoot(float now, PlayerHealth target)
    {
        float weaponSpread = gun.CurrentSpreadDegrees;
        if (!gun.TryFire(now))
            return;

        ShotsFired++;
        WeaponConfig config = gun.Config;
        Vector3 origin = EyePosition;
        float skillError = aim.ComputeInaccuracy(lastPlanarSpeed, target != null ? target.Speed : 0f);
        Vector3 shotDirection = BotAimModel.ApplyInaccuracy(aim.AimDirection, skillError, rng);

        HitDetection resolver = BotManager.HitResolver;
        Vector3 tracerEnd = origin + shotDirection * Mathf.Min(config.range, 60f);
        if (resolver != null)
        {
            int pellets = Mathf.Max(1, config.pelletCount);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 pelletDirection = pellets > 1 || weaponSpread > 0f
                    ? BotAimModel.ApplyInaccuracy(shotDirection, weaponSpread, rng)
                    : shotDirection;
                shotCounter++;
                HitRequest request = BotGun.CreateHitRequest(config, origin, pelletDirection, shotCounter, SourceId, transform,
                    settings.damageMultiplier);
                HitResult result = resolver.Resolve(request);
                DamageDealt += result.TotalDamageApplied;
                if (i == 0)
                {
                    tracerEnd = result.Impacts.Count > 0
                        ? result.Impacts[result.Impacts.Count - 1].RaycastHit.point
                        : origin + pelletDirection * Mathf.Min(config.range, 60f);
                }
            }
        }

        PlayShotEffects(config, tracerEnd);
    }

    private void PlayShotEffects(WeaponConfig config, Vector3 tracerEnd)
    {
        if (body != null && body.Animator != null)
            body.Animator.AddFlinch(0.1f, Vector3.back);

        if (tracer != null)
        {
            Vector3 start = muzzle != null ? muzzle.position : EyePosition;
            tracer.SetPosition(0, start);
            tracer.SetPosition(1, tracerEnd);
            tracer.enabled = true;
            tracerHideTime = Time.time + 0.05f;
        }

        if (config.fireSound != null)
            AudioSource.PlayClipAtPoint(config.fireSound, transform.position + Vector3.up * 1.4f, config.fireSoundVolume * 0.8f);
    }

    #endregion

    #region Damage, death, respawn

    public void ApplyHitDamage(HitDamageInfo damage)
    {
        if (IsDead || IsBotSource(damage.SourceId))
            return;

        TakeDamage(damage.FinalDamage, damage.Direction, damage.Zone == HitZoneType.Head);
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

        health = Mathf.Max(0f, health - amount);
        damagedPending = true;
        Damaged?.Invoke(this, amount);

        if (body != null && body.Animator != null)
        {
            float intensity = 0.35f + Mathf.Clamp01(amount / 50f) * 0.65f + (headshot ? 0.3f : 0f);
            body.Animator.AddFlinch(intensity, transform.InverseTransformDirection(direction));
        }

        if (health <= 0f)
            Die(direction);
    }

    public float GetHealth() => health;

    bool IDamageable.IsDead() => IsDead;

    private void Die(Vector3 shotDirection)
    {
        health = 0f;
        Deaths++;
        deathTime = Time.time;
        brain.Kill();
        SetCollidersEnabled(false);
        if (tracer != null)
            tracer.enabled = false;
        currentSpeed = 0f;
        lastPlanarSpeed = 0f;

        Vector3 flat = Flatten(shotDirection);
        if (flat.sqrMagnitude < 1e-4f)
            flat = transform.forward;
        flat.Normalize();
        Vector3 axis = Vector3.Cross(Vector3.up, flat);
        fallFrom = transform.rotation;
        fallTo = Quaternion.AngleAxis(88f, axis) * fallFrom;
        Died?.Invoke(this);
    }

    private void UpdateDead(float now)
    {
        float elapsed = now - deathTime;
        transform.rotation = Quaternion.Slerp(fallFrom, fallTo, Mathf.Clamp01(elapsed / FallDuration));

        if (!bodyHidden && elapsed >= settings.corpseTime)
            SetBodyVisible(false);

        if (elapsed >= Mathf.Max(settings.respawnDelay, settings.corpseTime))
        {
            Vector3 position = BotManager.FindRespawnPosition(this, spawnPoint);
            Respawn(position);
        }
    }

    /// <summary>Brings the bot back with full health and ammo at <paramref name="position"/>.</summary>
    public void Respawn(Vector3 position)
    {
        transform.position = BotNavigation.Snap(position, transform);
        transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
        spawnPoint = transform.position;
        spawnRotation = transform.rotation;
        health = settings.maxHealth;
        gun.Refill();
        aim.SnapTo(transform.forward);
        aim.ResetTracking();
        waypoints.Clear();
        waypointsBuilt = false;
        waypointIndex = 0;
        hasPath = false;
        coverValid = false;
        damagedPending = false;
        stuckTimer = 0f;
        patrolPause = 0f;
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
        {
            if (renderers[i] != tracer)
                renderers[i].enabled = visible;
        }
    }

    #endregion

    #region Visual props

    private void BuildWeaponProp()
    {
        if (body == null)
            return;

        Transform hand = body.GetBone(HumanoidBone.HandR);
        if (hand == null)
            return;

        propMaterial = CreateUnlitMaterial(new Color(0.08f, 0.08f, 0.09f));
        Mesh cube = GetCubeMesh();
        if (cube == null)
            return;

        var rifle = new GameObject("BotRifle").transform;
        rifle.SetParent(hand, false);
        rifle.localPosition = new Vector3(0f, -0.12f, 0.02f);
        rifle.localRotation = Quaternion.identity;
        props.Add(rifle.gameObject);

        AddPropPart(rifle, cube, new Vector3(0f, -0.10f, 0f), new Vector3(0.05f, 0.55f, 0.07f));
        AddPropPart(rifle, cube, new Vector3(0f, 0.12f, 0.04f), new Vector3(0.04f, 0.18f, 0.06f));
        AddPropPart(rifle, cube, new Vector3(0f, -0.12f, -0.08f), new Vector3(0.035f, 0.1f, 0.1f));

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(rifle, false);
        muzzle.localPosition = new Vector3(0f, -0.4f, 0f);
    }

    private static Mesh GetCubeMesh()
    {
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (cube != null)
            return cube;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.transform.position = new Vector3(0f, -10000f, 0f);
        cube = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return cube;
    }

    private void AddPropPart(Transform parent, Mesh mesh, Vector3 localPosition, Vector3 localScale)
    {
        var part = new GameObject("Part");
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = propMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void BuildTracer()
    {
        var tracerObject = new GameObject("BotTracer");
        tracerObject.transform.SetParent(transform, false);
        tracer = tracerObject.AddComponent<LineRenderer>();
        tracer.useWorldSpace = true;
        tracer.positionCount = 2;
        tracer.startWidth = 0.03f;
        tracer.endWidth = 0.01f;
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.receiveShadows = false;
        tracerMaterial = CreateUnlitMaterial(new Color(1f, 0.85f, 0.4f));
        tracer.sharedMaterial = tracerMaterial;
        tracer.enabled = false;
    }

    private static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader) { color = color };
        return material;
    }

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
