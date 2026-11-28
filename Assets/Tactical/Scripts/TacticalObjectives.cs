using System.Collections.Generic;
using UnityEngine;

namespace FpsTactical
{
    /// <summary>Pure position helpers the bot orders are built from.</summary>
    public static class TacticalObjectives
    {
        public static Vector3 RingPoint(Vector3 center, float radius, int index, int count, float startDegrees = 0f)
        {
            count = Mathf.Max(1, count);
            float angle = (startDegrees + 360f * index / count) * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
        }

        /// <summary>Yaw in degrees that turns from <paramref name="from"/> toward <paramref name="to"/> on the ground plane.</summary>
        public static float YawToward(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            if (delta.sqrMagnitude < 1e-4f)
                return 0f;
            return Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
        }

        public static float Planar(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Up to <paramref name="wanted"/> distinct points to hold a site with: map waypoints inside the (slightly grown)
        /// zone first, nearest to its centre first, padded with a ring around the centre when the map has too few.
        /// </summary>
        public static List<Vector3> HoldPoints(Bounds zone, IReadOnlyList<Vector3> waypoints, int wanted, float grow = 6f)
        {
            var result = new List<Vector3>(Mathf.Max(0, wanted));
            if (wanted <= 0)
                return result;

            Vector3 center = zone.center;
            center.y = 0f;
            Vector3 extents = zone.extents + new Vector3(grow, 100f, grow);
            var inside = new List<Vector3>();
            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    Vector3 p = waypoints[i];
                    if (Mathf.Abs(p.x - zone.center.x) <= extents.x && Mathf.Abs(p.z - zone.center.z) <= extents.z
                        && p.y > zone.min.y - 1f && p.y < zone.max.y + 1f)
                        inside.Add(p);
                }
            }

            inside.Sort((a, b) => Planar(a, center).CompareTo(Planar(b, center)));
            for (int i = 0; i < inside.Count && result.Count < wanted; i++)
                result.Add(inside[i]);

            float ring = Mathf.Max(2f, Mathf.Min(zone.extents.x, zone.extents.z) * 0.6f);
            int missing = wanted - result.Count;
            for (int i = 0; i < missing; i++)
                result.Add(RingPoint(new Vector3(zone.center.x, zone.min.y + 0.05f, zone.center.z), ring, i, missing, 25f));
            return result;
        }
    }
}
