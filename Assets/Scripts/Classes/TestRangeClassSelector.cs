using UnityEngine;

/// <summary>
/// TestRange helper: pick class loadout at runtime with F1–F4.
/// </summary>
public class TestRangeClassSelector : MonoBehaviour
{
    [SerializeField] private ClassLoadoutApplier loadoutApplier;
    [SerializeField] private PlayerWeaponBridge weaponBridge;

    [Header("Classes (F1–F4)")]
    [SerializeField] private ClassDefinition assaultClass;
    [SerializeField] private ClassDefinition smgClass;
    [SerializeField] private ClassDefinition sniperClass;
    [SerializeField] private ClassDefinition hybridClass;

    [Header("Options")]
    [SerializeField] private bool allowRuntimeSwap = true;

    public ClassDefinition AssaultClass => assaultClass;
    public ClassDefinition SmgClass => smgClass;
    public ClassDefinition SniperClass => sniperClass;
    public ClassDefinition HybridClass => hybridClass;

    private void Awake()
    {
        if (loadoutApplier == null)
            loadoutApplier = GetComponent<ClassLoadoutApplier>();
        if (weaponBridge == null)
            weaponBridge = GetComponent<PlayerWeaponBridge>();
    }

    private void Update()
    {
        if (!allowRuntimeSwap || loadoutApplier == null)
            return;

        ClassDefinition picked = null;
        if (Input.GetKeyDown(KeyCode.F1))
            picked = assaultClass;
        else if (Input.GetKeyDown(KeyCode.F2))
            picked = smgClass;
        else if (Input.GetKeyDown(KeyCode.F3))
            picked = sniperClass;
        else if (Input.GetKeyDown(KeyCode.F4))
            picked = hybridClass;

        if (picked == null)
            return;

        ApplyClass(picked);
    }

    public bool ApplyClass(ClassDefinition definition)
    {
        if (definition == null || loadoutApplier == null)
            return false;
        if (!loadoutApplier.ApplyLoadout(definition))
            return false;
        weaponBridge?.RefreshWeaponBindings();
        return true;
    }
}