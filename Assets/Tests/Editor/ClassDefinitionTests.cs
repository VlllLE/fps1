using NUnit.Framework;
using UnityEngine;

public class ClassDefinitionTests
{
    [Test]
    public void ResolvePrefab_PrefersExplicitPrefabOverConfig()
    {
        var prefab = new GameObject("ExplicitPrefab");
        var configPrefab = new GameObject("ConfigPrefab");
        var config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponPrefab = configPrefab;

        try
        {
            Assert.That(ClassDefinition.ResolvePrefab(prefab, config), Is.SameAs(prefab));
            Assert.That(ClassDefinition.ResolvePrefab(null, config), Is.SameAs(configPrefab));
            Assert.That(ClassDefinition.ResolvePrefab(null, null), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(configPrefab);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ClassDefinition_Getters_UsePrimaryAndSecondaryFields()
    {
        var primary = new GameObject("Primary");
        var secondary = new GameObject("Secondary");
        var def = ScriptableObject.CreateInstance<ClassDefinition>();
        def.primaryWeaponPrefab = primary;
        def.secondaryWeaponPrefab = secondary;
        def.moveSpeedMultiplier = 1.08f;

        try
        {
            Assert.That(def.HasPrimary, Is.True);
            Assert.That(def.HasSecondary, Is.True);
            Assert.That(def.HasMelee, Is.False);
            Assert.That(def.GetPrimaryPrefab(), Is.SameAs(primary));
            Assert.That(def.GetSecondaryPrefab(), Is.SameAs(secondary));
            Assert.That(def.moveSpeedMultiplier, Is.EqualTo(1.08f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(primary);
            Object.DestroyImmediate(secondary);
            Object.DestroyImmediate(def);
        }
    }
}
