using System;
using UnityEngine;

public class PlayerWeaponBridge : MonoBehaviour
{
    [SerializeField] private WeaponController weapon;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private LayerMask hitLayers = ~0;
    
    [Tooltip("Only accept fire/aim input while the game has focus and the cursor is locked, so clicks that merely regain focus or relock the cursor never shoot.")]
    [SerializeField] private bool requireLiveInput = true;

    private bool wasFiring;
    private bool wasAiming;
    private bool waitForFireRelease = true;
    private bool waitForAimRelease = true;
    private WeaponController lastActive;
    private Func<bool> fireInput = DefaultFireInput;
    private Func<bool> aimInput = DefaultAimInput;
    private Func<bool> reloadInput = DefaultReloadInput;

    private static bool DefaultFireInput() => Input.GetMouseButton(0);
    private static bool DefaultAimInput() => Input.GetMouseButton(1);
    private static bool DefaultReloadInput() => Input.GetKeyDown(KeyCode.R);

    /// <summary>Replaces the raw input readers (tests drive the bridge through these); null restores the mouse and keyboard.</summary>
    public void SetInputSources(Func<bool> fire, Func<bool> aim, Func<bool> reload)
    {
        fireInput = fire ?? DefaultFireInput;
        aimInput = aim ?? DefaultAimInput;
        reloadInput = reload ?? DefaultReloadInput;
    }

    public bool RequireLiveInput
    {
        get => requireLiveInput;
        set => requireLiveInput = value;
    }

    /// <summary>When true the player cannot fire or aim (round freeze time, round end); button holds are released.</summary>
    public bool FireLocked { get; set; }

    private bool InputLive =>
        !requireLiveInput || (Application.isFocused && Cursor.lockState == CursorLockMode.Locked);

    private void OnEnable()
    {
        wasFiring = false;
        wasAiming = false;
        waitForFireRelease = true;
        waitForAimRelease = true;
    }

    private void OnDisable()
    {
        ReleaseHeldInput(lastActive != null ? lastActive : ActiveWeapon);
    }

    private void ReleaseHeldInput(WeaponController target)
    {
        if (target != null && (wasFiring || wasAiming))
        {
            target.StopFiring();
            target.StopAiming();
        }
        wasFiring = false;
        wasAiming = false;
        waitForFireRelease = true;
        waitForAimRelease = true;
    }

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
        RefreshWeaponBindings();
    }

    public void RefreshWeaponBindings()
    {
        if (weaponSwitcher != null)
            weapon = weaponSwitcher.CurrentWeapon;
        WireWeaponCameras();
    }
    
    private void Update()
    {
        var active = ActiveWeapon;
        if (active == null || cameraTransform == null) return;
        
        if (lastActive != active)
        {
            if (lastActive != null)
                ReleaseHeldInput(lastActive);
            lastActive = active;
        }

        if (!InputLive || FireLocked)
        {
            ReleaseHeldInput(active);
            return;
        }

        bool isFiring = ConsumeFreshPress(fireInput(), ref waitForFireRelease);
        bool isAiming = ConsumeFreshPress(aimInput(), ref waitForAimRelease);
        
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
        
        if (reloadInput())
            active.TryReload();
        
        wasFiring = isFiring;
        wasAiming = isAiming;
    }

    // A button already down when input becomes live (menu close, respawn, focus or lock regained,
    // weapon swap) is ignored until it has been released once.
    private static bool ConsumeFreshPress(bool raw, ref bool waitForRelease)
    {
        if (waitForRelease)
        {
            if (raw)
                return false;
            waitForRelease = false;
        }
        return raw;
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