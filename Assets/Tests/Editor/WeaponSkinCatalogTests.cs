using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class WeaponSkinCatalogTests
{
    private const string SkinFolder = "Assets/Skins/Definitions";
    private const string KarambitPrefabPath = "Assets/Weapons/Prefabs/Karambit.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    private static readonly string[] AnimatedPaletteSkins =
    {
        "Skin_StormArc", "Skin_ChromaticShift", "Skin_Holographic", "Skin_EmberAsh", "Skin_FlowingLava",
    };

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

    private static List<WeaponSkinDefinition> LoadAllSkinAssets()
    {
        var skins = new List<WeaponSkinDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponSkinDefinition", new[] { SkinFolder }))
        {
            var skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skin != null)
                skins.Add(skin);
        }
        return skins;
    }

    [Test]
    public void SkinCatalog_ContainsEverySkinDefinitionInTheProject()
    {
        WeaponSkinCatalog catalog = WeaponSkinCatalog.LoadFromResources();
        Assert.That(catalog, Is.Not.Null, "Resources/WeaponSkinCatalog is missing.");

        List<WeaponSkinDefinition> all = LoadAllSkinAssets();
        Assert.That(all.Count, Is.EqualTo(20));
        Assert.That(catalog.skins, Is.EquivalentTo(all),
            "Catalog is stale: run Tools > Skins > Rebuild Weapon Skin Catalog.");
    }

    [Test]
    public void TestRangeSkinCycler_UsesTheWholeCatalog()
    {
        var go = new GameObject("Cycler");
        created.Add(go);
        var cycler = go.AddComponent<TestRangeSkinCycler>();

        Assert.That(cycler.SkinCount, Is.EqualTo(WeaponSkinCatalog.LoadFromResources().skins.Length));
        Assert.That(cycler.SkinCount, Is.EqualTo(20));
    }

    [Test]
    public void PaletteShaderSkins_DoNotOverrideShaderColoursWithASingleAccent()
    {
        // WritePropertyBlock maps one accent colour onto every colour slot, which collapses
        // multi-colour shaders (chromatic shift, holographic, lava, ...) to a single flat colour.
        foreach (string name in AnimatedPaletteSkins)
        {
            var skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>($"{SkinFolder}/Animated/{name}.asset");
            Assert.That(skin, Is.Not.Null, name);
            Assert.That(skin.applyParameterOverrides, Is.False, name);
        }
    }

    [Test]
    public void EmberAshAndFlowingLava_UseDistinctShadersAndPalettes()
    {
        var ember = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>($"{SkinFolder}/Animated/Skin_EmberAsh.asset");
        var lava = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>($"{SkinFolder}/Animated/Skin_FlowingLava.asset");

        Assert.That(ember.material.shader, Is.Not.SameAs(lava.material.shader));
        Color emberBase = ember.material.GetColor("_Color") * ember.material.GetColor("_SkinColor");
        Color lavaBase = lava.material.GetColor("_Color") * lava.material.GetColor("_SkinColor");
        // Ember is a pale charcoal ash; lava is a near-black crust.
        Assert.That(emberBase.grayscale, Is.GreaterThan(lavaBase.grayscale * 1.5f));
    }

    [Test]
    public void StormArc_IntensityIsLowEnoughNotToBlowOut()
    {
        var skin = AssetDatabase.LoadAssetAtPath<WeaponSkinDefinition>($"{SkinFolder}/Animated/Skin_StormArc.asset");
        Assert.That(skin.material.GetFloat("_Intensity"), Is.LessThanOrEqualTo(2f));
    }

    [Test]
    public void KarambitPrefab_HasExactlyOneSkinApplier()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KarambitPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponents<WeaponSkinApplier>().Length, Is.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<WeaponSkinApplier>(true).Length, Is.EqualTo(1));
    }

    [Test]
    public void PlayerPrefab_PistolHasExactlyOneSkinApplier()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        Transform pistol = FindDeep(prefab.transform, "TestPistol");
        Assert.That(pistol, Is.Not.Null);
        Assert.That(pistol.GetComponents<WeaponSkinApplier>().Length, Is.EqualTo(1));
        Assert.That(pistol.GetComponentsInChildren<WeaponSkinApplier>(true).Length, Is.EqualTo(1));
        Assert.That(pistol.GetComponent<WeaponSkinApplier>().DefaultSkin, Is.Not.Null);
    }

    [Test]
    public void GetOrAddSkinApplier_ReusesTheAuthoredApplier()
    {
        var go = new GameObject("Weapon");
        created.Add(go);
        var weapon = go.AddComponent<WeaponController>();
        var authored = go.AddComponent<WeaponSkinApplier>();

        Assert.That(WeaponLoadoutService.GetOrAddSkinApplier(weapon), Is.SameAs(authored));
        Assert.That(go.GetComponents<WeaponSkinApplier>().Length, Is.EqualTo(1));
    }

    [Test]
    public void GetOrAddSkinApplier_AddsOneWhenThePrefabHasNone()
    {
        var go = new GameObject("Weapon");
        created.Add(go);
        var weapon = go.AddComponent<WeaponController>();

        WeaponSkinApplier first = WeaponLoadoutService.GetOrAddSkinApplier(weapon);
        WeaponSkinApplier second = WeaponLoadoutService.GetOrAddSkinApplier(weapon);

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.SameAs(first));
        Assert.That(go.GetComponents<WeaponSkinApplier>().Length, Is.EqualTo(1));
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }
}
