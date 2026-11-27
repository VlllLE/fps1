using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class HumanoidPlayModeTests
{
    private GameObject player;
    private CharacterController characterController;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);

        player = GameObject.Find("Player");
        if (player == null)
        {
            var movement = Object.FindAnyObjectByType<MovementController>();
            if (movement != null)
                player = movement.gameObject;
        }

        if (player != null)
            characterController = player.GetComponent<CharacterController>();
    }

    private static bool IsUnderHeldGun(Transform t, Transform bodyRoot)
    {
        for (; t != null && t != bodyRoot; t = t.parent)
            if (t.name == ThirdPersonGun.RootName)
                return true;
        return false;
    }

    [UnityTest]
    public IEnumerator Player_HasProceduralBodyAndFirstPersonArms()
    {
        Assert.IsNotNull(player, "Player should exist in scene");
        var body = player.GetComponent<HumanoidBody>();
        Assert.IsNotNull(body, "Player body should be upgraded to the humanoid");
        Assert.IsTrue(body.IsBuilt);
        int bodyRenderers = body.BodyRoot.GetComponentsInChildren<Renderer>().Count(r => !IsUnderHeldGun(r.transform, body.BodyRoot));
        if (!string.IsNullOrEmpty(body.ActiveLookId))
            Assert.GreaterOrEqual(bodyRenderers, 1, "the player wears the " + body.ActiveLookId + " look");
        else
            Assert.AreEqual(body.UsesImportedModel ? 4 : HumanoidParts.BoneCount, bodyRenderers);
        Assert.AreEqual(0, body.BodyRoot.GetComponentsInChildren<Collider>().Length, "body must not add colliders to the CharacterController player");

        var arms = player.GetComponentInChildren<ProceduralFirstPersonArms>();
        Assert.IsNotNull(arms, "first-person arms should exist under the camera");
        Assert.IsTrue(arms.IsBuilt);
        Assert.IsNotNull(arms.transform.Find(ProceduralFirstPersonArms.WeaponHolderName), "weapon holder is preserved");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Player_ControllerDimensionsAreUnchangedByTheBody()
    {
        Assert.IsNotNull(characterController);
        yield return null;
        Assert.AreEqual(2f, characterController.height, 0.01f);
        Assert.AreEqual(0.4f, characterController.radius, 0.01f);
    }

    [UnityTest]
    public IEnumerator Player_BodyAnimatesWhileTheSceneRuns()
    {
        var body = player.GetComponent<HumanoidBody>();
        Assert.IsNotNull(body);
        Quaternion before = body.GetBone(HumanoidBone.Chest).localRotation;
        yield return new WaitForSeconds(0.6f);
        Quaternion after = body.GetBone(HumanoidBone.Chest).localRotation;
        Assert.Greater(Quaternion.Angle(before, after), 0.001f, "idle breathing should move the chest");
    }

    [UnityTest]
    public IEnumerator Targets_AreUpgradedToHumanoidsWithWorkingHitVolumes()
    {
        GameObject targetObject = GameObject.Find("Target_Near");
        Assert.IsNotNull(targetObject, "Target_Near should exist");
        var body = targetObject.GetComponent<HumanoidBody>();
        Assert.IsNotNull(body, "Target_Near should have a humanoid body");
        Assert.GreaterOrEqual(body.BodyRoot.GetComponentsInChildren<Renderer>().Length, body.UsesImportedModel ? 4 : HumanoidParts.BoneCount);

        Transform head = targetObject.transform.Find("Head");
        Assert.IsNotNull(head);
        Assert.IsNotNull(head.GetComponent<SphereCollider>());
        Assert.IsNull(head.GetComponent<MeshRenderer>(), "hit volumes are invisible");
        Assert.AreEqual(HitZoneType.Head, head.GetComponent<HitZone>().ZoneType);
        Assert.IsNotNull(targetObject.transform.Find("Body")?.GetComponent<HitZone>());
        yield return null;
    }

    [UnityTest]
    public IEnumerator Bootstrap_UpgradeIsIdempotentAfterLoad()
    {
        Assert.AreEqual(0, HumanoidBodyBootstrap.UpgradeAll());
        yield return null;
    }

    [UnityTest]
    public IEnumerator Target_HitFlashUsesPropertyBlocksAndClearsAfterwards()
    {
        var target = Object.FindAnyObjectByType<Target>();
        Assert.IsNotNull(target);
        var renderer = target.GetComponentInChildren<Renderer>();
        Assert.IsNotNull(renderer);
        int materialCountBefore = renderer.sharedMaterials.Length;

        target.TakeDamage(new DamageInfo
        {
            damage = 1f,
            hitPoint = target.transform.position,
            hitNormal = Vector3.up,
            hitDirection = Vector3.forward,
            isHeadshot = true
        });
        yield return null;

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Assert.IsFalse(block.isEmpty, "flash should be applied through a property block");
        Assert.AreEqual(materialCountBefore, renderer.sharedMaterials.Length);

        yield return new WaitForSeconds(0.25f);
        renderer.GetPropertyBlock(block);
        Assert.IsTrue(block.isEmpty, "flash should clear");
    }
}
