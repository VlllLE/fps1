using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Static checks for the Pump Shotgun and LMG: config values, prefab wiring, catalog registration and
/// that every skin definition applies to their generated models.
/// </summary>
public class ShotgunLmgWeaponTests
{
    private const string ShotgunPrefabPath = "Assets/Weapons/Prefabs/Shotgun.prefab";
    private const string LmgPrefabPath = "Assets/Weapons/Prefabs/LMG.prefab";
    private const string ShotgunConfigPath = "Assets/Configs/Weapons/ShotgunConfig.asset";
    private const string LmgConfigPath = "Assets/Configs/Weapons/LMGConfig.asset";
    private const string M4ConfigPath = "Assets/Configs/Weapons/M4A1Config.asset";

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

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, "Missing asset " + path);
        return asset;
    }

    private GameObject Spawn(string prefabPath)
    {
        var instance = (GameObject)Object.Instantiate(Load<GameObject>(prefabPath));
        created.Add(instance);
        return instance;
    }

    // ------------------------------------------------------------------ Configs

    [Test]
    public void ShotgunConfig_FiresSpreadPelletsAtShortRangeWithPerShellReload()
    {
        var config = Load<WeaponConfig>(ShotgunConfigPath);

        Assert.That(config.weaponType, Is.EqualTo(WeaponType.Shotgun));
        Assert.That(config.pelletCount, Is.GreaterThanOrEqualTo(6));
        Assert.That(config.baseSpread, Is.GreaterThan(1f), "Pellets need a visible spread.");
        Assert.That(config.range, Is.LessThanOrEqualTo(40f), "Shotgun is a short-range weapon.");
        Assert.That(config.availableFireModes, Is.EqualTo(new[] { FireMode.Semi }));
        Assert.That(config.magazineSize, Is.InRange(4, 10));
        Assert.That(config.perShellReload, Is.True);
        Assert.That(config.shellReloadTime, Is.GreaterThan(0f));
        Assert.That(config.canCancelReload, Is.True, "Firing must be able to interrupt a shell-by-shell reload.");
        Assert.That(config.damage * config.pelletCount, Is.InRange(60f, 160f), "Point-blank damage should be a believable one-to-two shot kill.");
        Assert.That(config.damageFalloff.Evaluate(1f), Is.LessThan(config.damageFalloff.Evaluate(0f)), "Damage must fall off over range.");
    }

    [Test]
    public void LmgConfig_HasLargeMagazineSlowReloadAndHeavyRecoil()
    {
        var lmg = Load<WeaponConfig>(LmgConfigPath);
        var m4 = Load<WeaponConfig>(M4ConfigPath);

        Assert.That(lmg.weaponType, Is.EqualTo(WeaponType.LMG));
        Assert.That(lmg.availableFireModes, Is.EqualTo(new[] { FireMode.Auto }));
        Assert.That(lmg.pelletCount, Is.EqualTo(1));
        Assert.That(lmg.perShellReload, Is.False);
        Assert.That(lmg.magazineSize, Is.GreaterThanOrEqualTo(75));
        Assert.That(lmg.magazineSize, Is.GreaterThan(m4.magazineSize * 2));
        Assert.That(lmg.reloadTime, Is.GreaterThan(m4.reloadTime * 2f));
        Assert.That(lmg.tacticalReloadTime, Is.GreaterThan(m4.tacticalReloadTime * 2f));
        Assert.That(lmg.verticalRecoil, Is.GreaterThan(m4.verticalRecoil));
        Assert.That(lmg.adsSpeed, Is.GreaterThan(m4.adsSpeed), "LMG aims slower.");
        Assert.That(lmg.reloadAmmoTime, Is.LessThan(lmg.reloadTime));
        Assert.That(lmg.startingReserveAmmo, Is.LessThanOrEqualTo(lmg.maxReserveAmmo));
    }

    [Test]
    public void ExistingWeaponConfigs_KeepPerShellReloadOff()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { "Assets/Configs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == ShotgunConfigPath)
                continue;
            Assert.That(AssetDatabase.LoadAssetAtPath<WeaponConfig>(path).perShellReload, Is.False, path);
        }
    }

    // ------------------------------------------------------------------ Prefabs

    [Test]
    public void ShotgunPrefab_IsWiredLikeOtherGuns()
    {
        AssertGunPrefabWiring(ShotgunPrefabPath, ShotgunConfigPath, minimumRenderers: 8);
    }

    [Test]
    public void LmgPrefab_IsWiredLikeOtherGuns_AndHasBeltFedAmmoBox()
    {
        AssertGunPrefabWiring(LmgPrefabPath, LmgConfigPath, minimumRenderers: 20);

        GameObject lmg = Spawn(LmgPrefabPath);
        Transform magazine = lmg.transform.Find("Magazine");
        Assert.That(magazine, Is.Not.Null, "WeaponController animates the child named 'Magazine' on reload.");
        Assert.That(magazine.Find("AmmoBox"), Is.Not.Null);
        int links = 0;
        foreach (Transform child in magazine)
        {
            if (child.name.StartsWith("BeltLink"))
                links++;
        }
        Assert.That(links, Is.GreaterThanOrEqualTo(5), "The belt should be visible between the box and the receiver.");
        Assert.That(lmg.transform.Find("BipodLegLeft"), Is.Not.Null);
        Assert.That(lmg.transform.Find("BipodLegRight"), Is.Not.Null);
    }

    [Test]
    public void ShotgunPrefab_HasPumpAndTubeParts()
    {
        GameObject shotgun = Spawn(ShotgunPrefabPath);
        Assert.That(shotgun.transform.Find("Pump"), Is.Not.Null);
        Assert.That(shotgun.transform.Find("ShellTube"), Is.Not.Null);
    }

    private void AssertGunPrefabWiring(string prefabPath, string configPath, int minimumRenderers)
    {
        var prefab = Load<GameObject>(prefabPath);
        var config = Load<WeaponConfig>(configPath);

        var controller = prefab.GetComponent<WeaponController>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.Config, Is.SameAs(config));
        Assert.That(config.weaponPrefab, Is.SameAs(prefab), "WeaponConfig.weaponPrefab should point back at the prefab.");

        var so = new SerializedObject(controller);
        foreach (string field in new[] { "firePoint", "muzzlePoint", "shellEjectPoint" })
        {
            Object reference = so.FindProperty(field).objectReferenceValue;
            Assert.That(reference, Is.Not.Null, field);
            Assert.That(((Component)reference).transform.IsChildOf(prefab.transform));
        }

        var applier = prefab.GetComponent<WeaponSkinApplier>();
        Assert.That(applier, Is.Not.Null);
        Assert.That(applier.DefaultSkin, Is.Not.Null);

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(minimumRenderers));
        foreach (Renderer renderer in renderers)
        {
            Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name);
            Assert.That(renderer, Is.InstanceOf<MeshRenderer>());
            Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null, renderer.name);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        Assert.That(bounds.size.z, Is.InRange(0.5f, 1.3f), "Viewmodel length should be in the same ballpark as the other guns.");
    }

    // ------------------------------------------------------------------ Skins

    [Test]
    public void AllSkinDefinitions_ApplyToBothNewWeapons()
    {
        Assume.That(Shader.Find("Standard") != null, "Standard shader needed for skin fallback materials.");

        var skins = new List<WeaponSkinDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponSkinDefinition", new[] { "Assets/Skins/Definitions" }))
            skins.Add(AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        Assert.That(skins.Count, Is.GreaterThanOrEqualTo(20), "Expected the full skin set.");

        foreach (string prefabPath in new[] { ShotgunPrefabPath, LmgPrefabPath })
        {
            GameObject weapon = Spawn(prefabPath);
            var applier = weapon.GetComponent<WeaponSkinApplier>();
            int rendererCount = weapon.GetComponentsInChildren<Renderer>(true).Length;
            Assert.That(applier.RendererCount, Is.EqualTo(rendererCount), prefabPath);

            foreach (WeaponSkinDefinition skin in skins)
            {
                applier.ApplySkin(skin);
                Assert.That(applier.CurrentSkin, Is.SameAs(skin), prefabPath + " / " + skin.name);
                Material expected = applier.ResolveRenderableMaterial(skin);
                Assert.That(expected, Is.Not.Null, skin.name);
                foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
                    Assert.That(renderer.sharedMaterial, Is.SameAs(expected), prefabPath + " / " + skin.name + " / " + renderer.name);
            }

            applier.ClearSkin();
            Assert.That(applier.CurrentSkin, Is.Null);
        }
    }

    // ------------------------------------------------------------------ Catalog

    [Test]
    public void ShippedCatalog_ListsShotgunAndLmgAsPrimaryAndSecondaryOptions()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        Assert.That(catalog, Is.Not.Null);

        AssertCatalogEntry(catalog, "Shotgun", ShotgunPrefabPath, ShotgunConfigPath, WeaponType.Shotgun);
        AssertCatalogEntry(catalog, "LMG", LmgPrefabPath, LmgConfigPath, WeaponType.LMG);

        var primaryIds = new List<string>();
        foreach (LoadoutWeaponEntry entry in catalog.GetPrimaryOptions())
            primaryIds.Add(entry.Id);
        Assert.That(primaryIds, Does.Contain("Shotgun").And.Contain("LMG"));
        Assert.That(catalog.skins.Length, Is.GreaterThanOrEqualTo(20));
    }

    [Test]
    public void ShippedCatalog_IdsAreUnique_AndOriginalWeaponsRemain()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        var seen = new HashSet<string>();
        foreach (LoadoutWeaponEntry entry in catalog.weapons)
            Assert.That(seen.Add(entry.Id), Is.True, "Duplicate catalog id " + entry.Id);

        foreach (string id in new[] { "M4A1", "SCAR", "UMP", "BoltActionSniper", "DMRSniper", "Karambit", "Shotgun", "LMG" })
            Assert.That(seen, Does.Contain(id));
    }

    [Test]
    public void Catalog_Normalize_KeepsShotgunPrimaryAndLmgSecondary()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        var selection = new LoadoutSelection { PrimaryId = "Shotgun", SecondaryId = "LMG" };
        selection.SetSkinId("LMG", "Skin_Prism");

        catalog.Normalize(selection);

        Assert.That(selection.PrimaryId, Is.EqualTo("Shotgun"));
        Assert.That(selection.SecondaryId, Is.EqualTo("LMG"));
        Assert.That(selection.GetSkinId("LMG"), Is.EqualTo("Skin_Prism"));
    }

    private static void AssertCatalogEntry(WeaponLoadoutCatalog catalog, string id, string prefabPath, string configPath, WeaponType type)
    {
        LoadoutWeaponEntry entry = catalog.FindWeapon(id);
        Assert.That(entry, Is.Not.Null, id + " must be registered (Tools > Loadout > Rebuild Weapon Catalog).");
        Assert.That(entry.prefab, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)));
        Assert.That(entry.config, Is.SameAs(AssetDatabase.LoadAssetAtPath<WeaponConfig>(configPath)));
        Assert.That(entry.WeaponType, Is.EqualTo(type));
        Assert.That(entry.allowPrimary && entry.allowSecondary, Is.True);
        Assert.That(entry.IsMelee, Is.False);
        Assert.That(entry.DisplayName, Is.Not.Empty);
    }
}
