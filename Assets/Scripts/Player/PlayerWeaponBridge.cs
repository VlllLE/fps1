using UnityEngine;

public class PlayerWeaponBridge : MonoBehaviour
{
    [SerializeField] private WeaponController weapon;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private LayerMask hitLayers = ~0;
    
    private bool wasFiring;
    private bool wasAiming;
    
    private void Awake()
    {
        if (inputHandler == null)
            inputHandler = GetComponent<InputHandler>();
        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;
    }
    
    private void Start()
    {
        if (weapon == null)
            weapon = GetComponentInChildren<WeaponController>();
        
        if (weapon != null)
            weapon.Equip();
    }
    
    private void Update()
    {
        if (weapon == null || cameraTransform == null) return;
        
        bool isFiring = Input.GetMouseButton(0);
        bool isAiming = Input.GetMouseButton(1);
        
        if (isFiring && !wasFiring)
        {
            weapon.StartFiring();
        }
        else if (!isFiring && wasFiring)
        {
            weapon.StopFiring();
        }
        
        if (isFiring)
        {
            weapon.UpdateFiring();
            TryHitscan();
        }
        
        if (isAiming && !wasAiming)
        {
            weapon.StartAiming();
        }
        else if (!isAiming && wasAiming)
        {
            weapon.StopAiming();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            weapon.TryReload();
        }
        
        wasFiring = isFiring;
        wasAiming = isAiming;
    }
    
    private void TryHitscan()
    {
        if (!weapon.HasAmmo()) return;
        
        Vector3 origin = cameraTransform.position;
        Vector3 direction = cameraTransform.forward;
        
        var config = weapon.Config;
        if (config == null) return;
        
        float spread = weapon.GetCurrentSpread();
        if (spread > 0)
        {
            direction = ApplySpread(direction, spread);
        }
        
        PerformHitscan(origin, direction, config);
    }
    
    private Vector3 ApplySpread(Vector3 direction, float spread)
    {
        float spreadAngle = spread * Mathf.Deg2Rad;
        Vector2 randomCircle = Random.insideUnitCircle * spreadAngle;
        
        Quaternion spreadRotation = Quaternion.Euler(randomCircle.y, randomCircle.x, 0);
        return spreadRotation * direction;
    }
    
    private void PerformHitscan(Vector3 origin, Vector3 direction, WeaponConfig config)
    {
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, config.range, hitLayers))
        {
            Debug.DrawLine(origin, hit.point, Color.red, 0.5f);
            
            var target = hit.collider.GetComponentInParent<Target>();
            if (target != null)
            {
                var hitZone = hit.collider.GetComponent<HitZone>();
                HitZoneType zone = hitZone != null ? hitZone.ZoneType : HitZoneType.Body;
                
                float damage = config.damage;
                bool isHeadshot = false;
                
                switch (zone)
                {
                    case HitZoneType.Head:
                        damage *= config.headshotMultiplier;
                        isHeadshot = true;
                        break;
                    case HitZoneType.Body:
                        break;
                    case HitZoneType.Limb:
                        damage *= config.limbMultiplier;
                        break;
                }
                
                var damageInfo = new DamageInfo
                {
                    damage = damage,
                    hitPoint = hit.point,
                    hitNormal = hit.normal,
                    hitDirection = direction,
                    isHeadshot = isHeadshot,
                    weaponConfig = config
                };
                
                target.TakeDamage(damageInfo);
                
                Debug.Log($"Hit {target.name} for {damage:F1} damage" + (isHeadshot ? " (HEADSHOT!)" : ""));
            }
        }
        else
        {
            Debug.DrawRay(origin, direction * config.range, Color.yellow, 0.5f);
        }
    }
}
