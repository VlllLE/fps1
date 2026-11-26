using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Weapon skins cover the weapon but never the optics: scope lenses keep their transparent, tinted glass material
/// through skinning, re-skinning and clearing, on the synthetic weapons below and on the real sniper prefabs.
/// </summary>
public class ScopeGlassTests
{
    private const string GlassMaterialPath = "Assets/Weapons/Materials/Mat_SniperGlass.mat";
    private const string BoltPrefab = "Assets/Weapons/Prefabs/BoltActionSniper.prefab";
    private const string DmrPrefab = "Assets/Weapons/Prefabs/DMRSniper.prefab";

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

    [Test]
    public void SniperGlassMaterial_IsTransparentTintedGlassWithReflection()
    {
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        Assert.That(glass, Is.Not.Null);

        Assert.That(glass.shader.name, Is.EqualTo(WeaponGlass.ShaderName));
        Assert.That(glass.renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
        Assert.That(glass.GetColor("_Color").a, Is.InRange(0.05f, 0.6f), "lens must stay see-through");
        Assert.That(glass.GetFloat("_Glossiness"), Is.GreaterThan(0.8f), "lens must reflect");
        Assert.That(glass.GetFloat("_ReflectionStrength"), Is.GreaterThan(0f));
        Assert.That(WeaponGlass.IsGlassMaterial(glass), Is.True);
    }

    [Test]
    public void GlassPredicate_RecognisesGlassByShaderOrName_AndNothingElse()
    {
        Material byShader = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        Material byName = Track(new Material(Shader.Find("Standard")) { name = "Mat_RedDotLens" });
        Material metal = Track(new Material(Shader.Find("Standard")) { name = "Mat_GunMetal" });

        Assert.That(WeaponGlass.IsGlassMaterial(byShader), Is.True);
        Assert.That(WeaponGlass.IsGlassMaterial(byName), Is.True);
        Assert.That(WeaponGlass.IsGlassMaterial(metal), Is.False);
        Assert.That(WeaponGlass.IsGlassMaterial(null), Is.False);

        var lensObject = Track(new GameObject("ScopeLensFront", typeof(MeshRenderer)));
        var bodyObject = Track(new GameObject("ScopeBody", typeof(MeshRenderer)));
        Assert.That(WeaponGlass.IsGlassSlot(lensObject.GetComponent<Renderer>(), metal), Is.True);
        Assert.That(WeaponGlass.IsGlassSlot(bodyObject.GetComponent<Renderer>(), metal), Is.False);
    }

    [Test]
    public void ApplySkin_SkinsBodySlots_AndLeavesGlassSlotUntouched()
    {
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        Material metal = Track(new Material(Shader.Find("Standard")) { name = "Mat_GunMetal" });
        GameObject weapon = Track(new GameObject("Weapon"));
        MeshRenderer scope = AddCube(weapon, "Scope");
        scope.sharedMaterials = new[] { metal, glass };
        MeshRenderer body = AddCube(weapon, "Body");
        body.sharedMaterial = metal;

        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();
        WeaponSkinDefinition skin = CreateSkin();

        applier.ApplySkin(skin);

        Assert.That(scope.sharedMaterials[0], Is.SameAs(skin.material));
        Assert.That(scope.sharedMaterials[1], Is.SameAs(glass));
        Assert.That(body.sharedMaterial, Is.SameAs(skin.material));
        Assert.That(applier.GlassSlotCount, Is.EqualTo(1));
    }

    [Test]
    public void ApplySkin_KeepsWholeLensRenderersUnskinned_ByRendererName()
    {
        Material metal = Track(new Material(Shader.Find("Standard")) { name = "Mat_GunMetal" });
        GameObject weapon = Track(new GameObject("Weapon"));
        MeshRenderer lens = AddCube(weapon, "ScopeLensRear");
        lens.sharedMaterial = metal;
        MeshRenderer body = AddCube(weapon, "ScopeBody");
        body.sharedMaterial = metal;

        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();
        WeaponSkinDefinition skin = CreateSkin();
        applier.ApplySkin(skin);

        Assert.That(lens.sharedMaterial, Is.SameAs(metal));
        Assert.That(body.sharedMaterial, Is.SameAs(skin.material));
    }

    [Test]
    public void ApplySkin_ParameterOverrides_NeverReachTheGlassSlot()
    {
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        Material metal = Track(new Material(Shader.Find("Standard")) { name = "Mat_GunMetal" });
        GameObject weapon = Track(new GameObject("Weapon"));
        MeshRenderer scope = AddCube(weapon, "Scope");
        scope.sharedMaterials = new[] { metal, glass };

        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();
        WeaponSkinDefinition skin = CreateSkin();
        skin.applyParameterOverrides = true;
        skin.skinColor = new Color(1f, 0f, 0f, 1f);
        applier.ApplySkin(skin);

        var block = new MaterialPropertyBlock();
        scope.GetPropertyBlock(block, 0);
        Assert.That(block.isEmpty, Is.False, "the skinned slot should carry the skin's overrides");

        var glassBlock = new MaterialPropertyBlock();
        scope.GetPropertyBlock(glassBlock, 1);
        Assert.That(glassBlock.isEmpty, Is.True, "the lens slot must not carry the skin's _Color override");
    }

    [Test]
    public void ClearSkin_AndReSkin_KeepTheGlassSlot()
    {
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        Material metal = Track(new Material(Shader.Find("Standard")) { name = "Mat_GunMetal" });
        GameObject weapon = Track(new GameObject("Weapon"));
        MeshRenderer scope = AddCube(weapon, "Scope");
        scope.sharedMaterials = new[] { metal, glass };
        WeaponSkinApplier applier = weapon.AddComponent<WeaponSkinApplier>();

        WeaponSkinDefinition first = CreateSkin();
        WeaponSkinDefinition second = CreateSkin();
        applier.ApplySkin(first);
        applier.ApplySkin(second);
        Assert.That(scope.sharedMaterials[1], Is.SameAs(glass));
        Assert.That(scope.sharedMaterials[0], Is.SameAs(second.material));

        applier.ClearSkin();
        Assert.That(scope.sharedMaterials[0], Is.SameAs(metal));
        Assert.That(scope.sharedMaterials[1], Is.SameAs(glass));
    }

    [TestCase(BoltPrefab)]
    [TestCase(DmrPrefab)]
    public void SkinnedSniperPrefab_KeepsGlassShaderOnTheScopeLens(string prefabPath)
    {
        GameObject gun = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        created.Add(gun);
        ImportedGunModel model = gun.GetComponent<ImportedGunModel>();
        model.Build();
        Assume.That(model.UsesImportedModel, Is.True);

        WeaponSkinApplier applier = gun.GetComponentInChildren<WeaponSkinApplier>(true);
        Assert.That(applier, Is.Not.Null);
        WeaponSkinDefinition skin = CreateSkin();
        applier.ApplySkin(skin);

        Renderer scope = FindRenderer(model.ImportedInstance, "Scope");
        Assert.That(scope, Is.Not.Null, "imported model has no Scope renderer");

        int glassSlots = 0;
        int skinnedSlots = 0;
        foreach (Material material in scope.sharedMaterials)
        {
            if (WeaponGlass.IsGlassMaterial(material))
            {
                glassSlots++;
                Assert.That(material.shader.name, Is.EqualTo(WeaponGlass.ShaderName));
            }
            else
            {
                skinnedSlots++;
                Assert.That(material, Is.SameAs(skin.material), "scope body, tube and mounts keep the skin");
            }
        }

        Assert.That(glassSlots, Is.EqualTo(1), "the lens is one material slot of the Scope mesh");
        Assert.That(skinnedSlots, Is.GreaterThanOrEqualTo(2));

        Renderer body = FindRenderer(model.ImportedInstance, "Body");
        Assert.That(body.sharedMaterial, Is.SameAs(skin.material));

        applier.ApplySkin(null);
        Assert.That(WeaponGlass.IsGlassMaterial(scope.sharedMaterials[scope.sharedMaterials.Length - 1]), Is.True);
    }

    [TestCase(BoltPrefab)]
    [TestCase(DmrPrefab)]
    public void SniperPrefab_FallbackLensRenderers_AreNeverSkinned(string prefabPath)
    {
        GameObject gun = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        created.Add(gun);
        gun.GetComponent<ImportedGunModel>().Build();

        WeaponSkinApplier applier = gun.GetComponentInChildren<WeaponSkinApplier>(true);
        WeaponSkinDefinition skin = CreateSkin();
        applier.ApplySkin(skin);

        int lenses = 0;
        foreach (Renderer renderer in gun.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name.IndexOf("Lens", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            lenses++;
            foreach (Material material in renderer.sharedMaterials)
                Assert.That(material, Is.Not.SameAs(skin.material), renderer.name + " must not wear the skin");
        }
        Assert.That(lenses, Is.GreaterThan(0));
    }

    [TestCase(BoltPrefab)]
    [TestCase(DmrPrefab)]
    public void ThirdPersonAndDroppedGun_KeepGlassShaderOnTheScopeLens(string prefabPath)
    {
        var parent = Track(new GameObject("Parent"));
        WeaponConfig config = Track(ScriptableObject.CreateInstance<WeaponConfig>());
        config.weaponType = WeaponType.Sniper;
        config.weaponName = "Sniper";

        ThirdPersonGun gun = ThirdPersonGun.Create(parent.transform, config, AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), 0);
        Assume.That(gun.UsesImportedModel, Is.True);

        Renderer scope = FindRenderer(gun.Root.gameObject, "Scope");
        Assert.That(scope, Is.Not.Null);
        Assert.That(HasGlassSlot(scope), Is.True, "held third-person scope lens");

        gun.BeginDrop(Vector3.forward, Vector3.zero, _ => 0f);
        Assert.That(gun.IsDropped, Is.True);
        Assert.That(HasGlassSlot(scope), Is.True, "dropped world model scope lens");
        gun.Destroy();
    }

    private static bool HasGlassSlot(Renderer renderer)
    {
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material != null && material.shader.name == WeaponGlass.ShaderName)
                return true;
        }
        return false;
    }

    private static Renderer FindRenderer(GameObject root, string name)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == name)
                return renderer;
        }
        return null;
    }

    private MeshRenderer AddCube(GameObject parent, string name)
    {
        GameObject part = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        part.name = name;
        part.transform.SetParent(parent.transform, false);
        return part.GetComponent<MeshRenderer>();
    }

    private WeaponSkinDefinition CreateSkin()
    {
        var skin = ScriptableObject.CreateInstance<WeaponSkinDefinition>();
        skin.material = Track(new Material(Shader.Find("Standard")) { name = "SkinMaterial" });
        created.Add(skin);
        return skin;
    }

    private T Track<T>(T obj) where T : Object
    {
        created.Add(obj);
        return obj;
    }
}
