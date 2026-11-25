using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class AssaultWeaponModelTests
{
    private static readonly string[] Guns = { "M4A1", "SCAR", "UMP" };

    private GameObject instance;

    [TearDown]
    public void TearDown()
    {
        if (instance != null)
            Object.DestroyImmediate(instance);
    }

    private static GameObject LoadFbx(string gun) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Weapons/Models/{gun}/{gun}.fbx");

    private static GameObject LoadPrefab(string gun) =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Weapons/Prefabs/{gun}.prefab");

    private static int Triangles(GameObject go)
    {
        int tris = 0;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null)
                tris += CountTriangles(mf.sharedMesh);
        return tris;
    }

    private static int CountTriangles(Mesh mesh)
    {
        int tris = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
            tris += (int)(mesh.GetIndexCount(s) / 3);
        return tris;
    }

    private GameObject InstantiatePrefab(string gun)
    {
        instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(gun));
        return instance;
    }

    [TestCase("M4A1")]
    [TestCase("SCAR")]
    [TestCase("UMP")]
    public void ImportedModel_HasReloadPartsDetailAndMaterialSlots(string gun)
    {
        GameObject fbx = LoadFbx(gun);
        Assert.That(fbx, Is.Not.Null, $"{gun}.fbx did not import.");

        Assert.That(AssaultWeaponModel.FindDescendant(fbx.transform, "Magazine"), Is.Not.Null);
        Assert.That(AssaultWeaponModel.FindDescendant(fbx.transform, "Bolt"), Is.Not.Null);

        int tris = Triangles(fbx);
        Assert.That(tris, Is.InRange(15000, 40000), $"{gun} triangle count");

        var names = new System.Collections.Generic.HashSet<string>();
        foreach (Renderer r in fbx.GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
                if (m != null)
                    names.Add(m.name);
        Assert.That(names.Count, Is.EqualTo(3), "Metal, Polymer and Accent slots expected.");

        foreach (MeshFilter mf in fbx.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = mf.sharedMesh;
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), $"{gun}/{mf.name} needs UVs");
            Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
        }

        Bounds bounds = new Bounds();
        bool first = true;
        foreach (Renderer r in fbx.GetComponentsInChildren<Renderer>(true))
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        Assert.That(bounds.size.z, Is.InRange(0.6f, 1.1f), "Weapon length along +Z (metres).");
        Assert.That(bounds.max.z, Is.GreaterThan(Mathf.Abs(bounds.min.z) * 0.3f));
    }

    [TestCase("M4A1")]
    [TestCase("SCAR")]
    [TestCase("UMP")]
    public void Prefab_UsesImportedModel_AndAnimatesImportedMagazineAndBolt(string gun)
    {
        InstantiatePrefab(gun);
        var model = instance.GetComponent<AssaultWeaponModel>();
        Assert.That(model, Is.Not.Null);

        model.Build();

        Assert.That(model.UsesImportedModel, Is.True, "Imported FBX should instantiate; check the importedModelPrefab reference.");
        Assert.That(model.ProceduralRoot.activeSelf, Is.False);

        Transform magazine = AssaultWeaponModel.FindDescendant(instance.transform, "Magazine");
        Transform bolt = AssaultWeaponModel.FindDescendant(instance.transform, "Bolt");
        Assert.That(magazine.IsChildOf(model.ImportedInstance.transform), "WeaponController must find the imported magazine first.");
        Assert.That(bolt.IsChildOf(model.ImportedInstance.transform), "WeaponController must find the imported bolt first.");
        Assert.That(magazine.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
    }

    [TestCase("M4A1")]
    [TestCase("SCAR")]
    [TestCase("UMP")]
    public void Prefab_MuzzleIsAtTheFrontOfTheModel(string gun)
    {
        InstantiatePrefab(gun);
        instance.GetComponent<AssaultWeaponModel>().Build();

        var weapon = instance.GetComponent<WeaponController>();
        Assert.That(weapon, Is.Not.Null);

        float maxZ = float.MinValue;
        foreach (Renderer r in instance.GetComponent<AssaultWeaponModel>().ImportedInstance.GetComponentsInChildren<Renderer>())
            maxZ = Mathf.Max(maxZ, instance.transform.InverseTransformPoint(r.bounds.max).z);

        Transform muzzle = instance.transform.Find("MuzzlePoint");
        Assert.That(muzzle, Is.Not.Null);
        Assert.That(muzzle.localPosition.z, Is.EqualTo(maxZ).Within(0.03f), "Muzzle point should sit at the muzzle device.");
        Assert.That(instance.transform.Find("FirePoint").localPosition, Is.EqualTo(muzzle.localPosition));
    }

    [TestCase("M4A1")]
    [TestCase("SCAR")]
    [TestCase("UMP")]
    public void Prefab_WithoutModel_FallsBackToProceduralPartsAndPoints(string gun)
    {
        InstantiatePrefab(gun);
        var model = instance.GetComponent<AssaultWeaponModel>();
        var so = new SerializedObject(model);
        so.FindProperty("importedModelPrefab").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        model.Build();

        Assert.That(model.UsesImportedModel, Is.False);
        Assert.That(model.ProceduralRoot.activeSelf, Is.True);

        Vector3 expected = so.FindProperty("fallbackMuzzleLocal").vector3Value;
        Assert.That(instance.transform.Find("MuzzlePoint").localPosition, Is.EqualTo(expected));
    }

    [TestCase("M4A1")]
    [TestCase("SCAR")]
    [TestCase("UMP")]
    public void SkinApplier_CoversEveryImportedMaterialSlot(string gun)
    {
        InstantiatePrefab(gun);
        var model = instance.GetComponent<AssaultWeaponModel>();
        model.Build();

        WeaponSkinDefinition skin = null;
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponSkinDefinition", new[] { "Assets/Skins/Definitions" }))
        {
            skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skin != null && skin.material != null)
                break;
        }
        Assert.That(skin, Is.Not.Null);

        var applier = instance.GetComponent<WeaponSkinApplier>();
        Assert.That(applier, Is.Not.Null);
        applier.ApplySkin(skin);

        Renderer[] renderers = model.ImportedInstance.GetComponentsInChildren<Renderer>();
        Assert.That(renderers.Length, Is.GreaterThan(0));
        Material expected = applier.ResolveRenderableMaterial(skin);
        foreach (Renderer r in renderers)
            foreach (Material m in r.sharedMaterials)
                Assert.That(m, Is.EqualTo(expected), $"{r.name} slot not skinned");
    }
}
