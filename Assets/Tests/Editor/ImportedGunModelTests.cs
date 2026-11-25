using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the Blender-generated Bolt-Action Sniper and DMR models (FBX in Assets/Weapons/Models) and their
/// prefab wiring: part names, triangle budget, UVs, muzzle position (validates the FBX axis/scale import),
/// scope-on-rail and bipod attachment measured on the imported mesh bounds, material remap and fallback behaviour.
/// </summary>
public class ImportedGunModelTests
{
    private const string BoltModel = "Assets/Weapons/Models/BoltActionSniper/BoltActionSniper.fbx";
    private const string DmrModel = "Assets/Weapons/Models/DMRSniper/DMRSniper.fbx";
    private const string BoltPrefab = "Assets/Weapons/Prefabs/BoltActionSniper.prefab";
    private const string DmrPrefab = "Assets/Weapons/Prefabs/DMRSniper.prefab";
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

    [TestCase(BoltModel, "Body", "Scope", "Magazine", "CheekRiser", "Bolt", "BipodLegLeft", "BipodLegRight")]
    [TestCase(DmrModel, "Body", "Scope", "Magazine", "CheekRiser", "Stock", "ChargingHandle")]
    public void Model_HasExpectedParts(string path, params string[] parts)
    {
        GameObject model = Spawn(path);
        foreach (string part in parts)
        {
            Transform t = FindChild(model.transform, part);
            Assert.That(t, Is.Not.Null, "missing part " + part);
            Assert.That(t.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null, part + " has no mesh");
        }
    }

    [TestCase(BoltModel)]
    [TestCase(DmrModel)]
    public void Model_TriangleBudget_AndUVs(string path)
    {
        GameObject model = Spawn(path);
        int triangles = 0;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            for (int s = 0; s < mesh.subMeshCount; s++)
                triangles += (int)(mesh.GetIndexCount(s) / 3);

            Vector2[] uv = mesh.uv;
            Assert.That(uv.Length, Is.EqualTo(mesh.vertexCount), filter.name + " is missing UVs");
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2 v in uv)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            Assert.That(max.x - min.x, Is.GreaterThan(0.05f), filter.name + " UVs are collapsed");
            Assert.That(max.y - min.y, Is.GreaterThan(0.05f), filter.name + " UVs are collapsed");
        }

        Assert.That(triangles, Is.InRange(20000, 45000), "triangle count outside the 20k-45k budget");
    }

    [TestCase(BoltModel, 0.66f)]
    [TestCase(DmrModel, 0.58f)]
    public void Model_MuzzleEndsAtTheFirePointDistance(string path, float muzzleZ)
    {
        GameObject model = Spawn(path);
        Bounds all = AllBounds(model);
        Assert.That(all.max.z, Is.EqualTo(muzzleZ).Within(0.005f), "muzzle not at the FirePoint z (axis/scale import?)");
        Assert.That(all.center.x, Is.EqualTo(0f).Within(0.01f), "model must be centred on x");
        Assert.That(all.size.z, Is.InRange(0.8f, 1.25f), "model length looks mis-scaled");
    }

    [TestCase(BoltModel)]
    [TestCase(DmrModel)]
    public void Scope_SitsOnTheRail_AndAboveTheBore(string path)
    {
        GameObject model = Spawn(path);
        Bounds body = BoundsOf(model, "Body");
        Bounds scope = BoundsOf(model, "Scope");

        Assert.That(Gap(scope, body), Is.LessThanOrEqualTo(Tolerance), "scope rings do not reach the rail");
        Assert.That(scope.center.x, Is.EqualTo(0f).Within(0.005f), "scope must be centred over the bore");
        Assert.That(scope.center.y, Is.GreaterThan(body.center.y), "scope sits above the receiver");
        Assert.That(scope.min.y, Is.LessThan(body.max.y), "ring feet must reach down into the rail");
        Assert.That(scope.min.y, Is.GreaterThan(body.max.y - 0.03f), "scope is sunk into the receiver");
    }

    [Test]
    public void BoltSniper_BipodLegsHangFromTheForend()
    {
        GameObject model = Spawn(BoltModel);
        Bounds body = BoundsOf(model, "Body");
        Bounds left = BoundsOf(model, "BipodLegLeft");
        Bounds right = BoundsOf(model, "BipodLegRight");

        Assert.That(Gap(left, body), Is.LessThanOrEqualTo(Tolerance), "left leg is detached");
        Assert.That(Gap(right, body), Is.LessThanOrEqualTo(Tolerance), "right leg is detached");
        Assert.That(left.center.x, Is.LessThan(0f));
        Assert.That(right.center.x, Is.GreaterThan(0f));
        Assert.That(left.min.y, Is.LessThan(body.min.y), "legs should reach below the rifle");
    }

    [Test]
    public void BoltSniper_BoltAndMagazineTouchTheReceiver()
    {
        GameObject model = Spawn(BoltModel);
        Bounds body = BoundsOf(model, "Body");
        Assert.That(Gap(BoundsOf(model, "Bolt"), body), Is.LessThanOrEqualTo(Tolerance));
        Assert.That(Gap(BoundsOf(model, "Magazine"), body), Is.LessThanOrEqualTo(Tolerance));
        Assert.That(Gap(BoundsOf(model, "CheekRiser"), body), Is.LessThanOrEqualTo(Tolerance));
    }

    [Test]
    public void Dmr_StockMagazineAndChargingHandleTouchTheReceiver()
    {
        GameObject model = Spawn(DmrModel);
        Bounds body = BoundsOf(model, "Body");
        foreach (string part in new[] { "Stock", "Magazine", "ChargingHandle", "CheekRiser" })
            Assert.That(Gap(BoundsOf(model, part), body), Is.LessThanOrEqualTo(Tolerance), part + " is detached");
    }

    [TestCase(BoltPrefab, 0.66f)]
    [TestCase(DmrPrefab, 0.58f)]
    public void Prefab_KeepsAnchorsAndFallback(string path, float muzzleZ)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        Transform root = prefab.transform;

        Transform fallback = root.Find("ProceduralFallback");
        Assert.That(fallback, Is.Not.Null, "primitive fallback group missing");
        Assert.That(fallback.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(fallback.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(10));
        Assert.That(root.Find("Model"), Is.Not.Null);

        foreach (string anchor in new[] { "MuzzlePoint", "FirePoint", "ShellEjectPoint" })
            Assert.That(root.Find(anchor), Is.Not.Null, anchor + " must stay a direct child of the weapon root");
        Assert.That(root.Find("MuzzlePoint").localPosition.z, Is.EqualTo(muzzleZ).Within(1e-4f));
        Assert.That(root.Find("FirePoint").localPosition.z, Is.EqualTo(muzzleZ).Within(1e-4f));

        Assert.That(prefab.GetComponent<WeaponController>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<WeaponSkinApplier>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<ImportedGunModel>(), Is.Not.Null);
    }

    [TestCase(BoltPrefab)]
    [TestCase(DmrPrefab)]
    public void Prefab_ImportedModel_ReplacesFallback_AndRemapsMaterials(string path)
    {
        GameObject gun = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        created.Add(gun);
        var model = gun.GetComponent<ImportedGunModel>();
        model.Build();

        Assert.That(model.UsesImportedModel, Is.True, "imported model did not instantiate (reference or import problem)");
        Assert.That(model.ProceduralFallback.activeSelf, Is.False);

        var names = new HashSet<string>();
        foreach (Renderer renderer in model.ImportedInstance.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
                names.Add(material.name);
        }
        Assert.That(names, Does.Contain("Mat_GunMetal"));
        Assert.That(names, Does.Contain("Mat_GunPolymer"));
        Assert.That(names, Does.Contain("Mat_SniperGlass"));
        Assert.That(names, Does.Contain("Mat_SniperAccent"));

        Assert.That(model.FindPart("Magazine"), Is.Not.Null);
        Assert.That(model.FindPart("Magazine").IsChildOf(model.ImportedInstance.transform), Is.True);
    }

    [Test]
    public void ImportedGunModel_WithoutModel_KeepsTheFallbackVisible()
    {
        var go = new GameObject("TestGun");
        created.Add(go);
        var fallback = new GameObject("ProceduralFallback");
        fallback.transform.SetParent(go.transform, false);
        var model = go.AddComponent<ImportedGunModel>();
        model.Configure(null, null, fallback);

        model.Build();

        Assert.That(model.IsBuilt, Is.True);
        Assert.That(model.UsesImportedModel, Is.False);
        Assert.That(fallback.activeSelf, Is.True);
    }

    [Test]
    public void ImportedGunModel_FailingModel_FallsBackWithoutThrowing()
    {
        var go = new GameObject("TestGun");
        created.Add(go);
        var fallback = new GameObject("ProceduralFallback");
        fallback.transform.SetParent(go.transform, false);
        var meshless = new GameObject("MeshlessModel");
        created.Add(meshless);
        var model = go.AddComponent<ImportedGunModel>();
        model.Configure(meshless, null, fallback);

        Assert.DoesNotThrow(() => model.Build());
        Assert.That(model.UsesImportedModel, Is.False, "a model without meshes must be rejected");
        Assert.That(fallback.activeSelf, Is.True);
    }

    private GameObject Spawn(string assetPath)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        Assert.That(asset, Is.Not.Null, "missing model " + assetPath);
        GameObject instance = Object.Instantiate(asset);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        created.Add(instance);
        return instance;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }

    private static Bounds BoundsOf(GameObject root, string name)
    {
        Transform t = FindChild(root.transform, name);
        Assert.That(t, Is.Not.Null, "missing part " + name);
        return WorldBounds(t.GetComponent<MeshFilter>());
    }

    private static Bounds AllBounds(GameObject root)
    {
        bool first = true;
        Bounds all = new Bounds();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            Bounds b = WorldBounds(filter);
            if (first)
                all = b;
            else
                all.Encapsulate(b);
            first = false;
        }
        return all;
    }

    private static Bounds WorldBounds(MeshFilter filter)
    {
        Bounds local = filter.sharedMesh.bounds;
        Matrix4x4 m = filter.transform.localToWorldMatrix;
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
