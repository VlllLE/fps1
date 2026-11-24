using NUnit.Framework;
using UnityEngine;

public class KarambitMeshBuilderTests
{
    private Mesh mesh;

    [TearDown]
    public void TearDown()
    {
        if (mesh != null)
            Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Blade_IsCurvedForwardAndDownward()
    {
        mesh = KarambitMeshBuilder.BuildBlade();

        AssertValidMesh(mesh);
        Bounds bounds = mesh.bounds;
        Assert.That(bounds.max.z, Is.GreaterThan(0.09f), "Blade should reach forward of the handle.");
        Assert.That(bounds.min.y, Is.LessThan(-0.05f), "Blade should hook downward.");
        Assert.That(bounds.size.x, Is.LessThan(0.02f), "Blade should be thin.");
    }

    [Test]
    public void BladeSection_TipIsNarrowerThanBase_AndSpineSweepsAhead()
    {
        KarambitMeshBuilder.BladeSection(0f, out Vector2 baseSpine, out Vector2 baseEdge, out float baseThickness);
        KarambitMeshBuilder.BladeSection(1f, out Vector2 tipSpine, out Vector2 tipEdge, out float tipThickness);

        Assert.That((baseSpine - baseEdge).magnitude, Is.GreaterThan((tipSpine - tipEdge).magnitude * 5f));
        Assert.That(tipSpine.y, Is.GreaterThan(baseSpine.y), "Spine Z should advance toward the tip.");
        Assert.That(tipSpine.x, Is.LessThan(baseSpine.x), "Tip should sit lower than the base.");
        Assert.That(tipThickness, Is.LessThan(baseThickness));
    }

    [Test]
    public void Handle_IsClosedAndOutwardFacing()
    {
        mesh = KarambitMeshBuilder.BuildHandle();

        AssertValidMesh(mesh);
        Assert.That(SignedVolume(mesh), Is.GreaterThan(0.00001f));
        Assert.That(mesh.bounds.min.z, Is.EqualTo(-KarambitMeshBuilder.HandleLength).Within(0.001f));
        Assert.That(mesh.bounds.max.z, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void Ring_IsClosedTorusWithExpectedVolume()
    {
        mesh = KarambitMeshBuilder.BuildRing();

        AssertValidMesh(mesh);
        float r = KarambitMeshBuilder.RingMinorRadius;
        float major = KarambitMeshBuilder.RingMajorRadius;
        float expected = 2f * Mathf.PI * Mathf.PI * major * r * r;
        Assert.That(SignedVolume(mesh), Is.EqualTo(expected).Within(expected * 0.12f));
    }

    [Test]
    public void Bolster_IsOutwardFacingBox()
    {
        mesh = KarambitMeshBuilder.BuildBolster();

        AssertValidMesh(mesh);
        Assert.That(SignedVolume(mesh), Is.EqualTo(0.016f * 0.042f * 0.022f).Within(0.000001f));
    }

    [Test]
    public void Model_BuildsMeshesIntoFilters_AndCleansUp()
    {
        var root = new GameObject("KarambitModelTest");
        try
        {
            MeshFilter blade = AddPart(root, "Blade");
            MeshFilter handle = AddPart(root, "Handle");
            MeshFilter bolster = AddPart(root, "Bolster");
            MeshFilter ring = AddPart(root, "Ring");
            KarambitModel model = root.AddComponent<KarambitModel>();
            model.SetFilters(blade, handle, bolster, ring);

            model.Build();

            Assert.That(model.IsBuilt, Is.True);
            Assert.That(blade.sharedMesh, Is.SameAs(model.BladeMesh));
            Assert.That(handle.sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(bolster.sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(ring.sharedMesh.vertexCount, Is.GreaterThan(0));

            Mesh first = blade.sharedMesh;
            model.Build();
            Assert.That(blade.sharedMesh, Is.SameAs(first), "Build is idempotent.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static MeshFilter AddPart(GameObject root, string name)
    {
        var part = new GameObject(name);
        part.transform.SetParent(root.transform, false);
        part.AddComponent<MeshRenderer>();
        return part.AddComponent<MeshFilter>();
    }

    private static void AssertValidMesh(Mesh m)
    {
        Assert.That(m.vertexCount, Is.GreaterThan(8));
        Assert.That(m.triangles.Length % 3, Is.Zero);
        Assert.That(m.triangles.Length, Is.GreaterThan(0));

        foreach (int index in m.triangles)
            Assert.That(index, Is.InRange(0, m.vertexCount - 1));

        foreach (Vector3 v in m.vertices)
        {
            Assert.That(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), Is.False);
            Assert.That(v.magnitude, Is.LessThan(0.5f), "Knife geometry should be sized in metres.");
        }

        foreach (Vector3 n in m.normals)
            Assert.That(float.IsNaN(n.x) || float.IsNaN(n.y) || float.IsNaN(n.z), Is.False);
    }

    private static float SignedVolume(Mesh m)
    {
        Vector3[] vertices = m.vertices;
        int[] triangles = m.triangles;
        float volume = 0f;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];
            volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
        }
        return volume;
    }
}
