using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure-code geometry for a karambit knife: curved blade, ergonomic handle, bolster and
/// pommel ring. Units are metres, +Z is the blade direction (handle at -Z), +Y is up and
/// the blade curves toward -Y. All meshes are built without any DCC asset or ProBuilder.
/// </summary>
public static class KarambitMeshBuilder
{
    public const float HandleLength = 0.115f;
    public const float BladeBaseY = 0.017f;
    public const float BladeRadius = 0.105f;
    public const float BladeSweepRadians = 1.5f;
    public const float BladeBaseWidth = 0.034f;
    public const float BladeHalfThickness = 0.0032f;
    public const float RingMajorRadius = 0.026f;
    public const float RingMinorRadius = 0.0058f;

    public static Mesh BuildBlade(int segments = 28)
    {
        segments = Mathf.Max(4, segments);
        var b = new MeshBuilder();

        var leftIdx = new List<int>(); // pairs: spineLeft, edge
        var rightIdx = new List<int>(); // pairs: spineRight, edge
        var topIdx = new List<int>(); // pairs: spineLeft, spineRight
        var topNormals = new List<Vector3>();

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            BladeSection(t, out Vector2 spineYZ, out Vector2 edgeYZ, out float ht);
            float theta = t * BladeSweepRadians;

            leftIdx.Add(b.AddVertex(new Vector3(-ht, spineYZ.x, spineYZ.y), new Vector2(t, 1f)));
            leftIdx.Add(b.AddVertex(new Vector3(0f, edgeYZ.x, edgeYZ.y), new Vector2(t, 0f)));
            rightIdx.Add(b.AddVertex(new Vector3(ht, spineYZ.x, spineYZ.y), new Vector2(t, 1f)));
            rightIdx.Add(b.AddVertex(new Vector3(0f, edgeYZ.x, edgeYZ.y), new Vector2(t, 0f)));
            topIdx.Add(b.AddVertex(new Vector3(-ht, spineYZ.x, spineYZ.y), new Vector2(t, 0f)));
            topIdx.Add(b.AddVertex(new Vector3(ht, spineYZ.x, spineYZ.y), new Vector2(t, 1f)));
            topNormals.Add(new Vector3(0f, Mathf.Cos(theta), Mathf.Sin(theta)));
        }

        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            int c = (i + 1) * 2;
            b.AddOrientedQuad(leftIdx[a], leftIdx[a + 1], leftIdx[c], leftIdx[c + 1], Vector3.left);
            b.AddOrientedQuad(rightIdx[a], rightIdx[a + 1], rightIdx[c], rightIdx[c + 1], Vector3.right);
            b.AddOrientedQuad(topIdx[a], topIdx[a + 1], topIdx[c], topIdx[c + 1], topNormals[i]);
        }

        // Rear cap where the blade meets the bolster.
        BladeSection(0f, out Vector2 s0, out Vector2 e0, out float ht0);
        int c0 = b.AddVertex(new Vector3(-ht0, s0.x, s0.y), Vector2.zero);
        int c1 = b.AddVertex(new Vector3(ht0, s0.x, s0.y), Vector2.right);
        int c2 = b.AddVertex(new Vector3(0f, e0.x, e0.y), Vector2.up);
        b.AddOrientedTriangle(c0, c1, c2, Vector3.back);

        return b.ToMesh("KarambitBlade");
    }

    public static Mesh BuildHandle(int sections = 9, int sides = 20)
    {
        sections = Mathf.Max(3, sections);
        sides = Mathf.Max(6, sides);
        var b = new MeshBuilder();

        float[] grip = { 0.86f, 1.00f, 0.93f, 1.00f, 0.93f, 1.00f, 0.93f, 1.00f, 0.88f };
        var rings = new List<int[]>(sections);

        for (int s = 0; s < sections; s++)
        {
            float u = s / (float)(sections - 1);
            float z = Mathf.Lerp(-HandleLength, 0f, u);
            float swell = 1f + 0.12f * Mathf.Sin(u * Mathf.PI);
            float taper = Mathf.Lerp(0.92f, 1.0f, u);
            float mult = grip[Mathf.Min(s, grip.Length - 1)] * swell * taper;
            float halfWidth = 0.0145f * mult;
            float halfHeight = 0.0185f * mult;

            var ring = new int[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = k / (float)sides * Mathf.PI * 2f;
                float cs = Mathf.Cos(a);
                float sn = Mathf.Sin(a);
                float x = halfWidth * Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), 0.62f);
                float y = halfHeight * Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 0.62f);
                ring[k] = b.AddVertex(new Vector3(x, y, z), new Vector2(k / (float)sides, u));
            }
            rings.Add(ring);
        }

        for (int s = 0; s < sections - 1; s++)
        {
            for (int k = 0; k < sides; k++)
            {
                int k2 = (k + 1) % sides;
                int a0 = rings[s][k];
                int a1 = rings[s][k2];
                int b0 = rings[s + 1][k];
                int b1 = rings[s + 1][k2];
                Vector3 centre = (b.Position(a0) + b.Position(a1) + b.Position(b0) + b.Position(b1)) * 0.25f;
                Vector3 outward = new Vector3(centre.x, centre.y, 0f);
                b.AddOrientedQuad(a0, b0, a1, b1, outward);
            }
        }

        AddCap(b, rings[0], -HandleLength, Vector3.back);
        AddCap(b, rings[sections - 1], 0f, Vector3.forward);

        return b.ToMesh("KarambitHandle");
    }

    public static Mesh BuildBolster()
    {
        var b = new MeshBuilder();
        b.AddBox(new Vector3(0f, 0.001f, 0.006f), new Vector3(0.016f, 0.042f, 0.022f));
        return b.ToMesh("KarambitBolster");
    }

    public static Mesh BuildRing(int majorSegments = 32, int minorSegments = 10)
    {
        majorSegments = Mathf.Max(8, majorSegments);
        minorSegments = Mathf.Max(4, minorSegments);
        var b = new MeshBuilder();

        Vector3 centre = RingCentre;
        var grid = new int[majorSegments + 1, minorSegments + 1];

        for (int i = 0; i <= majorSegments; i++)
        {
            float u = i / (float)majorSegments * Mathf.PI * 2f;
            Vector3 m = new Vector3(0f, Mathf.Cos(u), Mathf.Sin(u));
            for (int j = 0; j <= minorSegments; j++)
            {
                float v = j / (float)minorSegments * Mathf.PI * 2f;
                Vector3 pos = centre
                    + m * (RingMajorRadius + RingMinorRadius * Mathf.Cos(v))
                    + Vector3.right * (RingMinorRadius * Mathf.Sin(v));
                grid[i, j] = b.AddVertex(pos, new Vector2(i / (float)majorSegments, j / (float)minorSegments));
            }
        }

        for (int i = 0; i < majorSegments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                int a = grid[i, j];
                int c = grid[i + 1, j];
                int d = grid[i, j + 1];
                int e = grid[i + 1, j + 1];
                Vector3 mid = (b.Position(a) + b.Position(c) + b.Position(d) + b.Position(e)) * 0.25f;
                float u = (i + 0.5f) / majorSegments * Mathf.PI * 2f;
                Vector3 m = new Vector3(0f, Mathf.Cos(u), Mathf.Sin(u));
                Vector3 outward = mid - (centre + m * RingMajorRadius);
                b.AddOrientedQuad(a, d, c, e, outward);
            }
        }

        return b.ToMesh("KarambitRing");
    }

    /// <summary>World-local centre of the pommel ring.</summary>
    public static Vector3 RingCentre => new Vector3(0f, -0.004f, -HandleLength - RingMajorRadius + 0.006f);

    /// <summary>
    /// Evaluates the blade centre-line at <paramref name="t"/> (0 = base, 1 = tip) in (Y, Z).
    /// </summary>
    public static void BladeSection(float t, out Vector2 spineYZ, out Vector2 edgeYZ, out float halfThickness)
    {
        t = Mathf.Clamp01(t);
        float theta = t * BladeSweepRadians;
        Vector2 centre = new Vector2(BladeBaseY - BladeRadius, 0f);
        Vector2 radial = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
        spineYZ = centre + radial * BladeRadius;

        float width = BladeBaseWidth * Mathf.Max(0.04f, 1f - Mathf.Pow(t, 1.8f));
        edgeYZ = spineYZ - radial * width;
        halfThickness = BladeHalfThickness * Mathf.Lerp(1f, 0.45f, t);
    }

    private static void AddCap(MeshBuilder b, int[] ringIndices, float z, Vector3 normal)
    {
        var capRing = new int[ringIndices.Length];
        for (int k = 0; k < ringIndices.Length; k++)
            capRing[k] = b.AddVertex(b.Position(ringIndices[k]), Vector2.zero);

        int centre = b.AddVertex(new Vector3(0f, 0f, z), new Vector2(0.5f, 0.5f));
        for (int k = 0; k < capRing.Length; k++)
            b.AddOrientedTriangle(centre, capRing[k], capRing[(k + 1) % capRing.Length], normal);
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public int AddVertex(Vector3 position, Vector2 uv)
        {
            vertices.Add(position);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        public Vector3 Position(int index) => vertices[index];

        public void AddOrientedTriangle(int a, int b, int c, Vector3 desiredNormal)
        {
            Vector3 n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(n, desiredNormal) >= 0f)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        public void AddOrientedQuad(int a, int b, int c, int d, Vector3 desiredNormal)
        {
            // Strip order: a,b are one rung, c,d the next rung (a-c and b-d are the long edges).
            AddOrientedTriangle(a, b, c, desiredNormal);
            AddOrientedTriangle(b, d, c, desiredNormal);
        }

        public void AddBox(Vector3 centre, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            AddFace(centre, Vector3.right, Vector3.up * h.y, Vector3.forward * h.z, h.x);
            AddFace(centre, Vector3.left, Vector3.up * h.y, Vector3.forward * h.z, h.x);
            AddFace(centre, Vector3.up, Vector3.right * h.x, Vector3.forward * h.z, h.y);
            AddFace(centre, Vector3.down, Vector3.right * h.x, Vector3.forward * h.z, h.y);
            AddFace(centre, Vector3.forward, Vector3.right * h.x, Vector3.up * h.y, h.z);
            AddFace(centre, Vector3.back, Vector3.right * h.x, Vector3.up * h.y, h.z);
        }

        private void AddFace(Vector3 centre, Vector3 normal, Vector3 axisU, Vector3 axisV, float offset)
        {
            Vector3 c = centre + normal * offset;
            int a = AddVertex(c - axisU - axisV, new Vector2(0f, 0f));
            int b = AddVertex(c + axisU - axisV, new Vector2(1f, 0f));
            int d = AddVertex(c - axisU + axisV, new Vector2(0f, 1f));
            int e = AddVertex(c + axisU + axisV, new Vector2(1f, 1f));
            AddOrientedQuad(a, b, d, e, normal);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
