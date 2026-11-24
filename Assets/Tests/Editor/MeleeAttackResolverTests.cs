using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class MeleeAttackResolverTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private HitDetection hitDetection;
    private WeaponConfig config;

    [SetUp]
    public void SetUp()
    {
        hitDetection = CreateObject("HitDetection").AddComponent<HitDetection>();
        config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponType = WeaponType.Melee;
        config.damage = 55f;
        config.headshotMultiplier = 1.5f;
        config.limbMultiplier = 0.8f;
        config.meleeRange = 1.8f;
        config.meleeRadius = 0.3f;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
        Object.DestroyImmediate(config);
    }

    [Test]
    public void BuildDirections_CentreRayMatchesForward_AndAllAreUnitLength()
    {
        Vector3[] directions = MeleeAttackResolver.BuildDirections(Vector3.forward, Vector3.up, 1.8f, 0.3f);

        Assert.That(directions.Length, Is.EqualTo(MeleeAttackResolver.RayCount));
        Assert.That(Vector3.Dot(directions[0], Vector3.forward), Is.EqualTo(1f).Within(0.0001f));
        foreach (Vector3 direction in directions)
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void BuildDirections_FansAreSymmetricAroundForward()
    {
        Vector3[] directions = MeleeAttackResolver.BuildDirections(Vector3.forward, Vector3.up, 2f, 0.4f);

        Assert.That(directions[1].x, Is.EqualTo(-directions[2].x).Within(0.0001f));
        Assert.That(directions[3].y, Is.EqualTo(-directions[4].y).Within(0.0001f));
        Assert.That(Mathf.Abs(directions[1].x), Is.GreaterThan(0.1f));
    }

    [Test]
    public void BuildDirections_HandlesForwardParallelToUp()
    {
        Vector3[] directions = MeleeAttackResolver.BuildDirections(Vector3.up, Vector3.up, 1.8f, 0.3f);

        foreach (Vector3 direction in directions)
        {
            Assert.That(float.IsNaN(direction.x), Is.False);
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }
    }

    [Test]
    public void Resolve_TargetInRange_AppliesDamageOnce()
    {
        DamageReceiver target = CreateTarget(new Vector3(0f, 0f, 1.2f));

        HitResult result = Swing();

        Assert.That(result.DidHit, Is.True);
        Assert.That(target.HitCount, Is.EqualTo(1));
        Assert.That(target.LastDamage.Amount, Is.EqualTo(55f).Within(0.001f));
        Assert.That(result.TotalDamageApplied, Is.EqualTo(55f).Within(0.001f));
    }

    [Test]
    public void Resolve_TargetBeyondRange_DoesNothing()
    {
        DamageReceiver target = CreateTarget(new Vector3(0f, 0f, 3.5f));

        HitResult result = Swing();

        Assert.That(result.DidHit, Is.False);
        Assert.That(target.HitCount, Is.Zero);
    }

    [Test]
    public void Resolve_OffCentreTargetWithinRadius_IsStillHit()
    {
        DamageReceiver target = CreateTarget(new Vector3(0.25f, 0f, 1.6f), new Vector3(0.1f, 1f, 0.1f));

        HitResult result = Swing();

        Assert.That(target.HitCount, Is.EqualTo(1));
        Assert.That(result.TotalDamageApplied, Is.GreaterThan(0f));
    }

    [Test]
    public void Resolve_NonAuthoritative_ReportsHitWithoutDamage()
    {
        DamageReceiver target = CreateTarget(new Vector3(0f, 0f, 1.2f));

        HitResult result = MeleeAttackResolver.Resolve(
            hitDetection, config, Vector3.zero, Vector3.forward, Vector3.up, null, 1, 1, false);

        Assert.That(result.DidHit, Is.True);
        Assert.That(target.HitCount, Is.Zero);
        Assert.That(result.TotalDamageApplied, Is.Zero);
    }

    [Test]
    public void Resolve_NullArguments_ReturnNull()
    {
        Assert.That(MeleeAttackResolver.Resolve(null, config, Vector3.zero, Vector3.forward, Vector3.up, null, 1, 1, true), Is.Null);
        Assert.That(MeleeAttackResolver.Resolve(hitDetection, null, Vector3.zero, Vector3.forward, Vector3.up, null, 1, 1, true), Is.Null);
    }

    private HitResult Swing()
    {
        return MeleeAttackResolver.Resolve(
            hitDetection, config, Vector3.zero, Vector3.forward, Vector3.up, null, 1, 1, true);
    }

    private DamageReceiver CreateTarget(Vector3 position, Vector3? size = null)
    {
        GameObject target = CreateObject("MeleeTarget");
        target.transform.position = position;
        BoxCollider box = target.AddComponent<BoxCollider>();
        if (size.HasValue)
            box.size = size.Value;
        target.AddComponent<HitZone>().Configure(HitZoneType.Body);
        DamageReceiver receiver = target.AddComponent<DamageReceiver>();
        Physics.SyncTransforms();
        return receiver;
    }

    private GameObject CreateObject(string name)
    {
        var go = new GameObject(name);
        createdObjects.Add(go);
        return go;
    }
}
