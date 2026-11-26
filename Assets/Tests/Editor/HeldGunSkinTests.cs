using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The gun a soldier holds in the world wears the same weapon skin as the first-person gun. The Scripts assembly reaches
/// skins only through the <see cref="HeldGunSkins"/> seam; the Skins assembly registers the provider. Skins paint the
/// outer parts of the held gun and never the scope glass.
/// </summary>
public class HeldGunSkinTests
{
    private const string BoltPrefab = "Assets/Weapons/Prefabs/BoltActionSniper.prefab";
    private const string KarambitPrefab = "Assets/Weapons/Prefabs/Karambit.prefab";

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

    public static IEnumerable<string> HeldModelWeaponIds()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        foreach (LoadoutWeaponEntry entry in catalog.weapons)
        {
            if (entry != null && entry.prefab != null && WeaponVisualSource.FindModel(entry.prefab, out _) != null)
                yield return entry.Id;
        }
    }

    [Test]
    public void SkinsAssembly_RegistersItsProviderWithTheScriptsSeam()
    {
        Assert.That(HeldGunSkins.Provider, Is.Not.Null);
        Assert.That(HeldGunSkins.Provider, Is.InstanceOf<WeaponSkinHeldGunProvider>());
    }

    [Test]
    public void EveryCatalogWeaponHasAThirdPersonModel()
    {
        Assert.That(new List<string>(HeldModelWeaponIds()).Count, Is.EqualTo(WeaponLoadoutCatalog.LoadFromResources().weapons.Length),
            "every catalog weapon has an imported model that soldiers can hold");
    }

    [TestCaseSource(nameof(HeldModelWeaponIds))]
    public void HeldGun_OfEveryCatalogWeapon_WearsThePrefabDefaultSkinOnItsOuterParts(string weaponId)
    {
        LoadoutWeaponEntry entry = WeaponLoadoutCatalog.LoadFromResources().FindWeapon(weaponId);
        WeaponSkinApplier prefabApplier = entry.prefab.GetComponentInChildren<WeaponSkinApplier>(true);
        Assert.That(prefabApplier, Is.Not.Null, weaponId + " prefab has a skin applier");
        WeaponSkinDefinition expected = prefabApplier.DefaultSkin;
        ThirdPersonGun gun = MakeGun(entry.prefab);

        Assert.That(gun.UsesImportedModel, Is.True, weaponId);
        if (expected == null)
        {
            Assert.That(gun.AppliedSkin, Is.Null, weaponId + " has no default skin, so its held gun stays plain");
            return;
        }

        Assert.That(gun.AppliedSkin, Is.SameAs(expected), weaponId);
        AssertOuterPartsWearSkin(gun, expected, weaponId);
    }

    [TestCaseSource(nameof(HeldModelWeaponIds))]
    public void HeldGun_WithAnExplicitSkin_UsesItOnEveryCatalogWeapon(string weaponId)
    {
        LoadoutWeaponEntry entry = WeaponLoadoutCatalog.LoadFromResources().FindWeapon(weaponId);
        WeaponSkinDefinition skin = PickCatalogSkin(1);
        GameObject weapon = SpawnWeapon(entry.prefab, skin);

        ThirdPersonGun gun = MakeGun(weapon);

        Assert.That(gun.AppliedSkin, Is.SameAs(skin), weaponId);
        AssertOuterPartsWearSkin(gun, skin, weaponId);
    }

    [TestCase(BoltPrefab)]
    public void SkinnedHeldSniper_KeepsTheGlassShaderOnTheScopeLens_AndSkinsTheScopeBody(string prefabPath)
    {
        WeaponSkinDefinition skin = PickCatalogSkin(2);
        GameObject weapon = SpawnWeapon(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), skin);

        ThirdPersonGun gun = MakeGun(weapon);

        Renderer scope = FindRenderer(gun.Root.gameObject, "Scope");
        Assert.That(scope, Is.Not.Null, "held sniper has a Scope part");
        int glass = 0;
        int skinned = 0;
        foreach (Material material in scope.sharedMaterials)
        {
            if (WeaponGlass.IsGlassMaterial(material))
            {
                glass++;
                Assert.That(material.shader.name, Is.EqualTo(WeaponGlass.ShaderName), "lens keeps the glass shader");
            }
            else
            {
                skinned++;
                Assert.That(IsSkinMaterial(material, skin), Is.True, "scope tube and mounts wear the skin");
            }
        }

        Assert.That(glass, Is.EqualTo(1), "one lens slot");
        Assert.That(skinned, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void HeldGun_FollowsAWeaponWhoseSkinChanges_AndRestoresWhenItIsCleared()
    {
        LoadoutWeaponEntry entry = WeaponLoadoutCatalog.LoadFromResources().FindWeapon("SCAR");
        WeaponSkinDefinition first = PickCatalogSkin(0);
        WeaponSkinDefinition second = PickCatalogSkin(3);
        Assume.That(first, Is.Not.SameAs(second));
        GameObject weapon = SpawnWeapon(entry.prefab, first);
        ThirdPersonGun gun = MakeGun(weapon);
        Assert.That(gun.AppliedSkin, Is.SameAs(first));

        weapon.GetComponentInChildren<WeaponSkinApplier>(true).ApplySkin(second);
        Assert.That(gun.CopiesSkinFrom(weapon), Is.True);
        gun.RefreshSkin();
        Assert.That(gun.AppliedSkin, Is.SameAs(second));
        AssertOuterPartsWearSkin(gun, second, "re-skinned");

        weapon.GetComponentInChildren<WeaponSkinApplier>(true).ApplySkin(null);
        gun.RefreshSkin();
        Assert.That(gun.AppliedSkin, Is.SameAs(entry.prefab.GetComponentInChildren<WeaponSkinApplier>(true).DefaultSkin).Or.Null);
    }

    [Test]
    public void HeldGun_WithoutAProvider_StaysUnskinned()
    {
        IHeldGunSkinProvider registered = HeldGunSkins.Provider;
        HeldGunSkins.Register(null);
        try
        {
            LoadoutWeaponEntry entry = WeaponLoadoutCatalog.LoadFromResources().FindWeapon("M4A1");
            ThirdPersonGun gun = MakeGun(entry.prefab);
            Assert.That(gun.AppliedSkin, Is.Null);
        }
        finally
        {
            HeldGunSkins.Register(registered);
        }
    }

    [Test]
    public void PrimitiveFallbackGun_IsNeverSkinned()
    {
        var parent = new GameObject("Parent");
        created.Add(parent);
        var config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponName = "Pistol";
        created.Add(config);

        ThirdPersonGun gun = ThirdPersonGun.Create(parent.transform, config, null, 0);

        Assert.That(gun.UsesImportedModel, Is.False);
        Assert.That(gun.AppliedSkin, Is.Null);
        Assert.That(gun.Root.GetComponent<WeaponSkinApplier>(), Is.Null);
    }

    [Test]
    public void Karambit_WithoutADefaultSkin_StaysPlain_AndWearsAChosenSkin()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KarambitPrefab);
        Assert.That(prefab.GetComponentInChildren<WeaponSkinApplier>(true).DefaultSkin, Is.Null);
        Assert.That(MakeGun(prefab).AppliedSkin, Is.Null);

        WeaponSkinDefinition skin = PickCatalogSkin(1);
        ThirdPersonGun skinned = MakeGun(SpawnWeapon(prefab, skin));
        Assert.That(skinned.AppliedSkin, Is.SameAs(skin));
        AssertOuterPartsWearSkin(skinned, skin, "Karambit");
    }

    private ThirdPersonGun MakeGun(GameObject weaponSource)
    {
        var parent = new GameObject("HeldGunParent");
        created.Add(parent);
        var config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponName = weaponSource.name;
        WeaponController controller = weaponSource.GetComponent<WeaponController>();
        if (controller != null && controller.Config != null)
        {
            Object.DestroyImmediate(config);
            config = controller.Config;
        }
        else
        {
            created.Add(config);
        }

        return ThirdPersonGun.Create(parent.transform, config, weaponSource, 0);
    }

    private GameObject SpawnWeapon(GameObject prefab, WeaponSkinDefinition skin)
    {
        GameObject instance = Object.Instantiate(prefab);
        created.Add(instance);
        WeaponSkinApplier applier = instance.GetComponentInChildren<WeaponSkinApplier>(true);
        applier.ApplySkin(skin);
        return instance;
    }

    private static WeaponSkinDefinition PickCatalogSkin(int index)
    {
        WeaponSkinDefinition[] skins = WeaponLoadoutCatalog.LoadFromResources().skins;
        Assert.That(skins.Length, Is.GreaterThan(index));
        return skins[index];
    }

    private static void AssertOuterPartsWearSkin(ThirdPersonGun gun, WeaponSkinDefinition skin, string label)
    {
        int skinned = 0;
        foreach (Renderer renderer in gun.Root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (WeaponGlass.IsGlassSlot(renderer, material))
                    continue;
                Assert.That(IsSkinMaterial(material, skin), Is.True, label + ": " + renderer.name + " should wear " + skin.name + " but has " + (material != null ? material.name : "null"));
                skinned++;
            }
        }

        Assert.That(skinned, Is.GreaterThan(0), label + ": the held gun has skinnable parts");
    }

    private static bool IsSkinMaterial(Material material, WeaponSkinDefinition skin)
    {
        return material != null && (material == skin.material || material.name == skin.material.name + "_Fallback");
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
}
