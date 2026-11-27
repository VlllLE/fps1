using UnityEngine;

/// <summary>
/// How a weapon looks and feels when it fires: size of the muzzle flash and its light, the casing it ejects, the
/// tracer streak, and the first-person kick spring. Chosen from <see cref="WeaponConfig.weaponType"/> so every gun
/// gets distinct feedback without touching the config assets.
/// </summary>
public struct WeaponFxProfile
{
    public float flashScale;
    public float flashLightIntensity;
    public bool ejectsCasings;
    public float casingScale;
    public Color casingColor;
    /// <summary>Seconds between the shot and the casing leaving the gun (a bolt-action ejects when the bolt is worked).</summary>
    public float casingDelay;
    public float tracerWidth;
    public float tracerLength;
    public float tracerSpeed;
    public Color tracerColor;

    /// <summary>First-person kick spring: strength multiplier on the config's kick values and recovery speed multiplier.</summary>
    public float kickStrength;
    public float kickRecovery;

    /// <summary>The bolt handle is cycled by hand after each shot (bolt-action rifles).</summary>
    public bool boltAction;
    /// <summary>Seconds between the shot and the cycle of the bolt/pump/slide.</summary>
    public float cycleDelay;
    public float cycleDuration;
    public float cycleTravel;

    public static readonly Color Brass = new Color(0.95f, 0.72f, 0.28f, 1f);
    public static readonly Color ShotgunHull = new Color(0.78f, 0.12f, 0.1f, 1f);

    private static readonly Color RifleTracer = new Color(1f, 0.88f, 0.5f, 1f);
    private static readonly Color PelletTracer = new Color(1f, 0.7f, 0.35f, 1f);

    public static WeaponFxProfile For(WeaponConfig config)
    {
        return For(config != null ? config.weaponType : WeaponType.AssaultRifle, config != null ? config.fireRate : 600f);
    }

    public static WeaponFxProfile For(WeaponType type, float fireRate)
    {
        var p = new WeaponFxProfile
        {
            flashScale = 1f,
            flashLightIntensity = 1.6f,
            ejectsCasings = true,
            casingScale = 1f,
            casingColor = Brass,
            casingDelay = 0f,
            tracerWidth = 0.035f,
            tracerLength = 3.2f,
            tracerSpeed = 280f,
            tracerColor = RifleTracer,
            kickStrength = 1f,
            kickRecovery = 1f,
            cycleDelay = 0f,
            cycleDuration = 0.07f,
            cycleTravel = 0.03f
        };

        switch (type)
        {
            case WeaponType.SMG:
                p.flashScale = 0.75f;
                p.flashLightIntensity = 1.2f;
                p.casingScale = 0.7f;
                p.tracerWidth = 0.028f;
                p.tracerLength = 2.4f;
                p.kickStrength = 0.8f;
                p.kickRecovery = 1.25f;
                p.cycleDuration = 0.05f;
                p.cycleTravel = 0.022f;
                break;
            case WeaponType.LMG:
                p.flashScale = 1.25f;
                p.flashLightIntensity = 1.9f;
                p.casingScale = 1.15f;
                p.tracerWidth = 0.042f;
                p.tracerLength = 4f;
                p.kickStrength = 1.15f;
                p.kickRecovery = 0.85f;
                break;
            case WeaponType.Shotgun:
                p.flashScale = 1.6f;
                p.flashLightIntensity = 2.6f;
                p.casingScale = 1.7f;
                p.casingColor = ShotgunHull;
                p.casingDelay = 0.2f;
                p.tracerWidth = 0.02f;
                p.tracerLength = 1.6f;
                p.tracerSpeed = 220f;
                p.tracerColor = PelletTracer;
                p.kickStrength = 1.45f;
                p.kickRecovery = 0.7f;
                p.cycleDelay = 0.12f;
                p.cycleDuration = 0.32f;
                p.cycleTravel = 0.05f;
                break;
            case WeaponType.Sniper:
                p.flashScale = 1.5f;
                p.flashLightIntensity = 2.3f;
                p.casingScale = 1.8f;
                p.tracerWidth = 0.05f;
                p.tracerLength = 6f;
                p.tracerSpeed = 380f;
                p.kickStrength = 1.45f;
                p.kickRecovery = 0.75f;
                if (fireRate <= 90f)
                {
                    p.boltAction = true;
                    p.casingDelay = 0.5f;
                    p.cycleDelay = 0.35f;
                    p.cycleDuration = 0.6f;
                    p.cycleTravel = 0.085f;
                }
                else
                {
                    p.cycleDuration = 0.08f;
                    p.cycleTravel = 0.035f;
                }

                break;
            case WeaponType.Pistol:
                p.flashScale = 0.7f;
                p.flashLightIntensity = 1.1f;
                p.casingScale = 0.7f;
                p.tracerWidth = 0.026f;
                p.tracerLength = 2f;
                p.kickStrength = 1.25f;
                p.kickRecovery = 1.3f;
                p.cycleDuration = 0.09f;
                p.cycleTravel = 0.035f;
                break;
            case WeaponType.Launcher:
                p.flashScale = 2f;
                p.flashLightIntensity = 3f;
                p.ejectsCasings = false;
                p.kickStrength = 1.6f;
                p.kickRecovery = 0.6f;
                break;
            case WeaponType.Melee:
                p.flashScale = 0f;
                p.flashLightIntensity = 0f;
                p.ejectsCasings = false;
                p.kickStrength = 0f;
                break;
        }

        return p;
    }
}
