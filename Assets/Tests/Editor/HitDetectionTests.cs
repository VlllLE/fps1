using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HitDetectionTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private HitDetection hitDetection;

    [SetUp]
    public void SetUp()
    {
        var resolverObject = CreateObject("HitDetection");
        hitDetection = resolverObject.AddComponent<HitDetection>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }

    [Test]
    public void Resolve_Miss_ReturnsEmptyResult()
    {
        HitResult result = hitDetection.Resolve(
            HitRequest.Create(Vector3.zero, Vector3.forward, 10f, 20f));

        Assert.That(result.DidHit, Is.False);
        Assert.That(result.Impacts, Is.Empty);
        Assert.That(result.TotalDamageApplied, Is.Zero);
    }

    [TestCase(HitZoneType.Head, 2f, 40f)]
    [TestCase(HitZoneType.Body, 1f, 20f)]
    [TestCase(HitZoneType.Limb, 0.75f, 15f)]
    public void Resolve_AppliesConfiguredZoneMultiplier(
        HitZoneType zoneType,
        float multiplier,
        float expectedDamage)
    {
        DamageReceiver receiver = CreateDamageTarget(new Vector3(0f, 0f, 5f), zoneType);
        HitRequest request = HitRequest.Create(Vector3.zero, Vector3.forward, 10f, 20f);
        request.HeadMultiplier = zoneType == HitZoneType.Head ? multiplier : 2f;
        request.BodyMultiplier = zoneType == HitZoneType.Body ? multiplier : 1f;
        request.LimbMultiplier = zoneType == HitZoneType.Limb ? multiplier : 0.75f;

        HitResult result = hitDetection.Resolve(request);

        Assert.That(result.DidHit, Is.True);
        Assert.That(receiver.HitCount, Is.EqualTo(1));
        Assert.That(receiver.LastDamage.Amount, Is.EqualTo(expectedDamage).Within(0.001f));
        Assert.That(receiver.LastDamage.Zone, Is.EqualTo(zoneType));
        Assert.That(result.TotalDamageApplied, Is.EqualTo(expectedDamage).Within(0.001f));
    }

    [Test]
    public void Resolve_NonAuthoritativeRequest_ReportsHitWithoutMutatingHealth()
    {
        DamageReceiver receiver = CreateDamageTarget(new Vector3(0f, 0f, 5f), HitZoneType.Body);
        HitRequest request = HitRequest.Create(Vector3.zero, Vector3.forward, 10f, 20f);
        request.IsAuthoritative = false;

        HitResult result = hitDetection.Resolve(request);

        Assert.That(result.DidHit, Is.True);
        Assert.That(result.Impacts[0].DamageApplied, Is.False);
        Assert.That(receiver.HitCount, Is.Zero);
        Assert.That(result.TotalDamageApplied, Is.Zero);
    }

    [Test]
    public void Resolve_IgnoresShooterHierarchy()
    {
        GameObject shooter = CreateObject("Shooter");
        shooter.transform.position = new Vector3(0f, 0f, 1f);
        shooter.AddComponent<BoxCollider>();
        DamageReceiver target = CreateDamageTarget(new Vector3(0f, 0f, 5f), HitZoneType.Body);
        Physics.SyncTransforms();

        HitRequest request = HitRequest.Create(Vector3.zero, Vector3.forward, 10f, 10f);
        request.IgnoreRoot = shooter.transform;
        HitResult result = hitDetection.Resolve(request);

        Assert.That(result.Impacts.Count, Is.EqualTo(1));
        Assert.That(target.HitCount, Is.EqualTo(1));
    }

    [Test]
    public void Resolve_RespectsRequestLayerMask()
    {
        const int includedLayer = 8;
        const int excludedLayer = 9;
        DamageReceiver included = CreateDamageTarget(new Vector3(0f, 0f, 6f), HitZoneType.Body);
        included.gameObject.layer = includedLayer;
        DamageReceiver excluded = CreateDamageTarget(new Vector3(0f, 0f, 3f), HitZoneType.Body);
        excluded.gameObject.layer = excludedLayer;
        Physics.SyncTransforms();

        HitRequest request = HitRequest.Create(Vector3.zero, Vector3.forward, 10f, 10f);
        request.HitLayers = 1 << includedLayer;
        HitResult result = hitDetection.Resolve(request);

        Assert.That(result.Impacts.Count, Is.EqualTo(1));
        Assert.That(result.Impacts[0].RaycastHit.collider.gameObject.layer, Is.EqualTo(includedLayer));
        Assert.That(included.HitCount, Is.EqualTo(1));
        Assert.That(excluded.HitCount, Is.Zero);
    }

    [Test]
    public void Resolve_PenetrationRetainsDamageAndHitsNextTarget()
    {
        const int penetrableLayer = 8;
        DamageReceiver first = CreateDamageTarget(new Vector3(0f, 0f, 3f), HitZoneType.Body);
        first.gameObject.layer = penetrableLayer;
        DamageReceiver second = CreateDamageTarget(new Vector3(0f, 0f, 6f), HitZoneType.Body);
        SetPrivateField(hitDetection, "penetrableLayers", (LayerMask)(1 << penetrableLayer));
        Physics.SyncTransforms();

        HitRequest request = HitRequest.Create(Vector3.zero, Vector3.forward, 10f, 20f);
        request.AllowPenetration = true;
        request.MaxPenetrations = 1;
        request.PenetrationDamageRetention = 0.5f;
        HitResult result = hitDetection.Resolve(request);

        Assert.That(result.Impacts.Count, Is.EqualTo(2));
        Assert.That(result.PenetrationCount, Is.EqualTo(1));
        Assert.That(first.LastDamage.Amount, Is.EqualTo(20f).Within(0.001f));
        Assert.That(second.LastDamage.Amount, Is.EqualTo(10f).Within(0.001f));
        Assert.That(result.Impacts[1].IsAfterPenetration, Is.True);
    }

    [Test]
    public void Resolve_InvokesSurfaceHookAndImpactEvent()
    {
        GameObject surfaceObject = CreateObject("Surface");
        surfaceObject.transform.position = new Vector3(0f, 0f, 4f);
        surfaceObject.AddComponent<BoxCollider>();
        SurfaceReceiver surface = surfaceObject.AddComponent<SurfaceReceiver>();
        Physics.SyncTransforms();
        int eventCount = 0;
        hitDetection.ImpactResolved += _ => eventCount++;

        hitDetection.Resolve(HitRequest.Create(Vector3.zero, Vector3.forward, 10f, 1f));

        Assert.That(surface.HitCount, Is.EqualTo(1));
        Assert.That(eventCount, Is.EqualTo(1));
    }

    [Test]
    public void Resolve_RejectsInvalidDirection()
    {
        HitRequest request = HitRequest.Create(Vector3.zero, Vector3.zero, 10f, 1f);
        Assert.Throws<ArgumentException>(() => hitDetection.Resolve(request));
    }

    private DamageReceiver CreateDamageTarget(Vector3 position, HitZoneType zone)
    {
        GameObject target = CreateObject("DamageTarget");
        target.transform.position = position;
        target.AddComponent<BoxCollider>();
        target.AddComponent<HitZone>().Configure(zone);
        DamageReceiver receiver = target.AddComponent<DamageReceiver>();
        Physics.SyncTransforms();
        return receiver;
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Expected private test seam was not found.");
        field.SetValue(target, value);
    }
}

public sealed class DamageReceiver : MonoBehaviour, IHitDamageReceiver
{
    public int HitCount { get; private set; }
    public HitDamageInfo LastDamage { get; private set; }

    public void ApplyHitDamage(HitDamageInfo damage)
    {
        HitCount++;
        LastDamage = damage;
    }
}

public sealed class SurfaceReceiver : MonoBehaviour, IHitSurfaceResponse
{
    public int HitCount { get; private set; }

    public void OnHitSurface(HitImpact impact)
    {
        HitCount++;
    }
}
