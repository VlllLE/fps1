using UnityEngine;

namespace FpsMaps.Bazaar
{
    public enum BazaarZoneKind
    {
        Plant,
        Buy
    }

    /// <summary>
    /// Marker for a future bomb mode. It only labels a trigger volume (a bomb plant zone or a team's buy area) and does
    /// nothing at runtime yet; the round, plant and buy logic is deliberately not implemented.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BazaarPlantZone : MonoBehaviour
    {
        [SerializeField] private BazaarZoneKind kind;
        [SerializeField] private string site;

        public BazaarZoneKind Kind => kind;

        /// <summary>"A" or "B" for plant zones, "Attack" or "Defend" for buy areas.</summary>
        public string Site => site;

        public void Configure(BazaarZoneKind zoneKind, string zoneSite)
        {
            kind = zoneKind;
            site = zoneSite;
        }

        public bool Contains(Vector3 worldPoint)
        {
            var box = GetComponent<BoxCollider>();
            return box != null && box.bounds.Contains(worldPoint);
        }
    }
}
