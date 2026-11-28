using System;
using UnityEngine;

/// <summary>
/// Health of the local player. Receives hitscan damage through <see cref="IHitDamageReceiver"/> (the player's
/// CharacterController is the hit collider), regenerates after a delay, and respawns at its starting point after dying.
/// Added at runtime by <see cref="BotManager"/> when a scene has a player but no PlayerHealth.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour, IHitDamageReceiver, IDamageable, IBotTarget
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [Tooltip("Seconds without taking damage before health regenerates.")]
    [SerializeField, Min(0f)] private float regenDelay = 5f;
    [SerializeField, Min(0f)] private float regenPerSecond = 25f;
    [SerializeField, Min(0.1f)] private float respawnDelay = 3f;
    [SerializeField] private bool godMode;

    private float currentHealth;
    private bool initialised;
    private float lastDamageTime = -999f;
    private float deathTime;
    private bool dead;
    private Vector3 spawnPosition;
    private float spawnYaw;
    private float spawnPitch;
    private FirstPersonCamera lookCamera;
    private CharacterController controller;
    private MovementController movement;

    public static PlayerHealth Local { get; private set; }

    /// <summary>Side the player fights on. <see cref="CombatTeam.None"/> outside team modes.</summary>
    public CombatTeam Team { get; set; }

    /// <summary>When false the player stays dead until a mode revives it (tactical rounds).</summary>
    public bool AutoRespawn { get; set; } = true;

    /// <summary>When false health never regenerates (tactical rounds).</summary>
    public bool RegenEnabled { get; set; } = true;

    public Transform TargetTransform => transform;

    public float CurrentHealth { get { Initialise(); return currentHealth; } }
    public float MaxHealth => maxHealth;
    public float Health01 { get { Initialise(); return maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f; } }
    public bool IsDead => dead;
    public bool GodMode { get => godMode; set => godMode = value; }
    public int Deaths { get; private set; }
    public float TotalDamageTaken { get; private set; }
    public float TimeSinceLastDamage => Now - lastDamageTime;
    public Vector3 SpawnPosition { get { Initialise(); return spawnPosition; } }

    /// <summary>Time source (seconds). Replaceable so tests can step regeneration and respawn deterministically.</summary>
    public Func<float> Clock { get; set; }

    private float Now => Clock != null ? Clock() : Time.time;

    /// <summary>Raised on every hit: damage dealt, world position of the shooter and whether it was fatal.</summary>
    public event Action<float, Vector3, bool> OnDamaged;
    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action OnRespawned;

    /// <summary>World-space point bots aim at (chest height).</summary>
    public Vector3 AimPoint => ResolveCenter() + Vector3.up * 0.2f;

    /// <summary>World-space head point, used as a second line-of-sight probe.</summary>
    public Vector3 HeadPoint => transform.position + Vector3.up * 1.7f;

    /// <summary>Horizontal speed in m/s (from the movement controller, otherwise 0).</summary>
    public float Speed
    {
        get
        {
            Initialise();
            if (movement == null)
                return 0f;
            Vector3 v = movement.Velocity;
            return new Vector2(v.x, v.z).magnitude;
        }
    }

    private void Awake()
    {
        Initialise();
    }

    private void Initialise()
    {
        if (initialised)
            return;

        initialised = true;
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        spawnYaw = transform.eulerAngles.y;
        controller = GetComponent<CharacterController>();
        movement = GetComponent<MovementController>();
        lookCamera = GetComponentInChildren<FirstPersonCamera>();
        spawnPitch = lookCamera != null ? NormalizeAngle(lookCamera.transform.localEulerAngles.x) : 0f;
        Local = this;
    }

    private void OnDestroy()
    {
        if (Local == this)
            Local = null;
    }

    private void Update()
    {
        Step(Now, Time.deltaTime);
    }

    /// <summary>Advances regeneration and the respawn timer. Called every frame by <see cref="Update"/>; public for tests.</summary>
    public void Step(float now, float deltaTime)
    {
        Initialise();
        if (dead)
        {
            if (AutoRespawn && now >= deathTime + respawnDelay)
                Respawn();
            return;
        }

        if (RegenEnabled && currentHealth < maxHealth && now >= lastDamageTime + regenDelay)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * deltaTime);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    public void ApplyHitDamage(HitDamageInfo damage)
    {
        Vector3 source;
        if (!BotManager.TryGetShooterPosition(damage.SourceId, out source))
            source = damage.Point - damage.Direction.normalized * 10f;
        CombatTeam shooterTeam;
        if (BotManager.TryGetShooterTeam(damage.SourceId, out shooterTeam) && !CombatTeams.CanDamage(shooterTeam, Team))
            return;
        TakeDamage(damage.FinalDamage, source);
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        TakeDamage(damageInfo.damage, damageInfo.hitPoint - damageInfo.hitDirection.normalized * 10f);
    }

    /// <summary>Applies damage from a shooter at <paramref name="sourcePosition"/>. Ignored while dead or in god mode.</summary>
    public void TakeDamage(float amount, Vector3 sourcePosition)
    {
        Initialise();
        if (dead || amount <= 0f)
            return;

        lastDamageTime = Now;
        TotalDamageTaken += amount;
        bool fatal = false;
        if (!godMode)
        {
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            fatal = currentHealth <= 0f;
        }

        OnDamaged?.Invoke(amount, sourcePosition, fatal);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (fatal)
            Die();
    }

    public float GetHealth() => CurrentHealth;

    bool IDamageable.IsDead() => dead;

    public void Heal(float amount)
    {
        Initialise();
        if (dead || amount <= 0f)
            return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetSpawnPosition(Vector3 position)
    {
        Initialise();
        spawnPosition = position;
    }

    /// <summary>Facing restored on respawn (degrees).</summary>
    public void SetSpawnFacing(float yaw, float pitch)
    {
        Initialise();
        spawnYaw = yaw;
        spawnPitch = pitch;
    }

    public float SpawnYaw { get { Initialise(); return spawnYaw; } }
    public float SpawnPitch { get { Initialise(); return spawnPitch; } }

    private void Die()
    {
        dead = true;
        deathTime = Now;
        Deaths++;
        OnDied?.Invoke();
    }

    /// <summary>Restores full health and moves the player back to its spawn position.</summary>
    public void Respawn()
    {
        Initialise();
        dead = false;
        currentHealth = maxHealth;
        lastDamageTime = -999f;

        if (controller != null)
            controller.enabled = false;
        transform.position = spawnPosition;
        transform.rotation = Quaternion.Euler(0f, spawnYaw, 0f);
        if (lookCamera != null)
            lookCamera.SetLook(spawnYaw, spawnPitch);
        if (controller != null)
            controller.enabled = true;
        if (movement != null)
            movement.ResetMovement();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnRespawned?.Invoke();
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f)
            degrees -= 360f;
        else if (degrees < -180f)
            degrees += 360f;
        return degrees;
    }

    private Vector3 ResolveCenter()
    {
        Initialise();
        if (controller != null)
            return transform.TransformPoint(controller.center);
        return transform.position + Vector3.up;
    }
}
