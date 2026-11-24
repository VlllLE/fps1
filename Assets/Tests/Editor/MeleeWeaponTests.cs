using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class MeleeWeaponTests
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
    public void MeleeWeapon_ReportsNoAmmo()
    {
        WeaponController weapon = CreateWeapon(WeaponType.Melee, out _);

        Assert.That(weapon.IsMelee, Is.True);
        Assert.That(weapon.UsesAmmo, Is.False);
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(WeaponController.NoAmmoDisplay));
        Assert.That(weapon.ReserveAmmo, Is.EqualTo(WeaponController.NoAmmoDisplay));
        Assert.That(weapon.HasAmmo(), Is.True);
        Assert.That(weapon.NeedsReload(), Is.False);
    }

    [Test]
    public void MeleeWeapon_BroadcastsNoAmmoSentinel()
    {
        var weaponObject = new GameObject("MeleeWeapon");
        created.Add(weaponObject);
        WeaponController weapon = weaponObject.AddComponent<WeaponController>();
        int magazine = 0;
        int reserve = 0;
        weapon.OnAmmoChanged += (m, r) =>
        {
            magazine = m;
            reserve = r;
        };

        weapon.Initialize(CreateConfig(WeaponType.Melee));

        Assert.That(magazine, Is.EqualTo(WeaponController.NoAmmoDisplay));
        Assert.That(reserve, Is.EqualTo(WeaponController.NoAmmoDisplay));
    }

    [Test]
    public void FirearmWeapon_KeepsRealAmmoCounts()
    {
        WeaponController weapon = CreateWeapon(WeaponType.AssaultRifle, out WeaponConfig config);

        Assert.That(weapon.IsMelee, Is.False);
        Assert.That(weapon.UsesAmmo, Is.True);
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(config.magazineSize));
        Assert.That(weapon.ReserveAmmo, Is.EqualTo(config.startingReserveAmmo));
    }

    [Test]
    public void WeaponConfig_IsMelee_TracksWeaponType()
    {
        WeaponConfig config = CreateConfig(WeaponType.Melee);
        Assert.That(config.IsMelee, Is.True);
        config.weaponType = WeaponType.SMG;
        Assert.That(config.IsMelee, Is.False);
    }

    [Test]
    public void SlashAnimator_PoseIsRestAtEndpoints_AndMovesInBetween()
    {
        MeleeSlashAnimator animator = CreateAnimator();

        animator.Evaluate(0f, false, out Vector3 startPos, out Quaternion startRot);
        animator.Evaluate(1f, false, out Vector3 endPos, out Quaternion endRot);
        animator.Evaluate(0.5f, false, out Vector3 midPos, out Quaternion midRot);

        Assert.That(startPos.magnitude, Is.LessThan(0.0001f));
        Assert.That(endPos.magnitude, Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(startRot, Quaternion.identity), Is.LessThan(0.01f));
        Assert.That(Quaternion.Angle(endRot, Quaternion.identity), Is.LessThan(0.01f));
        Assert.That(midPos.magnitude, Is.GreaterThan(0.05f));
        Assert.That(Quaternion.Angle(midRot, Quaternion.identity), Is.GreaterThan(10f));
    }

    [Test]
    public void SlashAnimator_MirroredSwingFlipsSide()
    {
        MeleeSlashAnimator animator = CreateAnimator();

        animator.Evaluate(0.5f, false, out Vector3 forehand, out _);
        animator.Evaluate(0.5f, true, out Vector3 backhand, out _);

        Assert.That(forehand.x, Is.EqualTo(-backhand.x).Within(0.0001f));
        Assert.That(forehand.z, Is.EqualTo(backhand.z).Within(0.0001f));
    }

    private MeleeSlashAnimator CreateAnimator()
    {
        var go = new GameObject("SlashAnimator");
        created.Add(go);
        return go.AddComponent<MeleeSlashAnimator>();
    }

    private WeaponController CreateWeapon(WeaponType type, out WeaponConfig config)
    {
        var go = new GameObject("Weapon");
        created.Add(go);
        WeaponController weapon = go.AddComponent<WeaponController>();
        config = CreateConfig(type);
        weapon.Initialize(config);
        return weapon;
    }

    private WeaponConfig CreateConfig(WeaponType type)
    {
        var config = ScriptableObject.CreateInstance<WeaponConfig>();
        created.Add(config);
        config.weaponType = type;
        config.magazineSize = 30;
        config.startingReserveAmmo = 90;
        config.maxReserveAmmo = 120;
        return config;
    }
}
