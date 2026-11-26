using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// A soldier holding a gun has exactly one pair of arms. The local player additionally owns a first-person arm
/// model (the viewmodel) on the camera; it is only meant for that camera, so a capture camera that renders every
/// layer must leave it out or it shows up as a second pair of arms next to the third-person body.
/// </summary>
public class SoldierArmCountPlayModeTests
{
    private const string ArmLeft = "Soldier_ArmL";
    private const string ArmRight = "Soldier_ArmR";

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

    private static bool IsUnderHeldGun(Transform t, Transform bodyRoot)
    {
        for (; t != null && t != bodyRoot; t = t.parent)
            if (t.name == ThirdPersonGun.RootName)
                return true;
        return false;
    }

    private static List<Renderer> BodyArmRenderers(HumanoidBody body)
    {
        return body.BodyRoot.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.activeInHierarchy && r.enabled && !IsUnderHeldGun(r.transform, body.BodyRoot))
            .Where(r => r.name == ArmLeft || r.name == ArmRight)
            .ToList();
    }

    [UnityTest]
    public IEnumerator SoldierHoldingAnyGun_HasExactlyOneLeftAndOneRightArm()
    {
        Assert.IsNotNull(service);
        var soldier = new GameObject("ArmCountSoldier");
        try
        {
            var controller = soldier.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 1f, 0f);
            soldier.transform.position = new Vector3(0f, 0.05f, 30f);
            HumanoidBody body = HumanoidBody.AddTo(soldier, HumanoidBodySettings.Player());
            body.Build();
            Assert.IsTrue(body.UsesImportedModel, "the imported soldier is expected here");

            int checkedWeapons = 0;
            foreach (LoadoutWeaponEntry entry in service.Catalog.weapons)
            {
                if (entry.prefab == null || entry.config == null)
                    continue;

                body.EquipWeapon(entry.config, entry.prefab);
                yield return null;

                List<Renderer> arms = BodyArmRenderers(body);
                Assert.AreEqual(1, arms.Count(r => r.name == ArmLeft), entry.Id + ": left arm count");
                Assert.AreEqual(1, arms.Count(r => r.name == ArmRight), entry.Id + ": right arm count");

                if (body.Gun != null)
                {
                    foreach (Renderer renderer in body.Gun.Root.GetComponentsInChildren<Renderer>(true))
                    {
                        Assert.IsFalse(renderer is SkinnedMeshRenderer, entry.Id + ": the held gun must not carry a skinned limb (" + renderer.name + ")");
                        Assert.IsFalse(renderer.name.StartsWith("Soldier_"), entry.Id + ": the held gun must not carry a soldier limb mesh (" + renderer.name + ")");
                    }
                }

                checkedWeapons++;
            }

            Assert.Greater(checkedWeapons, 4, "the catalog should supply several weapons");
        }
        finally
        {
            Object.Destroy(soldier);
        }
    }

    [UnityTest]
    public IEnumerator LocalPlayer_PlayerCameraSeesExactlyOneArmPair_WithAnyGun()
    {
        Assert.IsNotNull(service);
        var player = Object.FindAnyObjectByType<WeaponSwitcher>().transform.root;
        var body = player.GetComponent<HumanoidBody>();
        var firstPersonArms = player.GetComponentInChildren<ProceduralFirstPersonArms>(true);
        Camera playerCamera = firstPersonArms.GetComponentInParent<Camera>();
        Assert.IsNotNull(body);
        Assert.IsNotNull(firstPersonArms);
        Assert.IsNotNull(playerCamera);

        foreach (string weaponId in new[] { "BoltActionSniper", "DMRSniper", "M4A1" })
        {
            LoadoutWeaponEntry entry = service.Catalog.FindWeapon(weaponId);
            if (entry == null)
                continue;

            Assert.IsTrue(service.SelectPrimary(entry), weaponId);
            yield return null;
            yield return null;

            var visibleToPlayerCamera = new List<Renderer>();
            visibleToPlayerCamera.AddRange(BodyArmRenderers(body).Where(r => (playerCamera.cullingMask & (1 << r.gameObject.layer)) != 0));
            Assert.AreEqual(0, visibleToPlayerCamera.Count, weaponId + ": the third-person arms must be culled from the player camera");

            List<SkinnedMeshRenderer> viewmodelArms = firstPersonArms.GetComponentsInChildren<SkinnedMeshRenderer>(false)
                .Where(r => r.enabled && (playerCamera.cullingMask & (1 << r.gameObject.layer)) != 0)
                .ToList();
            Assert.AreEqual(1, viewmodelArms.Count, weaponId + ": the first-person arm model is one mesh holding both arms");
            Assert.IsTrue(viewmodelArms[0].transform.IsChildOf(playerCamera.transform), weaponId + ": the viewmodel arms hang under the player camera");

            Assert.AreEqual(2, BodyArmRenderers(body).Count, weaponId + ": the third-person body still has its own pair for other cameras");
        }
    }

    [UnityTest]
    public IEnumerator ViewmodelArms_AreTheOnlyArmsAnotherCameraSeesBesidesTheBody()
    {
        var player = Object.FindAnyObjectByType<WeaponSwitcher>().transform.root;
        var firstPersonArms = player.GetComponentInChildren<ProceduralFirstPersonArms>(true);
        var body = player.GetComponent<HumanoidBody>();
        yield return null;

        var arms = new List<Renderer>(BodyArmRenderers(body));
        foreach (SkinnedMeshRenderer renderer in firstPersonArms.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (renderer.enabled)
                arms.Add(renderer);
        }

        int viewmodel = arms.Count(r => r.transform.IsChildOf(firstPersonArms.transform));
        int third = arms.Count - viewmodel;
        Assert.AreEqual(1, viewmodel, "one viewmodel arm mesh");
        Assert.AreEqual(2, third, "two third-person arm meshes");
    }
}
