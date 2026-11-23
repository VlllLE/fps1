using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages weapon inventory, switching, and slot management.
/// Attach to the player's weapon holder object.
/// </summary>
public class WeaponSwitcher : MonoBehaviour
{
    [Header("Weapon Slots")]
    [SerializeField] private WeaponSlot[] weaponSlots;
    
    [Header("Settings")]
    [SerializeField] private int startingSlot = 0;
    [SerializeField] private float switchSpeed = 0.5f;
    [SerializeField] private float quickSwitchSpeed = 0.3f;
    [SerializeField] private bool allowScrollSwitching = true;
    [SerializeField] private bool invertScrollDirection = false;
    [Tooltip("When true, number keys select weapon slot index (1 = first slot).")]
    [SerializeField] private bool useDirectSlotIndexKeys = true;
    
    [Header("Input Keys")]
    [SerializeField] private KeyCode primaryKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode secondaryKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode meleeKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode lethalKey = KeyCode.Alpha4;
    [SerializeField] private KeyCode tacticalKey = KeyCode.Alpha5;
    [SerializeField] private KeyCode lastWeaponKey = KeyCode.Q;
    
    [Header("Animation")]
    [SerializeField] private Animator armsAnimator;
    [SerializeField] private string holsterTrigger = "Holster";
    [SerializeField] private string equipTrigger = "Equip";
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip switchSound;
    
    private int currentSlotIndex = -1;
    private int previousSlotIndex = -1;
    private bool isSwitching;
    private bool hasStarted;
    private Coroutine switchCoroutine;
    
    private Dictionary<SlotType, int> slotTypeToIndex;
    
    public event Action<WeaponController> OnWeaponChanged;
    public event Action<int> OnSlotChanged;
    public event Action OnSwitchStarted;
    public event Action OnSwitchCompleted;
    
    public WeaponController CurrentWeapon => GetCurrentWeapon();
    public int CurrentSlotIndex => currentSlotIndex;
    public bool IsSwitching => isSwitching;
    public WeaponSlot[] Slots => weaponSlots;
    
    private void Awake()
    {
        InitializeSlotMapping();
    }

    /// <summary>
    /// Replaces the switcher's slot array (used by class / loadout systems).
    /// Safe to call from another component's Awake before this Start runs;
    /// if Start already ran, the starting slot is equipped immediately.
    /// </summary>
    public void ConfigureSlots(WeaponSlot[] slots, int startSlot = 0)
    {
        CancelSwitch();

        if (currentSlotIndex >= 0 && weaponSlots != null
            && currentSlotIndex < weaponSlots.Length
            && weaponSlots[currentSlotIndex].weapon != null)
        {
            weaponSlots[currentSlotIndex].weapon.Holster();
        }

        weaponSlots = slots ?? Array.Empty<WeaponSlot>();
        startingSlot = startSlot;
        currentSlotIndex = -1;
        previousSlotIndex = -1;

        InitializeSlotMapping();
        InitializeWeapons();

        if (hasStarted)
            EquipConfiguredStartingSlot();
    }

    /// <summary>
    /// Forces an immediate equip of the configured starting slot after <see cref="ConfigureSlots"/>.
    /// </summary>
    public void EquipConfiguredStartingSlot()
    {
        if (startingSlot >= 0 && weaponSlots != null && startingSlot < weaponSlots.Length
            && weaponSlots[startingSlot].weapon != null)
        {
            EquipSlotImmediate(startingSlot);
        }
    }
    
    private void Start()
    {
        hasStarted = true;
        InitializeWeapons();
        
        if (startingSlot >= 0 && weaponSlots != null && startingSlot < weaponSlots.Length)
        {
            EquipSlotImmediate(startingSlot);
        }
    }
    
    private void Update()
    {
        HandleInput();
    }
    
    private void InitializeSlotMapping()
    {
        slotTypeToIndex = new Dictionary<SlotType, int>();
        if (weaponSlots == null)
            weaponSlots = Array.Empty<WeaponSlot>();
        
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (!slotTypeToIndex.ContainsKey(weaponSlots[i].slotType))
            {
                slotTypeToIndex[weaponSlots[i].slotType] = i;
            }
        }
    }
    
    private void InitializeWeapons()
    {
        if (weaponSlots == null)
            return;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i].weapon != null)
            {
                weaponSlots[i].weapon.gameObject.SetActive(false);
            }
        }
    }
    
    private void HandleInput()
    {
        if (isSwitching) return;
        
        if (useDirectSlotIndexKeys)
        {
            for (int i = 0; i < weaponSlots.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SwitchToSlot(i);
                    return;
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(primaryKey))
                SwitchToSlotType(SlotType.Primary);
            else if (Input.GetKeyDown(secondaryKey))
                SwitchToSlotType(SlotType.Secondary);
            else if (Input.GetKeyDown(meleeKey))
                SwitchToSlotType(SlotType.Melee);
            else if (Input.GetKeyDown(lethalKey))
                SwitchToSlotType(SlotType.Lethal);
            else if (Input.GetKeyDown(tacticalKey))
                SwitchToSlotType(SlotType.Tactical);
        }

        if (Input.GetKeyDown(lastWeaponKey))
            SwitchToLastWeapon();
        
        if (allowScrollSwitching)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                int direction = invertScrollDirection ? -1 : 1;
                if (scroll > 0f)
                    SwitchToNextWeapon(direction);
                else
                    SwitchToNextWeapon(-direction);
            }
        }
    }
    
    #region Switching Methods
    
    public void SwitchToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;
        if (slotIndex == currentSlotIndex) return;
        if (weaponSlots[slotIndex].weapon == null) return;
        
        if (switchCoroutine != null)
            StopCoroutine(switchCoroutine);
            
        switchCoroutine = StartCoroutine(SwitchWeaponRoutine(slotIndex));
    }
    
    public void SwitchToSlotType(SlotType type)
    {
        if (slotTypeToIndex.TryGetValue(type, out int index))
        {
            SwitchToSlot(index);
        }
    }
    
    public void SwitchToNextWeapon(int direction = 1)
    {
        if (weaponSlots.Length == 0) return;
        
        int nextIndex = currentSlotIndex;
        int attempts = 0;
        
        do
        {
            nextIndex += direction;
            
            if (nextIndex >= weaponSlots.Length)
                nextIndex = 0;
            else if (nextIndex < 0)
                nextIndex = weaponSlots.Length - 1;
                
            attempts++;
            
            if (attempts > weaponSlots.Length) return;
            
        } while (weaponSlots[nextIndex].weapon == null);
        
        SwitchToSlot(nextIndex);
    }
    
    public void SwitchToLastWeapon()
    {
        if (previousSlotIndex >= 0 && previousSlotIndex < weaponSlots.Length)
        {
            if (weaponSlots[previousSlotIndex].weapon != null)
            {
                SwitchToSlot(previousSlotIndex);
            }
        }
    }
    
    public void QuickSwitch(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;
        if (weaponSlots[slotIndex].weapon == null) return;
        
        if (switchCoroutine != null)
            StopCoroutine(switchCoroutine);
            
        switchCoroutine = StartCoroutine(SwitchWeaponRoutine(slotIndex, true));
    }
    
    private void EquipSlotImmediate(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;
        if (weaponSlots[slotIndex].weapon == null) return;
        
        if (currentSlotIndex >= 0 && weaponSlots[currentSlotIndex].weapon != null)
        {
            weaponSlots[currentSlotIndex].weapon.Holster();
            weaponSlots[currentSlotIndex].weapon.gameObject.SetActive(false);
        }
        
        previousSlotIndex = currentSlotIndex;
        currentSlotIndex = slotIndex;
        
        weaponSlots[currentSlotIndex].weapon.gameObject.SetActive(true);
        weaponSlots[currentSlotIndex].weapon.Equip();
        
        OnSlotChanged?.Invoke(currentSlotIndex);
        OnWeaponChanged?.Invoke(GetCurrentWeapon());
    }
    
    private IEnumerator SwitchWeaponRoutine(int newSlotIndex, bool isQuickSwitch = false)
    {
        isSwitching = true;
        OnSwitchStarted?.Invoke();
        
        float actualSwitchSpeed = isQuickSwitch ? quickSwitchSpeed : switchSpeed;
        
        if (currentSlotIndex >= 0 && weaponSlots[currentSlotIndex].weapon != null)
        {
            WeaponController currentWeapon = weaponSlots[currentSlotIndex].weapon;
            currentWeapon.Holster();
            
            if (armsAnimator != null)
                armsAnimator.SetTrigger(holsterTrigger);
            
            yield return new WaitForSeconds(actualSwitchSpeed * 0.5f);
            
            currentWeapon.gameObject.SetActive(false);
        }
        
        previousSlotIndex = currentSlotIndex;
        currentSlotIndex = newSlotIndex;
        
        if (switchSound != null && audioSource != null)
            audioSource.PlayOneShot(switchSound);
        
        WeaponController newWeapon = weaponSlots[currentSlotIndex].weapon;
        newWeapon.gameObject.SetActive(true);
        
        if (armsAnimator != null)
            armsAnimator.SetTrigger(equipTrigger);
        
        yield return new WaitForSeconds(actualSwitchSpeed * 0.5f);
        
        newWeapon.Equip();
        
        isSwitching = false;
        switchCoroutine = null;
        
        OnSlotChanged?.Invoke(currentSlotIndex);
        OnWeaponChanged?.Invoke(newWeapon);
        OnSwitchCompleted?.Invoke();
    }
    
    #endregion
    
    #region Inventory Management
    
    public bool AddWeapon(WeaponController weapon, SlotType preferredSlot)
    {
        int slotIndex = FindSlotForWeapon(preferredSlot);
        
        if (slotIndex < 0) return false;
        
        return SetWeaponInSlot(weapon, slotIndex);
    }
    
    public bool SetWeaponInSlot(WeaponController weapon, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return false;
        
        if (weaponSlots[slotIndex].weapon != null)
        {
            DropWeapon(slotIndex);
        }
        
        weapon.transform.SetParent(transform);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        
        weaponSlots[slotIndex].weapon = weapon;
        weapon.gameObject.SetActive(slotIndex == currentSlotIndex);
        
        if (slotIndex == currentSlotIndex)
        {
            weapon.Equip();
            OnWeaponChanged?.Invoke(weapon);
        }
        
        return true;
    }
    
    public WeaponController RemoveWeapon(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return null;
        if (weaponSlots[slotIndex].weapon == null) return null;
        
        WeaponController weapon = weaponSlots[slotIndex].weapon;
        weaponSlots[slotIndex].weapon = null;
        
        if (slotIndex == currentSlotIndex)
        {
            weapon.Holster();
            SwitchToNextAvailableWeapon();
        }
        
        return weapon;
    }
    
    public void DropWeapon(int slotIndex)
    {
        WeaponController weapon = RemoveWeapon(slotIndex);
        
        if (weapon != null)
        {
            weapon.transform.SetParent(null);
            
            Rigidbody rb = weapon.GetComponent<Rigidbody>();
            if (rb == null)
                rb = weapon.gameObject.AddComponent<Rigidbody>();
                
            rb.isKinematic = false;
            rb.AddForce(transform.forward * 3f + Vector3.up * 2f, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 5f, ForceMode.Impulse);
            
            Collider col = weapon.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
        }
    }
    
    public void DropCurrentWeapon()
    {
        if (currentSlotIndex >= 0)
        {
            DropWeapon(currentSlotIndex);
        }
    }
    
    private int FindSlotForWeapon(SlotType preferredSlot)
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i].slotType == preferredSlot && weaponSlots[i].weapon == null)
            {
                return i;
            }
        }
        
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i].slotType == preferredSlot)
            {
                return i;
            }
        }
        
        return -1;
    }
    
    private void SwitchToNextAvailableWeapon()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i].weapon != null && i != currentSlotIndex)
            {
                SwitchToSlot(i);
                return;
            }
        }
        
        currentSlotIndex = -1;
        OnWeaponChanged?.Invoke(null);
    }
    
    #endregion
    
    #region Queries
    
    public WeaponController GetCurrentWeapon()
    {
        if (currentSlotIndex >= 0 && currentSlotIndex < weaponSlots.Length)
        {
            return weaponSlots[currentSlotIndex].weapon;
        }
        return null;
    }
    
    public WeaponController GetWeaponInSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < weaponSlots.Length)
        {
            return weaponSlots[slotIndex].weapon;
        }
        return null;
    }
    
    public WeaponController GetWeaponOfType(SlotType type)
    {
        if (slotTypeToIndex.TryGetValue(type, out int index))
        {
            return weaponSlots[index].weapon;
        }
        return null;
    }
    
    public bool HasWeaponInSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < weaponSlots.Length && weaponSlots[slotIndex].weapon != null;
    }
    
    public bool HasWeaponOfType(SlotType type)
    {
        return GetWeaponOfType(type) != null;
    }
    
    public int GetWeaponCount()
    {
        int count = 0;
        foreach (var slot in weaponSlots)
        {
            if (slot.weapon != null) count++;
        }
        return count;
    }
    
    public List<WeaponController> GetAllWeapons()
    {
        List<WeaponController> weapons = new List<WeaponController>();
        foreach (var slot in weaponSlots)
        {
            if (slot.weapon != null)
                weapons.Add(slot.weapon);
        }
        return weapons;
    }
    
    #endregion
    
    public void CancelSwitch()
    {
        if (switchCoroutine != null)
        {
            StopCoroutine(switchCoroutine);
            switchCoroutine = null;
            isSwitching = false;
        }
    }
    
    public void SetSwitchingEnabled(bool enabled)
    {
        this.enabled = enabled;
    }
}

[System.Serializable]
public class WeaponSlot
{
    public string slotName = "Weapon";
    public SlotType slotType = SlotType.Primary;
    public WeaponController weapon;
    public Sprite emptySlotIcon;
}

public enum SlotType
{
    Primary,
    Secondary,
    Melee,
    Lethal,
    Tactical
}
