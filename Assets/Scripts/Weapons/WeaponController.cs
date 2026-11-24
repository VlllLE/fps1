using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main weapon controller handling shooting, reloading, ADS, and weapon feel.
/// Attach to each weapon prefab.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private WeaponConfig config;
    
    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform adsPosition;
    [SerializeField] private Transform hipPosition;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private RecoilSystem recoilSystem;
    [SerializeField] private HitDetection hitDetection;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Muzzle & Effects")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Transform shellEjectPoint;
    
    [Header("Sway Settings Override")]
    [SerializeField] private bool overrideSwaySettings = false;
    [SerializeField] private float customSwayAmount = 0.02f;
    [SerializeField] private float customMaxSway = 0.06f;
    
    [Header("Authority")]
    [Tooltip("Set true for local player or server-authoritative shots")]
    [SerializeField] private bool isAuthoritative = true;
    
    private int currentAmmo;
    private int reserveAmmo;
    private float lastFireTime;
    private float currentSpread;
    private int burstShotsRemaining;
    private int currentFireModeIndex;
    private long shotIdCounter;
    
    private bool isReloading;
    private bool isAiming;
    private bool isFiring;
    private bool isEquipped;
    private bool canFire = true;
    
    private Vector3 currentSwayPosition;
    private Vector3 swayVelocity;
    private float bobTimer;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private float reloadProgress;
    private Transform magazineTransform;
    private Vector3 magazineInitialLocalPos;
    private Quaternion magazineInitialLocalRot;
    private Vector3 magazineInitialLocalScale;
    private Transform boltTransform;
    private Vector3 boltInitialLocalPos;
    private Quaternion boltInitialLocalRot;

    private static AudioClip proceduralMagOutClip;
    private static AudioClip proceduralMagInClip;
    private static AudioClip proceduralBoltRackClip;
    
    private float adsProgress;
    private float defaultFOV;
    
    private Transform cachedIgnoreRoot;
    
    private Coroutine reloadCoroutine;
    private Coroutine burstCoroutine;
    private Coroutine meleeCoroutine;
    
    public event Action<int, int> OnAmmoChanged;
    public event Action<FireMode> OnFireModeChanged;
    public event Action OnWeaponFired;
    public event Action<HitResult> OnHitResolved;
    public event Action<List<HitResult>> OnShotgunHitsResolved;
    public event Action OnReloadStarted;
    public event Action OnReloadComplete;
    public event Action OnWeaponEquipped;
    public event Action OnWeaponHolstered;
    /// <summary>Raised when a melee slash starts (drives the slash animation).</summary>
    public event Action OnMeleeSwing;

    /// <summary>Value broadcast through <see cref="OnAmmoChanged"/> by weapons that do not use ammo.</summary>
    public const int NoAmmoDisplay = -1;
    
    public WeaponConfig Config => config;
    public bool IsMelee => config != null && config.IsMelee;
    public bool UsesAmmo => !IsMelee;
    public int CurrentAmmo => IsMelee ? NoAmmoDisplay : currentAmmo;
    public int ReserveAmmo => IsMelee ? NoAmmoDisplay : reserveAmmo;
    public bool IsReloading => isReloading;
    public float ReloadProgress => reloadProgress;
    public bool IsAiming => isAiming;
    public bool IsEquipped => isEquipped;
    public FireMode CurrentFireMode => config.availableFireModes[currentFireModeIndex];
    public float ADSProgress => adsProgress;
    public bool IsAuthoritative { get => isAuthoritative; set => isAuthoritative = value; }

    public void SetPlayerCamera(Camera camera)
    {
        playerCamera = camera;
        if (playerCamera != null)
            defaultFOV = playerCamera.fieldOfView;
    }
    
    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        
        if (hitDetection == null)
            hitDetection = FindAnyObjectByType<HitDetection>();
            
        cachedIgnoreRoot = GetComponentInParent<CharacterController>()?.transform 
            ?? GetComponentInParent<Rigidbody>()?.transform 
            ?? transform.root;
            
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;

        CacheReloadParts();
        
        if (playerCamera != null)
            defaultFOV = playerCamera.fieldOfView;
    }
    
    private void Start()
    {
        InitializeAmmo();
    }

    private void OnDisable()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }
        isReloading = false;
        reloadProgress = 0f;
        meleeCoroutine = null;
        ResetReloadVisuals();
    }
    
    private void Update()
    {
        if (!isEquipped) return;
        
        UpdateADS();
        ApplyReloadAnimation();
        UpdateSway();
        UpdateBob();
        UpdateSpreadRecovery();
    }
    
    public void Initialize(WeaponConfig weaponConfig)
    {
        config = weaponConfig;
        InitializeAmmo();
        
        if (recoilSystem != null)
            recoilSystem.SetWeaponConfig(config);
    }
    
    private void NotifyAmmoChanged()
    {
        if (IsMelee)
            OnAmmoChanged?.Invoke(NoAmmoDisplay, NoAmmoDisplay);
        else
            OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
    }

    private void InitializeAmmo()
    {
        if (config == null) return;
        
        currentAmmo = config.magazineSize;
        reserveAmmo = config.startingReserveAmmo;
        NotifyAmmoChanged();
    }
    
    #region Shooting
    
    public void StartFiring()
    {
        if (!canFire || isReloading || !isEquipped) return;
        
        isFiring = true;
        
        switch (CurrentFireMode)
        {
            case FireMode.Auto:
                TryFire();
                break;
            case FireMode.Semi:
                TryFire();
                break;
            case FireMode.Burst:
                StartBurst();
                break;
        }
    }
    
    public void StopFiring()
    {
        isFiring = false;
    }
    
    public void UpdateFiring()
    {
        if (!isFiring || !canFire || isReloading || !isEquipped) return;
        
        if (CurrentFireMode == FireMode.Auto)
        {
            TryFire();
        }
    }
    
    private void TryFire()
    {
        if (Time.time < lastFireTime + config.FireInterval) return;

        if (IsMelee)
        {
            MeleeAttack();
            return;
        }
        
        if (currentAmmo <= 0)
        {
            PlayEmptySound();
            if (config.canCancelReload || !isReloading)
            {
                TryReload();
            }
            return;
        }
        
        Fire();
    }
    
    private void Fire()
    {
        lastFireTime = Time.time;
        currentAmmo -= config.ammoPerShot;
        
        currentSpread = Mathf.Min(currentSpread + config.spreadPerShot, config.maxSpread);
        
        float finalSpread = isAiming ? config.adsSpread : config.baseSpread;
        finalSpread += currentSpread;
        
        Vector3 fireDirection = GetFireDirection(finalSpread);
        
        if (config.useProjectile)
        {
            FireProjectile(fireDirection);
        }
        else
        {
            FireHitscan(fireDirection);
        }
        
        recoilSystem?.ApplyRecoilShot(isAiming);
        
        PlayFireEffects();
        
        if (animator != null)
            animator.SetTrigger("Fire");
        
        NotifyAmmoChanged();
        OnWeaponFired?.Invoke();
    }
    
    private Vector3 GetFireDirection(float spread)
    {
        Vector3 direction = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        
        if (spread > 0f)
        {
            float spreadRad = spread * Mathf.Deg2Rad;
            float randomAngle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            float randomRadius = UnityEngine.Random.Range(0f, Mathf.Tan(spreadRad));
            
            Vector3 spreadOffset = new Vector3(
                Mathf.Cos(randomAngle) * randomRadius,
                Mathf.Sin(randomAngle) * randomRadius,
                1f
            );
            
            Quaternion rotation = Quaternion.LookRotation(direction);
            direction = rotation * spreadOffset.normalized;
        }
        
        return direction;
    }
    
    private void FireHitscan(Vector3 direction)
    {
        if (hitDetection == null) return;
        
        Vector3 origin = playerCamera != null ? playerCamera.transform.position : firePoint.position;
        int pellets = Mathf.Max(1, config.pelletCount);
        
        if (pellets > 1)
        {
            FireShotgunPellets(origin, direction, pellets);
        }
        else
        {
            FireSingleHitscan(origin, direction);
        }
    }
    
    private void FireSingleHitscan(Vector3 origin, Vector3 direction)
    {
        HitRequest request = CreateHitRequest(origin, direction);
        HitResult result = hitDetection.Resolve(request);
        OnHitResolved?.Invoke(result);
    }
    
    private void FireShotgunPellets(Vector3 origin, Vector3 baseDirection, int pelletCount)
    {
        List<HitResult> results = new List<HitResult>(pelletCount);
        float spreadAngle = isAiming ? config.adsSpread : config.baseSpread;
        spreadAngle += currentSpread;
        
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 pelletDirection = ApplySpreadToDirection(baseDirection, spreadAngle);
            HitRequest request = CreateHitRequest(origin, pelletDirection);
            HitResult result = hitDetection.Resolve(request);
            results.Add(result);
        }
        
        OnShotgunHitsResolved?.Invoke(results);
    }
    
    private HitRequest CreateHitRequest(Vector3 origin, Vector3 direction)
    {
        shotIdCounter++;
        
        return new HitRequest
        {
            ShotId = shotIdCounter,
            SourceId = gameObject.GetHashCode(),
            Origin = origin,
            Direction = direction,
            MaxDistance = config.range,
            BaseDamage = config.damage,
            IgnoreRoot = cachedIgnoreRoot,
            IsAuthoritative = isAuthoritative,
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
    
    private Vector3 ApplySpreadToDirection(Vector3 direction, float spreadAngle)
    {
        if (spreadAngle <= 0f) return direction;
        
        float spreadRad = spreadAngle * Mathf.Deg2Rad;
        float randomAngle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
        float randomRadius = UnityEngine.Random.Range(0f, Mathf.Tan(spreadRad));
        
        Vector3 spreadOffset = new Vector3(
            Mathf.Cos(randomAngle) * randomRadius,
            Mathf.Sin(randomAngle) * randomRadius,
            1f
        );
        
        Quaternion rotation = Quaternion.LookRotation(direction);
        return rotation * spreadOffset.normalized;
    }
    
    private void FireProjectile(Vector3 direction)
    {
        if (config.projectilePrefab == null) return;
        
        shotIdCounter++;
        Vector3 spawnPoint = firePoint != null ? firePoint.position : muzzlePoint.position;
        GameObject projectile = Instantiate(config.projectilePrefab, spawnPoint, Quaternion.LookRotation(direction));
        
        Projectile proj = projectile.GetComponent<Projectile>();
        if (proj != null)
        {
            proj.Initialize(config, direction, gameObject.GetHashCode(), cachedIgnoreRoot, isAuthoritative, shotIdCounter);
            proj.OnProjectileHit += OnHitResolved;
        }
        else
        {
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = direction * config.projectileSpeed;
            }
        }
    }
    
    private void StartBurst()
    {
        if (burstCoroutine != null) return;
        burstCoroutine = StartCoroutine(BurstFireRoutine());
    }
    
    private IEnumerator BurstFireRoutine()
    {
        burstShotsRemaining = config.burstCount;
        
        while (burstShotsRemaining > 0 && currentAmmo > 0)
        {
            if (Time.time >= lastFireTime + config.FireInterval)
            {
                Fire();
                burstShotsRemaining--;
            }
            yield return null;
        }
        
        yield return new WaitForSeconds(config.burstDelay);
        burstCoroutine = null;
    }
    
    private void PlayFireEffects()
    {
        if (config.muzzleFlashPrefab != null && muzzlePoint != null)
        {
            GameObject flash = Instantiate(config.muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
            Destroy(flash, 0.1f);
        }
        
        if (config.shellEjectPrefab != null && shellEjectPoint != null)
        {
            GameObject shell = Instantiate(config.shellEjectPrefab, shellEjectPoint.position, shellEjectPoint.rotation);
            Rigidbody shellRb = shell.GetComponent<Rigidbody>();
            if (shellRb != null)
            {
                Vector3 ejectDir = shellEjectPoint.right + Vector3.up * 0.5f;
                shellRb.AddForce(ejectDir * config.shellEjectForce, ForceMode.Impulse);
                shellRb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }
            Destroy(shell, 3f);
        }
        
        if (config.fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(config.fireSound, config.fireSoundVolume);
        }
    }
    
    private void PlayEmptySound()
    {
        if (config.emptySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(config.emptySound);
        }
    }
    
    #endregion
    
    #region Melee

    private void MeleeAttack()
    {
        lastFireTime = Time.time;

        if (animator != null)
            animator.SetTrigger("Fire");

        if (config.fireSound != null && audioSource != null)
            audioSource.PlayOneShot(config.fireSound, config.fireSoundVolume);

        OnMeleeSwing?.Invoke();
        OnWeaponFired?.Invoke();

        if (meleeCoroutine != null)
            StopCoroutine(meleeCoroutine);
        meleeCoroutine = StartCoroutine(MeleeHitRoutine());
    }

    private IEnumerator MeleeHitRoutine()
    {
        float delay = Mathf.Max(0f, config.meleeHitDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        meleeCoroutine = null;
        if (!isEquipped)
            yield break;

        if (hitDetection == null)
            hitDetection = FindAnyObjectByType<HitDetection>();
        if (hitDetection == null)
            yield break;

        Transform view = playerCamera != null ? playerCamera.transform : transform;
        shotIdCounter++;
        HitResult result = MeleeAttackResolver.Resolve(
            hitDetection,
            config,
            view.position,
            view.forward,
            view.up,
            cachedIgnoreRoot,
            gameObject.GetHashCode(),
            shotIdCounter,
            isAuthoritative);

        if (result != null)
            OnHitResolved?.Invoke(result);
    }

    #endregion

    #region Reloading
    
    public void TryReload()
    {
        if (IsMelee) return;
        if (isReloading || currentAmmo >= config.magazineSize || reserveAmmo <= 0) return;
        
        if (reloadCoroutine != null)
            StopCoroutine(reloadCoroutine);
            
        reloadCoroutine = StartCoroutine(ReloadRoutine());
    }
    
    public void CancelReload()
    {
        if (!isReloading || !config.canCancelReload) return;
        
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }
        
        isReloading = false;
        reloadProgress = 0f;
        ResetReloadVisuals();
        
        if (animator != null)
            animator.SetBool("IsReloading", false);
    }
    
    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        reloadProgress = 0f;
        if (isAiming)
            StopAiming();
        OnReloadStarted?.Invoke();
        
        if (animator != null)
            animator.SetBool("IsReloading", true);
            
        bool hasCustomReloadClip = config.reloadSound != null && audioSource != null;
        if (hasCustomReloadClip)
            audioSource.PlayOneShot(config.reloadSound);
        else
            EnsureProceduralReloadClips();
        
        float reloadTime = currentAmmo > 0 ? config.tacticalReloadTime : config.reloadTime;
        reloadTime = Mathf.Max(reloadTime, 0.25f);
        float ammoInsertTime = Mathf.Clamp(config.reloadAmmoTime, 0.05f, reloadTime);

        float elapsed = 0f;
        bool ammoAdded = false;
        bool playedMagOut = false;
        bool playedMagIn = false;
        bool playedBoltRack = false;

        while (elapsed < reloadTime)
        {
            elapsed += Time.deltaTime;
            reloadProgress = Mathf.Clamp01(elapsed / reloadTime);

            if (!hasCustomReloadClip && audioSource != null)
            {
                if (!playedMagOut && reloadProgress >= 0.20f)
                {
                    playedMagOut = true;
                    audioSource.PlayOneShot(proceduralMagOutClip, 0.55f);
                }
                if (!playedMagIn && elapsed >= ammoInsertTime)
                {
                    playedMagIn = true;
                    audioSource.PlayOneShot(proceduralMagInClip, 0.65f);
                }
                if (!playedBoltRack && reloadProgress >= 0.78f)
                {
                    playedBoltRack = true;
                    audioSource.PlayOneShot(proceduralBoltRackClip, 0.55f);
                }
            }

            if (!ammoAdded && elapsed >= ammoInsertTime)
            {
                ammoAdded = true;
                int ammoNeeded = config.magazineSize - currentAmmo;
                int ammoToAdd = Mathf.Min(ammoNeeded, reserveAmmo);

                currentAmmo += ammoToAdd;
                reserveAmmo -= ammoToAdd;

                NotifyAmmoChanged();
            }

            yield return null;
        }

        if (!ammoAdded)
        {
            int ammoNeeded = config.magazineSize - currentAmmo;
            int ammoToAdd = Mathf.Min(ammoNeeded, reserveAmmo);
            currentAmmo += ammoToAdd;
            reserveAmmo -= ammoToAdd;
            NotifyAmmoChanged();
        }
        
        isReloading = false;
        reloadProgress = 0f;
        reloadCoroutine = null;
        ResetReloadVisuals();
        
        if (animator != null)
            animator.SetBool("IsReloading", false);
            
        OnReloadComplete?.Invoke();
    }

    private void CacheReloadParts()
    {
        magazineTransform = FindDescendantByName(transform, "Magazine");
        if (magazineTransform != null)
        {
            magazineInitialLocalPos = magazineTransform.localPosition;
            magazineInitialLocalRot = magazineTransform.localRotation;
            magazineInitialLocalScale = magazineTransform.localScale;
        }

        boltTransform = FindDescendantByName(transform, "Bolt");
        if (boltTransform != null)
        {
            boltInitialLocalPos = boltTransform.localPosition;
            boltInitialLocalRot = boltTransform.localRotation;
        }
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        foreach (Transform child in root)
        {
            if (child.name == targetName)
                return child;
            Transform nested = FindDescendantByName(child, targetName);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private void ResetReloadVisuals()
    {
        if (magazineTransform != null)
        {
            magazineTransform.localPosition = magazineInitialLocalPos;
            magazineTransform.localRotation = magazineInitialLocalRot;
            magazineTransform.localScale = magazineInitialLocalScale;
        }

        if (boltTransform != null)
        {
            boltTransform.localPosition = boltInitialLocalPos;
            boltTransform.localRotation = boltInitialLocalRot;
        }
    }

    /// <summary>
    /// Procedural first-person reload motion (tilt, magazine drop/insert, bolt rack).
    /// Runs whenever no Animator overrides the weapon so every gun has clear reload feedback.
    /// </summary>
    private void ApplyReloadAnimation()
    {
        if (!isReloading)
            return;

        float t = reloadProgress;

        // Smooth raise into reload pose (0..0.18) and return to ready (0.82..1.0).
        float enter = SmoothStep01(t / 0.18f);
        float exit = 1f - SmoothStep01((t - 0.82f) / 0.18f);
        float poseWeight = enter * exit;

        // Subtle weapon reactions on mag out (~0.24), mag slap (~0.66), and rack (~0.82).
        float magOutDip = Pulse01(t, 0.24f, 0.08f);
        float magInKick = Pulse01(t, 0.66f, 0.09f);
        float rackPull = Pulse01(t, 0.82f, 0.09f);

        Vector3 posOffset = new Vector3(
            -0.065f * poseWeight,
            -0.055f * poseWeight - 0.028f * magOutDip + 0.035f * magInKick,
            -0.04f * poseWeight - 0.03f * rackPull);

        Vector3 rotOffset = new Vector3(
            -14f * poseWeight + 6f * magOutDip - 9f * magInKick + 4f * rackPull,
            15f * poseWeight,
            34f * poseWeight - 6f * rackPull);

        transform.localPosition += posOffset;
        transform.localRotation *= Quaternion.Euler(rotOffset);

        if (magazineTransform != null)
        {
            // 0.18..0.44 pull old mag out; 0.44..0.52 hidden below; 0.52..0.70 insert fresh mag.
            float magDepth = 0f;
            float magScale = 1f;
            if (t >= 0.18f && t < 0.44f)
            {
                float u = SmoothStep01((t - 0.18f) / 0.26f);
                magDepth = u;
                magScale = Mathf.Lerp(1f, 0.01f, SmoothStep01((u - 0.75f) / 0.25f));
            }
            else if (t >= 0.44f && t < 0.52f)
            {
                magDepth = 1f;
                magScale = 0.01f;
            }
            else if (t >= 0.52f && t <= 0.72f)
            {
                float u = SmoothStep01((t - 0.52f) / 0.20f);
                magDepth = 1f - u;
                magScale = Mathf.Lerp(0.01f, 1f, SmoothStep01(u / 0.25f));
            }

            magazineTransform.localPosition = magazineInitialLocalPos
                + new Vector3(0f, -0.32f * magDepth, -0.02f * magDepth);
            magazineTransform.localRotation = magazineInitialLocalRot
                * Quaternion.Euler(18f * magDepth, 0f, 0f);
            magazineTransform.localScale = magazineInitialLocalScale * magScale;
        }

        if (boltTransform != null)
        {
            float boltCycle = Pulse01(t, 0.82f, 0.10f);
            boltTransform.localPosition = boltInitialLocalPos + new Vector3(0f, 0f, -0.085f * boltCycle);
            boltTransform.localRotation = boltInitialLocalRot * Quaternion.Euler(0f, 0f, -50f * boltCycle);
        }
    }

    private static float SmoothStep01(float x)
    {
        float c = Mathf.Clamp01(x);
        return c * c * (3f - 2f * c);
    }

    private static float Pulse01(float t, float center, float halfWidth)
    {
        float d = Mathf.Abs(t - center) / Mathf.Max(halfWidth, 0.001f);
        if (d >= 1f) return 0f;
        float v = 1f - d;
        return v * v * (3f - 2f * v);
    }

    private static void EnsureProceduralReloadClips()
    {
        if (proceduralMagOutClip == null)
            proceduralMagOutClip = BuildClickClip("ReloadMagOut", 0.09f, 520f, 260f, 0.35f);
        if (proceduralMagInClip == null)
            proceduralMagInClip = BuildClickClip("ReloadMagIn", 0.11f, 220f, 780f, 0.55f);
        if (proceduralBoltRackClip == null)
            proceduralBoltRackClip = BuildClickClip("ReloadBoltRack", 0.14f, 640f, 1150f, 0.45f);
    }

    private static AudioClip BuildClickClip(string clipName, float duration, float freqA, float freqB, float noiseMix)
    {
        const int sampleRate = 22050;
        int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] data = new float[samples];
        uint seed = (uint)(clipName.GetHashCode() ^ 0x51a7c3);

        for (int i = 0; i < samples; i++)
        {
            float u = (float)i / samples;
            float env = Mathf.Pow(1f - u, 2.2f);
            float freq = Mathf.Lerp(freqA, freqB, u);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)sampleRate));

            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed & 0xffff) / 32768f) - 1f;

            data[i] = (tone * (1f - noiseMix) + noise * noiseMix) * env * 0.45f;
        }

        var clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
    
    #endregion
    
    #region ADS
    
    public void StartAiming()
    {
        if (isReloading || IsMelee) return;
        isAiming = true;
        
        if (animator != null)
            animator.SetBool("IsAiming", true);
    }
    
    public void StopAiming()
    {
        isAiming = false;
        
        if (animator != null)
            animator.SetBool("IsAiming", false);
    }
    
    private void UpdateADS()
    {
        float targetProgress = isAiming ? 1f : 0f;
        adsProgress = Mathf.MoveTowards(adsProgress, targetProgress, Time.deltaTime / config.adsSpeed);
        
        if (adsPosition != null && hipPosition != null)
        {
            transform.localPosition = Vector3.Lerp(hipPosition.localPosition, adsPosition.localPosition, adsProgress);
            transform.localRotation = Quaternion.Slerp(hipPosition.localRotation, adsPosition.localRotation, adsProgress);
        }
        else
        {
            // Reset to base pose each frame so sway, bob, and reload offsets compose without accumulating drift.
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
        }
        
        if (playerCamera != null)
        {
            playerCamera.fieldOfView = Mathf.Lerp(defaultFOV, config.adsFOV, adsProgress);
        }
    }
    
    #endregion
    
    #region Sway & Bob
    
    private void UpdateSway()
    {
        float swayAmount = overrideSwaySettings ? customSwayAmount : config.swayAmount;
        float maxSway = overrideSwaySettings ? customMaxSway : config.maxSwayAmount;
        
        float swayReduction = isAiming ? config.adsSwayReduction : 0f;
        swayAmount *= (1f - swayReduction);
        maxSway *= (1f - swayReduction);
        
        float mouseX = Input.GetAxis("Mouse X") * swayAmount;
        float mouseY = Input.GetAxis("Mouse Y") * swayAmount;
        
        Vector3 targetSway = new Vector3(
            Mathf.Clamp(-mouseY, -maxSway, maxSway),
            Mathf.Clamp(-mouseX, -maxSway, maxSway),
            0f
        );
        
        currentSwayPosition = Vector3.SmoothDamp(currentSwayPosition, targetSway, ref swayVelocity, 1f / config.swaySmooth);
        
        transform.localRotation *= Quaternion.Euler(currentSwayPosition);
    }
    
    private void UpdateBob()
    {
        if (isAiming) return;
        
        float speed = GetPlayerSpeed();
        if (speed < 0.1f)
        {
            bobTimer = 0f;
            return;
        }
        
        float bobMultiplier = IsPlayerSprinting() ? config.sprintBobMultiplier : 1f;
        
        bobTimer += Time.deltaTime * config.bobSpeed * (speed / 5f);
        
        float bobX = Mathf.Sin(bobTimer) * config.bobAmount * bobMultiplier;
        float bobY = Mathf.Sin(bobTimer * 2f) * config.bobAmount * 0.5f * bobMultiplier;
        
        Vector3 bobOffset = new Vector3(bobX, bobY, 0f);
        transform.localPosition += bobOffset;
    }
    
    private float GetPlayerSpeed()
    {
        CharacterController cc = GetComponentInParent<CharacterController>();
        if (cc != null)
        {
            Vector3 horizontalVelocity = new Vector3(cc.velocity.x, 0, cc.velocity.z);
            return horizontalVelocity.magnitude;
        }
        
        Rigidbody rb = GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            return horizontalVelocity.magnitude;
        }
        
        return 0f;
    }
    
    private bool IsPlayerSprinting()
    {
        return Input.GetKey(KeyCode.LeftShift);
    }
    
    #endregion
    
    #region Spread
    
    private void UpdateSpreadRecovery()
    {
        if (currentSpread > 0f)
        {
            currentSpread = Mathf.Max(0f, currentSpread - config.spreadRecoverySpeed * Time.deltaTime);
        }
    }
    
    public float GetCurrentSpread()
    {
        float baseSpread = isAiming ? config.adsSpread : config.baseSpread;
        return baseSpread + currentSpread;
    }
    
    #endregion
    
    #region Fire Mode
    
    public void CycleFireMode()
    {
        if (config.availableFireModes.Length <= 1) return;
        
        currentFireModeIndex = (currentFireModeIndex + 1) % config.availableFireModes.Length;
        OnFireModeChanged?.Invoke(CurrentFireMode);
    }
    
    public void SetFireMode(FireMode mode)
    {
        for (int i = 0; i < config.availableFireModes.Length; i++)
        {
            if (config.availableFireModes[i] == mode)
            {
                currentFireModeIndex = i;
                OnFireModeChanged?.Invoke(CurrentFireMode);
                return;
            }
        }
    }
    
    #endregion
    
    #region Equip/Holster
    
    public void Equip()
    {
        isEquipped = true;
        gameObject.SetActive(true);
        
        if (animator != null)
            animator.SetTrigger("Equip");
            
        if (config.equipSound != null && audioSource != null)
            audioSource.PlayOneShot(config.equipSound);
            
        if (recoilSystem != null)
            recoilSystem.SetWeaponConfig(config);
        
        OnWeaponEquipped?.Invoke();
    }
    
    public void Holster()
    {
        isEquipped = false;
        isAiming = false;
        isFiring = false;
        
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }
        isReloading = false;
        reloadProgress = 0f;
        if (meleeCoroutine != null)
        {
            StopCoroutine(meleeCoroutine);
            meleeCoroutine = null;
        }
        ResetReloadVisuals();
        if (animator != null)
            animator.SetBool("IsReloading", false);
        
        if (animator != null)
            animator.SetTrigger("Holster");
            
        OnWeaponHolstered?.Invoke();
    }
    
    public void SetEquippedState(bool equipped)
    {
        if (equipped)
            Equip();
        else
            Holster();
    }
    
    #endregion
    
    #region Ammo Management
    
    public void AddAmmo(int amount)
    {
        reserveAmmo = Mathf.Min(reserveAmmo + amount, config.maxReserveAmmo);
        NotifyAmmoChanged();
    }
    
    public void SetAmmo(int magazine, int reserve)
    {
        currentAmmo = Mathf.Clamp(magazine, 0, config.magazineSize);
        reserveAmmo = Mathf.Clamp(reserve, 0, config.maxReserveAmmo);
        NotifyAmmoChanged();
    }
    
    public bool HasAmmo()
    {
        return IsMelee || currentAmmo > 0 || reserveAmmo > 0;
    }
    
    public bool NeedsReload()
    {
        return !IsMelee && currentAmmo < config.magazineSize && reserveAmmo > 0;
    }
    
    #endregion
    
    public void SetCanFire(bool value)
    {
        canFire = value;
    }
    
    public void ResetWeapon()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }
        InitializeAmmo();
        currentSpread = 0f;
        currentFireModeIndex = 0;
        isReloading = false;
        reloadProgress = 0f;
        isAiming = false;
        isFiring = false;
        adsProgress = 0f;
        ResetReloadVisuals();
        
        if (recoilSystem != null)
            recoilSystem.ResetRecoil();
    }
}

public class Projectile : MonoBehaviour
{
    [SerializeField] private HitDetection hitDetection;
    
    private WeaponConfig config;
    private Vector3 direction;
    private int sourceId;
    private Transform ignoreRoot;
    private bool isAuthoritative;
    private float spawnTime;
    private float traveledDistance;
    private long shotId;
    
    public event Action<HitResult> OnProjectileHit;
    
    public void Initialize(WeaponConfig weaponConfig, Vector3 fireDirection, int shooterId, 
        Transform ownerRoot = null, bool authoritative = true, long projectileShotId = 0)
    {
        config = weaponConfig;
        direction = fireDirection.normalized;
        sourceId = shooterId;
        ignoreRoot = ownerRoot;
        isAuthoritative = authoritative;
        shotId = projectileShotId > 0 ? projectileShotId : System.DateTime.UtcNow.Ticks;
        spawnTime = Time.time;
        
        if (hitDetection == null)
            hitDetection = FindAnyObjectByType<HitDetection>();
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = direction * config.projectileSpeed;
            rb.useGravity = config.projectileGravity > 0f;
            if (config.projectileGravity > 0f && config.projectileGravity != 1f)
            {
                rb.linearVelocity += Physics.gravity * (config.projectileGravity - 1f) * Time.fixedDeltaTime;
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (ignoreRoot != null && collision.transform.IsChildOf(ignoreRoot)) return;
        if (collision.gameObject.GetHashCode() == sourceId) return;
        
        ContactPoint contact = collision.GetContact(0);
        
        if (hitDetection != null)
        {
            HitRequest request = new HitRequest
            {
                ShotId = shotId,
                SourceId = sourceId,
                Origin = contact.point - direction * 0.1f,
                Direction = direction,
                MaxDistance = 1f,
                BaseDamage = config.damage,
                IgnoreRoot = ignoreRoot,
                IsAuthoritative = isAuthoritative,
                InvokeSurfaceResponses = true,
                HeadMultiplier = config.headshotMultiplier,
                BodyMultiplier = 1f,
                LimbMultiplier = config.limbMultiplier,
                DamageFalloff = config.damageFalloff,
                AllowPenetration = config.canPenetrate,
                MaxPenetrations = config.canPenetrate ? 1 : 0,
                PenetrationDamageRetention = config.penetrationDamageRetention,
                MinimumPenetrationDamage = 1f
            };
            
            HitResult result = hitDetection.Resolve(request);
            OnProjectileHit?.Invoke(result);
        }
        
        Destroy(gameObject);
    }
    
    private void Update()
    {
        traveledDistance += Time.deltaTime * config.projectileSpeed;
        if (traveledDistance > config.range)
        {
            Destroy(gameObject);
        }
    }
}
