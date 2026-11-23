using UnityEngine;

public class PlayerWeaponBridge : MonoBehaviour
{
    [SerializeField] private WeaponController weapon;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private LayerMask hitLayers = ~0;
    
    private bool wasFiring;
    private bool wasAiming;

    private WeaponController ActiveWeapon =>
        weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : weapon;

    public WeaponSwitcher WeaponSwitcher => weaponSwitcher;
    
    private void Awake()
    {
        if (inputHandler == null)
            inputHandler = GetComponent<InputHandler>();
        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;
    }
    
    private void Start()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>();
        if (weapon == null)
            weapon = GetComponentInChildren<WeaponController>();

        WireWeaponCameras();
        
        if (weaponSwitcher == null && weapon != null)
            weapon.Equip();
    }

    /// <summary>
    /// Rebinds the active <see cref="WeaponSwitcher"/> after a class/loadout swap.
    /// </summary>
    public void BindSwitcher(WeaponSwitcher switcher)
    {
        weaponSwitcher = switcher;
        if (weaponSwitcher != null)
            weapon = weaponSwitcher.CurrentWeapon;
    }
    
    private void Update()
    {
        var active = ActiveWeapon;
        if (active == null || cameraTransform == null) return;
        
        bool isFiring = Input.GetMouseButton(0);
        bool isAiming = Input.GetMouseButton(1);
        
        if (isFiring && !wasFiring)
            active.StartFiring();
        else if (!isFiring && wasFiring)
            active.StopFiring();
        
        if (isFiring)
            active.UpdateFiring();
        
        if (isAiming && !wasAiming)
            active.StartAiming();
        else if (!isAiming && wasAiming)
            active.StopAiming();
        
        if (Input.GetKeyDown(KeyCode.R))
            active.TryReload();
        
        wasFiring = isFiring;
        wasAiming = isAiming;
    }

    private void WireWeaponCameras()
    {
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
        }

        var camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        if (camera == null)
            return;

        foreach (var w in GetComponentsInChildren<WeaponController>(true))
            w.SetPlayerCamera(camera);
    }
}