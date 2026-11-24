using UnityEngine;

namespace CodMovementShooter.Maps.Shipment
{
    /// <summary>Marks a spawn location in the Shipment scene. Facing is the transform's forward.</summary>
    public class ShipmentSpawnPoint : MonoBehaviour
    {
        [SerializeField] private SpawnTeam team = SpawnTeam.FreeForAll;

        public SpawnTeam Team => team;

        public void Configure(SpawnTeam spawnTeam)
        {
            team = spawnTeam;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = team == SpawnTeam.TeamA ? Color.cyan : team == SpawnTeam.TeamB ? Color.magenta : Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.4f);
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward);
        }
    }
}
