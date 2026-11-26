using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BotBrainTests
{
    private BotSettings settings;
    private BotBrain brain;

    [SetUp]
    public void SetUp()
    {
        settings = BotSettings.CreatePreset(BotDifficulty.Normal);
        settings.reactionTime = 0.5f;
        settings.lostSightGrace = 0.3f;
        settings.memoryTime = 5f;
        settings.investigateTime = 2f;
        settings.maxEngageRange = 40f;
        settings.coverDuration = 2f;
        settings.coverHealthFraction = 0.3f;
        brain = new BotBrain(settings);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(settings);
    }

    private static BotSenses Seen(float distance = 15f)
    {
        return new BotSenses
        {
            targetValid = true,
            seesTarget = true,
            targetDistance = distance,
            targetPosition = new Vector3(0f, 0f, distance),
            health01 = 1f
        };
    }

    private static BotSenses Unseen()
    {
        return new BotSenses { targetValid = true, health01 = 1f };
    }

    [Test]
    public void StartsInPatrol()
    {
        Assert.That(brain.State, Is.EqualTo(BotState.Patrol));
        Assert.That(brain.IsAlive, Is.True);
    }

    [Test]
    public void SeeingTargetInRange_StartsAttack()
    {
        brain.Tick(0.1f, Seen());

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
        Assert.That(brain.LastKnownPosition, Is.EqualTo(new Vector3(0f, 0f, 15f)));
    }

    [Test]
    public void SeeingTargetOutOfEngageRange_StartsChase()
    {
        brain.Tick(0.1f, Seen(60f));

        Assert.That(brain.State, Is.EqualTo(BotState.Chase));
    }

    [Test]
    public void CannotFireUntilReactionTimeHasElapsed()
    {
        brain.Tick(0.1f, Seen());
        Assert.That(brain.CanFire, Is.False);

        brain.Tick(0.3f, Seen());
        Assert.That(brain.CanFire, Is.False);

        brain.Tick(0.2f, Seen());
        Assert.That(brain.CanFire, Is.True);
    }

    [Test]
    public void LosingSightBriefly_KeepsAttackingUntilGraceExpires()
    {
        brain.Tick(0.1f, Seen());
        brain.Tick(0.2f, Unseen());
        Assert.That(brain.State, Is.EqualTo(BotState.Attack));

        brain.Tick(0.2f, Unseen());
        Assert.That(brain.State, Is.EqualTo(BotState.Chase));
        Assert.That(brain.CanFire, Is.False);
    }

    [Test]
    public void Chase_ReachingLastKnownPosition_StartsInvestigating()
    {
        brain.Tick(0.1f, Seen());
        brain.Tick(0.4f, Unseen());
        Assert.That(brain.State, Is.EqualTo(BotState.Chase));

        BotSenses arrived = Unseen();
        arrived.atDestination = true;
        brain.Tick(0.1f, arrived);

        Assert.That(brain.State, Is.EqualTo(BotState.Investigate));
    }

    [Test]
    public void Investigate_ReturnsToPatrolAfterLookingAround()
    {
        BotSenses noise = Unseen();
        noise.heardSomething = true;
        noise.heardPosition = new Vector3(5f, 0f, 5f);
        brain.Tick(0.1f, noise);
        Assert.That(brain.State, Is.EqualTo(BotState.Investigate));
        Assert.That(brain.LastKnownPosition, Is.EqualTo(noise.heardPosition));

        BotSenses there = Unseen();
        there.atDestination = true;
        brain.Tick(1f, there);
        Assert.That(brain.State, Is.EqualTo(BotState.Investigate));

        brain.Tick(1.2f, there);
        Assert.That(brain.State, Is.EqualTo(BotState.Patrol));
    }

    [Test]
    public void Chase_GivesUpAfterMemoryTime()
    {
        brain.Tick(0.1f, Seen(60f));
        Assert.That(brain.State, Is.EqualTo(BotState.Chase));

        brain.Tick(settings.memoryTime + 0.5f, Unseen());

        Assert.That(brain.State, Is.EqualTo(BotState.Patrol));
    }

    [Test]
    public void SeeingTargetWhileInvestigating_EngagesImmediately()
    {
        BotSenses noise = Unseen();
        noise.heardSomething = true;
        brain.Tick(0.1f, noise);

        brain.Tick(0.1f, Seen());

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void LowHealthWithCover_TakesCoverThenReturnsToTheFight()
    {
        brain.Tick(0.1f, Seen());
        BotSenses hurt = Seen();
        hurt.health01 = 0.2f;
        hurt.hasCoverPoint = true;
        brain.Tick(0.1f, hurt);
        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover));

        brain.Tick(1f, hurt);
        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover));

        brain.Tick(1.2f, hurt);
        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void LowHealthWithoutCover_KeepsFighting()
    {
        brain.Tick(0.1f, Seen());
        BotSenses hurt = Seen();
        hurt.health01 = 0.1f;
        hurt.hasCoverPoint = false;

        brain.Tick(0.1f, hurt);

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void CoverDisabledInSettings_NeverTakesCover()
    {
        settings.useCover = false;
        brain.Tick(0.1f, Seen());
        BotSenses hurt = Seen();
        hurt.health01 = 0.1f;
        hurt.hasCoverPoint = true;

        brain.Tick(0.1f, hurt);

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void TargetDying_ReturnsEngagedBotToPatrol()
    {
        brain.Tick(0.1f, Seen());

        brain.Tick(0.1f, new BotSenses { targetValid = false });

        Assert.That(brain.State, Is.EqualTo(BotState.Patrol));
    }

    [Test]
    public void Kill_StopsTheBrainUntilRevived()
    {
        brain.Kill();
        Assert.That(brain.State, Is.EqualTo(BotState.Dead));
        Assert.That(brain.IsAlive, Is.False);

        brain.Tick(1f, Seen());
        Assert.That(brain.State, Is.EqualTo(BotState.Dead));

        brain.Revive();
        Assert.That(brain.State, Is.EqualTo(BotState.Patrol));
        Assert.That(brain.HasLastKnownPosition, Is.False);
    }

    [Test]
    public void StateChanges_RaiseTheEvent()
    {
        var changes = new List<(BotState from, BotState to)>();
        brain.StateChanged += (from, to) => changes.Add((from, to));

        brain.Tick(0.1f, Seen());
        brain.Kill();

        Assert.That(changes, Is.EqualTo(new List<(BotState, BotState)>
        {
            (BotState.Patrol, BotState.Attack),
            (BotState.Attack, BotState.Dead)
        }));
    }

    [Test]
    public void ExternalInvestigate_DoesNotInterruptCombat()
    {
        brain.Tick(0.1f, Seen());

        brain.Investigate(new Vector3(9f, 0f, 9f));

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }
}

public class BotPerceptionTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    private GameObject CreateBox(string name, Vector3 position, Vector3 scale)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = position;
        box.transform.localScale = scale;
        created.Add(box);
        Physics.SyncTransforms();
        return box;
    }

    [Test]
    public void ViewCone_AcceptsTargetsAheadAndRejectsTargetsBehind()
    {
        Assert.That(BotPerception.IsInViewCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 10f), 90f, 50f, 0f), Is.True);
        Assert.That(BotPerception.IsInViewCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -10f), 90f, 50f, 0f), Is.False);
    }

    [Test]
    public void ViewCone_RespectsHalfAngle()
    {
        Vector3 at40 = Quaternion.Euler(0f, 40f, 0f) * Vector3.forward * 10f;
        Vector3 at60 = Quaternion.Euler(0f, 60f, 0f) * Vector3.forward * 10f;

        Assert.That(BotPerception.IsInViewCone(Vector3.zero, Vector3.forward, at40, 90f, 50f, 0f), Is.True);
        Assert.That(BotPerception.IsInViewCone(Vector3.zero, Vector3.forward, at60, 90f, 50f, 0f), Is.False);
    }

    [Test]
    public void ViewCone_RespectsRange()
    {
        Assert.That(BotPerception.IsInViewCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 60f), 90f, 50f, 0f), Is.False);
    }

    [Test]
    public void ViewCone_CloseTargetsAreNoticedFromBehind()
    {
        Assert.That(BotPerception.IsInViewCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -2f), 90f, 50f, 3.5f), Is.True);
    }

    [Test]
    public void LineOfSight_ClearWhenNothingIsInTheWay()
    {
        Assert.That(BotPerception.HasLineOfSight(new Vector3(0f, 1.6f, 0f), new Vector3(0f, 1.6f, 10f), null, null), Is.True);
    }

    [Test]
    public void LineOfSight_BlockedByAWall()
    {
        CreateBox("Wall", new Vector3(0f, 1.6f, 5f), new Vector3(4f, 4f, 0.5f));

        Assert.That(BotPerception.HasLineOfSight(new Vector3(0f, 1.6f, 0f), new Vector3(0f, 1.6f, 10f), null, null), Is.False);
    }

    [Test]
    public void LineOfSight_IgnoresTheTargetAndTheBotItself()
    {
        GameObject target = CreateBox("Target", new Vector3(0f, 1.6f, 10f), Vector3.one);
        GameObject self = CreateBox("Self", new Vector3(0f, 1.6f, 1f), Vector3.one);

        bool visible = BotPerception.HasLineOfSight(
            new Vector3(0f, 1.6f, 0f), new Vector3(0f, 1.6f, 10f), self.transform, target.transform);

        Assert.That(visible, Is.True);
    }

    [Test]
    public void LineOfSight_IgnoresTriggers()
    {
        GameObject zone = CreateBox("Zone", new Vector3(0f, 1.6f, 5f), new Vector3(4f, 4f, 0.5f));
        zone.GetComponent<Collider>().isTrigger = true;

        Assert.That(BotPerception.HasLineOfSight(new Vector3(0f, 1.6f, 0f), new Vector3(0f, 1.6f, 10f), null, null), Is.True);
    }

    [Test]
    public void HearingRadius_GrowsWithPlayerSpeedAndHearingScale()
    {
        float still = BotPerception.HearingRadius(0f, 1f);
        float walking = BotPerception.HearingRadius(2.5f, 1f);
        float running = BotPerception.HearingRadius(5f, 1f);
        float sprinting = BotPerception.HearingRadius(8f, 1f);

        Assert.That(still, Is.LessThan(walking));
        Assert.That(walking, Is.LessThan(running));
        Assert.That(running, Is.LessThan(sprinting));
        Assert.That(BotPerception.HearingRadius(5f, 2f), Is.EqualTo(running * 2f).Within(0.001f));
        Assert.That(BotPerception.HearingRadius(5f, 0f), Is.Zero);
    }
}

public class BotAimModelTests
{
    private BotSettings settings;
    private BotAimModel aim;

    [SetUp]
    public void SetUp()
    {
        settings = BotSettings.CreatePreset(BotDifficulty.Normal);
        settings.aimTurnSpeed = 90f;
        aim = new BotAimModel(settings);
        aim.SnapTo(Vector3.forward);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(settings);
    }

    [Test]
    public void Track_TurnsNoFasterThanTheConfiguredRate()
    {
        aim.Track(Vector3.right, 0.5f);

        Assert.That(Vector3.Angle(aim.AimDirection, Vector3.forward), Is.EqualTo(45f).Within(0.5f));
        Assert.That(aim.TrackingError, Is.EqualTo(45f).Within(0.5f));
        Assert.That(aim.IsOnTarget, Is.False);
    }

    [Test]
    public void Track_ReachesTheTargetAndAccumulatesTimeOnTarget()
    {
        for (int i = 0; i < 30; i++)
            aim.Track(Vector3.right, 0.1f);

        Assert.That(aim.IsOnTarget, Is.True);
        Assert.That(aim.TimeOnTarget, Is.GreaterThan(0.5f));
    }

    [Test]
    public void ComputeInaccuracy_ShrinksWhileTrackingAndGrowsWhenMoving()
    {
        aim.ResetTracking();
        float cold = aim.ComputeInaccuracy(0f, 0f);

        for (int i = 0; i < 40; i++)
            aim.Track(Vector3.forward, 0.1f);
        float settled = aim.ComputeInaccuracy(0f, 0f);
        float movingBot = aim.ComputeInaccuracy(3f, 0f);
        float movingTarget = aim.ComputeInaccuracy(0f, 6f);

        Assert.That(settled, Is.LessThan(cold));
        Assert.That(settled, Is.EqualTo(settings.aimErrorDegrees * settings.settledErrorFraction).Within(0.001f));
        Assert.That(movingBot, Is.GreaterThan(settled));
        Assert.That(movingTarget, Is.GreaterThan(settled));
    }

    [Test]
    public void ApplyInaccuracy_StaysInsideTheErrorCone()
    {
        var rng = new System.Random(7);
        for (int i = 0; i < 300; i++)
        {
            Vector3 shot = BotAimModel.ApplyInaccuracy(Vector3.forward, 5f, rng);
            Assert.That(Vector3.Angle(Vector3.forward, shot), Is.LessThanOrEqualTo(5.01f));
        }
    }

    [Test]
    public void ApplyInaccuracy_ZeroErrorIsExact()
    {
        Vector3 shot = BotAimModel.ApplyInaccuracy(Vector3.forward, 0f, new System.Random(1));

        Assert.That(Vector3.Angle(Vector3.forward, shot), Is.LessThan(0.001f));
    }

    [Test]
    public void ApplyInaccuracy_ActuallyDeviates()
    {
        var rng = new System.Random(3);
        float maxAngle = 0f;
        for (int i = 0; i < 100; i++)
            maxAngle = Mathf.Max(maxAngle, Vector3.Angle(Vector3.forward, BotAimModel.ApplyInaccuracy(Vector3.forward, 5f, rng)));

        Assert.That(maxAngle, Is.GreaterThan(2f));
    }
}

public class BotGunTests
{
    private BotSettings settings;
    private WeaponConfig config;
    private BotGun gun;

    [SetUp]
    public void SetUp()
    {
        settings = BotSettings.CreatePreset(BotDifficulty.Normal);
        settings.burstMin = 3;
        settings.burstMax = 3;
        settings.burstPauseMin = 1f;
        settings.burstPauseMax = 1f;
        config = BotGun.CreateFallbackConfig();
        config.fireRate = 600f;
        config.magazineSize = 10;
        config.reloadTime = 2f;
        config.tacticalReloadTime = 1.5f;
        gun = new BotGun(config, settings, new System.Random(5));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(settings);
        UnityEngine.Object.DestroyImmediate(config);
    }

    [Test]
    public void StartsWithAFullMagazine()
    {
        Assert.That(gun.Ammo, Is.EqualTo(10));
        Assert.That(gun.IsReloading, Is.False);
        Assert.That(gun.NeedsReload, Is.False);
    }

    [Test]
    public void Fire_RespectsTheFireRate()
    {
        Assert.That(gun.TryFire(0f), Is.True);
        Assert.That(gun.TryFire(0.05f), Is.False, "600 rpm is one round every 0.1 s");
        Assert.That(gun.TryFire(0.1f), Is.True);
        Assert.That(gun.Ammo, Is.EqualTo(8));
    }

    [Test]
    public void Fire_PausesBetweenBursts()
    {
        Assert.That(gun.TryFire(0f), Is.True);
        Assert.That(gun.TryFire(0.1f), Is.True);
        Assert.That(gun.TryFire(0.2f), Is.True);

        Assert.That(gun.TryFire(0.3f), Is.False, "burst of three then a pause");
        Assert.That(gun.TryFire(1.1f), Is.False);
        Assert.That(gun.TryFire(1.25f), Is.True);
    }

    [Test]
    public void EmptyMagazine_StartsAReloadThatRestoresAmmo()
    {
        settings.burstMin = 20;
        settings.burstMax = 20;
        float now = 0f;
        for (int i = 0; i < 10; i++)
        {
            Assert.That(gun.TryFire(now), Is.True, "shot " + i);
            now += 0.11f;
        }

        Assert.That(gun.IsEmpty, Is.True);
        Assert.That(gun.IsReloading, Is.True);
        Assert.That(gun.TryFire(now), Is.False);

        gun.Tick(now + 1f, 1f);
        Assert.That(gun.IsReloading, Is.True);
        gun.Tick(now + 2.5f, 1.5f);
        Assert.That(gun.IsReloading, Is.False);
        Assert.That(gun.Ammo, Is.EqualTo(10));
    }

    [Test]
    public void ReloadDuration_UsesTacticalTimeWhenRoundsRemain()
    {
        Assert.That(BotGun.ReloadDuration(config, 4), Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(BotGun.ReloadDuration(config, 0), Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void LowAmmo_AsksForAReload()
    {
        settings.burstMin = 20;
        settings.burstMax = 20;
        float now = 0f;
        for (int i = 0; i < 8; i++)
        {
            gun.TryFire(now);
            now += 0.11f;
        }

        Assert.That(gun.Ammo, Is.EqualTo(2));
        Assert.That(gun.NeedsReload, Is.True);
        Assert.That(gun.TryStartReload(now), Is.True);
        Assert.That(gun.TryStartReload(now), Is.False, "already reloading");
    }

    [Test]
    public void Bloom_GrowsWhileFiringAndRecovers()
    {
        float before = gun.CurrentSpreadDegrees;
        gun.TryFire(0f);
        float after = gun.CurrentSpreadDegrees;
        gun.Tick(5f, 5f);

        Assert.That(after, Is.GreaterThan(before));
        Assert.That(gun.CurrentSpreadDegrees, Is.EqualTo(before).Within(0.001f));
    }

    [Test]
    public void CreateHitRequest_ScalesDamageAndIgnoresTheShooter()
    {
        var shooter = new GameObject("Shooter");
        try
        {
            HitRequest request = BotGun.CreateHitRequest(config, Vector3.up, Vector3.forward, 12, 99, shooter.transform, 0.5f);

            Assert.That(request.BaseDamage, Is.EqualTo(config.damage * 0.5f).Within(0.001f));
            Assert.That(request.SourceId, Is.EqualTo(99));
            Assert.That(request.ShotId, Is.EqualTo(12));
            Assert.That(request.IgnoreRoot, Is.EqualTo(shooter.transform));
            Assert.That(request.MaxDistance, Is.EqualTo(config.range));
            Assert.That(request.IsAuthoritative, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(shooter);
        }
    }

    [Test]
    public void SemiAutomaticWeapons_FireOneRoundThenWait()
    {
        config.availableFireModes = new[] { FireMode.Semi };
        settings.semiDelay = 0.5f;
        var semi = new BotGun(config, settings, new System.Random(2));

        Assert.That(semi.TryFire(0f), Is.True);
        Assert.That(semi.TryFire(0.1f), Is.False);
        Assert.That(semi.TryFire(1.5f), Is.True);
    }
}

public class BotSettingsTests
{
    [Test]
    public void Presets_GetProgressivelyMoreDangerous()
    {
        BotSettings easy = BotSettings.CreatePreset(BotDifficulty.Easy);
        BotSettings normal = BotSettings.CreatePreset(BotDifficulty.Normal);
        BotSettings hard = BotSettings.CreatePreset(BotDifficulty.Hard);
        BotSettings veteran = BotSettings.CreatePreset(BotDifficulty.Veteran);
        try
        {
            BotSettings[] ladder = { easy, normal, hard, veteran };
            for (int i = 1; i < ladder.Length; i++)
            {
                Assert.That(ladder[i].reactionTime, Is.LessThan(ladder[i - 1].reactionTime), "reaction " + i);
                Assert.That(ladder[i].aimErrorDegrees, Is.LessThan(ladder[i - 1].aimErrorDegrees), "aim error " + i);
                Assert.That(ladder[i].runSpeed, Is.GreaterThan(ladder[i - 1].runSpeed), "speed " + i);
                Assert.That(ladder[i].damageMultiplier, Is.GreaterThan(ladder[i - 1].damageMultiplier), "damage " + i);
                Assert.That(ladder[i].maxHealth, Is.GreaterThan(ladder[i - 1].maxHealth), "health " + i);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(easy);
            UnityEngine.Object.DestroyImmediate(normal);
            UnityEngine.Object.DestroyImmediate(hard);
            UnityEngine.Object.DestroyImmediate(veteran);
        }
    }

    [Test]
    public void ApplyPreset_UpdatesTheDifficultyField()
    {
        BotSettings settings = BotSettings.CreatePreset(BotDifficulty.Easy);
        try
        {
            settings.ApplyPreset(BotDifficulty.Veteran);
            Assert.That(settings.difficulty, Is.EqualTo(BotDifficulty.Veteran));
            Assert.That(settings.reactionTime, Is.EqualTo(0.15f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void Next_CyclesThroughAllDifficulties()
    {
        Assert.That(BotSettings.Next(BotDifficulty.Easy), Is.EqualTo(BotDifficulty.Normal));
        Assert.That(BotSettings.Next(BotDifficulty.Normal), Is.EqualTo(BotDifficulty.Hard));
        Assert.That(BotSettings.Next(BotDifficulty.Hard), Is.EqualTo(BotDifficulty.Veteran));
        Assert.That(BotSettings.Next(BotDifficulty.Veteran), Is.EqualTo(BotDifficulty.Easy));
    }
}

public class PlayerHealthTests
{
    private GameObject host;
    private PlayerHealth health;
    private float clock;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("PlayerHealthHost");
        health = host.AddComponent<PlayerHealth>();
        clock = 100f;
        health.Clock = () => clock;
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(host);
    }

    [Test]
    public void StartsAtFullHealth()
    {
        Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        Assert.That(health.Health01, Is.EqualTo(1f));
        Assert.That(health.IsDead, Is.False);
    }

    [Test]
    public void Damage_ReducesHealthAndReportsTheShooterPosition()
    {
        float reported = 0f;
        Vector3 source = Vector3.zero;
        health.OnDamaged += (amount, position, fatal) =>
        {
            reported = amount;
            source = position;
        };

        health.TakeDamage(30f, new Vector3(4f, 0f, 9f));

        Assert.That(health.CurrentHealth, Is.EqualTo(70f));
        Assert.That(reported, Is.EqualTo(30f));
        Assert.That(source, Is.EqualTo(new Vector3(4f, 0f, 9f)));
    }

    [Test]
    public void FatalDamage_KillsThePlayerOnce()
    {
        int deaths = 0;
        health.OnDied += () => deaths++;

        health.TakeDamage(60f, Vector3.forward);
        health.TakeDamage(60f, Vector3.forward);
        health.TakeDamage(60f, Vector3.forward);

        Assert.That(health.IsDead, Is.True);
        Assert.That(health.CurrentHealth, Is.Zero);
        Assert.That(deaths, Is.EqualTo(1));
        Assert.That(health.Deaths, Is.EqualTo(1));
    }

    [Test]
    public void GodMode_TakesNoDamage()
    {
        health.GodMode = true;

        health.TakeDamage(500f, Vector3.forward);

        Assert.That(health.IsDead, Is.False);
        Assert.That(health.CurrentHealth, Is.EqualTo(100f));
    }

    [Test]
    public void Health_RegeneratesAfterTheDelayOnly()
    {
        health.TakeDamage(50f, Vector3.forward);

        clock += 1f;
        health.Step(clock, 1f);
        Assert.That(health.CurrentHealth, Is.EqualTo(50f), "no regeneration straight after damage");

        clock += 6f;
        health.Step(clock, 1f);
        Assert.That(health.CurrentHealth, Is.GreaterThan(50f));
    }

    [Test]
    public void Death_RespawnsAfterTheDelayWithFullHealth()
    {
        host.transform.position = new Vector3(1f, 0f, 2f);
        health.SetSpawnPosition(new Vector3(10f, 0f, 20f));
        health.TakeDamage(500f, Vector3.forward);
        Assert.That(health.IsDead, Is.True);

        clock += 1f;
        health.Step(clock, 1f);
        Assert.That(health.IsDead, Is.True);

        clock += 10f;
        health.Step(clock, 1f);

        Assert.That(health.IsDead, Is.False);
        Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        Assert.That(host.transform.position, Is.EqualTo(new Vector3(10f, 0f, 20f)));
    }

    [Test]
    public void HitDamageInfo_AppliesTheFinalDamage()
    {
        var info = new HitDamageInfo(1, 12345, 25f, Vector3.zero, Vector3.up, Vector3.forward, HitZoneType.Body, null);

        health.ApplyHitDamage(info);

        Assert.That(health.CurrentHealth, Is.EqualTo(75f));
    }

    [Test]
    public void Heal_CannotExceedMaximumHealth()
    {
        health.TakeDamage(20f, Vector3.forward);
        health.Heal(500f);

        Assert.That(health.CurrentHealth, Is.EqualTo(100f));
    }
}

public class DamageIndicatorMathTests
{
    [TestCase(0f, 0f, 10f, 0f)]
    [TestCase(10f, 0f, 0f, 90f)]
    [TestCase(-10f, 0f, 0f, -90f)]
    public void SourceAngle_IsRelativeToTheFacingDirection(float x, float y, float z, float expected)
    {
        float angle = DamageIndicatorMath.SourceAngle(Vector3.zero, Vector3.forward, new Vector3(x, y, z));

        Assert.That(angle, Is.EqualTo(expected).Within(0.01f));
    }

    [Test]
    public void SourceAngle_BehindIsHalfATurn()
    {
        float angle = DamageIndicatorMath.SourceAngle(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -10f));

        Assert.That(Mathf.Abs(angle), Is.EqualTo(180f).Within(0.01f));
    }

    [Test]
    public void SourceAngle_IgnoresPitchAndHeight()
    {
        float angle = DamageIndicatorMath.SourceAngle(Vector3.zero, new Vector3(0f, -0.7f, 0.7f), new Vector3(10f, 5f, 0f));

        Assert.That(angle, Is.EqualTo(90f).Within(0.01f));
    }

    [Test]
    public void Fade_GoesFromOpaqueToTransparent()
    {
        Assert.That(DamageIndicatorMath.Fade(0f, 2f), Is.EqualTo(1f));
        Assert.That(DamageIndicatorMath.Fade(1f, 2f), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(DamageIndicatorMath.Fade(2f, 2f), Is.Zero);
        Assert.That(DamageIndicatorMath.Fade(3f, 2f), Is.Zero);
    }
}
