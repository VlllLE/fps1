using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Training dummies wear the Soldier / Operator / Ghillie looks while keeping their hit volumes, colour cue and classic fallback.</summary>
public class DummyLookTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }

        created.Clear();
    }

    [Test]
    public void ForDummy_DealsEveryLookInTurnAndStaysValidForAnyIndex()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < CharacterLooks.AllIds.Length; i++)
            seen.Add(CharacterLooks.ForDummy(i));
        Assert.That(seen.Count, Is.EqualTo(CharacterLooks.AllIds.Length), "three dummies in a row show all three looks");

        Assert.That(CharacterLooks.ForDummy(0), Is.EqualTo(CharacterLooks.ForDummy(CharacterLooks.AllIds.Length)), "wraps around");
        Assert.That(CharacterLooks.AllIds, Does.Contain(CharacterLooks.ForDummy(-4)));
    }

    [Test]
    public void UpgradingALegacyTarget_WithALook_WearsItAndKeepsHitVolumesAndTint()
    {
        Target target = CreateLegacyTarget(Color.green);

        Assert.That(HumanoidBodyBootstrap.UpgradeTarget(target, CharacterLooks.Operator), Is.True);
        HumanoidBody body = target.GetComponent<HumanoidBody>();
        body.Build();

        Assert.That(body.ActiveLookId, Is.EqualTo(CharacterLooks.Operator), body.LookFallbackReason);
        AssertHitVolumes(target.transform);

        Assert.That(HumanoidBodyBootstrap.UpgradeTarget(target, CharacterLooks.Ghillie), Is.False, "upgrade is idempotent");
        Assert.That(body.ActiveLookId, Is.EqualTo(CharacterLooks.Operator), "a second upgrade does not change the look");
    }

    [Test]
    public void ApplyDummyLook_DressesAClassicDummyInPlaceKeepingItsHitVolumes([ValueSource(typeof(CharacterLooks), nameof(CharacterLooks.AllIds))] string look)
    {
        Target target = CreateDummy(Color.red, null, 1.5f);
        HumanoidBody body = target.GetComponent<HumanoidBody>();
        Assert.That(body.ActiveLookId, Is.Null.Or.Empty);
        int hitboxCountBefore = body.Hitboxes.Count;

        Assert.That(HumanoidBodyBootstrap.ApplyDummyLook(target, look), Is.True);

        Assert.That(body.ActiveLookId, Is.EqualTo(look), body.LookFallbackReason);
        Assert.That(body.UsesImportedModel, Is.True);
        Assert.That(body.Hitboxes.Count, Is.EqualTo(hitboxCountBefore), "same hit volumes as the classic soldier");
        Assert.That(body.BuiltScale, Is.EqualTo(1.5f).Within(1e-4f));
        Assert.That(body.BodyRoot.localPosition.y, Is.EqualTo(-1.5f).Within(1e-4f), "feet stay on the ground below the pivot");
        Assert.That(body.BodyRoot.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
        AssertHitVolumes(target.transform);
    }

    [Test]
    public void ApplyDummyLook_IsIdempotentAndKeepsAnExplicitLook()
    {
        Target classic = CreateDummy(Color.red, null);
        Assert.That(HumanoidBodyBootstrap.ApplyDummyLook(classic, CharacterLooks.Soldier), Is.True);
        Assert.That(HumanoidBodyBootstrap.ApplyDummyLook(classic, CharacterLooks.Ghillie), Is.False, "already dressed");
        Assert.That(classic.GetComponent<HumanoidBody>().ActiveLookId, Is.EqualTo(CharacterLooks.Soldier));

        Target explicitLook = CreateDummy(Color.red, CharacterLooks.Ghillie);
        Assert.That(HumanoidBodyBootstrap.ApplyDummyLook(explicitLook, CharacterLooks.Operator), Is.False, "a map's own choice wins");
        Assert.That(explicitLook.GetComponent<HumanoidBody>().ActiveLookId, Is.EqualTo(CharacterLooks.Ghillie));

        Assert.That(HumanoidBodyBootstrap.ApplyDummyLook(null, CharacterLooks.Soldier), Is.False);
        Assert.That(HumanoidBodyBootstrap.ApplyDummyLook(classic, null), Is.False);
    }

    [Test]
    public void UnknownLook_FallsBackToTheClassicSoldier()
    {
        Target target = CreateDummy(Color.red, null);
        HumanoidBodyBootstrap.ApplyDummyLook(target, "NoSuchLook");

        HumanoidBody body = target.GetComponent<HumanoidBody>();
        Assert.That(body.ActiveLookId, Is.Null.Or.Empty);
        Assert.That(body.LookFallbackReason, Is.Not.Null.And.Not.Empty);
        Assert.That(body.UsesImportedModel, Is.True, "the classic Blender soldier is shown instead");
        AssertHitVolumes(target.transform);
    }

    [Test]
    public void DummyColourCue_StaysDistinguishableOnTheSameLook()
    {
        Target red = CreateDummy(Color.red, null);
        Target blue = CreateDummy(Color.blue, null);
        HumanoidBodyBootstrap.ApplyDummyLook(red, CharacterLooks.Soldier);
        HumanoidBodyBootstrap.ApplyDummyLook(blue, CharacterLooks.Soldier);

        Color redSuit = red.GetComponent<HumanoidBody>().Materials[0].color;
        Color blueSuit = blue.GetComponent<HumanoidBody>().Materials[0].color;
        float redLean = redSuit.r / Mathf.Max(redSuit.b, 1e-4f);
        float blueLean = blueSuit.r / Mathf.Max(blueSuit.b, 1e-4f);
        Assert.That(redLean, Is.GreaterThan(blueLean * 1.1f), "the red dummy leans redder than the blue one on the same look");
    }

    [Test]
    public void FaceTowards_TurnsOnlyTheYawAndIgnoresAPointOnTheTarget()
    {
        Target target = CreateDummy(Color.red, null);
        target.transform.position = new Vector3(0f, 1f, 10f);

        target.FaceTowards(new Vector3(0f, 5f, -5f));
        Assert.That(Vector3.Dot(target.transform.forward, Vector3.back), Is.GreaterThan(0.999f), "faces the spawn");
        Assert.That(target.transform.up.y, Is.GreaterThan(0.999f), "stays upright");

        Quaternion before = target.transform.rotation;
        target.FaceTowards(target.transform.position + Vector3.up * 3f);
        Assert.That(target.transform.rotation, Is.EqualTo(before), "a point straight above has no direction");
    }

    private Target CreateLegacyTarget(Color color)
    {
        var root = new GameObject("Target_LegacyLook");
        created.Add(root);
        root.transform.position = new Vector3(0f, 1f, 8f);

        GameObject legacyBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        legacyBody.name = "Body";
        legacyBody.transform.SetParent(root.transform, false);
        legacyBody.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        legacyBody.AddComponent<HitZone>().Configure(HitZoneType.Body);
        var material = new Material(Shader.Find("Standard")) { color = color };
        created.Add(material);
        legacyBody.GetComponent<Renderer>().sharedMaterial = material;

        GameObject legacyHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        legacyHead.name = "Head";
        legacyHead.transform.SetParent(root.transform, false);
        legacyHead.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        legacyHead.transform.localScale = Vector3.one * 0.5f;
        legacyHead.AddComponent<HitZone>().Configure(HitZoneType.Head);

        return root.AddComponent<Target>();
    }

    private Target CreateDummy(Color tint, string look, float scale = 1f)
    {
        var root = new GameObject("Target_Dummy");
        created.Add(root);
        root.transform.position = new Vector3(0f, scale, 8f);
        HumanoidBodySettings settings = HumanoidBodySettings.Target(tint, scale);
        if (look != null)
        {
            settings.look = look;
            settings.lookTint = CharacterLooks.DummyLookTint;
        }

        HumanoidBody body = HumanoidBody.AddTo(root, settings);
        body.Build();
        return root.AddComponent<Target>();
    }

    private static void AssertHitVolumes(Transform root)
    {
        Transform head = root.Find("Head");
        Transform bodyBox = root.Find("Body");
        Transform leg = root.Find("Leg_L");
        Assert.That(head, Is.Not.Null, "Head hit volume");
        Assert.That(bodyBox, Is.Not.Null, "Body hit volume");
        Assert.That(leg, Is.Not.Null, "limb hit volume");
        Assert.That(head.GetComponent<HitZone>().ZoneType, Is.EqualTo(HitZoneType.Head));
        Assert.That(bodyBox.GetComponent<HitZone>().ZoneType, Is.EqualTo(HitZoneType.Body));
        Assert.That(leg.GetComponent<HitZone>().ZoneType, Is.EqualTo(HitZoneType.Limb));
        Assert.That(head.GetComponent<MeshFilter>(), Is.Null, "hit volumes stay invisible");
        Assert.That(head.GetComponent<Collider>(), Is.Not.Null);
        Assert.That(root.Find("Hitbox_Old"), Is.Null, "no stale hit volumes after a rebuild");
    }
}
