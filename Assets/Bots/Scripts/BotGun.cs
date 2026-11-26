using System;
using UnityEngine;

/// <summary>
/// Ammo, fire rate, burst discipline, spread bloom and reload of a bot weapon, driven by a real
/// <see cref="WeaponConfig"/>. Time is passed in so the logic is deterministic and testable; bots have infinite reserve ammo.
/// </summary>
public sealed class BotGun
{
    private readonly WeaponConfig config;
    private readonly BotSettings settings;
    private readonly System.Random rng;

    private float nextShotTime;
    private float burstPauseUntil;
    private float reloadEndTime;
    private float reloadStartTime;
    private int burstRemaining;
    private float bloom;

    public BotGun(WeaponConfig config, BotSettings settings, System.Random rng)
    {
        this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
        this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        this.rng = rng ?? new System.Random();
        Ammo = MagazineSize;
    }

    public WeaponConfig Config => config;
    public int Ammo { get; private set; }
    public int MagazineSize => Mathf.Max(1, config.magazineSize);
    public bool IsReloading { get; private set; }
    public bool IsEmpty => Ammo <= 0;
    public float Bloom => bloom;

    public FireMode FireMode
    {
        get
        {
            FireMode[] modes = config.availableFireModes;
            return modes != null && modes.Length > 0 ? modes[0] : FireMode.Auto;
        }
    }

    /// <summary>Spread cone (degrees) for the next shot: weapon hip spread plus accumulated bloom, scaled by difficulty.</summary>
    public float CurrentSpreadDegrees => (config.baseSpread + bloom) * settings.weaponSpreadScale;

    /// <summary>Wants a reload before engaging again (empty, or low while nobody is shooting at the bot).</summary>
    public bool NeedsReload => !IsReloading && Ammo < MagazineSize && (Ammo <= 0 || Ammo <= MagazineSize * 0.25f);

    public static float ReloadDuration(WeaponConfig config, int ammo)
    {
        if (config.perShellReload)
        {
            int missing = Mathf.Max(0, Mathf.Max(1, config.magazineSize) - ammo);
            return Mathf.Max(0.1f, config.reloadAmmoTime + missing * config.shellReloadTime + config.shellReloadFinishTime);
        }

        float time = ammo > 0 ? config.tacticalReloadTime : config.reloadTime;
        return Mathf.Max(0.1f, time);
    }

    public bool TryStartReload(float now)
    {
        if (IsReloading || Ammo >= MagazineSize)
            return false;

        IsReloading = true;
        reloadStartTime = now;
        reloadEndTime = now + ReloadDuration(config, Ammo);
        burstRemaining = 0;
        return true;
    }

    /// <summary>0..1 progress of the running reload (0 when not reloading).</summary>
    public float ReloadProgress01(float now)
    {
        if (!IsReloading)
            return 0f;
        float length = reloadEndTime - reloadStartTime;
        return length > 1e-3f ? Mathf.Clamp01((now - reloadStartTime) / length) : 1f;
    }

    /// <summary>Advances reload completion and spread recovery.</summary>
    public void Tick(float now, float dt)
    {
        if (IsReloading && now >= reloadEndTime)
        {
            IsReloading = false;
            Ammo = MagazineSize;
            bloom = 0f;
        }

        if (bloom > 0f)
            bloom = Mathf.Max(0f, bloom - config.spreadRecoverySpeed * Mathf.Max(0f, dt));
    }

    /// <summary>Gives the gun a full magazine without a reload (respawn).</summary>
    public void Refill()
    {
        IsReloading = false;
        Ammo = MagazineSize;
        bloom = 0f;
        burstRemaining = 0;
        burstPauseUntil = 0f;
    }

    public bool CanFire(float now)
    {
        return !IsReloading && Ammo > 0 && now >= nextShotTime && now >= burstPauseUntil;
    }

    /// <summary>
    /// Fires one round if the fire rate, burst pause, ammo and reload state allow it. The caller resolves the shot with
    /// <see cref="CurrentSpreadDegrees"/> read before this call. Starts a reload automatically on an empty magazine.
    /// </summary>
    public bool TryFire(float now)
    {
        if (IsReloading)
            return false;
        if (Ammo <= 0)
        {
            TryStartReload(now);
            return false;
        }

        if (!CanFire(now))
            return false;

        if (burstRemaining <= 0)
            burstRemaining = RollBurstLength();

        Ammo = Mathf.Max(0, Ammo - Mathf.Max(1, config.ammoPerShot));
        burstRemaining--;
        nextShotTime = now + config.FireInterval;
        bloom = Mathf.Min(config.maxSpread, bloom + config.spreadPerShot);

        if (burstRemaining <= 0)
            burstPauseUntil = now + RollPause();

        if (Ammo <= 0)
            TryStartReload(now + config.FireInterval);

        return true;
    }

    private int RollBurstLength()
    {
        switch (FireMode)
        {
            case FireMode.Semi:
                return 1;
            case FireMode.Burst:
                return Mathf.Max(1, config.burstCount);
            default:
                int min = Mathf.Max(1, settings.burstMin);
                int max = Mathf.Max(min, settings.burstMax);
                return rng.Next(min, max + 1);
        }
    }

    private float RollPause()
    {
        if (FireMode == FireMode.Semi)
            return settings.semiDelay * (0.7f + 0.6f * (float)rng.NextDouble());

        float min = Mathf.Max(0f, settings.burstPauseMin);
        float max = Mathf.Max(min, settings.burstPauseMax);
        float pause = min + (max - min) * (float)rng.NextDouble();
        if (FireMode == FireMode.Burst)
            pause += config.burstDelay;
        return pause;
    }

    /// <summary>Builds the hitscan request for one pellet, mirroring what the player's <see cref="WeaponController"/> sends.</summary>
    public static HitRequest CreateHitRequest(
        WeaponConfig config,
        Vector3 origin,
        Vector3 direction,
        long shotId,
        int sourceId,
        Transform ignoreRoot,
        float damageMultiplier)
    {
        return new HitRequest
        {
            ShotId = shotId,
            SourceId = sourceId,
            Origin = origin,
            Direction = direction,
            MaxDistance = Mathf.Max(1f, config.range),
            BaseDamage = config.damage * Mathf.Max(0f, damageMultiplier),
            HitLayers = ~0,
            IgnoreRoot = ignoreRoot,
            IsAuthoritative = true,
            InvokeSurfaceResponses = true,
            HeadMultiplier = config.headshotMultiplier,
            BodyMultiplier = 1f,
            LimbMultiplier = config.limbMultiplier,
            DamageFalloff = config.damageFalloff,
            AllowPenetration = config.canPenetrate,
            MaxPenetrations = config.canPenetrate ? 3 : 0,
            PenetrationDamageRetention = config.penetrationDamageRetention,
            MinimumPenetrationDamage = 1f
        };
    }

    /// <summary>Rifle used when no loadout catalog is available. The caller owns (and should destroy) the instance.</summary>
    public static WeaponConfig CreateFallbackConfig()
    {
        var fallback = ScriptableObject.CreateInstance<WeaponConfig>();
        fallback.name = "BotFallbackRifle";
        fallback.weaponName = "Bot Rifle";
        fallback.damage = 26f;
        fallback.fireRate = 650f;
        fallback.availableFireModes = new[] { FireMode.Auto };
        fallback.magazineSize = 30;
        fallback.reloadTime = 2.2f;
        fallback.tacticalReloadTime = 1.8f;
        fallback.range = 100f;
        fallback.baseSpread = 1.2f;
        fallback.spreadPerShot = 0.25f;
        fallback.maxSpread = 4f;
        fallback.spreadRecoverySpeed = 5f;
        return fallback;
    }
}
