using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode contract tests for the deterministic portion of movement. Physics,
/// Input System callbacks, and Time-driven transitions belong in PlayMode tests.
/// </summary>
public class MovementTests
{
    private MovementConfig config;

    [SetUp]
    public void SetUp()
    {
        config = ScriptableObject.CreateInstance<MovementConfig>();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(config);
    }

    [Test]
    public void DefaultConfig_HasOrderedGroundSpeeds()
    {
        float sprintSpeed = config.walkSpeed * config.sprintMultiplier;

        Assert.That(config.walkSpeed, Is.GreaterThan(0f));
        Assert.That(config.sprintMultiplier, Is.GreaterThanOrEqualTo(1f));
        Assert.That(sprintSpeed, Is.GreaterThanOrEqualTo(config.walkSpeed));
        Assert.That(config.groundAcceleration, Is.GreaterThan(0f));
        Assert.That(config.groundFriction, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void DefaultConfig_JumpWindowsAndGravityAreValid()
    {
        Assert.That(config.jumpForce, Is.GreaterThan(0f));
        Assert.That(config.gravityMultiplier, Is.GreaterThan(0f));
        Assert.That(config.coyoteTime, Is.GreaterThanOrEqualTo(0f));
        Assert.That(config.jumpBufferTime, Is.GreaterThanOrEqualTo(0f));
        Assert.That(config.bhopWindow, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void DefaultConfig_SlideThresholdAndColliderAreValid()
    {
        Assert.That(config.minSlideSpeed, Is.GreaterThanOrEqualTo(0f));
        Assert.That(config.slideBoostSpeed, Is.GreaterThanOrEqualTo(config.minSlideSpeed));
        Assert.That(config.slideDuration, Is.GreaterThan(0f));
        Assert.That(config.slideHeight, Is.GreaterThan(0f));
        Assert.That(config.slideCancelBoost, Is.GreaterThanOrEqualTo(1f));
    }

    [Test]
    public void DefaultConfig_BhopRetentionAndCapAreSafe()
    {
        const float currentSpeed = 12f;
        const int chainCount = 5;
        float bonus = config.bhopBonus * Mathf.Min(chainCount, 5);
        float nextSpeed = Mathf.Min(
            currentSpeed * config.bhopSpeedRetention + bonus,
            config.bhopMaxSpeed);

        Assert.That(config.bhopSpeedRetention, Is.InRange(0f, 1.1f));
        Assert.That(config.bhopMaxSpeed, Is.GreaterThanOrEqualTo(config.walkSpeed));
        Assert.That(nextSpeed, Is.LessThanOrEqualTo(config.bhopMaxSpeed));
        Assert.That(nextSpeed, Is.GreaterThan(0f));
    }

    [Test]
    public void DefaultConfig_DiveAndWallDurationsArePositive()
    {
        Assert.That(config.diveDuration, Is.GreaterThan(0f));
        Assert.That(config.diveRecoveryTime, Is.GreaterThanOrEqualTo(0f));
        Assert.That(config.diveCooldown, Is.GreaterThanOrEqualTo(0f));
        Assert.That(config.wallBounceCooldown, Is.GreaterThanOrEqualTo(0f));
        Assert.That(config.maxWallRunTime, Is.GreaterThan(0f));
        Assert.That(config.mantleDuration, Is.GreaterThan(0f));
    }

    [Test]
    public void MovementState_ContainsExpectedDistinctStates()
    {
        string[] expected =
        {
            "Walking",
            "Sprinting",
            "Sliding",
            "Airborne",
            "Diving",
            "DiveRecovery",
            "WallRunning",
            "Mantling"
        };
        string[] actual = Enum.GetNames(typeof(MovementController.MovementState));

        CollectionAssert.AreEquivalent(expected, actual);
        Assert.That(
            Enum.GetValues(typeof(MovementController.MovementState))
                .Cast<MovementController.MovementState>()
                .Select(value => (int)value)
                .Distinct()
                .Count(),
            Is.EqualTo(expected.Length));
    }

    [Test]
    public void ConfigInstances_DoNotShareRuntimeMutation()
    {
        MovementConfig second = ScriptableObject.CreateInstance<MovementConfig>();
        try
        {
            float originalSecondWalkSpeed = second.walkSpeed;
            config.walkSpeed = 99f;

            Assert.That(second.walkSpeed, Is.EqualTo(originalSecondWalkSpeed));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(second);
        }
    }
}
