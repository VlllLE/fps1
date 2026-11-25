using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Checks the Blender-built Shotgun, LMG and Pistol models (FBX import, parts, triangle budget, material slots) and
/// that the prefabs show them through WeaponModelLoader while keeping the primitive meshes as a fallback.
/// </summary>
public class WeaponModelTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string PlayerCharacterPrefabPath = "Assets/Characters/Player/PlayerCharacter.prefab";

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

    private static string ModelPath(string gun) => $"Assets/Weapons/Models/{gun}/{gun}.fbx";

    private static GameObject LoadModel(string gun)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(gun));
        Assert.That(model, Is.Not.Null, "Missing imported model " + ModelPath(gun));
        return model;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform hit = FindDeep(child, name);
            if (hit != null)
                return hit;
        }

        return null;
    }

    private static int TriangleCount(GameObject model)
    {
        int tris = 0;
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;
            for (int s = 0; s < mesh.subMeshCount; s++)
                tris += (int)(mesh.GetIndexCount(s) / 3);
        }

        return tris;
    }

    private static Transform SpawnWeaponRoot(string prefabPath, string rootName, List<Object> created)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, prefabPath);
        var instance = (GameObject)Object.Instantiate(prefab);
        created.Add(instance);
        Transform root = FindDeep(instance.transform, rootName);
        Assert.That(root, Is.Not.Null, rootName + " in " + prefabPath);
        return root;
    }

    public static IEnumerable<TestCaseData> Guns()
    {
        yield return new TestCaseData("Shotgun", new[] { "ShotgunBody", "Pump" }, "Pump");
        yield return new TestCaseData("LMG", new[] { "LMGBody", "Magazine", "Bolt" }, "Magazine");
        yield return new TestCaseData("Pistol", new[] { "PistolBody", "Slide", "Magazine" }, "Slide");
    }

    public static IEnumerable<TestCaseData> WeaponObjects()
    {
        yield return new TestCaseData("Assets/Weapons/Prefabs/Shotgun.prefab", "Shotgun", "Shotgun");
        yield return new TestCaseData("Assets/Weapons/Prefabs/LMG.prefab", "LMG", "LMG");
        yield return new TestCaseData(PlayerPrefabPath, "TestPistol", "Pistol");
        yield return new TestCaseData(PlayerCharacterPrefabPath, "TestPistol", "Pistol");
    }

    [TestCaseSource(nameof(Guns))]
    public void Model_HasPartsDetailBudgetUvsAndMaterialSlots(string gun, string[] parts, string animated)
    {
        GameObject model = LoadModel(gun);

        foreach (string part in parts)
        {
            Transform t = FindDeep(model.transform, part);
            Assert.That(t, Is.Not.Null, gun + " is missing mesh object " + part);
            Assert.That(t.GetComponent<MeshFilter>()?.sharedMesh, Is.Not.Null, part + " has no mesh");
        }

        int tris = TriangleCount(model);
        Assert.That(tris, Is.InRange(15000, 40000), gun + " triangle count");

        var slots = new HashSet<string>();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            Assert.That(mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0), Is.True,
                gun + "/" + filter.name + " is not UV unwrapped");
            Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount), filter.name + " normals");
        }

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null, renderer.name);
                string n = material.name;
                if (n.Contains("Metal")) slots.Add("Metal");
                if (n.Contains("Polymer")) slots.Add("Polymer");
                if (n.Contains("Accent")) slots.Add("Accent");
            }
        }

        Assert.That(slots, Is.EquivalentTo(new[] { "Metal", "Polymer", "Accent" }), gun + " material slots");
    }

    [TestCaseSource(nameof(Guns))]
    public void Model_PivotsOfAnimatedPartsStayOnTheMesh(string gun, string[] parts, string animated)
    {
        GameObject model = LoadModel(gun);
        Transform part = FindDeep(model.transform, animated);
        Assert.That(part, Is.Not.Null);
        Bounds bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
        Assert.That(bounds.size.magnitude, Is.GreaterThan(0.02f));
        bounds.Expand(0.25f);
        Assert.That(bounds.Contains(Vector3.zero), Is.True,
            animated + " mesh must be authored around its own pivot so animation does not fly it away");
    }

    [TestCaseSource(nameof(WeaponObjects))]
    public void Prefab_WiresLoaderToModelAndKeepsFallbackAndController(string prefabPath, string rootName, string gun)
    {
        Transform root = SpawnWeaponRoot(prefabPath, rootName, created);

        var loader = root.GetComponent<WeaponModelLoader>();
        Assert.That(loader, Is.Not.Null, "WeaponModelLoader missing on " + rootName);
        var so = new SerializedObject(loader);
        Assert.That(so.FindProperty("modelPrefab").objectReferenceValue, Is.SameAs(LoadModel(gun)));
        Assert.That(so.FindProperty("metalMaterial").objectReferenceValue, Is.Not.Null);
        Assert.That(so.FindProperty("polymerMaterial").objectReferenceValue, Is.Not.Null);
        Assert.That(so.FindProperty("accentMaterial").objectReferenceValue, Is.Not.Null);

        foreach (string fallback in loader.FallbackChildren)
            Assert.That(root.Find(fallback), Is.Not.Null, "fallback child " + fallback);

        var controller = root.GetComponent<WeaponController>();
        Assert.That(controller, Is.Not.Null);
        var cso = new SerializedObject(controller);
        Assert.That(cso.FindProperty("muzzlePoint").objectReferenceValue, Is.Not.Null, "muzzle point must stay wired");
        Assert.That(cso.FindProperty("config").objectReferenceValue, Is.Not.Null);
        Assert.That(root.GetComponent<WeaponSkinApplier>(), Is.Not.Null, "skin applier must stay on the weapon");
        Assert.That(root.GetComponents<WeaponModelLoader>().Length, Is.EqualTo(1));
    }

    [TestCaseSource(nameof(WeaponObjects))]
    public void Loader_ShowsModelHidesFallbackAndExposesAnimatedParts(string prefabPath, string rootName, string gun)
    {
        Transform root = SpawnWeaponRoot(prefabPath, rootName, created);
        var loader = root.GetComponent<WeaponModelLoader>();

        Assert.That(loader.Load(), Is.True);
        Assert.That(loader.IsLoaded, Is.True);
        Transform model = root.Find(loader.InstanceName);
        Assert.That(model, Is.Not.Null);
        Assert.That(model.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(model.localScale, Is.EqualTo(Vector3.one));

        foreach (string fallback in loader.FallbackChildren)
        {
            Assert.That(root.Find(fallback), Is.Null, fallback + " should have been renamed");
            Transform hidden = root.Find(fallback + WeaponModelLoader.FallbackSuffix);
            Assert.That(hidden, Is.Not.Null);
            Assert.That(hidden.gameObject.activeSelf, Is.False);
        }

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
                Assert.That(material.name, Does.StartWith("Mat_Gun"), renderer.name);
        }

        if (gun == "LMG" || gun == "Pistol")
        {
            Transform magazine = FindDeep(root, "Magazine");
            Assert.That(magazine, Is.Not.Null);
            Assert.That(magazine.IsChildOf(model), Is.True, "WeaponController must animate the model's magazine, not the fallback");
        }

        if (gun == "LMG")
            Assert.That(FindDeep(root, "Bolt").IsChildOf(model), Is.True);
    }

    [TestCaseSource(nameof(WeaponObjects))]
    public void Loader_KeepsFallbackWhenModelIsMissingOrIncomplete(string prefabPath, string rootName, string gun)
    {
        Transform root = SpawnWeaponRoot(prefabPath, rootName, created);
        var loader = root.GetComponent<WeaponModelLoader>();
        string[] fallbacks = new string[loader.FallbackChildren.Count];
        for (int i = 0; i < fallbacks.Length; i++)
            fallbacks[i] = loader.FallbackChildren[i];

        loader.Configure(null, new string[0], fallbacks, null, null, null);
        Assert.That(loader.Load(), Is.False);
        foreach (string fallback in fallbacks)
            Assert.That(root.Find(fallback).gameObject.activeSelf, Is.True, fallback);

        loader.Configure(LoadModel(gun), new[] { "NoSuchPart" }, fallbacks, null, null, null);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("NoSuchPart"));
        Assert.That(loader.Load(), Is.False);
        Assert.That(loader.IsLoaded, Is.False);
        Assert.That(root.Find(loader.InstanceName), Is.Null, "rejected model must not stay under the weapon");
        foreach (string fallback in fallbacks)
            Assert.That(root.Find(fallback).gameObject.activeSelf, Is.True, fallback);
    }

    [Test]
    public void ShotgunPrefab_PumpAnimatorFollowsTheModelPump()
    {
        Transform root = SpawnWeaponRoot("Assets/Weapons/Prefabs/Shotgun.prefab", "Shotgun", created);
        var animator = root.GetComponent<WeaponPartAnimator>();
        Assert.That(animator, Is.Not.Null);

        root.GetComponent<WeaponModelLoader>().Load();
        animator.BeginStroke();
        Assert.That(animator.Part, Is.Not.Null);
        Assert.That(animator.Part.name, Is.EqualTo("Pump"));
        Assert.That(animator.Part.IsChildOf(root.Find("DetailedModel")), Is.True);
        Assert.That(animator.IsAnimating, Is.True);
    }

    [Test]
    public void PistolPrefabs_SlideAnimatorFollowsTheModelSlide()
    {
        foreach (string path in new[] { PlayerPrefabPath, PlayerCharacterPrefabPath })
        {
            Transform root = SpawnWeaponRoot(path, "TestPistol", created);
            var animator = root.GetComponent<WeaponPartAnimator>();
            Assert.That(animator, Is.Not.Null, path);

            root.GetComponent<WeaponModelLoader>().Load();
            animator.BeginStroke();
            Assert.That(animator.Part, Is.Not.Null, path);
            Assert.That(animator.Part.name, Is.EqualTo("Slide"), path);
        }
    }

    [Test]
    public void Models_MuzzleEndsMatchThePrefabMuzzlePoints()
    {
        var expected = new Dictionary<string, float> { { "Shotgun", 0.57f }, { "LMG", 0.74f }, { "Pistol", 0.30f } };
        foreach (KeyValuePair<string, float> pair in expected)
        {
            GameObject model = LoadModel(pair.Key);
            float maxZ = float.MinValue;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds b = filter.sharedMesh.bounds;
                float z = filter.transform.localPosition.z + b.max.z;
                if (z > maxZ)
                    maxZ = z;
            }

            Assert.That(maxZ, Is.EqualTo(pair.Value).Within(0.03f), pair.Key + " muzzle end");
        }
    }
}
