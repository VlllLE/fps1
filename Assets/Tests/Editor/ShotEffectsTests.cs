using System;
using NUnit.Framework;
using UnityEngine;

public class ShotEffectsTests
{
    private WeaponConfig config;

    [TearDown]
    public void TearDown()
    {
        if (config != null)
            UnityEngine.Object.DestroyImmediate(config);
    }

    private WeaponConfig MakeConfig(WeaponType type, float fireRate = 600f)
    {
        if (config != null)
            UnityEngine.Object.DestroyImmediate(config);
        config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponType = type;
        config.fireRate = fireRate;
        return config;
    }

    private static float PeakKick(WeaponConfig cfg, bool aiming, out float settled)
    {
        var kick = new WeaponFireKick();
        kick.Kick(cfg, aiming, 0f);
        float peak = 0f;
        for (int i = 0; i < 250; i++)
        {
            kick.Tick(0.004f);
            peak = Mathf.Max(peak, kick.Displacement);
        }

        for (int i = 0; i < 500; i++)
            kick.Tick(0.004f);
        settled = kick.Displacement;
        return peak;
    }

    // --- Per-weapon profile -----------------------------------------------------------------

    [Test]
    public void Profile_EveryWeaponTypeHasSaneValues()
    {
        foreach (WeaponType type in Enum.GetValues(typeof(WeaponType)))
        {
            WeaponFxProfile p = WeaponFxProfile.For(type, 600f);
            Assert.GreaterOrEqual(p.flashScale, 0f, type + " flash scale");
            Assert.Greater(p.tracerWidth, 0f, type + " tracer width");
            Assert.Greater(p.tracerSpeed, 50f, type + " tracer speed");
            Assert.Greater(p.tracerLength, 0f, type + " tracer length");
            Assert.Greater(p.kickRecovery, 0f, type + " kick recovery");
            Assert.Greater(p.cycleDuration, 0f, type + " cycle duration");
            Assert.GreaterOrEqual(p.cycleDelay, 0f, type + " cycle delay");
        }
    }

    [Test]
    public void Profile_ShotgunEjectsARedHullAfterThePump()
    {
        WeaponFxProfile p = WeaponFxProfile.For(WeaponType.Shotgun, 75f);

        Assert.IsTrue(p.ejectsCasings);
        Assert.AreEqual(WeaponFxProfile.ShotgunHull, p.casingColor);
        Assert.Greater(p.casingDelay, 0.1f, "the hull leaves with the pump, not with the shot");
        Assert.Greater(p.cycleDelay, 0f);
        Assert.Greater(p.cycleDuration, 0.2f);
        Assert.AreNotEqual(WeaponFxProfile.Brass, p.casingColor);
    }

    [Test]
    public void Profile_OnlyTheSlowSniperIsBoltAction()
    {
        WeaponFxProfile bolt = WeaponFxProfile.For(WeaponType.Sniper, 55f);
        WeaponFxProfile dmr = WeaponFxProfile.For(WeaponType.Sniper, 200f);

        Assert.IsTrue(bolt.boltAction);
        Assert.IsFalse(dmr.boltAction);
        Assert.Greater(bolt.cycleDuration, dmr.cycleDuration * 3f, "the bolt cycle is a long, deliberate motion");
        Assert.Greater(bolt.casingDelay, 0.2f, "the case leaves when the bolt is worked");
        Assert.AreEqual(0f, dmr.casingDelay, "a DMR ejects immediately");
    }

    [Test]
    public void Profile_LauncherAndMeleeEjectNothing_MeleeHasNoFlashOrKick()
    {
        Assert.IsFalse(WeaponFxProfile.For(WeaponType.Launcher, 60f).ejectsCasings);

        WeaponFxProfile melee = WeaponFxProfile.For(WeaponType.Melee, 100f);
        Assert.IsFalse(melee.ejectsCasings);
        Assert.AreEqual(0f, melee.flashScale);
        Assert.AreEqual(0f, melee.kickStrength);
    }

    [Test]
    public void Profile_HeavierWeaponsFlashBiggerAndSmallerOnesSmaller()
    {
        float smg = WeaponFxProfile.For(WeaponType.SMG, 900f).flashScale;
        float rifle = WeaponFxProfile.For(WeaponType.AssaultRifle, 700f).flashScale;
        float lmg = WeaponFxProfile.For(WeaponType.LMG, 700f).flashScale;
        float shotgun = WeaponFxProfile.For(WeaponType.Shotgun, 75f).flashScale;
        float pistol = WeaponFxProfile.For(WeaponType.Pistol, 400f).flashScale;

        Assert.Less(pistol, rifle);
        Assert.Less(smg, rifle);
        Assert.Greater(lmg, rifle);
        Assert.Greater(shotgun, lmg);
    }

    [Test]
    public void Profile_NullConfigFallsBackToARifle()
    {
        WeaponFxProfile p = WeaponFxProfile.For((WeaponConfig)null);
        WeaponFxProfile rifle = WeaponFxProfile.For(WeaponType.AssaultRifle, 600f);

        Assert.AreEqual(rifle.flashScale, p.flashScale);
        Assert.AreEqual(rifle.tracerSpeed, p.tracerSpeed);
    }

    // --- First-person kick ------------------------------------------------------------------

    [Test]
    public void Kick_SnapsBackThenSettlesAtRest()
    {
        float peak = PeakKick(MakeConfig(WeaponType.AssaultRifle), false, out float settled);

        Assert.Greater(peak, 0.6f, "a clearly visible kick");
        Assert.Less(peak, 2.3f, "bounded");
        Assert.AreEqual(0f, settled, 0.01f, "back at rest after a moment");
    }

    [Test]
    public void Kick_PushesTheGunBackAndTheMuzzleUp()
    {
        MakeConfig(WeaponType.AssaultRifle);
        var kick = new WeaponFireKick();
        Assert.IsFalse(kick.IsMoving);

        kick.Kick(config, false, 0f);
        for (int i = 0; i < 6; i++)
            kick.Tick(0.004f);

        Assert.IsTrue(kick.IsMoving);
        Assert.Less(kick.PositionOffset.z, -0.001f, "the gun goes back");
        Vector3 aim = kick.RotationOffset * Vector3.forward;
        Assert.Greater(aim.y, 0.0005f, "the muzzle climbs");
    }

    [Test]
    public void Kick_IsSmallerWhenAiming()
    {
        float hip = PeakKick(MakeConfig(WeaponType.AssaultRifle), false, out _);
        float ads = PeakKick(config, true, out _);

        Assert.Less(ads, hip * 0.8f);
        Assert.Greater(ads, 0.1f);
    }

    [Test]
    public void Kick_ShotgunAndSniperKickHarderThanSmg()
    {
        float smg = PeakKick(MakeConfig(WeaponType.SMG, 900f), false, out _);
        float shotgun = PeakKick(MakeConfig(WeaponType.Shotgun, 75f), false, out _);
        float sniper = PeakKick(MakeConfig(WeaponType.Sniper, 55f), false, out _);

        Assert.Greater(shotgun, smg * 1.25f);
        Assert.Greater(sniper, smg * 1.25f);
    }

    [Test]
    public void Kick_MeleeAndNullConfigsDoNothing()
    {
        var kick = new WeaponFireKick();
        kick.Kick(MakeConfig(WeaponType.Melee, 100f), false, 0f);
        kick.Kick(null, false, 0f);

        Assert.IsFalse(kick.IsMoving);
        Assert.AreEqual(0f, kick.Displacement);
    }

    [Test]
    public void Kick_RepeatedShotsStayBounded_AndResetClearsIt()
    {
        MakeConfig(WeaponType.LMG, 700f);
        var kick = new WeaponFireKick();
        for (int i = 0; i < 60; i++)
        {
            kick.Kick(config, false, 1f);
            for (int s = 0; s < 3; s++)
                kick.Tick(0.004f);
            Assert.LessOrEqual(kick.Displacement, 2.2001f);
            Assert.LessOrEqual(Mathf.Abs(kick.PositionOffset.x), config.gunKickback * 0.151f + 1e-4f);
        }

        kick.Reset();
        Assert.IsFalse(kick.IsMoving);
        Assert.AreEqual(Vector3.zero, kick.PositionOffset);
    }

    [Test]
    public void Kick_SideRollMovesTheGunSidewaysBothWays()
    {
        MakeConfig(WeaponType.AssaultRifle);
        var left = new WeaponFireKick();
        var right = new WeaponFireKick();
        left.Kick(config, false, -1f);
        right.Kick(config, false, 1f);
        for (int i = 0; i < 12; i++)
        {
            left.Tick(0.004f);
            right.Tick(0.004f);
        }

        Assert.Less(left.PositionOffset.x, 0f);
        Assert.Greater(right.PositionOffset.x, 0f);
    }

    // --- Tracer end point -------------------------------------------------------------------

    [Test]
    public void ShotEndPoint_UsesTheLastImpact()
    {
        MakeConfig(WeaponType.AssaultRifle);
        var result = new HitResult(1, Vector3.zero, Vector3.forward);
        var first = new RaycastHit { point = new Vector3(0f, 0f, 5f) };
        var second = new RaycastHit { point = new Vector3(0f, 0f, 9f) };
        result.Impacts.Add(new HitImpact(1, first, null, HitZoneType.Body, 10f, true, false));
        result.Impacts.Add(new HitImpact(1, second, null, HitZoneType.Body, 5f, false, true));

        Assert.AreEqual(new Vector3(0f, 0f, 9f), ShotEffects.ShotEndPoint(result, config));
    }

    [Test]
    public void ShotEndPoint_AMissRunsToRange_CappedAtTheMaximumTracerLength()
    {
        MakeConfig(WeaponType.AssaultRifle);
        config.range = 60f;
        var result = new HitResult(2, new Vector3(1f, 2f, 3f), Vector3.right);
        Assert.AreEqual(new Vector3(61f, 2f, 3f), ShotEffects.ShotEndPoint(result, config));

        config.range = 900f;
        Vector3 end = ShotEffects.ShotEndPoint(result, config);
        Assert.AreEqual(ShotEffects.MaxTracerDistance, (end - result.Origin).magnitude, 0.001f);
    }

    [Test]
    public void Effects_DoNothingOutsidePlayMode()
    {
        MakeConfig(WeaponType.AssaultRifle);
        var go = new GameObject("muzzle");
        try
        {
            Assert.IsNull(ShotEffects.PlayTracer(Vector3.zero, Vector3.forward, config));
            Assert.IsNull(ShotEffects.PlayMuzzleFlash(go.transform, config, true));
            Assert.IsNull(ShotEffects.PlayCasing(go.transform, config));
            ShotEffects.PlayImpact(Vector3.zero, Vector3.up, null);
            Assert.AreEqual(0, ShotEffects.ActiveTracers);
            Assert.AreEqual(0, ShotEffects.ActiveFlashes);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // --- Third-person animation -------------------------------------------------------------

    private static HumanoidAnimationInput MakeInput(GunHoldProfile profile, HoldStance stance = HoldStance.Ready)
    {
        return new HumanoidAnimationInput { holdProfile = profile, holdStance = stance };
    }

    private static HumanoidAnimator Settled(GunHoldProfile profile, HoldStance stance = HoldStance.Ready)
    {
        var animator = new HumanoidAnimator();
        HumanoidAnimationInput input = MakeInput(profile, stance);
        for (int i = 0; i < 125; i++)
            animator.Tick(0.016f, input);
        return animator;
    }

    [Test]
    public void Swing_PlaysForItsDurationAndTheKnifeMovesThroughIt()
    {
        GunHoldProfile knife = GunHoldCatalog.Build("Karambit");
        HumanoidAnimator animator = Settled(knife);
        HumanoidAnimationInput input = MakeInput(knife);
        Quaternion rest = animator.GunRotation;
        Vector3 restPos = animator.GunPosition;
        Assert.IsFalse(animator.IsSwinging);

        animator.NotifySwing();
        Assert.IsTrue(animator.IsSwinging);
        float maxAngle = 0f;
        float maxTravel = 0f;
        for (int i = 0; i < 40; i++)
        {
            animator.Tick(0.016f, input);
            maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(rest, animator.GunRotation));
            maxTravel = Mathf.Max(maxTravel, (animator.GunPosition - restPos).magnitude);
        }

        Assert.Greater(maxAngle, 25f, "the blade sweeps across");
        Assert.Greater(maxTravel, 0.03f, "and lunges forward");
        Assert.IsFalse(animator.IsSwinging, "the swing is over after its duration");
        Assert.AreEqual(1f, animator.SwingProgress, 1e-4f);

        for (int i = 0; i < 60; i++)
            animator.Tick(0.016f, input);
        Assert.Less(Quaternion.Angle(rest, animator.GunRotation), 6f, "recovered to the ready pose");
    }

    [Test]
    public void Swing_TwistsTheTorsoWithTheSlash()
    {
        GunHoldProfile knife = GunHoldCatalog.Build("Karambit");
        HumanoidAnimator animator = Settled(knife);
        HumanoidAnimationInput input = MakeInput(knife);
        Vector3 restPos = animator.GunPosition;

        animator.NotifySwing();
        float farthestSideways = 0f;
        for (int i = 0; i < 28; i++)
        {
            animator.Tick(0.016f, input);
            farthestSideways = Mathf.Max(farthestSideways, Mathf.Abs(animator.GunPosition.x - restPos.x));
        }

        Assert.Greater(farthestSideways, 0.02f, "the slash carries the knife across the body");
    }

    [Test]
    public void Swing_DoesNotMoveAGun()
    {
        GunHoldProfile rifle = GunHoldCatalog.Build("M4A1");
        HumanoidAnimator animator = Settled(rifle);
        HumanoidAnimationInput input = MakeInput(rifle);
        Quaternion rest = animator.GunRotation;

        animator.NotifySwing();
        for (int i = 0; i < 12; i++)
            animator.Tick(0.016f, input);

        Assert.Less(Quaternion.Angle(rest, animator.GunRotation), 3f);
    }

    [Test]
    public void Shot_IsIgnoredForTheKnife_ButKicksAGun()
    {
        GunHoldProfile knife = GunHoldCatalog.Build("Karambit");
        HumanoidAnimator k = Settled(knife);
        k.NotifyShot();
        k.Tick(0.016f, MakeInput(knife));
        Assert.AreEqual(0f, Mathf.Abs(k.Recoil), 1e-4f);

        GunHoldProfile rifle = GunHoldCatalog.Build("M4A1");
        HumanoidAnimator r = Settled(rifle, HoldStance.Aim);
        r.NotifyShot();
        r.Tick(0.016f, MakeInput(rifle, HoldStance.Aim));
        Assert.Greater(Mathf.Abs(r.Recoil), 0.001f);
    }

    [Test]
    public void FireCycle_ShotgunPumpsThenReturns()
    {
        GunHoldProfile shotgun = GunHoldCatalog.Build("Shotgun");
        HumanoidAnimator animator = Settled(shotgun);
        HumanoidAnimationInput input = MakeInput(shotgun);
        Assert.AreEqual(0f, animator.BoltOffset.magnitude, 1e-5f);

        animator.NotifyFireCycle();
        float deepest = 0f;
        for (int i = 0; i < 12; i++)
        {
            animator.Tick(0.016f, input);
            deepest = Mathf.Min(deepest, animator.BoltOffset.z);
        }

        Assert.Less(deepest, -0.02f, "the pump slides back along the gun");

        for (int i = 0; i < 40; i++)
            animator.Tick(0.016f, input);
        Assert.AreEqual(0f, animator.BoltOffset.magnitude, 1e-4f, "back in battery");
    }

    [Test]
    public void FireCycle_BoltActionSniperWorksTheBoltLongerThanTheShotgunPumps()
    {
        GunHoldProfile sniper = GunHoldCatalog.Build("BoltActionSniper");
        HumanoidAnimator animator = Settled(sniper);
        HumanoidAnimationInput input = MakeInput(sniper);

        animator.NotifyFireCycle();
        float firstMovement = -1f;
        float lastMovement = -1f;
        float deepest = 0f;
        float t = 0f;
        for (int i = 0; i < 90; i++)
        {
            animator.Tick(0.016f, input);
            t += 0.016f;
            deepest = Mathf.Min(deepest, animator.BoltOffset.z);
            if (animator.BoltOffset.magnitude > 1e-4f)
            {
                if (firstMovement < 0f)
                    firstMovement = t;
                lastMovement = t;
            }
        }

        Assert.Less(deepest, -0.04f, "a long bolt throw");
        Assert.Greater(firstMovement, 0.25f, "the bolt waits for the shot to leave");
        Assert.Greater(lastMovement - firstMovement, 0.45f, "a slow cycle");
        Assert.AreEqual(0f, animator.BoltOffset.magnitude, 1e-4f);
    }

    [Test]
    public void FireCycle_DoesNothingForTheKnife()
    {
        GunHoldProfile knife = GunHoldCatalog.Build("Karambit");
        HumanoidAnimator animator = Settled(knife);
        animator.NotifyFireCycle();
        for (int i = 0; i < 30; i++)
            animator.Tick(0.016f, MakeInput(knife));

        Assert.AreEqual(0f, animator.BoltOffset.magnitude, 1e-5f);
    }
}
