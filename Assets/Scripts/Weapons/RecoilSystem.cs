using UnityEngine;

/// <summary>
/// Handles procedural recoil with pattern-based control, camera kick, and recovery.
/// Attach to the player or camera rig.
/// </summary>
public class RecoilSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform to apply camera recoil rotation")]
    [SerializeField] private Transform cameraRecoilTransform;
    [Tooltip("Transform to apply gun visual recoil")]
    [SerializeField] private Transform gunRecoilTransform;
    [Tooltip("Player camera for input")]
    [SerializeField] private Transform playerCamera;
    
    [Header("Settings")]
    [Tooltip("Apply recoil to actual camera rotation (affects aim)")]
    [SerializeField] private bool applyToCameraRotation = true;
    [Tooltip("Snap back to original position or stay at recoiled position")]
    [SerializeField] private bool useRecovery = true;
    
    private Vector3 currentCameraRecoil;
    private Vector3 targetCameraRecoil;
    private Vector3 currentGunRecoil;
    private Vector3 targetGunRecoil;
    private Vector3 currentGunKickback;
    private Vector3 targetGunKickback;
    
    private WeaponConfig currentConfig;
    private int shotsFired;
    private float lastShotTime;
    private float timeSinceLastShot;
    private bool isRecovering;
    
    private Vector3 recoilVelocity;
    private Vector3 gunRecoilVelocity;
    private Vector3 gunKickbackVelocity;
    
    private float accumulatedVerticalRecoil;
    private float accumulatedHorizontalRecoil;
    
    public Vector3 CurrentRecoil => currentCameraRecoil;
    public bool IsRecovering => isRecovering;
    
    private void Awake()
    {
        if (cameraRecoilTransform == null)
            cameraRecoilTransform = transform;
            
        if (playerCamera == null)
            playerCamera = Camera.main?.transform;
    }
    
    private void Update()
    {
        UpdateRecoilRecovery();
        ApplyRecoil();
    }
    
    public void SetWeaponConfig(WeaponConfig config)
    {
        currentConfig = config;
        ResetRecoil();
    }
    
    public void ApplyRecoilShot(bool isADS)
    {
        if (currentConfig == null) return;
        
        timeSinceLastShot = Time.time - lastShotTime;
        lastShotTime = Time.time;
        isRecovering = false;
        
        if (timeSinceLastShot > 0.3f)
        {
            shotsFired = 0;
            accumulatedVerticalRecoil = 0f;
            accumulatedHorizontalRecoil = 0f;
        }
        
        shotsFired++;
        
        float recoilMultiplier = isADS ? currentConfig.adsRecoilMultiplier : 1f;
        float normalizedShot = Mathf.Clamp01((float)shotsFired / currentConfig.magazineSize);
        
        float verticalKick = CalculateVerticalRecoil(normalizedShot) * recoilMultiplier;
        float horizontalKick = CalculateHorizontalRecoil(normalizedShot) * recoilMultiplier;
        
        accumulatedVerticalRecoil += verticalKick;
        accumulatedHorizontalRecoil += horizontalKick;
        
        targetCameraRecoil.x -= verticalKick;
        targetCameraRecoil.y += horizontalKick;
        
        ApplyGunVisualRecoil(recoilMultiplier);
    }
    
    private float CalculateVerticalRecoil(float normalizedShot)
    {
        float curveValue = currentConfig.verticalRecoilCurve.Evaluate(normalizedShot);
        float baseRecoil = currentConfig.verticalRecoil * curveValue;
        float randomness = Random.Range(-currentConfig.verticalRandomness, currentConfig.verticalRandomness);
        
        return baseRecoil * (1f + randomness);
    }
    
    private float CalculateHorizontalRecoil(float normalizedShot)
    {
        float patternValue = currentConfig.horizontalRecoilPattern.Evaluate(normalizedShot);
        float baseRecoil = currentConfig.horizontalRecoil * patternValue;
        float randomness = Random.Range(-currentConfig.horizontalRandomness, currentConfig.horizontalRandomness);
        
        return baseRecoil + (currentConfig.horizontalRecoil * randomness);
    }
    
    private void ApplyGunVisualRecoil(float multiplier)
    {
        if (gunRecoilTransform == null || currentConfig == null) return;
        
        Vector3 rotationKick = currentConfig.gunRotationKick * multiplier;
        rotationKick.x += Random.Range(-0.5f, 0.5f);
        rotationKick.y += Random.Range(-0.5f, 0.5f);
        rotationKick.z += Random.Range(-0.5f, 0.5f);
        
        targetGunRecoil += rotationKick;
        targetGunKickback.z -= currentConfig.gunKickback * multiplier;
    }
    
    private void UpdateRecoilRecovery()
    {
        if (currentConfig == null) return;
        
        float timeSinceShot = Time.time - lastShotTime;
        
        if (useRecovery && timeSinceShot > currentConfig.recoilRecoveryDelay)
        {
            isRecovering = true;
            
            float recoverySpeed = currentConfig.recoilRecoverySpeed * Time.deltaTime;
            
            targetCameraRecoil = Vector3.Lerp(targetCameraRecoil, Vector3.zero, recoverySpeed);
            
            if (timeSinceShot > 0.5f)
            {
                shotsFired = Mathf.Max(0, shotsFired - Mathf.RoundToInt(Time.deltaTime * 10f));
            }
        }
        
        float gunRecoverySpeed = currentConfig.gunRecoilRecoverySpeed * Time.deltaTime;
        targetGunRecoil = Vector3.Lerp(targetGunRecoil, Vector3.zero, gunRecoverySpeed);
        targetGunKickback = Vector3.Lerp(targetGunKickback, Vector3.zero, gunRecoverySpeed);
    }
    
    private void ApplyRecoil()
    {
        currentCameraRecoil = Vector3.SmoothDamp(
            currentCameraRecoil, 
            targetCameraRecoil, 
            ref recoilVelocity, 
            0.05f
        );
        
        if (cameraRecoilTransform != null && applyToCameraRotation)
        {
            cameraRecoilTransform.localRotation = Quaternion.Euler(currentCameraRecoil);
        }
        
        if (gunRecoilTransform != null)
        {
            currentGunRecoil = Vector3.SmoothDamp(
                currentGunRecoil, 
                targetGunRecoil, 
                ref gunRecoilVelocity, 
                0.03f
            );
            
            currentGunKickback = Vector3.SmoothDamp(
                currentGunKickback, 
                targetGunKickback, 
                ref gunKickbackVelocity, 
                0.05f
            );
            
            gunRecoilTransform.localRotation = Quaternion.Euler(currentGunRecoil);
            gunRecoilTransform.localPosition = currentGunKickback;
        }
    }
    
    public void ResetRecoil()
    {
        currentCameraRecoil = Vector3.zero;
        targetCameraRecoil = Vector3.zero;
        currentGunRecoil = Vector3.zero;
        targetGunRecoil = Vector3.zero;
        currentGunKickback = Vector3.zero;
        targetGunKickback = Vector3.zero;
        shotsFired = 0;
        accumulatedVerticalRecoil = 0f;
        accumulatedHorizontalRecoil = 0f;
    }
    
    public Vector2 GetRecoilOffset()
    {
        return new Vector2(currentCameraRecoil.y, -currentCameraRecoil.x);
    }
    
    public void AddExternalRecoil(Vector3 recoil)
    {
        targetCameraRecoil += recoil;
    }
    
    public void SetRecoveryEnabled(bool enabled)
    {
        useRecovery = enabled;
    }
    
    public int GetShotsFired()
    {
        return shotsFired;
    }
    
    public float GetRecoilProgress()
    {
        if (currentConfig == null || currentConfig.magazineSize == 0) return 0f;
        return Mathf.Clamp01((float)shotsFired / currentConfig.magazineSize);
    }
}
