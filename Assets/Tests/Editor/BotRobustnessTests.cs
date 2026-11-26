using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BotStuckDetectorTests
{
    private BotStuckDetector detector;

    [SetUp]
    public void SetUp()
    {
        detector = new BotStuckDetector { Window = 2f, MinProgress = 0.6f, LongWindow = 14f, LongMinProgress = 3f };
        detector.Reset(Vector3.zero);
    }

    private List<BotStuckAction> Run(float seconds, float dt, System.Func<float, Vector3> position, bool shouldMove = true,
        bool trackLong = true)
    {
        var actions = new List<BotStuckAction>();
        for (float t = 0f; t < seconds; t += dt)
        {
            BotStuckAction action = detector.Update(dt, position(t), shouldMove, trackLong);
            if (action != BotStuckAction.None)
                actions.Add(action);
        }

        return actions;
    }

    [Test]
    public void MovingBot_IsNeverFlagged()
    {
        List<BotStuckAction> actions = Run(60f, 0.1f, t => new Vector3(t * 2f, 0f, 0f));

        Assert.That(actions, Is.Empty);
        Assert.That(detector.Level, Is.EqualTo(0));
    }

    [Test]
    public void BotThatDoesNotWantToMove_IsNeverFlagged()
    {
        List<BotStuckAction> actions = Run(60f, 0.1f, t => Vector3.zero, shouldMove: false);

        Assert.That(actions, Is.Empty);
    }

    [Test]
    public void StandingStill_EscalatesRepathNewGoalSteeringRespawn()
    {
        List<BotStuckAction> actions = Run(9f, 0.1f, t => Vector3.zero);

        Assert.That(actions, Is.EqualTo(new[]
        {
            BotStuckAction.Repath, BotStuckAction.NewGoal, BotStuckAction.Steering, BotStuckAction.Respawn
        }));
    }

    [Test]
    public void FirstActionComesAfterTheWindow()
    {
        Assert.That(Run(1.8f, 0.1f, t => Vector3.zero), Is.Empty);
        Assert.That(Run(0.5f, 0.1f, t => Vector3.zero), Is.EqualTo(new[] { BotStuckAction.Repath }));
    }

    [Test]
    public void ProgressResetsTheEscalation()
    {
        Run(2.5f, 0.1f, t => Vector3.zero);
        Assert.That(detector.Level, Is.EqualTo(1));

        detector.Update(0.1f, new Vector3(1f, 0f, 0f), true);

        Assert.That(detector.Level, Is.EqualTo(0));
        Assert.That(detector.TimeWithoutProgress, Is.EqualTo(0f));
    }

    [Test]
    public void JitteringInPlace_StillCountsAsStuck()
    {
        List<BotStuckAction> actions = Run(9f, 0.05f, t => new Vector3(Mathf.Sin(t * 20f) * 0.2f, 0f, 0f));

        Assert.That(actions, Contains.Item(BotStuckAction.Repath));
        Assert.That(actions, Contains.Item(BotStuckAction.Respawn));
    }

    [Test]
    public void PausingToIdle_ResetsTheWindow()
    {
        Run(1.5f, 0.1f, t => Vector3.zero);
        Run(1f, 0.1f, t => Vector3.zero, shouldMove: false);

        Assert.That(Run(1.5f, 0.1f, t => Vector3.zero), Is.Empty);
    }

    [Test]
    public void WanderingInASmallArea_IsCaughtByTheLongTermCheck()
    {
        List<BotStuckAction> actions = Run(32f, 0.1f, t => new Vector3(Mathf.Cos(t * 2f), 0f, Mathf.Sin(t * 2f)));

        Assert.That(actions, Contains.Item(BotStuckAction.NewGoal));
        Assert.That(actions[actions.Count - 1], Is.EqualTo(BotStuckAction.Respawn));
    }

    [Test]
    public void LongTermCheck_CanBeDisabledWhileFighting()
    {
        List<BotStuckAction> actions = Run(60f, 0.1f, t => new Vector3(Mathf.Cos(t * 2f), 0f, Mathf.Sin(t * 2f)), trackLong: false);

        Assert.That(actions, Is.Empty);
    }

    [Test]
    public void ResetClearsTheState()
    {
        Run(5f, 0.1f, t => Vector3.zero);
        Assert.That(detector.Level, Is.GreaterThan(0));

        detector.Reset(new Vector3(5f, 0f, 5f));

        Assert.That(detector.Level, Is.EqualTo(0));
        Assert.That(Run(1.5f, 0.1f, t => new Vector3(5f, 0f, 5f)), Is.Empty);
    }

    [Test]
    public void VerticalMovementDoesNotCountAsProgress()
    {
        List<BotStuckAction> actions = Run(2.5f, 0.1f, t => new Vector3(0f, t * 3f, 0f));

        Assert.That(actions, Is.EqualTo(new[] { BotStuckAction.Repath }));
    }
}

public class BotEngagementWatchdogTests
{
    [Test]
    public void FiresAfterTheLimitOfBlockedTime()
    {
        var watchdog = new BotEngagementWatchdog { Limit = 3f };

        Assert.That(watchdog.Update(2.9f, true), Is.False);
        Assert.That(watchdog.Update(0.2f, true), Is.True);
    }

    [Test]
    public void ResetsWhenNotBlocked()
    {
        var watchdog = new BotEngagementWatchdog { Limit = 3f };
        watchdog.Update(2.5f, true);
        watchdog.Update(0.1f, false);

        Assert.That(watchdog.Timer, Is.EqualTo(0f));
        Assert.That(watchdog.Update(2.5f, true), Is.False);
    }

    [Test]
    public void FiresRepeatedlyWhileStillBlocked()
    {
        var watchdog = new BotEngagementWatchdog { Limit = 1f };
        int fired = 0;
        for (int i = 0; i < 35; i++)
        {
            if (watchdog.Update(0.1f, true))
                fired++;
        }

        Assert.That(fired, Is.EqualTo(3));
    }
}

public class BotSeparationTests
{
    [Test]
    public void FarNeighbours_ProduceNoPush()
    {
        Vector3 push = BotSeparation.Compute(Vector3.zero, 1, new[] { new Vector3(5f, 0f, 0f) }, 1.8f);

        Assert.That(push, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void NoNeighbours_ProduceNoPush()
    {
        Assert.That(BotSeparation.Compute(Vector3.zero, 1, new List<Vector3>(), 1.8f), Is.EqualTo(Vector3.zero));
        Assert.That(BotSeparation.Compute(Vector3.zero, 1, null, 1.8f), Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void PushPointsAwayFromTheNeighbour()
    {
        Vector3 push = BotSeparation.Compute(Vector3.zero, 1, new[] { new Vector3(1f, 0f, 0f) }, 1.8f);

        Assert.That(push.x, Is.LessThan(0f));
        Assert.That(push.z, Is.EqualTo(0f).Within(1e-5f));
    }

    [Test]
    public void CloserNeighbourPushesHarder()
    {
        float near = BotSeparation.Compute(Vector3.zero, 1, new[] { new Vector3(0.4f, 0f, 0f) }, 1.8f).magnitude;
        float far = BotSeparation.Compute(Vector3.zero, 1, new[] { new Vector3(1.4f, 0f, 0f) }, 1.8f).magnitude;

        Assert.That(near, Is.GreaterThan(far));
    }

    [Test]
    public void PushIsPlanarAndCapped()
    {
        var crowd = new List<Vector3>();
        for (int i = 0; i < 6; i++)
            crowd.Add(new Vector3(0.3f, 4f, 0.1f * i));

        Vector3 push = BotSeparation.Compute(Vector3.zero, 1, crowd, 1.8f);

        Assert.That(push.y, Is.EqualTo(0f));
        Assert.That(push.magnitude, Is.LessThanOrEqualTo(1.0001f));
    }

    [Test]
    public void BotsOnTheSameSpot_AreStillPushedAndInDifferentDirections()
    {
        var spot = new[] { new Vector3(3f, 0f, 3f) };

        Vector3 a = BotSeparation.Compute(new Vector3(3f, 0f, 3f), 1, spot, 1.8f);
        Vector3 b = BotSeparation.Compute(new Vector3(3f, 0f, 3f), 2, spot, 1.8f);

        Assert.That(a.magnitude, Is.GreaterThan(0.5f));
        Assert.That(b.magnitude, Is.GreaterThan(0.5f));
        Assert.That(Vector3.Distance(a, b), Is.GreaterThan(0.1f));
    }

    [Test]
    public void StackedBots_SpreadApartWhenPushedEachTick()
    {
        var positions = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0f, 0f) };
        for (int step = 0; step < 300; step++)
        {
            var next = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var others = new List<Vector3>();
                for (int j = 0; j < positions.Length; j++)
                {
                    if (j != i)
                        others.Add(positions[j]);
                }

                next[i] = positions[i] + BotSeparation.Compute(positions[i], i + 1, others, 1.8f) * (1.6f * 0.05f);
            }

            positions = next;
        }

        for (int i = 0; i < positions.Length; i++)
        {
            for (int j = i + 1; j < positions.Length; j++)
                Assert.That(Vector3.Distance(positions[i], positions[j]), Is.GreaterThan(1.2f), "bots " + i + " and " + j);
        }
    }

    [Test]
    public void Blend_AddsThePushToTheDesiredDirectionAndNormalises()
    {
        Vector3 blended = BotSeparation.Blend(Vector3.forward, new Vector3(1f, 0f, 0f), 1f);

        Assert.That(blended.magnitude, Is.EqualTo(1f).Within(1e-4f));
        Assert.That(blended.x, Is.GreaterThan(0f));
        Assert.That(blended.z, Is.GreaterThan(0f));
    }

    [Test]
    public void Blend_WithoutPush_KeepsTheDirection()
    {
        Vector3 blended = BotSeparation.Blend(new Vector3(0f, 0f, 3f), Vector3.zero, 1f);

        Assert.That(blended, Is.EqualTo(Vector3.forward));
    }

    [Test]
    public void Blend_CancellingPush_ReturnsZero()
    {
        Assert.That(BotSeparation.Blend(Vector3.forward, Vector3.back, 1f), Is.EqualTo(Vector3.zero));
    }
}

public class BotHysteresisTests
{
    private BotSettings settings;
    private BotBrain brain;

    [SetUp]
    public void SetUp()
    {
        settings = BotSettings.CreatePreset(BotDifficulty.Normal);
        settings.lostSightGrace = 0.3f;
        settings.maxEngageRange = 40f;
        settings.minStateTime = 0.6f;
        settings.engageSightTime = 0.25f;
        settings.reactionTime = 0.5f;
        brain = new BotBrain(settings);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(settings);
    }

    private static BotSenses Seen(float distance)
    {
        return new BotSenses { targetValid = true, seesTarget = true, targetDistance = distance, targetPosition = new Vector3(0f, 0f, distance), health01 = 1f };
    }

    private static BotSenses Unseen()
    {
        return new BotSenses { targetValid = true, health01 = 1f };
    }

    private int CountTransitions(System.Action body)
    {
        int count = 0;
        brain.StateChanged += (a, b) => count++;
        body();
        return count;
    }

    [Test]
    public void ChaseDoesNotStopToAttackImmediately()
    {
        brain.Tick(0.1f, Seen(60f));
        Assert.That(brain.State, Is.EqualTo(BotState.Chase));

        brain.Tick(0.1f, Seen(20f));

        Assert.That(brain.State, Is.EqualTo(BotState.Chase));
    }

    [Test]
    public void ChaseBecomesAttackOnceSightAndTimeAreSufficient()
    {
        brain.Tick(0.1f, Seen(60f));
        for (int i = 0; i < 10; i++)
            brain.Tick(0.1f, Seen(20f));

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void FlickeringSightInChase_NeverStartsAttacking()
    {
        brain.Tick(0.1f, Seen(60f));
        int transitions = CountTransitions(() =>
        {
            for (int i = 0; i < 100; i++)
                brain.Tick(0.05f, i % 2 == 0 ? Seen(20f) : Unseen());
        });

        Assert.That(brain.State, Is.EqualTo(BotState.Chase));
        Assert.That(transitions, Is.EqualTo(0));
    }

    [Test]
    public void FlickeringSightInAttack_StaysInAttack()
    {
        brain.Tick(0.1f, Seen(20f));
        int transitions = CountTransitions(() =>
        {
            for (int i = 0; i < 100; i++)
                brain.Tick(0.1f, i % 2 == 0 ? Seen(20f) : Unseen());
        });

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
        Assert.That(transitions, Is.EqualTo(0));
    }

    [Test]
    public void AttackDoesNotDropToChaseJustBecauseTheTargetStepsOutOfRange()
    {
        brain.Tick(0.1f, Seen(20f));
        brain.Tick(0.1f, Seen(60f));
        Assert.That(brain.State, Is.EqualTo(BotState.Attack));

        for (int i = 0; i < 6; i++)
            brain.Tick(0.1f, Seen(60f));

        Assert.That(brain.State, Is.EqualTo(BotState.Chase));
    }

    [Test]
    public void ShortLossOfSight_DoesNotRestartTheReactionTime()
    {
        brain.Tick(0.1f, Seen(20f));
        brain.Tick(0.3f, Seen(20f));
        float before = brain.TimeSeeingTarget;

        brain.Tick(0.05f, Unseen());

        Assert.That(brain.TimeSeeingTarget, Is.GreaterThan(0f));
        Assert.That(brain.TimeSeeingTarget, Is.LessThan(before));
    }

    [Test]
    public void SightAccumulatedBeforeALongLoss_IsEventuallyForgotten()
    {
        brain.Tick(0.1f, Seen(20f));
        brain.Tick(0.3f, Seen(20f));

        for (int i = 0; i < 10; i++)
            brain.Tick(0.1f, Unseen());

        Assert.That(brain.TimeSeeingTarget, Is.EqualTo(0f));
        Assert.That(brain.CanFire, Is.False);
    }

    [Test]
    public void SustainedLossOfSight_StillLeavesAttack()
    {
        brain.Tick(0.1f, Seen(20f));
        brain.Tick(0.4f, Unseen());

        Assert.That(brain.State, Is.EqualTo(BotState.Chase));
    }

    [Test]
    public void SettingsDefaults_AreSane()
    {
        var fresh = BotSettings.CreatePreset(BotDifficulty.Hard);
        try
        {
            Assert.That(fresh.minStateTime, Is.GreaterThan(0f));
            Assert.That(fresh.stuckWindow, Is.GreaterThan(0.5f));
            Assert.That(fresh.separationRadius, Is.GreaterThan(1f));
            Assert.That(fresh.noFireRepositionTime, Is.GreaterThan(fresh.burstPauseMax));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fresh);
        }
    }
}

public class VignetteMathTests
{
    [Test]
    public void CentreIsTransparent()
    {
        Assert.That(DamageIndicatorMath.VignetteAlpha(0.5f, 0.5f), Is.EqualTo(0f));
    }

    [Test]
    public void CornersAreTheStrongestAndNeverExceedOne()
    {
        float corner = DamageIndicatorMath.VignetteAlpha(0f, 0f);

        Assert.That(corner, Is.EqualTo(1f).Within(1e-4f));
        Assert.That(DamageIndicatorMath.VignetteAlpha(0f, 0.5f), Is.LessThan(corner));
        Assert.That(DamageIndicatorMath.VignetteAlpha(0.5f, 0f), Is.LessThan(corner));
    }

    [Test]
    public void AlphaRisesSmoothlyTowardsTheEdge()
    {
        float previous = -1f;
        float largestStep = 0f;
        for (int i = 0; i <= 100; i++)
        {
            float u = 0.5f + i / 200f;
            float alpha = DamageIndicatorMath.VignetteAlpha(u, 0.5f);
            Assert.That(alpha, Is.GreaterThanOrEqualTo(previous));
            if (previous >= 0f)
                largestStep = Mathf.Max(largestStep, alpha - previous);
            previous = alpha;
        }

        Assert.That(largestStep, Is.LessThan(0.06f), "no hard band edge");
    }

    [Test]
    public void IsSymmetric()
    {
        Assert.That(DamageIndicatorMath.VignetteAlpha(0.1f, 0.2f), Is.EqualTo(DamageIndicatorMath.VignetteAlpha(0.9f, 0.8f)).Within(1e-5f));
    }
}

public class PlayerRespawnFacingTests
{
    [Test]
    public void Respawn_RestoresTheSpawnFacing()
    {
        var host = new GameObject("RespawnFacingHost");
        try
        {
            host.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
            PlayerHealth health = host.AddComponent<PlayerHealth>();
            float clock = 10f;
            health.Clock = () => clock;
            Assert.That(health.SpawnYaw, Is.EqualTo(40f).Within(0.5f));

            host.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            health.TakeDamage(500f, new Vector3(0f, 0f, 5f));
            Assert.That(health.IsDead, Is.True);
            clock += 10f;
            health.Step(clock, 0.1f);

            Assert.That(health.IsDead, Is.False);
            Assert.That(host.transform.eulerAngles.y, Is.EqualTo(40f).Within(0.5f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void SetSpawnFacing_OverridesTheCapturedFacing()
    {
        var host = new GameObject("RespawnFacingHost2");
        try
        {
            PlayerHealth health = host.AddComponent<PlayerHealth>();
            health.Clock = () => 5f;
            health.SetSpawnFacing(123f, -10f);

            Assert.That(health.SpawnYaw, Is.EqualTo(123f));
            Assert.That(health.SpawnPitch, Is.EqualTo(-10f));
            health.TakeDamage(500f, Vector3.zero);
            health.Step(100f, 0.1f);

            Assert.That(host.transform.eulerAngles.y, Is.EqualTo(123f).Within(0.5f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
