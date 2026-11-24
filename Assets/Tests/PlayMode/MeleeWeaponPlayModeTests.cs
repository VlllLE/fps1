using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MeleeWeaponPlayModeTests
{
    private GameObject root;
    private GameObject targetObject;
    private WeaponConfig config;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.Destroy(root);
        if (targetObject != null)
            Object.Destroy(targetObject);
        if (config != null)
            Object.Destroy(config);
    }

    [UnityTest]
    public IEnumerator MeleeSlash_DamagesTargetInFrontOfCamera_AndSpendsNoAmmo()
    {
        root = new GameObject("MeleeTestRoot");
        root.AddComponent<HitDetection>();

        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();

        targetObject = new GameObject("Target");
        targetObject.transform.position = new Vector3(0f, 0f, 1.2f);
        targetObject.AddComponent<BoxCollider>();
        var receiver = targetObject.AddComponent<MeleeTestReceiver>();
        Physics.SyncTransforms();

        config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponType = WeaponType.Melee;
        config.damage = 40f;
        config.fireRate = 600f;
        config.availableFireModes = new[] { FireMode.Semi };
        config.meleeRange = 1.8f;
        config.meleeHitDelay = 0.05f;

        var weaponObject = new GameObject("Knife");
        weaponObject.transform.SetParent(root.transform, false);
        WeaponController weapon = weaponObject.AddComponent<WeaponController>();
        weapon.SetPlayerCamera(camera);
        weapon.Initialize(config);
        weapon.Equip();

        bool swung = false;
        weapon.OnMeleeSwing += () => swung = true;

        yield return new WaitForSeconds(0.25f);
        weapon.StartFiring();
        weapon.StopFiring();
        yield return new WaitForSeconds(0.2f);

        Assert.That(swung, Is.True);
        Assert.That(receiver.HitCount, Is.EqualTo(1));
        Assert.That(receiver.LastAmount, Is.EqualTo(40f).Within(0.001f));
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(WeaponController.NoAmmoDisplay));
    }

    [UnityTest]
    public IEnumerator MeleeSlash_RespectsAttackInterval()
    {
        root = new GameObject("MeleeTestRoot");
        root.AddComponent<HitDetection>();

        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();

        config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponType = WeaponType.Melee;
        config.fireRate = 600f;
        config.availableFireModes = new[] { FireMode.Semi };
        config.meleeHitDelay = 0f;

        var weaponObject = new GameObject("Knife");
        weaponObject.transform.SetParent(root.transform, false);
        WeaponController weapon = weaponObject.AddComponent<WeaponController>();
        weapon.SetPlayerCamera(camera);
        weapon.Initialize(config);
        weapon.Equip();

        int swings = 0;
        weapon.OnMeleeSwing += () => swings++;
        yield return new WaitForSeconds(0.25f);

        weapon.StartFiring();
        weapon.StopFiring();
        weapon.StartFiring();
        weapon.StopFiring();
        yield return null;

        Assert.That(swings, Is.EqualTo(1), "A second press inside the attack interval must be ignored.");
    }
}

public sealed class MeleeTestReceiver : MonoBehaviour, IHitDamageReceiver
{
    public int HitCount { get; private set; }
    public float LastAmount { get; private set; }

    public void ApplyHitDamage(HitDamageInfo damage)
    {
        HitCount++;
        LastAmount = damage.Amount;
    }
}
