using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>A skinned sniper keeps real scope glass in the player's hands, on the loadout preview and on bots.</summary>
public class ScopeGlassPlayModeTests
{
    private WeaponLoadoutService service;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);
        service = Object.FindAnyObjectByType<WeaponLoadoutService>();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
    }

    [UnityTest]
    public IEnumerator SkinnedSniper_InHand_KeepsGlassShaderOnTheScopeLens([ValueSource(nameof(Snipers))] string weaponId)
    {
        Assert.IsNotNull(service);
        LoadoutWeaponEntry entry = service.Catalog.FindWeapon(weaponId);
        Assert.IsNotNull(entry);

        foreach (string skinId in new[] { "Skin_DesertTiger", "Skin_ChromaticShift" })
        {
            WeaponSkinDefinition skin = service.Catalog.FindSkin(skinId);
            Assert.IsNotNull(skin, skinId);

            Assert.IsTrue(service.SelectPrimary(entry));
            Assert.IsTrue(service.SelectSkin(weaponId, skin));
            yield return null;

            WeaponController weapon = service.GetEquippedWeapon(SlotType.Primary);
            WeaponSkinApplier applier = weapon.GetComponentInChildren<WeaponSkinApplier>(true);
            Assert.AreSame(skin, applier.CurrentSkin);
            Assert.That(applier.GlassSlotCount, Is.GreaterThan(0));

            Renderer scope = FindRenderer(weapon.gameObject, "Scope");
            Assert.IsNotNull(scope);
            int glass = 0;
            foreach (Material material in scope.sharedMaterials)
            {
                if (material.shader.name == WeaponGlass.ShaderName)
                    glass++;
                else
                    Assert.AreSame(skin.material, material, skinId + ": scope body keeps the skin");
            }
            Assert.AreEqual(1, glass, skinId + ": exactly one glass slot on the scope");
        }
    }

    [UnityTest]
    public IEnumerator LoadoutPreview_ShowsTheImportedSniper_WithGlassLens([ValueSource(nameof(Snipers))] string weaponId)
    {
        LoadoutWeaponEntry entry = service.Catalog.FindWeapon(weaponId);
        WeaponSkinDefinition skin = service.Catalog.FindSkin("Skin_DesertTiger");

        using (var rig = new WeaponPreviewRig(160, 90))
        {
            rig.SetPrefab(entry.prefab);
            rig.SetSkin(skin);
            yield return null;

            GameObject preview = null;
            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer.name == "Scope" && renderer.transform.root.name == "WeaponPreviewRig")
                {
                    preview = renderer.gameObject;
                    break;
                }
            }

            Assert.IsNotNull(preview, "the preview must show the imported model (its Scope part), not the primitive fallback");
            Renderer scope = preview.GetComponent<Renderer>();
            bool hasGlass = false;
            bool hasSkin = false;
            foreach (Material material in scope.sharedMaterials)
            {
                hasGlass |= material.shader.name == WeaponGlass.ShaderName;
                hasSkin |= material == skin.material;
            }

            Assert.IsTrue(hasGlass, "preview scope lens must stay glass");
            Assert.IsTrue(hasSkin, "preview scope body must wear the skin");
        }
    }

    [UnityTest]
    public IEnumerator SoldierHoldingASniper_HasGlassLensOnTheThirdPersonGun([ValueSource(nameof(Snipers))] string weaponId)
    {
        LoadoutWeaponEntry entry = service.Catalog.FindWeapon(weaponId);
        var soldier = new GameObject("GlassTestSoldier");
        try
        {
            var controller = soldier.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1f, 0f);
            soldier.transform.position = new Vector3(0f, 0.05f, 30f);
            HumanoidBody body = HumanoidBody.AddTo(soldier, HumanoidBodySettings.Player());
            body.Build();
            body.EquipWeapon(entry.config, entry.prefab);
            yield return null;

            Assert.IsNotNull(body.Gun, "body.Gun for " + weaponId);
            Assert.IsTrue(body.Gun.UsesImportedModel, "imported model for " + weaponId);
            Renderer scope = FindRenderer(body.Gun.Root.gameObject, "Scope");
            Assert.IsNotNull(scope);
            bool glass = false;
            foreach (Material material in scope.sharedMaterials)
                glass |= material.shader.name == WeaponGlass.ShaderName;
            Assert.IsTrue(glass, "third-person scope lens");
        }
        finally
        {
            Object.Destroy(soldier);
        }
    }
    private static readonly string[] Snipers = { "BoltActionSniper", "DMRSniper" };

    private static Renderer FindRenderer(GameObject root, string name)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == name && renderer.gameObject.activeInHierarchy)
                return renderer;
        }
        return null;
    }
}
