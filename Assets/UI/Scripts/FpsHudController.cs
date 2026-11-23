using UnityEngine;

namespace CodMovementShooter.UI
{
    /// <summary>
    /// Root HUD API. Views stay presentation-only; gameplay wires through
    /// <see cref="WeaponHudBridge"/> or these public methods.
    /// </summary>
    public sealed class FpsHudController : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private CrosshairView crosshair;
        [SerializeField] private AmmoCounterView ammoCounter;
        [SerializeField] private HitMarkerView hitMarker;
        [SerializeField] private DamageNumberView damageNumbers;

        [Header("Options")]
        [SerializeField] private bool enableDamageNumbers;
        [SerializeField] private bool buildHierarchyIfMissing = true;

        public CrosshairView Crosshair => crosshair;
        public AmmoCounterView AmmoCounter => ammoCounter;
        public HitMarkerView HitMarker => hitMarker;
        public DamageNumberView DamageNumbers => damageNumbers;
        public bool EnableDamageNumbers
        {
            get => enableDamageNumbers;
            set => enableDamageNumbers = value;
        }

        private void Awake()
        {
            if (buildHierarchyIfMissing)
                FpsHudHierarchyBuilder.EnsureBuilt(this);
        }

        public void BindViews(
            CrosshairView crosshairView,
            AmmoCounterView ammoView,
            HitMarkerView hitMarkerView,
            DamageNumberView damageView)
        {
            crosshair = crosshairView;
            ammoCounter = ammoView;
            hitMarker = hitMarkerView;
            damageNumbers = damageView;
        }

        /// <summary>
        /// Public fallback for hit confirmation when weapon events are unavailable.
        /// </summary>
        public void ShowHitMarker(bool headshot = false)
        {
            if (hitMarker != null)
                hitMarker.Show(headshot);

            if (enableDamageNumbers && damageNumbers != null)
                damageNumbers.Show(headshot ? 100f : 25f, headshot);
        }

        /// <summary>
        /// Hit confirmation with an explicit damage value (optional floating number).
        /// </summary>
        public void ShowHitMarker(float damage, bool headshot = false)
        {
            if (hitMarker != null)
                hitMarker.Show(headshot);

            if (enableDamageNumbers && damageNumbers != null && damage > 0f)
                damageNumbers.Show(damage, headshot);
        }

        public void SetAmmo(int magazine, int reserve)
        {
            if (ammoCounter != null)
                ammoCounter.SetAmmo(magazine, reserve);
        }

        public void SetWeaponName(string weaponName)
        {
            if (ammoCounter != null)
                ammoCounter.SetWeaponName(weaponName);
        }

        public void NotifyFired()
        {
            if (crosshair != null)
                crosshair.NotifyFired();
        }

        public void SetMovementSpeed(float horizontalSpeed)
        {
            if (crosshair != null)
                crosshair.SetMovementSpeed(horizontalSpeed);
        }
    }
}
