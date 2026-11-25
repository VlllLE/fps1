using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Geometry checks for the primitive sniper prefabs: scopes must sit on the rail and the bolt sniper's
/// bipod must hang from the fore-end instead of floating. Distances are measured between world-space
/// AABBs of each primitive mesh.
/// </summary>
public class SniperMountingTests
{
    private const string BoltPath = "Assets/Weapons/Prefabs/BoltActionSniper.prefab";
    private const string DmrPath = "Assets/Weapons/Prefabs/DMRSniper.prefab";
    private const float Tolerance = 0.002f;

    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [TestCase(BoltPath)]
    [TestCase(DmrPath)]
    public void Scope_RestsOnRail_ThroughRings(string path)
    {
        GameObject gun = Spawn(path);
        Bounds receiver = BoundsOf(gun, "GunBody");
        Bounds rail = BoundsOf(gun, "Rail");
        Bounds body = BoundsOf(gun, "ScopeBody");

        Assert.That(Gap(rail, receiver), Is.LessThanOrEqualTo(Tolerance), "rail floats above receiver");
        foreach (string ringName in new[] { "ScopeRingFront", "ScopeRingRear" })
        {
            Bounds ring = BoundsOf(gun, ringName);
            Assert.That(Gap(ring, rail), Is.LessThanOrEqualTo(Tolerance), ringName + " does not touch the rail");
            Assert.That(Gap(ring, body), Is.LessThanOrEqualTo(Tolerance), ringName + " does not hold the scope tube");
        }
        Assert.That(body.min.y, Is.GreaterThanOrEqualTo(rail.max.y - Tolerance),
            "scope tube should sit above the rail, not inside the receiver");
        Assert.That(body.min.y - rail.max.y, Is.LessThanOrEqualTo(0.02f),
            "scope tube is too far above the rail");
    }

    [TestCase(BoltPath)]
    [TestCase(DmrPath)]
    public void Scope_PivotIsOnBoreAxisAndParallelToBarrel(string path)
    {
        GameObject gun = Spawn(path);
        Transform scope = Find(gun, "Scope");
        Transform barrel = Find(gun, "Barrel");

        Assert.That(scope.localPosition.x, Is.EqualTo(0f).Within(1e-4f), "scope must be centred on the receiver");
        Assert.That(Vector3.Angle(scope.forward, barrel.forward), Is.LessThan(0.1f));
        Assert.That(scope.position.y, Is.GreaterThan(barrel.position.y), "scope axis sits above the bore");
        foreach (string partName in new[] { "ScopeBody", "ScopeLensFront", "ScopeRingFront", "ScopeRingRear" })
        {
            Transform part = Find(gun, partName);
            Assert.That(part.parent, Is.SameAs(scope), partName + " should be a child of Scope");
            Assert.That(part.localPosition.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(part.localPosition.y, Is.EqualTo(0f).Within(1e-4f));
        }
        Assert.That(Find(gun, "ScopeLensFront").localPosition.z, Is.GreaterThan(0f), "front lens faces the muzzle");
    }

    [Test]
    public void BoltSniper_BipodHangsFromForend()
    {
        GameObject gun = Spawn(BoltPath);
        Bounds receiver = BoundsOf(gun, "GunBody");
        Bounds barrel = BoundsOf(gun, "Barrel");
        Bounds forend = BoundsOf(gun, "Forend");
        Bounds mount = BoundsOf(gun, "BipodMount");

        Assert.That(Gap(forend, barrel), Is.LessThanOrEqualTo(Tolerance), "fore-end must hold the barrel");
        Assert.That(Gap(forend, receiver), Is.LessThanOrEqualTo(Tolerance), "fore-end must meet the receiver");
        Assert.That(Gap(mount, forend), Is.LessThanOrEqualTo(Tolerance), "bipod mount must touch the fore-end");

        Transform bipod = Find(gun, "Bipod");
        foreach (string legName in new[] { "BipodLeft", "BipodRight" })
        {
            Transform leg = Find(gun, legName);
            Assert.That(leg.parent, Is.SameAs(bipod), legName + " should be a child of Bipod");

            Vector3 top = leg.TransformPoint(new Vector3(0f, 0.5f, 0f));
            Vector3 bottom = leg.TransformPoint(new Vector3(0f, -0.5f, 0f));
            Assert.That(Mathf.Sqrt(mount.SqrDistance(top)), Is.LessThanOrEqualTo(Tolerance),
                legName + " top end is detached from the bipod mount");
            Assert.That(bottom.y, Is.LessThan(mount.min.y - 0.05f), legName + " should reach below the mount");
        }

        Assert.That(Find(gun, "BipodLeft").position.x, Is.LessThan(0f));
        Assert.That(Find(gun, "BipodRight").position.x, Is.GreaterThan(0f));
    }

    [TestCase(BoltPath)]
    [TestCase(DmrPath)]
    public void EveryMeshPart_IsConnectedToTheReceiver(string path)
    {
        GameObject gun = Spawn(path);
        var renderers = new List<MeshRenderer>(gun.GetComponentsInChildren<MeshRenderer>(true));
        var bounds = renderers.ConvertAll(r => WorldBounds(r));
        int receiverIndex = renderers.FindIndex(r => r.name == "GunBody");
        Assert.That(receiverIndex, Is.GreaterThanOrEqualTo(0));

        var reached = new HashSet<int> { receiverIndex };
        var queue = new Queue<int>();
        queue.Enqueue(receiverIndex);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            for (int i = 0; i < bounds.Count; i++)
            {
                if (!reached.Contains(i) && Gap(bounds[current], bounds[i]) <= Tolerance)
                {
                    reached.Add(i);
                    queue.Enqueue(i);
                }
            }
        }

        var floating = new List<string>();
        for (int i = 0; i < renderers.Count; i++)
        {
            if (!reached.Contains(i))
                floating.Add(renderers[i].name);
        }
        Assert.That(floating, Is.Empty, "detached parts: " + string.Join(", ", floating));
    }

    [TestCase(BoltPath, 0.66f)]
    [TestCase(DmrPath, 0.58f)]
    public void FirePoints_StayOnBoreAtMuzzle(string path, float muzzleZ)
    {
        GameObject gun = Spawn(path);
        Transform barrel = Find(gun, "Barrel");
        foreach (string pointName in new[] { "MuzzlePoint", "FirePoint" })
        {
            Transform point = Find(gun, pointName);
            Assert.That(point.parent, Is.SameAs(gun.transform));
            Assert.That(point.localPosition.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(point.localPosition.y, Is.EqualTo(barrel.localPosition.y).Within(1e-4f));
            Assert.That(point.localPosition.z, Is.EqualTo(muzzleZ).Within(1e-4f));
        }
    }

    private GameObject Spawn(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, "missing prefab " + path);
        GameObject instance = Object.Instantiate(prefab);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        created.Add(instance);
        return instance;
    }

    private static Transform Find(GameObject root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }
        Assert.Fail("missing child '" + name + "' on " + root.name);
        return null;
    }

    private static Bounds BoundsOf(GameObject root, string name)
    {
        return WorldBounds(Find(root, name).GetComponent<MeshRenderer>());
    }

    private static Bounds WorldBounds(MeshRenderer renderer)
    {
        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh, Is.Not.Null, renderer.name + " has no mesh");
        Bounds local = mesh.bounds;
        Matrix4x4 m = renderer.transform.localToWorldMatrix;
        Bounds world = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            world.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return world;
    }

    private static float Gap(Bounds a, Bounds b)
    {
        float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
        float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
        float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
