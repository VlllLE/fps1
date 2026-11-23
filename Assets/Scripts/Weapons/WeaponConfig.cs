using UnityEngine;

/// <summary>
/// ScriptableObject defining all weapon statistics and behavior parameters.
/// Create via Assets > Create > Weapons > Weapon Config
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponConfig", menuName = "Weapons/Weapon Config")]
public class WeaponConfig : ScriptableObject
{
    [Header("General")]
    public string weaponName = "Weapon";
    public WeaponType weaponType = WeaponType.AssaultRifle;
    public GameObject weaponPrefab;
    public Sprite weaponIcon;
    
    [Header("Damage")]
    [Tooltip("Base damage per hit")]
    public float damage = 30f;
    [Tooltip("Headshot damage multiplier")]
    public float headshotMultiplier = 1.5f;
    [Tooltip("Limb damage multiplier")]
    public float limbMultiplier = 0.8f;
    [Tooltip("Damage falloff over distance")]
    public AnimationCurve damageFalloff = AnimationCurve.Linear(0f, 1f, 100f, 0.5f);
    
    [Header("Fire Rate")]
    [Tooltip("Rounds per minute")]
    public float fireRate = 600f;
    [Tooltip("Available fire modes")]
    public FireMode[] availableFireModes = { FireMode.Auto };
    [Tooltip("Rounds per burst (for burst mode)")]
    public int burstCount = 3;
    [Tooltip("Delay between bursts")]
    public float burstDelay = 0.1f;
    
    [Header("Accuracy")]
    [Tooltip("Maximum effective range")]
    public float range = 100f;
    [Tooltip("Base spread in degrees (hip fire)")]
    public float baseSpread = 2f;
    [Tooltip("Spread while ADS")]
    public float adsSpread = 0.5f;
    [Tooltip("Spread increase per shot")]
    public float spreadPerShot = 0.3f;
    [Tooltip("Max spread from continuous fire")]
    public float maxSpread = 6f;
    [Tooltip("Spread recovery speed")]
    public float spreadRecoverySpeed = 5f;
    
    [Header("Ammo")]
    [Tooltip("Magazine capacity")]
    public int magazineSize = 30;
    [Tooltip("Max reserve ammo")]
    public int maxReserveAmmo = 120;
    [Tooltip("Starting reserve ammo")]
    public int startingReserveAmmo = 90;
    [Tooltip("Ammo consumed per shot")]
    public int ammoPerShot = 1;
    
    [Header("Reload")]
    [Tooltip("Full reload time in seconds")]
    public float reloadTime = 2.2f;
    [Tooltip("Tactical reload time (rounds remaining)")]
    public float tacticalReloadTime = 1.8f;
    [Tooltip("Time before ammo is actually added during reload")]
    public float reloadAmmoTime = 1.5f;
    [Tooltip("Can interrupt reload to fire")]
    public bool canCancelReload = true;
    
    [Header("ADS (Aim Down Sights)")]
    [Tooltip("Time to fully ADS")]
    public float adsSpeed = 0.2f;
    [Tooltip("FOV while aiming")]
    public float adsFOV = 50f;
    [Tooltip("Movement speed multiplier while ADS")]
    public float adsMovementMultiplier = 0.7f;
    [Tooltip("Sway reduction while ADS (0-1)")]
    [Range(0f, 1f)]
    public float adsSwayReduction = 0.7f;
    
    [Header("Recoil - Vertical")]
    [Tooltip("Vertical recoil curve over magazine")]
    public AnimationCurve verticalRecoilCurve = AnimationCurve.Linear(0f, 1f, 1f, 1.5f);
    [Tooltip("Base vertical recoil per shot")]
    public float verticalRecoil = 1.5f;
    [Tooltip("Vertical recoil randomness")]
    [Range(0f, 1f)]
    public float verticalRandomness = 0.1f;
    
    [Header("Recoil - Horizontal")]
    [Tooltip("Horizontal recoil pattern (positive = right, negative = left)")]
    public AnimationCurve horizontalRecoilPattern = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
    [Tooltip("Base horizontal recoil magnitude")]
    public float horizontalRecoil = 0.5f;
    [Tooltip("Horizontal recoil randomness")]
    [Range(0f, 1f)]
    public float horizontalRandomness = 0.3f;
    
    [Header("Recoil - Recovery")]
    [Tooltip("How fast recoil recovers")]
    public float recoilRecoverySpeed = 8f;
    [Tooltip("Delay before recovery starts")]
    public float recoilRecoveryDelay = 0.1f;
    [Tooltip("ADS recoil multiplier")]
    [Range(0f, 1f)]
    public float adsRecoilMultiplier = 0.65f;
    
    [Header("Visual Recoil")]
    [Tooltip("Gun kick back amount")]
    public float gunKickback = 0.05f;
    [Tooltip("Gun rotation kick")]
    public Vector3 gunRotationKick = new Vector3(-3f, 1f, 2f);
    [Tooltip("Gun visual recoil recovery speed")]
    public float gunRecoilRecoverySpeed = 15f;
    
    [Header("Weapon Sway")]
    [Tooltip("Sway amount when moving mouse")]
    public float swayAmount = 0.02f;
    [Tooltip("Max sway offset")]
    public float maxSwayAmount = 0.06f;
    [Tooltip("Sway smoothing speed")]
    public float swaySmooth = 6f;
    
    [Header("Weapon Bob")]
    [Tooltip("Bob speed while walking")]
    public float bobSpeed = 10f;
    [Tooltip("Bob amount while walking")]
    public float bobAmount = 0.03f;
    [Tooltip("Bob amount while sprinting")]
    public float sprintBobMultiplier = 1.5f;
    
    [Header("Shotgun")]
    [Tooltip("Number of pellets per shot (shotguns)")]
    public int pelletCount = 1;
    
    [Header("Projectile (Optional)")]
    [Tooltip("Use projectile instead of hitscan")]
    public bool useProjectile = false;
    [Tooltip("Projectile prefab")]
    public GameObject projectilePrefab;
    [Tooltip("Projectile speed")]
    public float projectileSpeed = 100f;
    [Tooltip("Projectile gravity multiplier")]
    public float projectileGravity = 0f;
    
    [Header("Penetration")]
    [Tooltip("Can penetrate surfaces")]
    public bool canPenetrate = false;
    [Tooltip("Max penetration depth in units")]
    public float penetrationDepth = 0.5f;
    [Tooltip("Damage retained after penetration (0-1)")]
    [Range(0f, 1f)]
    public float penetrationDamageRetention = 0.5f;
    
    [Header("Audio")]
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;
    public AudioClip equipSound;
    [Range(0f, 1f)]
    public float fireSoundVolume = 1f;
    
    [Header("Effects")]
    public GameObject muzzleFlashPrefab;
    public GameObject shellEjectPrefab;
    public float shellEjectForce = 2f;
    
    public float FireInterval => 60f / fireRate;
}

public enum WeaponType
{
    AssaultRifle,
    SMG,
    LMG,
    Shotgun,
    Sniper,
    Pistol,
    Launcher,
    Melee
}

public enum FireMode
{
    Auto,
    Semi,
    Burst
}
