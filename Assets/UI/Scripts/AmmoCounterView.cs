using UnityEngine;
using UnityEngine.UI;

namespace CodMovementShooter.UI
{
    /// <summary>
    /// Mag + reserve ammo readout. Format: "30 | 90"
    /// </summary>
    public sealed class AmmoCounterView : MonoBehaviour
    {
        [SerializeField] private Text weaponNameText;
        [SerializeField] private Text ammoText;
        [SerializeField] private string format = "{0} | {1}";
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color emptyColor = new Color(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private Color lowColor = new Color(1f, 0.75f, 0.25f, 1f);
        [SerializeField] private int lowAmmoThreshold = 5;

        private int _magazine;
        private int _reserve;

        public void Bind(Text text)
        {
            ammoText = text;
            Refresh();
        }

        public void Bind(Text ammo, Text weaponName)
        {
            ammoText = ammo;
            weaponNameText = weaponName;
            Refresh();
        }

        public void SetWeaponName(string weaponName)
        {
            if (weaponNameText == null)
                return;

            weaponNameText.text = string.IsNullOrEmpty(weaponName) ? string.Empty : weaponName;
        }

        public void SetAmmo(int magazine, int reserve)
        {
            _magazine = Mathf.Max(0, magazine);
            _reserve = Mathf.Max(0, reserve);
            Refresh();
        }

        private void Refresh()
        {
            if (ammoText == null)
                return;

            ammoText.text = string.Format(format, _magazine, _reserve);

            if (_magazine <= 0)
                ammoText.color = emptyColor;
            else if (_magazine <= lowAmmoThreshold)
                ammoText.color = lowColor;
            else
                ammoText.color = normalColor;
        }
    }
}
