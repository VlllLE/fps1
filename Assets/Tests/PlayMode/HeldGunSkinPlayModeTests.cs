using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// In the running game the third-person gun wears the skin of the weapon it copies: the player's chosen skin on the
/// player's own body (also when the skin is changed while the weapon is held), and the weapon's default skin on bots.
/// The scope lens stays on the glass shader in every case.
/// </summary>
public class HeldGunSkinPlayModeTests
{
    private const string FirstSkin = "Skin_DesertTiger";
    private const string SecondSkin = "Skin_ChromaticShift";

    private WeaponLoadoutService service;
    private HumanoidBody body;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);
        service = Object.FindAnyObjectByType<WeaponLoadoutService>();
        PlayerBodyWeaponSync sync = Object.FindAnyObjectByType<PlayerBodyWeaponSync>();
        body = sync != null ? sync.GetComponent<HumanoidBody>() : null;
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
    }

    public static IEnumerable<string> HeldWeaponIds()
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        foreach (LoadoutWeaponEntry entry in catalog.weapons)
        {
            if (entry != null && entry.prefab != null && WeaponVisualSource.FindModel(entry.prefab, out _) != null)
                yield return entry.Id;
        }
    }

    [UnityTest]
    public IEnumerator PlayersHeldGun_WearsThePickedSkin_AndRepaintsWhenItChanges([ValueSource(nameof(HeldWeaponIds))] string weaponId)
    {
        Assert.IsNotNull(service);
        Assert.IsNotNull(body, "the player has a third-person body with a weapon sync");
        LoadoutWeaponEntry entry = service.Catalog.FindWeapon(weaponId);
        Assert.IsNotNull(entry, weaponId);
        WeaponSkinDefinition first = service.Catalog.FindSkin(FirstSkin);
        WeaponSkinDefinition second = service.Catalog.FindSkin(SecondSkin);
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);

        Assert.IsTrue(entry.allowPrimary ? service.SelectPrimary(entry) : service.SelectSecondary(entry), "equip " + weaponId);
        Assert.IsTrue(service.SelectSkin(weaponId, first));
        yield return null;
        yield return null;

        Assert.IsNotNull(body.Gun, "held gun for " + weaponId);
        Assert.IsTrue(body.Gun.UsesImportedModel, weaponId);
        Assert.AreSame(first, body.Gun.AppliedSkin, weaponId + " held gun skin");
        AssertOuterPartsWearSkin(body.Gun, first, weaponId);
        AssertScopeLensIsGlass(body.Gun, weaponId);

        Assert.IsTrue(service.SelectSkin(weaponId, second));
        yield return null;

        Assert.AreSame(second, body.Gun.AppliedSkin, weaponId + " held gun repaints without re-equipping");
        AssertOuterPartsWearSkin(body.Gun, second, weaponId);
        AssertScopeLensIsGlass(body.Gun, weaponId);
    }

    [UnityTest]
    public IEnumerator SoldierHoldingACatalogWeapon_WearsItsDefaultSkin_LikeBotsDo([ValueSource(nameof(HeldWeaponIds))] string weaponId)
    {
        LoadoutWeaponEntry entry = service.Catalog.FindWeapon(weaponId);
        WeaponSkinDefinition expected = entry.prefab.GetComponentInChildren<WeaponSkinApplier>(true).DefaultSkin;
        var soldier = new GameObject("HeldSkinSoldier");
        try
        {
            var controller = soldier.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1f, 0f);
            soldier.transform.position = new Vector3(0f, 0.05f, 30f);
            HumanoidBody soldierBody = HumanoidBody.AddTo(soldier, HumanoidBodySettings.Player());
            soldierBody.Build();
            soldierBody.EquipWeapon(entry.config, entry.prefab);
            yield return null;

            Assert.IsNotNull(soldierBody.Gun);
            Assert.AreSame(expected, soldierBody.Gun.AppliedSkin, weaponId);
            if (expected != null)
                AssertOuterPartsWearSkin(soldierBody.Gun, expected, weaponId);
            AssertScopeLensIsGlass(soldierBody.Gun, weaponId);
        }
        finally
        {
            Object.Destroy(soldier);
        }
    }

    internal static void AssertOuterPartsWearSkin(ThirdPersonGun gun, WeaponSkinDefinition skin, string label)
    {
        int skinned = 0;
        foreach (Renderer renderer in gun.Root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (WeaponGlass.IsGlassSlot(renderer, material))
                    continue;
                bool wears = material != null && (material == skin.material || material.name == skin.material.name + "_Fallback");
                Assert.IsTrue(wears, label + ": " + renderer.name + " wears " + (material != null ? material.name : "null") + ", expected " + skin.name);
                skinned++;
            }
        }

        Assert.Greater(skinned, 0, label);
    }

    internal static void AssertScopeLensIsGlass(ThirdPersonGun gun, string label)
    {
        foreach (Renderer renderer in gun.Root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name != "Scope")
                continue;
            bool glass = false;
            foreach (Material material in renderer.sharedMaterials)
                glass |= material.shader.name == WeaponGlass.ShaderName;
            Assert.IsTrue(glass, label + ": held scope lens keeps the glass shader");
        }
    }
}
