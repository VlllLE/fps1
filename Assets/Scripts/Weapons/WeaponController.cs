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
    
    private float adsProgress;
    private float defaultFOV;
    
    private Transform cachedIgnoreRoot;
    
    private Coroutine reloadCoroutine;
    private Coroutine burstCoroutine;
    
    public event Action<int, int> OnAmmoChanged;
    public event Action<FireMode> OnFireModeChanged;
    public event Action OnWeaponFired;
    public event Action<HitResult> OnHitResolved;
    public event Action<List<HitResult>> OnShotgunHitsResolved;
    public event Action OnReloadStarted;
    public event Action OnReloadComplete;
    public event Action OnWeaponEquipped;
    public event Action OnWeaponHolstered;
    
    public WeaponConfig Config => config;
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => isReloading;
    public bool IsAiming => isAiming;
    public bool IsEquipped => isEquipped;
    public FireMode CurrentFireMode => config.availableFireModes[currentFireModeIndex];
    public float ADSProgress => adsProgress;
    public bool IsAuthoritative { get => isAuthoritative; set => isAuthoritative = value; }
    
    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
            
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        
        if (hitDetection == null)
            hitDetection = FindAnyObjectByType<HitDetection>();
            
        cachedIgnoreRoot = GetComponentInParent<CharacterController>()?.transform 
            ?? GetComponentInParent<Rigidbody>()?.transform 
            ?? transform.root;
            
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        
        if (playerCamera != null)
            defaultFOV = playerCamera.fieldOfView;
    }
    
    private void Start()
    {
        InitializeAmmo();
    }
    
    private void Update()
    {
        if (!isEquipped) return;
        
        UpdateADS();
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
    
    private void InitializeAmmo()
    {
        if (config == null) return;
        
        currentAmmo = config.magazineSize;
        reserveAmmo = config.startingReserveAmmo;
        OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
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
        
        OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
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
    
    #region Reloading
    
    public void TryReload()
    {
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
        
        if (animator != null)
            animator.SetBool("IsReloading", false);
    }
    
    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        OnReloadStarted?.Invoke();
        
        if (animator != null)
            animator.SetBool("IsReloading", true);
            
        if (config.reloadSound != null && audioSource != null)
            audioSource.PlayOneShot(config.reloadSound);
        
        float reloadTime = currentAmmo > 0 ? config.tacticalReloadTime : config.reloadTime;
        
        yield return new WaitForSeconds(config.reloadAmmoTime);
        
        int ammoNeeded = config.magazineSize - currentAmmo;
        int ammoToAdd = Mathf.Min(ammoNeeded, reserveAmmo);
        
        currentAmmo += ammoToAdd;
        reserveAmmo -= ammoToAdd;
        
        OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
        
        float remainingTime = reloadTime - config.reloadAmmoTime;
        if (remainingTime > 0)
            yield return new WaitForSeconds(remainingTime);
        
        isReloading = false;
        reloadCoroutine = null;
        
        if (animator != null)
            animator.SetBool("IsReloading", false);
            
        OnReloadComplete?.Invoke();
    }
    
    #endregion
    
    #region ADS
    
    public void StartAiming()
    {
        if (isReloading) return;
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
        
        CancelReload();
        
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
        OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
    }
    
    public void SetAmmo(int magazine, int reserve)
    {
        currentAmmo = Mathf.Clamp(magazine, 0, config.magazineSize);
        reserveAmmo = Mathf.Clamp(reserve, 0, config.maxReserveAmmo);
        OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
    }
    
    public bool HasAmmo()
    {
        return currentAmmo > 0 || reserveAmmo > 0;
    }
    
    public bool NeedsReload()
    {
        return currentAmmo < config.magazineSize && reserveAmmo > 0;
    }
    
    #endregion
    
    public void SetCanFire(bool value)
    {
        canFire = value;
    }
    
    public void ResetWeapon()
    {
        InitializeAmmo();
        currentSpread = 0f;
        currentFireModeIndex = 0;
        isReloading = false;
        isAiming = false;
        isFiring = false;
        adsProgress = 0f;
        
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
