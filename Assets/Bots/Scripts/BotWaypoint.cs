using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional patrol marker. When a scene contains any, bots patrol between the markers near them instead of
/// generating random waypoints. Maps need no markers: bots work without them.
/// </summary>
public sealed class BotWaypoint : MonoBehaviour
{
    private static readonly List<BotWaypoint> Registry = new List<BotWaypoint>();

    public static IReadOnlyList<BotWaypoint> All => Registry;

    private void OnEnable()
    {
        Registry.Add(this);
    }

    private void OnDisable()
    {
        Registry.Remove(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
