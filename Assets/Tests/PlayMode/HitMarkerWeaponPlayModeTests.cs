using System.Collections;
using System.Reflection;
using CodMovementShooter.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

public class HitMarkerWeaponPlayModeTests
{
    private static readonly string[] BulletWeaponIds = { "M4A1", "UMP", "BoltActionSniper", "DMRSniper", "LMG", "Shotgun" };
    private const string MeleeWeaponId = "Karambit";

    private GameObject root;
    private GameObject bridgeObject;
    private GameObject wall;
    private GameObject target;
    private GameObject projectileObject;
    private WeaponConfig runtimeConfig;
    private int markerCount;
    private static readonly Vector3 FarAway = new Vector3(2000f, 0f, 2000f);
    private Vector3 sceneOffset = FarAway;

    [TearDown]
    public void TearDown()
    {
        DestroyScene();
        if (runtimeConfig != null)
            Object.Destroy(runtimeConfig);
        Time.timeScale = 1f;
    }

    private void DestroyScene()
    {
        foreach (GameObject spawned in new[] { root, bridgeObject, wall, target, projectileObject })
        {
            if (spawned != null)
                Object.DestroyImmediate(spawned);
        }

        root = bridgeObject = wall = target = projectileObject = null;
        sceneOffset = FarAway;
        markerCount = 0;
    }

    [UnityTest]
    public IEnumerator Bullets_IntoWorldGeometry_ShowNoHitMarker()
    {
        foreach (string id in BulletWeaponIds)
        {
            DestroyScene();
            WeaponController weapon = SpawnWeapon(id);
            SpawnWall(1.2f);

            yield return ShootOnce(weapon);

            Assert.That(markerCount, Is.EqualTo(0), id + " shot a wall and must not show a hit marker.");
        }
    }

    [UnityTest]
    public IEnumerator Bullets_IntoEmptyAir_ShowNoHitMarker()
    {
        foreach (string id in BulletWeaponIds)
        {
            DestroyScene();
            WeaponController weapon = SpawnWeapon(id);

            yield return ShootOnce(weapon);

            Assert.That(markerCount, Is.EqualTo(0), id + " missed and must not show a hit marker.");
        }
    }

    [UnityTest]
    public IEnumerator Bullets_BehindAWall_ShowNoHitMarker()
    {
        foreach (string id in BulletWeaponIds)
        {
            DestroyScene();
            WeaponController weapon = SpawnWeapon(id);
            SpawnWall(1.2f);
            SpawnTarget(3f, out MeleeTestReceiver receiver);

            yield return ShootOnce(weapon);

            Assert.That(receiver.HitCount, Is.EqualTo(0), id + " should be blocked by the wall.");
            Assert.That(markerCount, Is.EqualTo(0), id + " struck the wall only.");
        }
    }

    [UnityTest]
    public IEnumerator Bullets_IntoTheShootersOwnBody_ShowNoHitMarker()
    {
        foreach (string id in BulletWeaponIds)
        {
            DestroyScene();
            WeaponController weapon = SpawnWeapon(id);
            var ownBody = new GameObject("OwnBody");
            ownBody.transform.SetParent(root.transform, false);
            ownBody.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            ownBody.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 0.5f);
            MeleeTestReceiver ownReceiver = ownBody.AddComponent<MeleeTestReceiver>();
            Physics.SyncTransforms();

            yield return ShootOnce(weapon);

            Assert.That(ownReceiver.HitCount, Is.EqualTo(0), id + " must ignore the shooter's own colliders.");
            Assert.That(markerCount, Is.EqualTo(0), id + " must not confirm a hit on the shooter.");
        }
    }

    [UnityTest]
    public IEnumerator Bullets_IntoADamageableTarget_ShowAHitMarker()
    {
        foreach (string id in BulletWeaponIds)
        {
            DestroyScene();
            WeaponController weapon = SpawnWeapon(id);
            SpawnTarget(1.2f, out MeleeTestReceiver receiver);

            yield return ShootOnce(weapon);

            Assert.That(receiver.HitCount, Is.GreaterThan(0), id + " should damage the target.");
            Assert.That(markerCount, Is.EqualTo(1), id + " should confirm the hit exactly once per shot.");
        }
    }

    [UnityTest]
    public IEnumerator Melee_OnWorldGeometryOrAir_ShowsNoHitMarker()
    {
        WeaponController weapon = SpawnWeapon(MeleeWeaponId);
        yield return SwingOnce(weapon);
        Assert.That(markerCount, Is.EqualTo(0), "Swinging at air must not show a hit marker.");

        DestroyScene();
        weapon = SpawnWeapon(MeleeWeaponId);
        SpawnWall(1.0f);
        yield return SwingOnce(weapon);
        Assert.That(markerCount, Is.EqualTo(0), "Knife striking a wall must not show a hit marker.");
    }

    [UnityTest]
    public IEnumerator Melee_OnAShootersOwnBody_ShowsNoHitMarker()
    {
        WeaponController weapon = SpawnWeapon(MeleeWeaponId);
        var ownBody = new GameObject("OwnBody");
        ownBody.transform.SetParent(root.transform, false);
        ownBody.transform.localPosition = new Vector3(0f, 0f, 1.0f);
        ownBody.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 0.5f);
        MeleeTestReceiver ownReceiver = ownBody.AddComponent<MeleeTestReceiver>();
        Physics.SyncTransforms();

        yield return SwingOnce(weapon);

        Assert.That(ownReceiver.HitCount, Is.EqualTo(0));
        Assert.That(markerCount, Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator Melee_OnADamageableTarget_ShowsAHitMarker()
    {
        WeaponController weapon = SpawnWeapon(MeleeWeaponId);
        SpawnTarget(1.0f, out MeleeTestReceiver receiver);

        yield return SwingOnce(weapon);

        Assert.That(receiver.HitCount, Is.EqualTo(1));
        Assert.That(markerCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Projectile_IntoWorldGeometry_ShowsNoHitMarker()
    {
        HitResult result = null;
        SpawnProjectile(r => result = r, Vector3.zero);
        SpawnWall(5f);

        yield return WaitForProjectile(() => result != null);

        Assert.That(result, Is.Not.Null, "The projectile should have struck the wall.");
        Assert.That(result.DidHit, Is.True);
        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.False);
    }

    [UnityTest]
    public IEnumerator Projectile_IntoADamageableTarget_ShowsAHitMarker()
    {
        HitResult result = null;
        SpawnProjectile(r => result = r, Vector3.zero);
        SpawnTarget(5f, out MeleeTestReceiver receiver);

        yield return WaitForProjectile(() => result != null);

        Assert.That(result, Is.Not.Null, "The projectile should have struck the target.");
        Assert.That(receiver.HitCount, Is.EqualTo(1));
        Assert.That(WeaponHudBridge.TryExtractHit(result, out float damage, out _), Is.True);
        Assert.That(damage, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator Projectile_IntoTheShootersOwnBody_IsIgnored()
    {
        HitResult result = null;
        root = new GameObject("Shooter");
        root.transform.position = sceneOffset;
        root.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 2f);
        MeleeTestReceiver ownReceiver = root.AddComponent<MeleeTestReceiver>();
        SpawnProjectile(r => result = r, new Vector3(0f, 0f, -3f), root.transform);
        yield return WaitForProjectile(() => result != null, 0.6f);

        Assert.That(ownReceiver.HitCount, Is.EqualTo(0));
        Assert.That(result == null || !WeaponHudBridge.TryExtractHit(result, out _, out _), Is.True);
    }

    private IEnumerator ShootOnce(WeaponController weapon)
    {
        yield return null;
        weapon.Equip();
        yield return new WaitForSeconds(1.0f);
        markerCount = 0;
        weapon.StartFiring();
        weapon.StopFiring();
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator SwingOnce(WeaponController weapon)
    {
        yield return null;
        weapon.Equip();
        yield return new WaitForSeconds(1.0f);
        markerCount = 0;
        weapon.StartFiring();
        weapon.StopFiring();
        yield return new WaitForSeconds(0.8f);
    }

    private static IEnumerator WaitForProjectile(System.Func<bool> done, float timeout = 3f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!done() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    private WeaponController SpawnWeapon(string id)
    {
        root = new GameObject("WeaponTestRoot");
        root.transform.position = sceneOffset;
        root.AddComponent<HitDetection>();

        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();

        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        Assert.That(catalog, Is.Not.Null);
        LoadoutWeaponEntry entry = catalog.FindWeapon(id);
        Assert.That(entry, Is.Not.Null, id + " missing from the weapon catalog");

        GameObject instance = Object.Instantiate(entry.prefab, root.transform);
        WeaponController weapon = instance.GetComponent<WeaponController>();
        Assert.That(weapon, Is.Not.Null);
        weapon.SetPlayerCamera(camera);

        bridgeObject = new GameObject("HudBridge");
        bridgeObject.SetActive(false);
        WeaponHudBridge bridge = bridgeObject.AddComponent<WeaponHudBridge>();
        typeof(WeaponHudBridge)
            .GetField("weaponSource", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(bridge, weapon);
        bridge.OnHitConfirmed = new UnityEvent();
        bridge.OnHitConfirmed.AddListener(() => markerCount++);
        bridgeObject.SetActive(true);
        return weapon;
    }

    private void SpawnWall(float distance)
    {
        wall = new GameObject("Wall");
        wall.transform.position = sceneOffset + new Vector3(0f, 0f, distance);
        wall.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 0.5f);
        Physics.SyncTransforms();
    }

    private void SpawnTarget(float distance, out MeleeTestReceiver receiver)
    {
        target = new GameObject("Target");
        target.transform.position = sceneOffset + new Vector3(0f, 0f, distance);
        target.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 0.5f);
        receiver = target.AddComponent<MeleeTestReceiver>();
        Physics.SyncTransforms();
    }

    private void SpawnProjectile(System.Action<HitResult> onHit, Vector3 position, Transform ownerRoot = null)
    {
        if (root == null)
            root = new GameObject("HitDetectionRoot");
        if (root.GetComponent<HitDetection>() == null)
            root.AddComponent<HitDetection>();

        runtimeConfig = ScriptableObject.CreateInstance<WeaponConfig>();
        runtimeConfig.damage = 30f;
        runtimeConfig.range = 50f;
        runtimeConfig.projectileSpeed = 30f;
        runtimeConfig.projectileGravity = 0f;
        runtimeConfig.damageFalloff = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        projectileObject = new GameObject("Projectile");
        projectileObject.transform.position = sceneOffset + position;
        projectileObject.AddComponent<SphereCollider>().radius = 0.1f;
        projectileObject.AddComponent<Rigidbody>();
        Projectile projectile = projectileObject.AddComponent<Projectile>();
        typeof(Projectile)
            .GetField("hitDetection", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(projectile, root.GetComponent<HitDetection>());
        projectile.Initialize(runtimeConfig, Vector3.forward, 12345, ownerRoot);
        projectile.OnProjectileHit += onHit;
    }
}
