using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsTactical
{
    /// <summary>
    /// Pure route maths for the bot team play: picks an alternative way to a goal through a map waypoint (flank / retake from
    /// the side) and finds a staging point short of a goal along a path. No scene access, so it is unit-testable; the director
    /// supplies the path length function (NavMesh path lengths in play).
    /// </summary>
    public static class TacticalRoutePlanner
    {
        /// <summary>A flank waypoint must be at least this far from both ends of the trip.</summary>
        public const float MinFromEnds = 8f;

        /// <summary>Planar distance from <paramref name="p"/> to the segment a-b.</summary>
        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float lengthSqr = abx * abx + abz * abz;
            float t = lengthSqr < 1e-6f ? 0f : Mathf.Clamp01(((p.x - a.x) * abx + (p.z - a.z) * abz) / lengthSqr);
            float dx = p.x - (a.x + abx * t);
            float dz = p.z - (a.z + abz * t);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>+1 when <paramref name="p"/> is left of the line a to b (seen from above), -1 when right, 0 on the line.</summary>
        public static int Side(Vector3 p, Vector3 a, Vector3 b)
        {
            float cross = (b.x - a.x) * (p.z - a.z) - (b.z - a.z) * (p.x - a.x);
            if (Mathf.Abs(cross) < 1e-4f)
                return 0;
            return cross > 0f ? 1 : -1;
        }

        /// <summary>Total length of a corner list.</summary>
        public static float PathLength(IReadOnlyList<Vector3> corners)
        {
            float total = 0f;
            for (int i = 1; i < corners.Count; i++)
                total += Vector3.Distance(corners[i - 1], corners[i]);
            return total;
        }

        /// <summary>
        /// Index in <paramref name="candidates"/> of a waypoint that makes a worthwhile alternative route from
        /// <paramref name="start"/> to <paramref name="goal"/>, or -1. Candidates must sit away from the direct line
        /// (<paramref name="minOffset"/> metres), away from both ends, and cost at most <paramref name="maxDetour"/> times the
        /// direct path. <paramref name="preferSide"/> (+1 / -1 / 0) picks a side of the direct line when one qualifies, so two
        /// flankers can take opposite routes.
        /// </summary>
        public static int PickFlank(
            Vector3 start,
            Vector3 goal,
            IReadOnlyList<Vector3> candidates,
            Func<Vector3, Vector3, float> pathLength,
            int preferSide,
            System.Random rng,
            float minOffset = 8f,
            float maxDetour = 1.9f)
        {
            if (candidates == null || candidates.Count == 0 || pathLength == null)
                return -1;

            float direct = pathLength(start, goal);
            if (float.IsNaN(direct) || float.IsInfinity(direct) || direct >= float.MaxValue || direct < 1f)
                return -1;

            var indices = new List<int>();
            var scores = new List<float>();
            var sides = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 c = candidates[i];
                if (Planar(c, goal) < MinFromEnds || Planar(c, start) < MinFromEnds)
                    continue;

                float offset = DistanceToSegment(c, start, goal);
                if (offset < minOffset)
                    continue;

                float first = pathLength(start, c);
                float second = pathLength(c, goal);
                if (first >= float.MaxValue || second >= float.MaxValue)
                    continue;

                float detour = (first + second) / direct;
                if (detour > maxDetour)
                    continue;

                indices.Add(i);
                scores.Add(offset / Mathf.Max(1f, detour));
                sides.Add(Side(c, start, goal));
            }

            if (indices.Count == 0)
                return -1;

            bool useSide = false;
            if (preferSide != 0)
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    if (sides[i] == preferSide)
                    {
                        useSide = true;
                        break;
                    }
                }
            }

            float best = 0f;
            for (int i = 0; i < indices.Count; i++)
            {
                if ((!useSide || sides[i] == preferSide) && scores[i] > best)
                    best = scores[i];
            }

            var pool = new List<int>();
            for (int i = 0; i < indices.Count; i++)
            {
                if ((!useSide || sides[i] == preferSide) && scores[i] >= best * 0.85f)
                    pool.Add(indices[i]);
            }

            if (pool.Count == 0)
                return -1;
            return rng != null ? pool[rng.Next(pool.Count)] : pool[0];
        }

        /// <summary>
        /// Point on the path <paramref name="corners"/> (start first, goal last) that lies <paramref name="distanceFromEnd"/>
        /// metres short of the goal; the start when the path is shorter than that.
        /// </summary>
        public static Vector3 StagingPoint(IReadOnlyList<Vector3> corners, float distanceFromEnd)
        {
            if (corners == null || corners.Count == 0)
                return Vector3.zero;

            float remaining = Mathf.Max(0f, distanceFromEnd);
            for (int i = corners.Count - 1; i > 0; i--)
            {
                Vector3 from = corners[i];
                Vector3 to = corners[i - 1];
                float segment = Vector3.Distance(from, to);
                if (remaining <= segment)
                    return segment < 1e-4f ? to : Vector3.Lerp(from, to, remaining / segment);
                remaining -= segment;
            }

            return corners[0];
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
