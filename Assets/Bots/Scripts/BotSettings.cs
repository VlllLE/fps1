using UnityEngine;

public enum BotDifficulty
{
    Easy = 0,
    Normal = 1,
    Hard = 2,
    Veteran = 3
}

/// <summary>
/// Tunable behaviour of a bot: health, movement, perception, aim and fire discipline. Create an asset via
/// Assets &gt; Create &gt; Bots &gt; Bot Settings, or use <see cref="CreatePreset"/> for the four built-in difficulties.
/// </summary>
[CreateAssetMenu(fileName = "BotSettings", menuName = "Bots/Bot Settings")]
public class BotSettings : ScriptableObject
{
    [Header("Identity")]
    public BotDifficulty difficulty = BotDifficulty.Normal;

    [Header("Health")]
    [Min(1f)] public float maxHealth = 100f;
    [Tooltip("Seconds between dying and coming back at a new spawn point.")]
    [Min(0f)] public float respawnDelay = 5f;
    [Tooltip("Seconds the body stays visible after dying.")]
    [Min(0f)] public float corpseTime = 2.5f;

    [Header("Movement (m/s, deg/s)")]
    [Min(0.1f)] public float walkSpeed = 2.2f;
    [Min(0.1f)] public float runSpeed = 4.4f;
    [Min(0.1f)] public float strafeSpeed = 2.6f;
    [Min(10f)] public float turnSpeed = 300f;

    [Header("Perception")]
    [Min(1f)] public float viewDistance = 70f;
    [Range(20f, 360f)] public float fieldOfView = 110f;
    [Tooltip("Targets closer than this are noticed even outside the view cone (still needs line of sight).")]
    [Min(0f)] public float closeAwarenessRadius = 3.5f;
    [Min(0.5f)] public float eyeHeight = 1.6f;
    [Tooltip("Seconds a bot keeps chasing the last known position after losing sight.")]
    [Min(0f)] public float memoryTime = 6f;
    [Tooltip("Seconds spent looking around at an investigated position before resuming patrol.")]
    [Min(0f)] public float investigateTime = 3f;
    [Tooltip("Seconds without line of sight before an attacking bot starts chasing.")]
    [Min(0f)] public float lostSightGrace = 0.35f;
    [Tooltip("Scales how far away the bot hears the player moving.")]
    [Min(0f)] public float hearingScale = 1f;

    [Header("Aim")]
    [Tooltip("Seconds between first seeing the target and the first shot.")]
    [Min(0f)] public float reactionTime = 0.5f;
    [Tooltip("How fast the aim line swings towards the target.")]
    [Min(5f)] public float aimTurnSpeed = 150f;
    [Tooltip("Base aim error (degrees, radius of the cone) before weapon spread.")]
    [Min(0f)] public float aimErrorDegrees = 3.5f;
    [Tooltip("Extra aim error while the bot itself is moving.")]
    [Min(0f)] public float aimErrorWhileMoving = 1.5f;
    [Tooltip("Extra aim error per m/s of target speed.")]
    [Min(0f)] public float aimErrorPerTargetSpeed = 0.18f;
    [Tooltip("Seconds of tracking until the aim error has shrunk to its minimum.")]
    [Min(0.05f)] public float aimSettleTime = 1.6f;
    [Tooltip("Aim error multiplier once fully settled (1 = no improvement).")]
    [Range(0.05f, 1f)] public float settledErrorFraction = 0.45f;
    [Tooltip("Multiplier on the weapon's own spread (including bloom).")]
    [Min(0f)] public float weaponSpreadScale = 1f;

    [Header("Fire discipline")]
    [Min(1)] public int burstMin = 3;
    [Min(1)] public int burstMax = 6;
    [Min(0f)] public float burstPauseMin = 0.35f;
    [Min(0f)] public float burstPauseMax = 0.9f;
    [Tooltip("Pause after each round of a semi-automatic weapon.")]
    [Min(0f)] public float semiDelay = 0.3f;
    [Tooltip("Scales weapon damage against the player (the player has 100 health).")]
    [Min(0f)] public float damageMultiplier = 0.4f;

    [Header("Tactics")]
    [Tooltip("Preferred engagement band: closer than min backs off, further than max advances.")]
    [Min(0f)] public float preferredRangeMin = 7f;
    [Min(1f)] public float preferredRangeMax = 24f;
    [Tooltip("Beyond this the bot chases instead of shooting.")]
    [Min(1f)] public float maxEngageRange = 45f;
    public bool strafe = true;
    [Min(0.1f)] public float strafeIntervalMin = 0.8f;
    [Min(0.1f)] public float strafeIntervalMax = 2f;
    public bool useCover = true;
    [Range(0f, 1f)] public float coverHealthFraction = 0.35f;
    [Min(0f)] public float coverDuration = 2.5f;
    [Min(0f)] public float coverCooldown = 8f;

    [Header("Patrol")]
    [Min(2f)] public float patrolRadius = 28f;
    [Min(2)] public int patrolWaypointCount = 4;
    [Min(0f)] public float patrolPauseMin = 0.5f;
    [Min(0f)] public float patrolPauseMax = 2f;

    public static BotSettings CreatePreset(BotDifficulty level)
    {
        var settings = CreateInstance<BotSettings>();
        settings.name = "BotSettings_" + level;
        settings.ApplyPreset(level);
        return settings;
    }

    /// <summary>Overwrites the difficulty-dependent fields (accuracy, reaction, speed, damage, health) with a preset.</summary>
    public void ApplyPreset(BotDifficulty level)
    {
        difficulty = level;
        switch (level)
        {
            case BotDifficulty.Easy:
                maxHealth = 70f;
                walkSpeed = 1.8f; runSpeed = 3.4f; strafeSpeed = 1.8f; turnSpeed = 220f;
                fieldOfView = 90f; viewDistance = 45f; memoryTime = 3.5f; hearingScale = 0.6f;
                reactionTime = 0.95f; aimTurnSpeed = 80f;
                aimErrorDegrees = 7f; aimErrorWhileMoving = 3f; aimErrorPerTargetSpeed = 0.35f;
                settledErrorFraction = 0.7f; weaponSpreadScale = 1.3f;
                burstMin = 2; burstMax = 3; burstPauseMin = 0.8f; burstPauseMax = 1.6f; semiDelay = 0.6f;
                damageMultiplier = 0.25f;
                strafe = false; useCover = false;
                break;
            case BotDifficulty.Hard:
                maxHealth = 130f;
                walkSpeed = 2.4f; runSpeed = 5f; strafeSpeed = 3f; turnSpeed = 380f;
                fieldOfView = 120f; viewDistance = 90f; memoryTime = 9f; hearingScale = 1.3f;
                reactionTime = 0.3f; aimTurnSpeed = 240f;
                aimErrorDegrees = 2f; aimErrorWhileMoving = 1f; aimErrorPerTargetSpeed = 0.12f;
                settledErrorFraction = 0.35f; weaponSpreadScale = 0.85f;
                burstMin = 4; burstMax = 8; burstPauseMin = 0.25f; burstPauseMax = 0.6f; semiDelay = 0.2f;
                damageMultiplier = 0.6f;
                strafe = true; useCover = true;
                break;
            case BotDifficulty.Veteran:
                maxHealth = 160f;
                walkSpeed = 2.6f; runSpeed = 5.6f; strafeSpeed = 3.4f; turnSpeed = 450f;
                fieldOfView = 140f; viewDistance = 110f; memoryTime = 12f; hearingScale = 1.6f;
                reactionTime = 0.15f; aimTurnSpeed = 340f;
                aimErrorDegrees = 1f; aimErrorWhileMoving = 0.6f; aimErrorPerTargetSpeed = 0.07f;
                settledErrorFraction = 0.3f; weaponSpreadScale = 0.7f;
                burstMin = 5; burstMax = 10; burstPauseMin = 0.15f; burstPauseMax = 0.4f; semiDelay = 0.12f;
                damageMultiplier = 0.8f;
                strafe = true; useCover = true;
                break;
            default:
                maxHealth = 100f;
                walkSpeed = 2.2f; runSpeed = 4.4f; strafeSpeed = 2.6f; turnSpeed = 300f;
                fieldOfView = 110f; viewDistance = 70f; memoryTime = 6f; hearingScale = 1f;
                reactionTime = 0.5f; aimTurnSpeed = 150f;
                aimErrorDegrees = 3.5f; aimErrorWhileMoving = 1.5f; aimErrorPerTargetSpeed = 0.18f;
                settledErrorFraction = 0.45f; weaponSpreadScale = 1f;
                burstMin = 3; burstMax = 6; burstPauseMin = 0.35f; burstPauseMax = 0.9f; semiDelay = 0.3f;
                damageMultiplier = 0.4f;
                strafe = true; useCover = true;
                break;
        }
    }

    /// <summary>Next difficulty in Easy &gt; Normal &gt; Hard &gt; Veteran &gt; Easy order.</summary>
    public static BotDifficulty Next(BotDifficulty level)
    {
        return (BotDifficulty)(((int)level + 1) % 4);
    }

    private void OnValidate()
    {
        burstMax = Mathf.Max(burstMin, burstMax);
        burstPauseMax = Mathf.Max(burstPauseMin, burstPauseMax);
        strafeIntervalMax = Mathf.Max(strafeIntervalMin, strafeIntervalMax);
        patrolPauseMax = Mathf.Max(patrolPauseMin, patrolPauseMax);
        preferredRangeMax = Mathf.Max(preferredRangeMin + 1f, preferredRangeMax);
    }
}
