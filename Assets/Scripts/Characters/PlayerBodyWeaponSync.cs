using UnityEngine;

/// <summary>
/// Mirrors the first-person weapon state onto the player's third-person <see cref="HumanoidBody"/>: the equipped
/// weapon model, aim/sprint stance, shots and reloads. Added automatically to bodies that hide parts from the local
/// camera; the gun is culled from the first-person camera like the rest of the body.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
public sealed class PlayerBodyWeaponSync : MonoBehaviour
{
    private HumanoidBody body;
    private WeaponSwitcher switcher;
    private MovementController movement;
    private WeaponController current;

    private void OnEnable()
    {
        body = GetComponent<HumanoidBody>();
        movement = GetComponent<MovementController>();
    }

    private void OnDisable()
    {
        Unsubscribe();
        current = null;
    }

    private void Update()
    {
        if (body == null || !body.IsBuilt || body.IsDying)
            return;

        if (switcher == null)
            switcher = GetComponentInChildren<WeaponSwitcher>(true);

        WeaponController weapon = switcher != null ? switcher.CurrentWeapon : null;
        if (weapon != current)
            SetWeapon(weapon);

        if (current == null)
            return;

        HoldStance stance = HoldStance.Ready;
        if (current.IsAiming && current.ADSProgress > 0.3f)
            stance = HoldStance.Aim;
        else if (movement != null && (movement.CurrentState == MovementController.MovementState.Sprinting || movement.CurrentState == MovementController.MovementState.Sliding))
            stance = HoldStance.Lowered;
        body.Stance = stance;

        int cycles = 1;
        if (current.Config != null && current.Config.perShellReload)
            cycles = Mathf.Clamp(current.Config.magazineSize - Mathf.Max(0, current.CurrentAmmo), 1, 12);
        body.SetReloadState(current.IsReloading, current.ReloadProgress, cycles);
    }

    private void SetWeapon(WeaponController weapon)
    {
        Unsubscribe();
        current = weapon;
        if (current == null || current.Config == null)
        {
            body.ClearWeapon();
            current = null;
            return;
        }

        current.OnWeaponFired += HandleFired;
        current.OnMeleeSwing += HandleSwing;
        body.EquipWeapon(current.Config, current.gameObject);
    }

    private void Unsubscribe()
    {
        if (current == null)
            return;
        current.OnWeaponFired -= HandleFired;
        current.OnMeleeSwing -= HandleSwing;
    }

    private void HandleFired()
    {
        if (body != null)
            body.NotifyShot(1f);
    }

    private void HandleSwing()
    {
        if (body != null)
            body.NotifyShot(1.6f);
    }
}
