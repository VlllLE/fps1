using UnityEngine;

namespace FpsMaps.Bazaar
{
    public enum BazaarZoneKind
    {
        Plant,
        Buy
    }

    /// <summary>
    /// Labels a trigger volume: a bomb plant zone (site A or B) or a team's yard around its spawns. The tactical mode reads
    /// these through <see cref="ITacticalZone"/>; the component itself holds no logic. Yards keep the historical
    /// "Buy" kind and the BuyZone object names so the generated scene stays stable, although the mode has no economy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BazaarPlantZone : MonoBehaviour, ITacticalZone
    {
        [SerializeField] private BazaarZoneKind kind;
        [SerializeField] private string site;

        public BazaarZoneKind Kind => kind;

        /// <summary>"A" or "B" for plant zones, "Attack" or "Defend" for yards.</summary>
        public string Site => site;

        public TacticalZoneKind ZoneKind => kind == BazaarZoneKind.Plant ? TacticalZoneKind.Plant : TacticalZoneKind.Buy;

        public string ZoneSite => site;

        public Bounds ZoneBounds
        {
            get
            {
                var box = GetComponent<BoxCollider>();
                return box != null ? box.bounds : new Bounds(transform.position, Vector3.one);
            }
        }

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
