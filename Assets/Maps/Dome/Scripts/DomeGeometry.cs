using System.Collections.Generic;
using UnityEngine;

namespace FpsMaps.Dome
{
    /// <summary>One hexagonal (or pentagonal) glass panel of the geodesic dome, in world space.</summary>
    public sealed class DomePanel
    {
        public Vector3 center;
        public Vector3[] ring;
        public int[] ringIds;
    }

    /// <summary>
    /// Pure procedural geometry for the Dome map: a Goldberg-style geodesic dome (hex panels
    /// built as the dual of a subdivided icosahedron) plus small mesh helpers. No editor-only API.
    /// </summary>
    public static class DomeGeometry
    {
        private sealed class MeshData
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();

            public void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector3 outwardReference)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;
                if (Vector3.Dot(n, centroid - outwardReference) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                    n = -n;
                }

                if (n.sqrMagnitude < 1e-12f)
                    return;

                n.Normalize();
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                normals.Add(n);
                normals.Add(n);
                normals.Add(n);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outwardReference)
            {
                Vector3 n = Vector3.Cross(b - a, d - a);
                Vector3 centroid = (a + b + c + d) * 0.25f;
                if (Vector3.Dot(n, centroid - outwardReference) < 0f)
                {
                    Vector3 swap = b;
                    b = d;
                    d = swap;
                    n = -n;
                }

                if (n.sqrMagnitude < 1e-12f)
                    return;

                n.Normalize();
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                for (int i = 0; i < 4; i++)
                    normals.Add(n);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        #region Geodesic sphere

        /// <summary>Unit-sphere vertices and outward-wound triangle indices of an icosphere.</summary>
        public static void BuildIcosphere(int frequency, out List<Vector3> vertices, out List<int> triangles)
        {
            int n = Mathf.Max(1, frequency);
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] baseVerts =
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f)
            };
            for (int i = 0; i < baseVerts.Length; i++)
                baseVerts[i] = baseVerts[i].normalized;

            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var lookup = new Dictionary<Vector3Int, int>();

            int Index(Vector3 p)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(p.x * 10000f),
                    Mathf.RoundToInt(p.y * 10000f),
                    Mathf.RoundToInt(p.z * 10000f));
                if (lookup.TryGetValue(key, out int existing))
                    return existing;
                verts.Add(p);
                lookup[key] = verts.Count - 1;
                return verts.Count - 1;
            }

            void AddTriangle(int a, int b, int c)
            {
                Vector3 normal = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (Vector3.Dot(normal, verts[a] + verts[b] + verts[c]) < 0f)
                {
                    int swap = b;
                    b = c;
                    c = swap;
                }

                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
            }

            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = baseVerts[faces[f]];
                Vector3 b = baseVerts[faces[f + 1]];
                Vector3 c = baseVerts[faces[f + 2]];
                var grid = new int[n + 1, n + 1];
                for (int i = 0; i <= n; i++)
                {
                    for (int j = 0; j <= n - i; j++)
                    {
                        Vector3 p = (a + (b - a) * (i / (float)n) + (c - a) * (j / (float)n)).normalized;
                        grid[i, j] = Index(p);
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n - i; j++)
                    {
                        AddTriangle(grid[i, j], grid[i + 1, j], grid[i, j + 1]);
                        if (j < n - 1 - i)
                            AddTriangle(grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]);
                    }
                }
            }

            vertices = verts;
            triangles = tris;
        }

        /// <summary>
        /// Builds the hex/pent panels of a dome centred at the origin. Panels whose centre is below
        /// <paramref name="minCenterY"/> are dropped, and panels reaching into a doorway are removed.
        /// </summary>
        public static List<DomePanel> BuildPanels(
            int frequency,
            float radius,
            float heightScale,
            float minCenterY,
            IList<float> doorAzimuthsDeg,
            float doorClearHeight,
            float doorHalfAngleDeg)
        {
            BuildIcosphere(frequency, out List<Vector3> verts, out List<int> tris);

            int triCount = tris.Count / 3;
            var triCentroids = new Vector3[triCount];
            var adjacency = new List<int>[verts.Count];
            for (int i = 0; i < adjacency.Length; i++)
                adjacency[i] = new List<int>(6);

            for (int t = 0; t < triCount; t++)
            {
                int a = tris[t * 3];
                int b = tris[t * 3 + 1];
                int c = tris[t * 3 + 2];
                triCentroids[t] = (verts[a] + verts[b] + verts[c]).normalized;
                adjacency[a].Add(t);
                adjacency[b].Add(t);
                adjacency[c].Add(t);
            }

            var panels = new List<DomePanel>();
            for (int v = 0; v < verts.Count; v++)
            {
                Vector3 unit = verts[v];
                Vector3 centerPos = ToDome(unit, radius, heightScale);
                if (centerPos.y < minCenterY)
                    continue;

                Vector3 reference = Mathf.Abs(unit.y) < 0.9f ? Vector3.up : Vector3.right;
                Vector3 u = Vector3.Cross(reference, unit).normalized;
                Vector3 w = Vector3.Cross(unit, u);

                var ids = new List<int>(adjacency[v]);
                ids.Sort((x, y) =>
                {
                    float ax = Mathf.Atan2(Vector3.Dot(triCentroids[x], w), Vector3.Dot(triCentroids[x], u));
                    float ay = Mathf.Atan2(Vector3.Dot(triCentroids[y], w), Vector3.Dot(triCentroids[y], u));
                    return ax.CompareTo(ay);
                });

                int count = ids.Count;
                if (count < 5)
                    continue;

                var ring = new Vector3[count];
                var ringIds = new int[count];
                Vector3 average = Vector3.zero;
                for (int k = 0; k < count; k++)
                {
                    ringIds[k] = ids[k];
                    ring[k] = ToDome(triCentroids[ids[k]], radius, heightScale);
                    average += ring[k];
                }

                average /= count;

                Vector3 faceNormal = Vector3.Cross(ring[1] - ring[0], ring[2] - ring[0]);
                if (Vector3.Dot(faceNormal, average - Vector3.zero) < 0f)
                {
                    System.Array.Reverse(ring);
                    System.Array.Reverse(ringIds);
                }

                if (BlocksDoorway(ring, doorAzimuthsDeg, doorClearHeight, doorHalfAngleDeg))
                    continue;

                panels.Add(new DomePanel { center = average, ring = ring, ringIds = ringIds });
            }

            return panels;
        }

        private static Vector3 ToDome(Vector3 unit, float radius, float heightScale)
        {
            return new Vector3(unit.x * radius, unit.y * radius * heightScale, unit.z * radius);
        }

        private static bool BlocksDoorway(Vector3[] ring, IList<float> doors, float clearHeight, float halfAngle)
        {
            if (doors == null)
                return false;

            for (int i = 0; i < ring.Length; i++)
            {
                if (ring[i].y >= clearHeight)
                    continue;

                float az = DomeLayout.AzimuthOf(ring[i]);
                for (int d = 0; d < doors.Count; d++)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(az, doors[d])) < halfAngle)
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region Meshes

        /// <summary>Render mesh for the glass: each panel is inset toward its centre to leave a gap for the struts.</summary>
        public static Mesh BuildGlassMesh(IList<DomePanel> panels, float inset)
        {
            var data = new MeshData();
            foreach (DomePanel panel in panels)
            {
                int n = panel.ring.Length;
                Vector3 c = panel.center;
                Vector3 normal = Vector3.Cross(panel.ring[1] - panel.ring[0], panel.ring[2] - panel.ring[0]).normalized;
                Vector3 t1 = Vector3.Cross(Vector3.up, normal);
                if (t1.sqrMagnitude < 1e-4f)
                    t1 = Vector3.Cross(Vector3.right, normal);
                t1.Normalize();
                Vector3 t2 = Vector3.Cross(normal, t1);

                float maxR = 0.001f;
                for (int i = 0; i < n; i++)
                    maxR = Mathf.Max(maxR, (panel.ring[i] - c).magnitude);

                int start = data.vertices.Count;
                data.vertices.Add(c);
                data.normals.Add(normal);
                data.uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = c + (panel.ring[i] - c) * inset;
                    Vector3 rel = p - c;
                    data.vertices.Add(p);
                    data.normals.Add(normal);
                    data.uvs.Add(new Vector2(
                        0.5f + Vector3.Dot(rel, t1) / (2f * maxR),
                        0.5f + Vector3.Dot(rel, t2) / (2f * maxR)));
                }

                for (int i = 0; i < n; i++)
                {
                    data.triangles.Add(start);
                    data.triangles.Add(start + 1 + i);
                    data.triangles.Add(start + 1 + (i + 1) % n);
                }
            }

            return data.ToMesh("DomeGlass");
        }

        /// <summary>Double-sided collision shell (both windings) so rays and the character controller hit it from either side.</summary>
        public static Mesh BuildShellColliderMesh(IList<DomePanel> panels)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (DomePanel panel in panels)
            {
                int n = panel.ring.Length;
                int start = verts.Count;
                verts.Add(panel.center);
                for (int i = 0; i < n; i++)
                    verts.Add(panel.ring[i]);

                for (int i = 0; i < n; i++)
                {
                    int a = start;
                    int b = start + 1 + i;
                    int c = start + 1 + (i + 1) % n;
                    tris.Add(a);
                    tris.Add(b);
                    tris.Add(c);
                    tris.Add(a);
                    tris.Add(c);
                    tris.Add(b);
                }
            }

            var mesh = new Mesh { name = "DomeShellCollider" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Thin box struts along every unique panel edge (the emissive hex trim).</summary>
        public static Mesh BuildStrutMesh(IList<DomePanel> panels, float thickness)
        {
            var data = new MeshData();
            var seen = new HashSet<long>();
            float half = thickness * 0.5f;
            foreach (DomePanel panel in panels)
            {
                int n = panel.ring.Length;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    int idA = Mathf.Min(panel.ringIds[i], panel.ringIds[j]);
                    int idB = Mathf.Max(panel.ringIds[i], panel.ringIds[j]);
                    long key = (long)idA * 1000003L + idB;
                    if (!seen.Add(key))
                        continue;

                    AddStrut(data, panel.ring[i], panel.ring[j], half);
                }
            }

            return data.ToMesh("DomeStruts");
        }

        public static int CountUniqueEdges(IList<DomePanel> panels)
        {
            var seen = new HashSet<long>();
            foreach (DomePanel panel in panels)
            {
                int n = panel.ring.Length;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    int idA = Mathf.Min(panel.ringIds[i], panel.ringIds[j]);
                    int idB = Mathf.Max(panel.ringIds[i], panel.ringIds[j]);
                    seen.Add((long)idA * 1000003L + idB);
                }
            }

            return seen.Count;
        }

        private static void AddStrut(MeshData data, Vector3 p, Vector3 q, float half)
        {
            Vector3 dir = q - p;
            if (dir.sqrMagnitude < 1e-6f)
                return;

            dir.Normalize();
            Vector3 mid = (p + q) * 0.5f;
            Vector3 outward = mid.normalized;
            Vector3 side = Vector3.Cross(dir, outward);
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.Cross(dir, Vector3.up);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, dir).normalized;

            Vector3 s = side * half;
            Vector3 u = up * half;
            Vector3 a0 = p - s - u;
            Vector3 a1 = p + s - u;
            Vector3 a2 = p + s + u;
            Vector3 a3 = p - s + u;
            Vector3 b0 = q - s - u;
            Vector3 b1 = q + s - u;
            Vector3 b2 = q + s + u;
            Vector3 b3 = q - s + u;

            data.AddQuad(a0, a1, b1, b0, mid);
            data.AddQuad(a1, a2, b2, b1, mid);
            data.AddQuad(a2, a3, b3, b2, mid);
            data.AddQuad(a3, a0, b0, b3, mid);
            data.AddQuad(a0, a1, a2, a3, mid);
            data.AddQuad(b0, b1, b2, b3, mid);
        }

        /// <summary>Flat-shaded regular prism with its base on y = 0 and unit radius/height (scale it in the scene).</summary>
        public static Mesh BuildPrism(int sides, float startAngleDeg)
        {
            int n = Mathf.Max(3, sides);
            var data = new MeshData();
            var bottom = new Vector3[n];
            var top = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (startAngleDeg + 360f * i / n) * Mathf.Deg2Rad;
                bottom[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                top[i] = new Vector3(Mathf.Cos(a), 1f, Mathf.Sin(a));
            }

            Vector3 centre = new Vector3(0f, 0.5f, 0f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                data.AddQuad(bottom[i], bottom[j], top[j], top[i], centre);
                data.AddTri(top[0], top[i], top[j], centre);
                data.AddTri(bottom[0], bottom[i], bottom[j], centre);
            }

            return data.ToMesh("DomePrism" + n);
        }

        #endregion
    }
}
