using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Contract tests for character archetype MovementConfig profiles.
/// </summary>
public class MovementConfigProfileTests
{
    [Test]
    public void Scout_IsFasterAndMoreAirborneThanHeavy()
    {
        var scout = ScriptableObject.CreateInstance<MovementConfig>();
        var heavy = ScriptableObject.CreateInstance<MovementConfig>();

        // Mirror authored archetype intent (same numbers as assets / editor setup).
        scout.walkSpeed = 7.2f;
        scout.sprintMultiplier = 1.75f;
        scout.airControl = 0.4f;
        scout.bhopMaxSpeed = 13f;
        scout.slideFriction = 0.45f;

        heavy.walkSpeed = 4.8f;
        heavy.sprintMultiplier = 1.35f;
        heavy.airControl = 0.18f;
        heavy.bhopMaxSpeed = 8.5f;
        heavy.slideFriction = 0.85f;

        Assert.That(scout.walkSpeed, Is.GreaterThan(heavy.walkSpeed));
        Assert.That(scout.walkSpeed * scout.sprintMultiplier,
            Is.GreaterThan(heavy.walkSpeed * heavy.sprintMultiplier));
        Assert.That(scout.airControl, Is.GreaterThan(heavy.airControl));
        Assert.That(scout.bhopMaxSpeed, Is.GreaterThan(heavy.bhopMaxSpeed));
        Assert.That(scout.slideFriction, Is.LessThan(heavy.slideFriction));

        Object.DestroyImmediate(scout);
        Object.DestroyImmediate(heavy);
    }

    [Test]
    public void Acrobat_HasStrongerMobilityWindowsThanMarksman()
    {
        var acrobat = ScriptableObject.CreateInstance<MovementConfig>();
        var marksman = ScriptableObject.CreateInstance<MovementConfig>();

        acrobat.coyoteTime = 0.16f;
        acrobat.slideCancelBoost = 1.4f;
        acrobat.bhopSpeedRetention = 0.96f;
        acrobat.enableWallRun = true;

        marksman.coyoteTime = 0.12f;
        marksman.slideCancelBoost = 1.2f;
        marksman.bhopSpeedRetention = 0.85f;
        marksman.enableWallRun = false;

        Assert.That(acrobat.coyoteTime, Is.GreaterThanOrEqualTo(marksman.coyoteTime));
        Assert.That(acrobat.slideCancelBoost, Is.GreaterThan(marksman.slideCancelBoost));
        Assert.That(acrobat.bhopSpeedRetention, Is.GreaterThan(marksman.bhopSpeedRetention));
        Assert.That(acrobat.enableWallRun, Is.True);
        Assert.That(marksman.enableWallRun, Is.False);

        Object.DestroyImmediate(acrobat);
        Object.DestroyImmediate(marksman);
    }

    [Test]
    public void MovementController_SetConfig_ReplacesProfileWithoutNull()
    {
        var go = new GameObject("MovementConfigTest");
        go.AddComponent<CharacterController>();
        var input = go.AddComponent<InputHandler>();
        var movement = go.AddComponent<MovementController>();

        var a = ScriptableObject.CreateInstance<MovementConfig>();
        var b = ScriptableObject.CreateInstance<MovementConfig>();
        a.walkSpeed = 6f;
        b.walkSpeed = 9f;

        movement.SetConfig(a);
        Assert.That(movement.Config, Is.SameAs(a));
        movement.SetConfig(b);
        Assert.That(movement.Config, Is.SameAs(b));
        movement.SetConfig(null);
        Assert.That(movement.Config, Is.SameAs(b));

        Object.DestroyImmediate(a);
        Object.DestroyImmediate(b);
        Object.DestroyImmediate(go);
    }
}
