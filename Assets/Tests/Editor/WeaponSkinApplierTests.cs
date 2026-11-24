using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WeaponSkinApplierTests
{
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        Assume.That(Shader.Find("Standard") != null && Shader.Find("Standard").isSupported,
            "Standard shader must be usable for skin material tests.");
    }

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

    [Test]
    public void ApplySkin_AssignsSkinMaterialToEveryRenderer()
    {
        GameObject weapon = CreateWeaponWithRenderers(3, out Material original);
        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();
        WeaponSkinDefinition skin = CreateSkin();

        applier.ApplySkin(skin);

        Assert.That(applier.RendererCount, Is.EqualTo(3));
        Assert.That(applier.CurrentSkin, Is.SameAs(skin));
        foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>())
        {
            Assert.That(renderer.sharedMaterial, Is.Not.SameAs(original));
            Assert.That(renderer.sharedMaterial, Is.SameAs(skin.material));
        }
    }

    [Test]
    public void ClearSkin_RestoresOriginalMaterials()
    {
        GameObject weapon = CreateWeaponWithRenderers(2, out Material original);
        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();

        applier.ApplySkin(CreateSkin());
        applier.ClearSkin();

        Assert.That(applier.CurrentSkin, Is.Null);
        foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>())
            Assert.That(renderer.sharedMaterial, Is.SameAs(original));
    }

    [Test]
    public void ApplySkin_PreservesSubMeshSlotCount()
    {
        GameObject weapon = CreateWeaponWithRenderers(1, out Material original);
        Renderer renderer = weapon.GetComponentInChildren<Renderer>();
        renderer.sharedMaterials = new[] { original, original };
        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();
        WeaponSkinDefinition skin = CreateSkin();

        applier.ApplySkin(skin);

        Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2));
        Assert.That(renderer.sharedMaterials[1], Is.SameAs(skin.material));
    }

    [Test]
    public void SetDefaultSkin_AppliesImmediatelyWhenRequested()
    {
        GameObject weapon = CreateWeaponWithRenderers(1, out _);
        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();
        WeaponSkinDefinition skin = CreateSkin();

        applier.SetDefaultSkin(skin, true);

        Assert.That(applier.DefaultSkin, Is.SameAs(skin));
        Assert.That(applier.CurrentSkin, Is.SameAs(skin));
    }

    [Test]
    public void ResolveRenderableMaterial_ReturnsSkinMaterialWhenShaderIsSupported()
    {
        GameObject weapon = CreateWeaponWithRenderers(1, out _);
        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();
        WeaponSkinDefinition skin = CreateSkin();

        Assert.That(applier.ResolveRenderableMaterial(skin), Is.SameAs(skin.material));
        Assert.That(applier.ResolveRenderableMaterial(null), Is.Null);
    }

    private GameObject CreateWeaponWithRenderers(int count, out Material original)
    {
        original = new Material(Shader.Find("Standard")) { name = "Original" };
        created.Add(original);

        var root = new GameObject("Weapon");
        created.Add(root);
        for (int i = 0; i < count; i++)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "Part" + i;
            part.transform.SetParent(root.transform, false);
            part.GetComponent<MeshRenderer>().sharedMaterial = original;
        }
        return root;
    }

    private WeaponSkinDefinition CreateSkin()
    {
        var skin = ScriptableObject.CreateInstance<WeaponSkinDefinition>();
        skin.material = new Material(Shader.Find("Standard")) { name = "SkinMaterial" };
        created.Add(skin.material);
        created.Add(skin);
        return skin;
    }
}
