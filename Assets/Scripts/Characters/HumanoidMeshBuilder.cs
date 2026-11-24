using System.Collections.Generic;
using UnityEngine;

/// <summary>Material slot a procedural humanoid surface is assigned to.</summary>
public enum HumanoidSlot
{
    Suit = 0,
    SuitDark = 1,
    Skin = 2,
    Accent = 3,
    Visor = 4
}

/// <summary>
/// One elliptical cross-section of a lofted surface. The ring lies in the XZ plane at height
/// <see cref="y"/> and is centred on (<see cref="cx"/>, <see cref="cz"/>).
/// </summary>
public struct LoftRing
{
    public float y;
    public float rx;
    public float rz;
    public float cx;
    public float cz;

    public LoftRing(float y, float rx, float rz, float cx = 0f, float cz = 0f)
    {
        this.y = y;
        this.rx = rx;
        this.rz = rz;
        this.cx = cx;
        this.cz = cz;
    }

    public LoftRing(float y, float radius) : this(y, radius, radius)
    {
    }
}

/// <summary>
/// Engine-independent (no native calls) procedural geometry accumulator used to model the
/// stylised humanoid. Geometry is plain arrays so it can be validated in unit tests without
/// creating <see cref="Mesh"/> objects. Winding is Unity's clockwise-front convention.
/// </summary>
public sealed class HumanoidMeshBuilder
{
    public const int SlotCount = 5;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int>[] triangles = new List<int>[SlotCount];

    /// <summary>Applied to everything added (typically a uniform scale).</summary>
    public Matrix4x4 Root = Matrix4x4.identity;

    public HumanoidMeshBuilder()
    {
        for (int i = 0; i < SlotCount; i++)
            triangles[i] = new List<int>();
    }

    public IReadOnlyList<Vector3> Vertices => vertices;
    public IReadOnlyList<Vector3> Normals => normals;
    public IReadOnlyList<Vector2> Uvs => uvs;
    public int VertexCount => vertices.Count;

    public IReadOnlyList<int> GetTriangles(HumanoidSlot slot)
    {
        return triangles[(int)slot];
    }

    public int TriangleCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < SlotCount; i++)
                count += triangles[i].Count / 3;
            return count;
        }
    }

    public Bounds CalculateBounds()
    {
        if (vertices.Count == 0)
            return new Bounds(Vector3.zero, Vector3.zero);
        var bounds = new Bounds(vertices[0], Vector3.zero);
        for (int i = 1; i < vertices.Count; i++)
            bounds.Encapsulate(vertices[i]);
        return bounds;
    }

    public void AddLoft(
        HumanoidSlot slot,
        IList<LoftRing> rings,
        int segments,
        bool capBottom = false,
        bool capTop = false)
    {
        AddLoft(slot, rings, segments, Matrix4x4.identity, capBottom, capTop);
    }

    /// <summary>
    /// Sweeps elliptical rings (ordered bottom to top) into a closed-around surface with analytic
    /// smooth normals. Rings should end in a near-zero radius (a pole) or request a flat cap.
    /// </summary>
    public void AddLoft(
        HumanoidSlot slot,
        IList<LoftRing> rings,
        int segments,
        Matrix4x4 local,
        bool capBottom,
        bool capTop)
    {
        int ringCount = rings != null ? rings.Count : 0;
        if (ringCount < 2 || segments < 3)
            return;

        Matrix4x4 xf = Root * local;
        bool flip = Determinant3x3(xf) < 0f;
        int stride = segments + 1;
        int baseIndex = vertices.Count;
        var positions = new Vector3[ringCount * stride];

        for (int i = 0; i < ringCount; i++)
        {
            LoftRing r = rings[i];
            for (int j = 0; j <= segments; j++)
            {
                float a = j / (float)segments * Mathf.PI * 2f;
                positions[i * stride + j] = new Vector3(
                    r.cx + r.rx * Mathf.Cos(a),
                    r.y,
                    r.cz + r.rz * Mathf.Sin(a));
            }
        }

        for (int i = 0; i < ringCount; i++)
        {
            int im = Mathf.Max(i - 1, 0);
            int ip = Mathf.Min(i + 1, ringCount - 1);
            for (int j = 0; j <= segments; j++)
            {
                int jm = j == 0 ? segments - 1 : j - 1;
                int jp = j == segments ? 1 : j + 1;
                Vector3 around = positions[i * stride + jp] - positions[i * stride + jm];
                Vector3 profile = positions[ip * stride + j] - positions[im * stride + j];
                Vector3 n = Vector3.Cross(profile, around);
                float lengthSq = n.sqrMagnitude;
                n = lengthSq < 1e-24f
                    ? (i == 0 ? Vector3.down : Vector3.up)
                    : n / Mathf.Sqrt(lengthSq);

                vertices.Add(xf.MultiplyPoint3x4(positions[i * stride + j]));
                normals.Add(xf.MultiplyVector(n).normalized);
                uvs.Add(new Vector2(j / (float)segments, i / (float)(ringCount - 1)));
            }
        }

        List<int> tris = triangles[(int)slot];
        for (int i = 0; i < ringCount - 1; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int a = baseIndex + i * stride + j;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                AddTriangle(tris, a, c, d, flip);
                AddTriangle(tris, a, d, b, flip);
            }
        }

        if (capBottom)
            AddCap(slot, rings[0], segments, xf, flip, false);
        if (capTop)
            AddCap(slot, rings[ringCount - 1], segments, xf, flip, true);
    }

    /// <summary>
    /// Adds (part of) an ellipsoid. Latitude angles are measured from the top pole (0) to the
    /// bottom pole (180), so a dome is (phiBottomDeg &lt; 180).
    /// </summary>
    public void AddEllipsoid(
        HumanoidSlot slot,
        Vector3 center,
        Vector3 radii,
        int latitudeSegments,
        int segments,
        float phiBottomDeg = 180f,
        float phiTopDeg = 0f,
        Matrix4x4? local = null)
    {
        latitudeSegments = Mathf.Max(2, latitudeSegments);
        var rings = new LoftRing[latitudeSegments + 1];
        for (int k = 0; k <= latitudeSegments; k++)
        {
            float phi = Mathf.Lerp(phiBottomDeg, phiTopDeg, k / (float)latitudeSegments) * Mathf.Deg2Rad;
            float ring = Mathf.Max(Mathf.Sin(phi), 0.02f);
            rings[k] = new LoftRing(
                center.y + radii.y * Mathf.Cos(phi),
                radii.x * ring,
                radii.z * ring,
                center.x,
                center.z);
        }

        bool dome = phiBottomDeg < 179f;
        AddLoft(slot, rings, segments, local ?? Matrix4x4.identity, dome, false);
    }

    public void AddBox(HumanoidSlot slot, Vector3 center, Vector3 size, Matrix4x4? local = null)
    {
        Matrix4x4 xf = Root * (local ?? Matrix4x4.identity);
        bool flip = Determinant3x3(xf) < 0f;
        Vector3 half = size * 0.5f;
        List<int> tris = triangles[(int)slot];

        AddBoxFace(tris, xf, flip, center, Vector3.right, Vector3.up, Vector3.forward, half.x, half.y, half.z);
        AddBoxFace(tris, xf, flip, center, Vector3.left, Vector3.up, Vector3.back, half.x, half.y, half.z);
        AddBoxFace(tris, xf, flip, center, Vector3.up, Vector3.forward, Vector3.right, half.y, half.z, half.x);
        AddBoxFace(tris, xf, flip, center, Vector3.down, Vector3.back, Vector3.right, half.y, half.z, half.x);
        AddBoxFace(tris, xf, flip, center, Vector3.forward, Vector3.up, Vector3.left, half.z, half.y, half.x);
        AddBoxFace(tris, xf, flip, center, Vector3.back, Vector3.up, Vector3.right, half.z, half.y, half.x);
    }

    public Mesh ToMesh(string meshName, out HumanoidSlot[] usedSlots)
    {
        var used = new List<HumanoidSlot>();
        for (int i = 0; i < SlotCount; i++)
        {
            if (triangles[i].Count > 0)
                used.Add((HumanoidSlot)i);
        }

        usedSlots = used.ToArray();
        var mesh = new Mesh { name = meshName };
        if (vertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = Mathf.Max(1, used.Count);
        for (int s = 0; s < used.Count; s++)
            mesh.SetTriangles(triangles[(int)used[s]], s, false);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void AddCap(
        HumanoidSlot slot,
        LoftRing ring,
        int segments,
        Matrix4x4 xf,
        bool flip,
        bool top)
    {
        int baseIndex = vertices.Count;
        Vector3 normal = xf.MultiplyVector(top ? Vector3.up : Vector3.down).normalized;
        vertices.Add(xf.MultiplyPoint3x4(new Vector3(ring.cx, ring.y, ring.cz)));
        normals.Add(normal);
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int j = 0; j <= segments; j++)
        {
            float a = j / (float)segments * Mathf.PI * 2f;
            vertices.Add(xf.MultiplyPoint3x4(new Vector3(
                ring.cx + ring.rx * Mathf.Cos(a),
                ring.y,
                ring.cz + ring.rz * Mathf.Sin(a))));
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
        }

        List<int> tris = triangles[(int)slot];
        for (int j = 0; j < segments; j++)
        {
            int ringA = baseIndex + 1 + j;
            int ringB = ringA + 1;
            if (top)
                AddTriangle(tris, baseIndex, ringB, ringA, flip);
            else
                AddTriangle(tris, baseIndex, ringA, ringB, flip);
        }
    }

    private void AddBoxFace(
        List<int> tris,
        Matrix4x4 xf,
        bool flip,
        Vector3 center,
        Vector3 normal,
        Vector3 up,
        Vector3 right,
        float halfAlongNormal,
        float halfUp,
        float halfRight)
    {
        Vector3 faceCenter = center + normal * halfAlongNormal;
        Vector3 worldNormal = xf.MultiplyVector(normal).normalized;
        int baseIndex = vertices.Count;
        Vector3[] corners =
        {
            faceCenter - right * halfRight - up * halfUp,
            faceCenter - right * halfRight + up * halfUp,
            faceCenter + right * halfRight + up * halfUp,
            faceCenter + right * halfRight - up * halfUp
        };
        Vector2[] cornerUv = { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        for (int i = 0; i < 4; i++)
        {
            vertices.Add(xf.MultiplyPoint3x4(corners[i]));
            normals.Add(worldNormal);
            uvs.Add(cornerUv[i]);
        }

        AddTriangle(tris, baseIndex, baseIndex + 1, baseIndex + 2, flip);
        AddTriangle(tris, baseIndex, baseIndex + 2, baseIndex + 3, flip);
    }

    private static void AddTriangle(List<int> tris, int a, int b, int c, bool flip)
    {
        tris.Add(a);
        if (flip)
        {
            tris.Add(c);
            tris.Add(b);
        }
        else
        {
            tris.Add(b);
            tris.Add(c);
        }
    }

    private static float Determinant3x3(Matrix4x4 m)
    {
        Vector3 c0 = new Vector3(m.m00, m.m10, m.m20);
        Vector3 c1 = new Vector3(m.m01, m.m11, m.m21);
        Vector3 c2 = new Vector3(m.m02, m.m12, m.m22);
        return Vector3.Dot(Vector3.Cross(c0, c1), c2);
    }
}
