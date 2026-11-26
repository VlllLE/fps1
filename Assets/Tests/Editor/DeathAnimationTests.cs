using NUnit.Framework;
using UnityEngine;

public class DeathAnimationTests
{
    private static HumanoidAnimator StandingAnimator()
    {
        var animator = new HumanoidAnimator();
        for (int i = 0; i < 30; i++)
            animator.Tick(0.016f, new HumanoidAnimationInput());
        return animator;
    }

    private static HumanoidDeathAnimator Run(DeathVariant variant, float seconds, float impulse = 1f)
    {
        var death = new HumanoidDeathAnimator(StandingAnimator(), variant, impulse);
        for (float t = 0f; t < seconds; t += 0.016f)
            death.Tick(0.016f);
        return death;
    }

    // --- Variant selection -----------------------------------------------------------------

    private static DeathContext Hit(Vector3 travel, bool headshot = false, float damage = 40f, float speed = 0f, bool crouched = false, float roll = 0.9f)
    {
        return new DeathContext { localHitDirection = travel, headshot = headshot, damage = damage, forwardSpeed = speed, crouched = crouched, roll = roll };
    }

    [Test]
    public void Selector_FrontHit_FallsBackward()
    {
        Assert.AreEqual(DeathVariant.FallBackward, DeathVariantSelector.Select(Hit(Vector3.back)));
    }

    [Test]
    public void Selector_BackHit_CollapsesForward()
    {
        Assert.AreEqual(DeathVariant.ForwardCollapse, DeathVariantSelector.Select(Hit(Vector3.forward)));
    }

    [Test]
    public void Selector_FrontHeadshot_SnapsBack_BackHeadshot_CollapsesForward()
    {
        Assert.AreEqual(DeathVariant.HeadshotSnapBack, DeathVariantSelector.Select(Hit(Vector3.back, headshot: true)));
        Assert.AreEqual(DeathVariant.ForwardCollapse, DeathVariantSelector.Select(Hit(Vector3.forward, headshot: true)));
    }

    [Test]
    public void Selector_SideHits_FallTowardTheShotDirection()
    {
        Assert.AreEqual(DeathVariant.SideFallRight, DeathVariantSelector.Select(Hit(Vector3.right)));
        Assert.AreEqual(DeathVariant.SideFallLeft, DeathVariantSelector.Select(Hit(Vector3.left)));
        Assert.AreEqual(DeathVariant.SideFallLeft, DeathVariantSelector.Select(Hit(new Vector3(-0.9f, 0f, -0.3f), headshot: true)));
    }

    [Test]
    public void Selector_SprintingSoldierPitchesForwardEvenWhenShotFromFront()
    {
        Assert.AreEqual(DeathVariant.ForwardCollapse, DeathVariantSelector.Select(Hit(Vector3.back, speed: 7f)));
    }

    [Test]
    public void Selector_CrouchedOrLowRoll_KneelsUnlessHeadshot()
    {
        Assert.AreEqual(DeathVariant.KneelCollapse, DeathVariantSelector.Select(Hit(Vector3.back, crouched: true)));
        Assert.AreEqual(DeathVariant.KneelCollapse, DeathVariantSelector.Select(Hit(Vector3.forward, roll: 0.1f)));
        Assert.AreEqual(DeathVariant.HeadshotSnapBack, DeathVariantSelector.Select(Hit(Vector3.back, headshot: true, crouched: true)));
    }

    [Test]
    public void Selector_IgnoresVerticalComponentAndHandlesZeroDirection()
    {
        Assert.AreEqual(DeathVariant.FallBackward, DeathVariantSelector.Select(Hit(new Vector3(0f, -5f, -1f))));
        Assert.AreEqual(DeathVariant.FallBackward, DeathVariantSelector.Select(Hit(Vector3.zero)));
        Assert.AreEqual(DeathVariant.FallBackward, DeathVariantSelector.Select(Hit(Vector3.down)));
    }

    [Test]
    public void Selector_AlwaysReturnsADefinedVariantAndMatchingFallDirection()
    {
        var rng = new System.Random(5);
        for (int i = 0; i < 500; i++)
        {
            var travel = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 2f - 1f);
            DeathVariant v = DeathVariantSelector.Select(Hit(travel, rng.NextDouble() < 0.2, (float)rng.NextDouble() * 150f,
                (float)rng.NextDouble() * 9f, rng.NextDouble() < 0.2, (float)rng.NextDouble()));
            Assert.IsTrue(System.Enum.IsDefined(typeof(DeathVariant), v));
            Vector3 fall = DeathVariantSelector.FallDirection(v);
            Assert.AreEqual(1f, fall.magnitude, 1e-4f);
            Assert.AreEqual(0f, fall.y, 1e-6f);
        }
    }

    // --- Death animator --------------------------------------------------------------------

    [Test]
    public void StartsExactlyAtTheLivePose()
    {
        var live = StandingAnimator();
        var death = new HumanoidDeathAnimator(live, DeathVariant.FallBackward);
        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            Assert.Less(Quaternion.Angle(live.GetLocalRotation(bone), death.GetLocalRotation(bone)), 0.3f, bone.ToString());
            Assert.AreEqual(0f, Vector3.Distance(live.GetWorldPosition(bone), death.GetWorldPosition(bone)), 0.02f, bone.ToString());
        }
    }

    [Test]
    public void EveryVariantSettlesLyingDownOnTheFloor()
    {
        foreach (DeathVariant variant in System.Enum.GetValues(typeof(DeathVariant)))
        {
            var death = Run(variant, 4f);
            Assert.IsTrue(death.Settled, variant + " never settled");
            Assert.AreEqual(90f, death.FallAngle, 4f, variant + " does not lie down");
            Assert.AreEqual(0f, death.LowestBodyPoint, 0.02f, variant + " floats or sinks");
            float headHeight = death.GetWorldPosition(HumanoidBone.Head).y;
            Assert.Less(headHeight, 0.6f, variant + " head is not near the floor: " + headHeight);
        }
    }

    [Test]
    public void NoBodyVolumeEverSinksBelowTheFloorDuringTheFall()
    {
        foreach (DeathVariant variant in System.Enum.GetValues(typeof(DeathVariant)))
        {
            var death = new HumanoidDeathAnimator(StandingAnimator(), variant);
            for (int i = 0; i < 300; i++)
            {
                death.Tick(0.016f);
                Assert.GreaterOrEqual(death.LowestBodyPoint, -0.02f, variant + " at frame " + i);
            }
        }
    }

    [Test]
    public void HeadLandsInTheFallDirection()
    {
        foreach (DeathVariant variant in System.Enum.GetValues(typeof(DeathVariant)))
        {
            var death = Run(variant, 4f);
            Vector3 head = death.GetWorldPosition(HumanoidBone.Head);
            Vector3 flat = new Vector3(head.x, 0f, head.z);
            Vector3 expected = DeathVariantSelector.FallDirection(variant);
            Assert.Greater(Vector3.Dot(flat.normalized, expected), 0.95f, variant.ToString());
            Assert.Greater(flat.magnitude, 0.8f, variant + " fell in place");
            Assert.Less(flat.magnitude, 2.0f);
        }
    }

    [Test]
    public void FallingIsContinuousAndNotInstant()
    {
        var death = new HumanoidDeathAnimator(StandingAnimator(), DeathVariant.FallBackward);
        float previous = death.FallAngle;
        float at200ms = 0f;
        float at1500ms = 0f;
        for (int i = 1; i <= 120; i++)
        {
            death.Tick(0.016f);
            Assert.Less(Mathf.Abs(death.FallAngle - previous), 9f, "angle jumped at frame " + i);
            previous = death.FallAngle;
            if (i == 12) at200ms = previous;
            if (i == 94) at1500ms = previous;
        }

        Assert.Less(at200ms, 45f, "body should not be down after 0.2 s");
        Assert.Greater(at1500ms, 80f, "body should be down after 1.5 s");
    }

    [Test]
    public void HeadshotSnapBackFallsFasterThanForwardCollapse()
    {
        var snap = new HumanoidDeathAnimator(StandingAnimator(), DeathVariant.HeadshotSnapBack);
        var collapse = new HumanoidDeathAnimator(StandingAnimator(), DeathVariant.ForwardCollapse);
        for (int i = 0; i < 20; i++)
        {
            snap.Tick(0.016f);
            collapse.Tick(0.016f);
        }

        Assert.Greater(snap.FallAngle, collapse.FallAngle);
    }

    [Test]
    public void KneelCollapseDropsTheHipsBeforeToppling()
    {
        var death = new HumanoidDeathAnimator(StandingAnimator(), DeathVariant.KneelCollapse);
        float startHips = death.GetWorldPosition(HumanoidBone.Hips).y;
        for (int i = 0; i < 20; i++)
            death.Tick(0.016f);
        Assert.Less(death.FallAngle, 6f, "topple should be delayed while the knees buckle");
        Assert.Less(death.GetWorldPosition(HumanoidBone.Hips).y, startHips - 0.2f);
    }

    [Test]
    public void SideFallsAreMirrorImages()
    {
        var left = Run(DeathVariant.SideFallLeft, 3f);
        var right = Run(DeathVariant.SideFallRight, 3f);
        foreach (HumanoidBone bone in HumanoidParts.AllBones)
        {
            Vector3 l = left.GetWorldPosition(bone);
            Vector3 r = right.GetWorldPosition(HumanoidDeathAnimator.Mirrored(bone));
            Assert.AreEqual(-l.x, r.x, 0.03f, bone.ToString());
            Assert.AreEqual(l.y, r.y, 0.03f, bone.ToString());
            Assert.AreEqual(l.z, r.z, 0.03f, bone.ToString());
        }
    }

    [Test]
    public void LimbsOvershootThenSettle()
    {
        var death = new HumanoidDeathAnimator(StandingAnimator(), DeathVariant.FallBackward);
        float peak = 0f;
        Quaternion target = Quaternion.identity;
        for (int i = 0; i < 400; i++)
        {
            death.Tick(0.016f);
            target = death.GetLocalRotation(HumanoidBone.ForearmL);
        }

        float settled = Quaternion.Angle(Quaternion.identity, target);
        var again = new HumanoidDeathAnimator(StandingAnimator(), DeathVariant.FallBackward);
        for (int i = 0; i < 400; i++)
        {
            again.Tick(0.016f);
            peak = Mathf.Max(peak, Quaternion.Angle(Quaternion.identity, again.GetLocalRotation(HumanoidBone.ForearmL)));
        }

        Assert.Greater(peak, settled + 1f, "forearm should ring past its final angle");
        Assert.AreEqual(Quaternion.Angle(Quaternion.identity, again.GetLocalRotation(HumanoidBone.ForearmL)), settled, 0.01f);
    }

    [Test]
    public void StrongerImpulseFallsFaster()
    {
        var soft = Run(DeathVariant.FallBackward, 0.25f, 0.5f);
        var hard = Run(DeathVariant.FallBackward, 0.25f, 1.6f);
        Assert.Greater(hard.FallAngle, soft.FallAngle);
    }

    [Test]
    public void TickIsIndependentOfFrameRate()
    {
        var fast = new HumanoidDeathAnimator(StandingAnimator(), DeathVariant.ForwardCollapse);
        var slow = new HumanoidDeathAnimator(StandingAnimator(), DeathVariant.ForwardCollapse);
        for (int i = 0; i < 100; i++)
            fast.Tick(0.01f);
        for (int i = 0; i < 25; i++)
            slow.Tick(0.04f);
        Assert.AreEqual(fast.FallAngle, slow.FallAngle, 1.5f);
    }

    [Test]
    public void ScaleRotation_HandlesLargeFactorsAndIdentity()
    {
        Quaternion q = Quaternion.Euler(30f, 0f, 0f);
        Assert.Less(Quaternion.Angle(HumanoidDeathAnimator.ScaleRotation(q, 1f), q), 0.01f);
        Assert.AreEqual(45f, Quaternion.Angle(Quaternion.identity, HumanoidDeathAnimator.ScaleRotation(q, 1.5f)), 0.05f);
        Assert.Less(Quaternion.Angle(HumanoidDeathAnimator.ScaleRotation(Quaternion.identity, 3f), Quaternion.identity), 0.01f);
    }

    [Test]
    public void SpringResponse_StartsAtZeroOvershootsAndConvergesToOne()
    {
        Assert.AreEqual(0f, HumanoidDeathAnimator.SpringResponse(-1f, 10f), 1e-6f);
        float peak = 0f;
        for (float t = 0f; t < 2f; t += 0.005f)
            peak = Mathf.Max(peak, HumanoidDeathAnimator.SpringResponse(t, 10f));
        Assert.Greater(peak, 1.1f);
        Assert.AreEqual(1f, HumanoidDeathAnimator.SpringResponse(3f, 10f), 0.01f);
    }

    // --- Dropped gun -----------------------------------------------------------------------

    [Test]
    public void DroppedGun_FallsBouncesAndComesToRestOnTheGround()
    {
        var gun = new DroppedGunPhysics(new Vector3(0f, 1.1f, 0f), Quaternion.Euler(20f, 40f, 0f),
            new Vector3(0.5f, 0.6f, 0.2f), new Vector3(120f, 60f, 200f), p => 0.25f);
        float lowest = float.MaxValue;
        for (int i = 0; i < 400; i++)
        {
            gun.Tick(0.016f);
            lowest = Mathf.Min(lowest, gun.Position.y);
        }

        Assert.IsTrue(gun.AtRest);
        Assert.GreaterOrEqual(lowest, 0.25f + 0.045f - 1e-3f, "gun tunnelled through the ground");
        Assert.AreEqual(0.25f + 0.045f, gun.Position.y, 0.01f);
        Assert.GreaterOrEqual(gun.Bounces, 1);
        Vector3 barrel = gun.Rotation * Vector3.forward;
        Assert.AreEqual(0f, barrel.y, 0.05f, "barrel should lie flat");
    }

    [Test]
    public void DroppedGun_RestRotation_KeepsHeadingAndIsHorizontal()
    {
        Quaternion rest = DroppedGunPhysics.RestRotation(Quaternion.Euler(-35f, 70f, 20f));
        Vector3 barrel = rest * Vector3.forward;
        Assert.AreEqual(0f, barrel.y, 1e-4f);
        Assert.AreEqual(70f, Quaternion.LookRotation(barrel).eulerAngles.y, 0.1f);
    }
}
