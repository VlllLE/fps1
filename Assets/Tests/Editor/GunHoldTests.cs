using NUnit.Framework;
using UnityEngine;

public class GunHoldTests
{
    private static readonly string[] Guns =
        { "M4A1", "SCAR", "UMP", "LMG", "Shotgun", "Pistol", "BoltActionSniper", "DMRSniper", "Karambit" };

    private static readonly HoldStance[] Stances = { HoldStance.Lowered, HoldStance.Ready, HoldStance.Aim };

    private static HumanoidAnimator Settle(GunHoldProfile profile, HoldStance stance, float pitch = 0f, float seconds = 2f)
    {
        var animator = new HumanoidAnimator();
        var input = new HumanoidAnimationInput { holdProfile = profile, holdStance = stance, lookPitchDegrees = pitch };
        for (float t = 0f; t < seconds; t += 0.016f)
            animator.Tick(0.016f, input);
        return animator;
    }

    // --- Hold pose selection ----------------------------------------------------------------

    [TestCase(WeaponType.AssaultRifle, "M4A1", HoldCategory.Rifle)]
    [TestCase(WeaponType.AssaultRifle, "SCAR(Clone)", HoldCategory.Rifle)]
    [TestCase(WeaponType.SMG, "UMP", HoldCategory.Smg)]
    [TestCase(WeaponType.LMG, "LMG", HoldCategory.Lmg)]
    [TestCase(WeaponType.Shotgun, "Shotgun", HoldCategory.Shotgun)]
    [TestCase(WeaponType.Sniper, "BoltActionSniper", HoldCategory.Sniper)]
    [TestCase(WeaponType.Sniper, "DMRSniper", HoldCategory.Sniper)]
    [TestCase(WeaponType.Pistol, "Pistol", HoldCategory.Pistol)]
    [TestCase(WeaponType.Melee, "Karambit", HoldCategory.Melee)]
    public void Catalog_PicksTheProfileForTheWeaponName(WeaponType type, string name, HoldCategory expected)
    {
        Assert.AreEqual(expected, GunHoldCatalog.Get(type, name).category);
    }

    [TestCase(WeaponType.AssaultRifle, HoldCategory.Rifle)]
    [TestCase(WeaponType.SMG, HoldCategory.Smg)]
    [TestCase(WeaponType.LMG, HoldCategory.Lmg)]
    [TestCase(WeaponType.Shotgun, HoldCategory.Shotgun)]
    [TestCase(WeaponType.Sniper, HoldCategory.Sniper)]
    [TestCase(WeaponType.Pistol, HoldCategory.Pistol)]
    [TestCase(WeaponType.Melee, HoldCategory.Melee)]
    public void Catalog_FallsBackToTheWeaponTypeForUnknownNames(WeaponType type, HoldCategory expected)
    {
        Assert.AreEqual(expected, GunHoldCatalog.Get(type, "Mystery Weapon 9000").category);
        Assert.AreEqual(expected, GunHoldCatalog.Get(type, null).category);
    }

    [TestCase("DMRSniperConfig", "DMR", WeaponType.Sniper, "DMRSniper")]
    [TestCase("BoltActionSniperConfig", "Bolt Action Sniper", WeaponType.Sniper, "BoltActionSniper")]
    [TestCase("SCARConfig", "SCAR", WeaponType.AssaultRifle, "SCAR")]
    [TestCase("M4A1Config", "M4A1", WeaponType.AssaultRifle, "M4A1")]
    [TestCase("UMPConfig", "UMP", WeaponType.SMG, "UMP")]
    public void Catalog_ResolvesSlotNamedInstancesFromTheConfig(string assetName, string weaponName, WeaponType type, string expected)
    {
        var config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.name = assetName;
        config.weaponName = weaponName;
        config.weaponType = type;
        var instance = new GameObject("Assault_Primary");
        try
        {
            Assert.AreEqual(expected, GunHoldCatalog.Get(config, instance).name);
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Catalog_ReturnsIndependentInstances()
    {
        GunHoldProfile a = GunHoldCatalog.Build("M4A1");
        a.rightGrip = new Vector3(9f, 9f, 9f);
        Assert.AreNotEqual(a.rightGrip, GunHoldCatalog.Build("M4A1").rightGrip);
    }

    [Test]
    public void Catalog_LmgUsesTheBipodStanceAndOnlyShoulderedGunsHaveStocks()
    {
        Assert.IsTrue(GunHoldCatalog.Build("LMG").bipodStance);
        Assert.IsFalse(GunHoldCatalog.Build("Pistol").stocked);
        Assert.IsTrue(GunHoldCatalog.Build("M4A1").stocked);
    }

    [Test]
    public void Profiles_HaveSaneGeometry()
    {
        foreach (string name in Guns)
        {
            GunHoldProfile p = GunHoldCatalog.Build(name);
            Assert.GreaterOrEqual(p.muzzle.z, 0.1f, name + " muzzle is ahead of the weapon origin");
            Assert.Less(Vector3.Distance(p.rightGrip, p.leftGrip) , 1.2f, name + " hands are on the same gun");
            if (p.category != HoldCategory.Melee)
                Assert.Greater(p.leftGrip.z, p.rightGrip.z, name + " support hand is ahead of the shooting hand");
        }
    }

    // --- Hand placement ---------------------------------------------------------------------

    [Test]
    public void Hands_StayOnTheGunInEveryStanceAndLookPitch()
    {
        foreach (string name in Guns)
        {
            foreach (HoldStance stance in Stances)
            {
                foreach (float pitch in new[] { -35f, 0f, 35f })
                {
                    HumanoidAnimator a = Settle(GunHoldCatalog.Build(name), stance, pitch);
                    string label = name + " " + stance + " pitch " + pitch;
                    Assert.Less(Vector3.Distance(a.GetPalmPosition(true), a.GetPalmTarget(true)), 0.02f, label + " right palm");
                    // The lowered carry keeps the gun close to the body, so at extreme look pitch the support arm may fall a few cm short.
                    float leftTolerance = stance == HoldStance.Lowered ? 0.07f : 0.02f;
                    if (a.GetHandHoldWeight(false) > 0.5f)
                        Assert.Less(Vector3.Distance(a.GetPalmPosition(false), a.GetPalmTarget(false)), leftTolerance, label + " left palm");
                }
            }
        }
    }

    [Test]
    public void TwoHandedGuns_UseTheSupportHandAndTheKarambitDoesNot()
    {
        foreach (string name in Guns)
        {
            HumanoidAnimator a = Settle(GunHoldCatalog.Build(name), HoldStance.Ready);
            if (name == "Karambit")
                Assert.Less(a.GetHandHoldWeight(false), 0.1f, "the knife is one-handed");
            else
                Assert.Greater(a.GetHandHoldWeight(false), 0.9f, name + " is two-handed");
        }
    }

    // --- Aiming -----------------------------------------------------------------------------

    [Test]
    public void AimStance_PointsTheMuzzleAlongTheLookDirection()
    {
        foreach (string name in Guns)
        {
            if (name == "Karambit")
                continue;
            foreach (float pitch in new[] { -40f, -15f, 0f, 20f, 40f })
            {
                HumanoidAnimator a = Settle(GunHoldCatalog.Build(name), HoldStance.Aim, pitch);
                Vector3 muzzle = a.GunRotation * Vector3.forward;
                Vector3 look = Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;
                Assert.Less(Vector3.Angle(muzzle, look), 1.5f, name + " pitch " + pitch);
            }
        }
    }

    [Test]
    public void Stances_RaiseTheMuzzleFromLoweredToReadyToAim()
    {
        foreach (string name in Guns)
        {
            if (name == "Karambit" || name == "Pistol")
                continue;
            float lowered = (Settle(GunHoldCatalog.Build(name), HoldStance.Lowered).GunRotation * Vector3.forward).y;
            float aim = (Settle(GunHoldCatalog.Build(name), HoldStance.Aim).GunRotation * Vector3.forward).y;
            Assert.Less(lowered, aim - 0.15f, name + ": the lowered carry points the muzzle down");
        }
    }

    [Test]
    public void LookPitch_IsSharedBetweenSpineChestAndHead()
    {
        HumanoidAnimator level = Settle(GunHoldCatalog.Build("M4A1"), HoldStance.Aim, 0f);
        HumanoidAnimator down = Settle(GunHoldCatalog.Build("M4A1"), HoldStance.Aim, 40f);
        foreach (HumanoidBone bone in new[] { HumanoidBone.Spine, HumanoidBone.Chest, HumanoidBone.Head })
        {
            float turned = Quaternion.Angle(level.GetLocalRotation(bone), down.GetLocalRotation(bone));
            Assert.Greater(turned, 2f, bone + " follows the look pitch");
            Assert.Less(turned, 38f, bone + " does not take the whole pitch alone");
        }
    }

    [Test]
    public void ReleasingTheWeapon_FallsBackToTheLegacyArmPose()
    {
        var animator = new HumanoidAnimator();
        var held = new HumanoidAnimationInput { holdProfile = GunHoldCatalog.Build("M4A1"), holdStance = HoldStance.Ready };
        for (int i = 0; i < 60; i++)
            animator.Tick(0.016f, held);
        Assert.IsTrue(animator.IsHolding);

        var free = new HumanoidAnimationInput { armPose = HumanoidArmPose.Relaxed };
        for (int i = 0; i < 120; i++)
            animator.Tick(0.016f, free);
        Assert.IsFalse(animator.IsHolding);
    }

    // --- Shots and reloads ------------------------------------------------------------------

    [Test]
    public void Shots_KickTheGunBackAndItRecovers()
    {
        var animator = Settle(GunHoldCatalog.Build("M4A1"), HoldStance.Aim) ;
        var input = new HumanoidAnimationInput { holdProfile = GunHoldCatalog.Build("M4A1"), holdStance = HoldStance.Aim };
        float restZ = animator.GunPosition.z;
        float restPitch = (animator.GunRotation * Vector3.forward).y;

        animator.NotifyShot();
        animator.Tick(0.016f, input);
        Assert.Less(animator.GunPosition.z, restZ - 0.005f, "the gun moves back");
        Assert.Greater((animator.GunRotation * Vector3.forward).y, restPitch + 0.01f, "the muzzle climbs");

        for (int i = 0; i < 90; i++)
            animator.Tick(0.016f, input);
        Assert.AreEqual(restZ, animator.GunPosition.z, 0.012f, "back at rest (within the idle sway)");
    }

    [Test]
    public void Reload_MovesTheMagazineAndTheSupportHandThenRestsAgain()
    {
        GunHoldProfile profile = GunHoldCatalog.Build("M4A1");
        HumanoidAnimator a = Settle(profile, HoldStance.Ready);
        Assert.AreEqual(0f, a.MagazineOffset.magnitude, 1e-4f, "idle magazine is seated");

        float farthest = 0f;
        for (float progress = 0f; progress <= 1f; progress += 0.02f)
        {
            a.Tick(0.016f, new HumanoidAnimationInput
            {
                holdProfile = profile, holdStance = HoldStance.Ready, reloading = true, reloadProgress = progress, reloadCycles = 1
            });
            farthest = Mathf.Max(farthest, a.MagazineOffset.magnitude);
        }

        Assert.Greater(farthest, 0.08f, "the magazine leaves the well during the reload");

        for (int i = 0; i < 60; i++)
            a.Tick(0.016f, new HumanoidAnimationInput { holdProfile = profile, holdStance = HoldStance.Ready });
        Assert.AreEqual(0f, a.MagazineOffset.magnitude, 1e-3f, "the magazine is seated again afterwards");
    }

    [Test]
    public void Reload_SupportHandFollowsTheMagazineOut()
    {
        GunHoldProfile profile = GunHoldCatalog.Build("M4A1");
        HumanoidAnimator a = Settle(profile, HoldStance.Ready);
        Vector3 before = a.GetPalmTarget(false);
        float maxMove = 0f;
        for (float progress = 0f; progress <= 1f; progress += 0.02f)
        {
            a.Tick(0.016f, new HumanoidAnimationInput
            {
                holdProfile = profile, holdStance = HoldStance.Ready, reloading = true, reloadProgress = progress, reloadCycles = 1
            });
            maxMove = Mathf.Max(maxMove, Vector3.Distance(before, a.GetPalmTarget(false)));
        }

        Assert.Greater(maxMove, 0.1f, "the support palm leaves the handguard to work the magazine");
    }

    [Test]
    public void Hold_IsDeterministic()
    {
        HumanoidAnimator a = Settle(GunHoldCatalog.Build("SCAR"), HoldStance.Aim, 12f);
        HumanoidAnimator b = Settle(GunHoldCatalog.Build("SCAR"), HoldStance.Aim, 12f);
        Assert.AreEqual(a.GunPosition, b.GunPosition);
        Assert.AreEqual(a.GunRotation, b.GunRotation);
    }
}
