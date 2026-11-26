using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Scene-object tests for the third-person gun, death presentation and first-person grip clamp.</summary>
public class ThirdPersonGunTests
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

    private WeaponConfig MakeConfig(WeaponType type, string name)
    {
        WeaponConfig config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.weaponType = type;
        config.weaponName = name;
        created.Add(config);
        return config;
    }

    private HumanoidBody MakeBody()
    {
        var player = new GameObject("TestSoldier");
        created.Add(player);
        var controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 1f, 0f);
        HumanoidBodySettings settings = HumanoidBodySettings.Player();
        settings.forceProcedural = true;
        HumanoidBody body = HumanoidBody.AddTo(player, settings);
        body.Build();
        return body;
    }

    [Test]
    public void Gun_WithoutAModelFallsBackToAPrimitiveWithMagazineAndMuzzle()
    {
        var parent = new GameObject("Parent");
        created.Add(parent);
        ThirdPersonGun gun = ThirdPersonGun.Create(parent.transform, MakeConfig(WeaponType.Pistol, "Pistol"), null, 0);

        Assert.IsFalse(gun.UsesImportedModel);
        Assert.IsNotNull(gun.Muzzle);
        Assert.IsNotNull(gun.Root.Find("Model/Magazine"), "the magazine part exists so reloads can move it");
        Assert.AreEqual(0, gun.Root.GetComponentsInChildren<Collider>().Length, "a held gun never blocks anything");
        Assert.AreEqual(gun.Profile.muzzle.z, gun.Root.InverseTransformPoint(gun.Muzzle.position).z, 1e-3f);
        gun.Destroy();
    }

    [Test]
    public void Body_EquipWeaponPutsTheGunInBothHands()
    {
        HumanoidBody body = MakeBody();
        body.EquipWeapon(MakeConfig(WeaponType.AssaultRifle, "M4A1"));
        Assert.IsNotNull(body.Gun);
        Assert.IsNotNull(body.Muzzle);

        var input = new HumanoidAnimationInput { holdProfile = body.Gun.Profile, holdStance = HoldStance.Aim };
        for (int i = 0; i < 90; i++)
            body.Step(0.016f, input);

        HumanoidAnimator animator = body.Animator;
        Assert.IsTrue(animator.IsHolding);
        Vector3 expected = body.BodyRoot.TransformPoint(animator.GunPosition * body.BuiltScale);
        Assert.AreEqual(0f, Vector3.Distance(expected, body.Gun.Root.position), 0.005f);
        Assert.IsTrue(System.Array.IndexOf(body.GetLocalHideTargets(), body.Gun.Root) >= 0, "the first-person camera does not see the soldier's gun");

        body.ClearWeapon();
        Assert.IsNull(body.Gun);
    }

    [Test]
    public void Body_RebuildKeepsTheEquippedWeapon()
    {
        HumanoidBody body = MakeBody();
        body.EquipWeapon(MakeConfig(WeaponType.SMG, "UMP"));
        body.Build();
        Assert.IsNotNull(body.Gun);
        Assert.AreEqual(HoldCategory.Smg, body.Gun.Profile.category);
    }

    [Test]
    public void Body_DeathTopplesTheBodyDropsTheGunAndRespawnRestoresBoth()
    {
        HumanoidBody body = MakeBody();
        body.EquipWeapon(MakeConfig(WeaponType.AssaultRifle, "M4A1"));
        body.Step(0.016f, new HumanoidAnimationInput { holdProfile = body.Gun.Profile, holdStance = HoldStance.Ready });

        DeathVariant variant = body.BeginDeath(new DeathContext { localHitDirection = Vector3.back, damage = 40f, roll = 0.5f });
        Assert.AreEqual(DeathVariant.FallBackward, variant);
        Assert.IsTrue(body.IsDying);

        for (int i = 0; i < 240; i++)
            body.DeathAnimator.Tick(0.016f);
        Assert.IsTrue(body.DeathAnimator.Settled);

        body.EndDeath();
        Assert.IsFalse(body.IsDying);
        Assert.AreEqual(Quaternion.identity, body.BodyRoot.localRotation);
        Assert.IsFalse(body.Gun.IsDropped);
        Assert.AreEqual(body.BodyRoot, body.Gun.Root.parent);
    }

    [Test]
    public void DroppedGun_FallsToTheGroundAndComesToRest()
    {
        var parent = new GameObject("Parent");
        created.Add(parent);
        ThirdPersonGun gun = ThirdPersonGun.Create(parent.transform, MakeConfig(WeaponType.AssaultRifle, "M4A1"), null, 0);
        gun.Root.position = new Vector3(0f, 1.2f, 0f);
        gun.BeginDrop(new Vector3(0.5f, 0.8f, 0.3f), new Vector3(200f, 60f, 300f), p => 0f);
        Assert.IsTrue(gun.IsDropped);

        for (int i = 0; i < 400 && !gun.DropAtRest; i++)
            gun.TickDrop(0.02f);

        Assert.IsTrue(gun.DropAtRest);
        Assert.Less(gun.Root.position.y, 0.15f);
        Assert.GreaterOrEqual(gun.Root.position.y, -0.01f);

        gun.Regrip(parent.transform);
        Assert.IsFalse(gun.IsDropped);
        gun.Destroy();
    }

    [Test]
    public void BotStances_CarryLowWhilePatrollingAndShoulderTheGunWhenShooting()
    {
        Assert.AreEqual(HoldStance.Lowered, BotController.StanceFor(BotState.Patrol));
        Assert.AreEqual(HoldStance.Aim, BotController.StanceFor(BotState.Attack));
        Assert.AreEqual(HoldStance.Ready, BotController.StanceFor(BotState.Chase));
        Assert.AreEqual(HoldStance.Ready, BotController.StanceFor(BotState.TakeCover));
    }

    [Test]
    public void FirstPersonSupportGrip_SlidesBackWhenTheArmCannotReach()
    {
        Vector3 shoulder = Vector3.zero;
        Vector3 front = new Vector3(0f, 0f, 0.8f);
        Vector3 rear = new Vector3(0f, 0f, 0.5f);

        Assert.AreEqual(front, ProceduralFirstPersonArms.ClampSupportGrip(shoulder, front, rear, 0.9f), "in reach: stays on the handguard");
        Vector3 slid = ProceduralFirstPersonArms.ClampSupportGrip(shoulder, front, rear, 0.65f);
        Assert.Less(slid.z, front.z, "out of reach: the hand slides back");
        Assert.LessOrEqual(slid.magnitude, 0.65f + 0.04f);
        Assert.AreEqual(rear, ProceduralFirstPersonArms.ClampSupportGrip(shoulder, front, rear, 0.2f), "never beyond the rear point");
    }

    [Test]
    public void DeathCamera_EndsOnTheSideOfTheBodyAboveTheFloor()
    {
        Vector3 focus = new Vector3(2f, 0.4f, 3f);
        Vector3 end = DeathCamera.ComputeEndPosition(focus, Vector3.back, 1f, 0f, DeathCamera.Distance, DeathCamera.Height);
        Assert.AreEqual(DeathCamera.Distance, new Vector2(end.x - focus.x, end.z - focus.z).magnitude, 1e-3f);
        Assert.AreEqual(focus.y + DeathCamera.Height, end.y, 1e-4f);
        Assert.AreEqual(0f, Vector3.Dot(end - focus - Vector3.up * DeathCamera.Height, Vector3.back), 1e-3f, "the camera looks along the body's side, not down its length");
    }
}
