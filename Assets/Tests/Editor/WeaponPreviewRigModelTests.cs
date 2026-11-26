using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class WeaponPreviewRigModelTests
{
    public static IEnumerable<string> ModelWeaponIds()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        foreach (LoadoutWeaponEntry entry in catalog.weapons)
        {
            if (entry != null && entry.prefab != null && WeaponVisualSource.FindModel(entry.prefab, out _) != null)
                yield return entry.Id;
        }
    }

    [Test]
    public void EveryCatalogWeaponHasAnImportedModelToPreview()
    {
        int expected = WeaponLoadoutCatalog.LoadFromResources().weapons.Length;
        Assert.That(new List<string>(ModelWeaponIds()).Count, Is.EqualTo(expected));
    }

    [TestCaseSource(nameof(ModelWeaponIds))]
    public void Preview_OfEveryCatalogWeapon_ShowsTheImportedBlenderModel(string weaponId)
    {
        LoadoutWeaponEntry entry = WeaponLoadoutCatalog.LoadFromResources().FindWeapon(weaponId);
        using (var rig = new WeaponPreviewRig(160, 90))
        {
            rig.SetPrefab(entry.prefab);

            Assert.That(rig.ShowsImportedModel, Is.True, weaponId + " preview must use the imported model, not the primitive fallback");
            Assert.That(rig.PartCount, Is.GreaterThan(0));
        }
    }

    [Test]
    public void Preview_ClearsTheImportedFlagWhenThePrefabIsCleared()
    {
        LoadoutWeaponEntry entry = WeaponLoadoutCatalog.LoadFromResources().FindWeapon("M4A1");
        using (var rig = new WeaponPreviewRig(160, 90))
        {
            rig.SetPrefab(entry.prefab);
            Assert.That(rig.ShowsImportedModel, Is.True);

            rig.SetPrefab(null);
            Assert.That(rig.ShowsImportedModel, Is.False);
        }
    }
}
