using System.Collections.Generic;
using CodMovementShooter.UI;
using NUnit.Framework;
using UnityEngine;

public class HitMarkerConfirmationTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private HitDetection hitDetection;

    [SetUp]
    public void SetUp()
    {
        hitDetection = CreateObject("HitDetection").AddComponent<HitDetection>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }

    [Test]
    public void Miss_ShowsNoHitMarker()
    {
        HitResult result = Fire(Vector3.forward);

        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.False);
    }

    [Test]
    public void WorldGeometry_ShowsNoHitMarker()
    {
        CreateWall(2f);

        HitResult result = Fire(Vector3.forward);

        Assert.That(result.DidHit, Is.True, "The ray does strike the wall; only the marker must stay hidden.");
        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.False);
    }

    [Test]
    public void Target_ShowsHitMarkerWithDamageAndZone()
    {
        CreateTarget(4f, HitZoneType.Head);

        HitResult result = Fire(Vector3.forward);

        Assert.That(WeaponHudBridge.TryExtractHit(result, out float damage, out bool headshot), Is.True);
        Assert.That(damage, Is.GreaterThan(0f));
        Assert.That(headshot, Is.True);
    }

    [Test]
    public void WallInFrontOfTarget_ShowsNoHitMarker()
    {
        CreateWall(2f);
        CreateTarget(4f, HitZoneType.Body);

        HitResult result = Fire(Vector3.forward);

        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.False);
    }

    [Test]
    public void ShootersOwnBody_ShowsNoHitMarker()
    {
        GameObject shooter = CreateObject("Shooter");
        shooter.transform.position = new Vector3(0f, 0f, 1f);
        shooter.AddComponent<BoxCollider>();
        shooter.AddComponent<DamageReceiver>();
        Physics.SyncTransforms();

        HitRequest request = HitRequest.Create(Vector3.zero, Vector3.forward, 20f, 25f);
        request.IgnoreRoot = shooter.transform;
        HitResult result = hitDetection.Resolve(request);

        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.False);
    }

    [Test]
    public void ReceiverThatTookNoDamage_ShowsNoHitMarker()
    {
        CreateTarget(4f, HitZoneType.Body);
        HitRequest request = HitRequest.Create(Vector3.zero, Vector3.forward, 20f, 25f);
        request.IsAuthoritative = false;

        HitResult result = hitDetection.Resolve(request);

        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.False);
    }

    [Test]
    public void ShotgunBlast_AllPelletsOnWallOrMissing_ShowsNoHitMarker()
    {
        CreateWall(2f);

        var pellets = new List<HitResult> { Fire(Vector3.forward), Fire(Vector3.up), Fire(Vector3.forward) };

        Assert.That(WeaponHudBridge.TryExtractShotgunHit(pellets, out _, out _), Is.False);
    }

    [Test]
    public void ShotgunBlast_OneDamagingPellet_ShowsHitMarkerWithSummedDamage()
    {
        CreateTarget(4f, HitZoneType.Body);

        var pellets = new List<HitResult> { Fire(Vector3.up), Fire(Vector3.forward), Fire(Vector3.forward) };

        Assert.That(WeaponHudBridge.TryExtractShotgunHit(pellets, out float total, out _), Is.True);
        Assert.That(total, Is.GreaterThan(0f));
    }

    [Test]
    public void MeleeSwingOnWorldGeometry_ShowsNoHitMarker()
    {
        WeaponConfig config = CreateMeleeConfig();
        CreateWall(1f);

        HitResult result = Swing(config);

        Assert.That(result.DidHit, Is.True);
        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.False);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void MeleeSwingOnTarget_ShowsHitMarker()
    {
        WeaponConfig config = CreateMeleeConfig();
        CreateTarget(1f, HitZoneType.Body);

        HitResult result = Swing(config);

        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.True);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void MeleeSwingInEmptyAir_ShowsNoHitMarker()
    {
        WeaponConfig config = CreateMeleeConfig();

        HitResult result = Swing(config);

        Assert.That(WeaponHudBridge.TryExtractHit(result, out _, out _), Is.False);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void NullResult_ShowsNoHitMarker()
    {
        Assert.That(WeaponHudBridge.TryExtractHit(null, out _, out _), Is.False);
        Assert.That(WeaponHudBridge.TryExtractShotgunHit(null, out _, out _), Is.False);
    }

    private HitResult Fire(Vector3 direction)
    {
        return hitDetection.Resolve(HitRequest.Create(Vector3.zero, direction, 20f, 25f));
    }

    private HitResult Swing(WeaponConfig config)
    {
        return MeleeAttackResolver.Resolve(
            hitDetection, config, Vector3.zero, Vector3.forward, Vector3.up, null, 1, 1, true);
    }

    private static WeaponConfig CreateMeleeConfig()
    {
        WeaponConfig config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponType = WeaponType.Melee;
        config.damage = 40f;
        config.meleeRange = 1.8f;
        return config;
    }

    private void CreateWall(float distance)
    {
        GameObject wall = CreateObject("Wall");
        wall.transform.position = new Vector3(0f, 0f, distance);
        wall.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 0.25f);
        Physics.SyncTransforms();
    }

    private void CreateTarget(float distance, HitZoneType zone)
    {
        GameObject target = CreateObject("Target");
        target.transform.position = new Vector3(0f, 0f, distance);
        target.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, 0.25f);
        target.AddComponent<HitZone>().Configure(zone);
        target.AddComponent<DamageReceiver>();
        Physics.SyncTransforms();
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
