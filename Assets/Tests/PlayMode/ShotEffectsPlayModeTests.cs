using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Bullet tracers, impacts, muzzle flashes, casings and the first-person kick on a weapon in an isolated rig.</summary>
public class ShotEffectsPlayModeTests
{
    private static readonly string[] KickWeaponIds = { "M4A1", "SCAR", "UMP", "LMG", "Shotgun", "BoltActionSniper", "DMRSniper" };
    private static readonly Vector3 FarAway = new Vector3(3000f, 0f, 3000f);

    private GameObject root;
    private GameObject wall;
    private GameObject target;
    private GameObject ground;
    private WeaponController weapon;

    [SetUp]
    public void SetUp()
    {
        ShotEffects.Clear();
        ShotEffects.Enabled = true;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject spawned in new[] { root, wall, target, ground })
        {
            if (spawned != null)
                Object.DestroyImmediate(spawned);
        }

        root = wall = target = ground = null;
        weapon = null;
        ShotEffects.Enabled = true;
        ShotEffects.Clear();
        Time.timeScale = 1f;
    }

    private WeaponController SpawnWeapon(string id)
    {
        root = new GameObject("FxWeaponRoot");
        root.transform.position = FarAway;
        root.AddComponent<HitDetection>();

        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;

        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        Assert.That(catalog, Is.Not.Null);
        LoadoutWeaponEntry entry = catalog.FindWeapon(id);
        Assert.That(entry, Is.Not.Null, id + " missing from the weapon catalog");

        GameObject instance = Object.Instantiate(entry.prefab, root.transform);
        weapon = instance.GetComponent<WeaponController>();
        Assert.That(weapon, Is.Not.Null);
        weapon.SetPlayerCamera(camera);
        return weapon;
    }

    private void SpawnWall(float distance)
    {
        wall = new GameObject("FxWall");
        wall.transform.position = FarAway + new Vector3(0f, 0f, distance);
        wall.AddComponent<BoxCollider>().size = new Vector3(40f, 40f, 0.5f);
        Physics.SyncTransforms();
    }

    private MeleeTestReceiver SpawnTarget(float distance)
    {
        target = new GameObject("FxTarget");
        target.transform.position = FarAway + new Vector3(0f, 0f, distance);
        target.AddComponent<BoxCollider>().size = new Vector3(40f, 40f, 0.5f);
        MeleeTestReceiver receiver = target.AddComponent<MeleeTestReceiver>();
        Physics.SyncTransforms();
        return receiver;
    }

    private void SpawnGround(float topY)
    {
        ground = new GameObject("FxGround");
        ground.transform.position = FarAway + new Vector3(0f, topY - 0.5f, 0f);
        ground.AddComponent<BoxCollider>().size = new Vector3(60f, 1f, 60f);
        Physics.SyncTransforms();
    }

    private IEnumerator Ready(WeaponController w)
    {
        yield return null;
        w.Equip();
        yield return new WaitForSeconds(1.0f);
        ShotEffects.Clear();
    }

    private static void Shoot(WeaponController w)
    {
        w.StartFiring();
        w.StopFiring();
    }

    private static T[] Active<T>() where T : Object
    {
        return Object.FindObjectsByType<T>();
    }

    // --- Tracers ----------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Tracer_RunsFromTheMuzzleToTheWallFace()
    {
        SpawnWeapon("M4A1");
        SpawnWall(8f);
        yield return Ready(weapon);

        Shoot(weapon);

        Assert.That(ShotEffects.TracersPlayed, Is.EqualTo(1));
        TracerFx tracer = Active<TracerFx>().Single();
        Vector3 muzzle = weapon.MuzzleTransform.position;
        Assert.That(Vector3.Distance(tracer.Start, muzzle), Is.LessThan(0.01f), "the streak starts at the muzzle");
        Assert.That(tracer.End.z, Is.EqualTo(FarAway.z + 8f - 0.25f).Within(0.05f), "and ends on the wall's near face");
        Assert.That(tracer.Distance, Is.EqualTo(Vector3.Distance(tracer.Start, tracer.End)).Within(0.01f));
    }

    [UnityTest]
    public IEnumerator Tracer_ForAMiss_RunsToTheWeaponRange()
    {
        SpawnWeapon("M4A1");
        yield return Ready(weapon);
        float expected = Mathf.Min(weapon.Config.range, ShotEffects.MaxTracerDistance);

        Shoot(weapon);

        TracerFx tracer = Active<TracerFx>().Single();
        Assert.That(tracer.End.z - FarAway.z, Is.EqualTo(expected).Within(expected * 0.05f + 0.5f));
        Assert.That(tracer.Distance, Is.LessThanOrEqualTo(ShotEffects.MaxTracerDistance + 0.5f));
    }

    [UnityTest]
    public IEnumerator Tracer_Streaks_ThenIsReturnedToThePool()
    {
        var from = FarAway;
        TracerFx tracer = ShotEffects.PlayTracer(from, from + new Vector3(0f, 0f, 20f), null);
        Assert.That(tracer, Is.Not.Null);
        Assert.That(ShotEffects.ActiveTracers, Is.EqualTo(1));

        yield return new WaitForSeconds(0.5f);

        Assert.That(ShotEffects.ActiveTracers, Is.EqualTo(0), "a 20 m streak is long gone after half a second");

        ShotEffects.PlayTracer(from, from + Vector3.forward * 5f, null);
        Assert.That(ShotEffects.TracerObjectsCreated, Is.EqualTo(1), "the released streak is reused instead of a new one");
    }

    [UnityTest]
    public IEnumerator Shotgun_ShowsATracerForEveryPellet()
    {
        SpawnWeapon("Shotgun");
        SpawnWall(10f);
        yield return Ready(weapon);

        Shoot(weapon);

        Assert.That(weapon.Config.pelletCount, Is.GreaterThan(1));
        Assert.That(ShotEffects.TracersPlayed, Is.EqualTo(weapon.Config.pelletCount));
        Assert.That(ShotEffects.ActiveTracers, Is.EqualTo(weapon.Config.pelletCount));
    }

    // --- Impacts ----------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Impact_OnTheWorld_MakesDustSparksAndABulletHole()
    {
        SpawnWeapon("M4A1");
        SpawnWall(6f);
        yield return Ready(weapon);

        Shoot(weapon);

        Assert.That(ShotEffects.ImpactsPlayed, Is.GreaterThanOrEqualTo(1));
        Assert.That(ShotEffects.DecalsPlayed, Is.EqualTo(1), "a bullet hole is left on the wall");
        Assert.That(ShotEffects.ActiveDecals, Is.EqualTo(1));
        ImpactFx impact = Active<ImpactFx>().First();
        Assert.That(impact.LastEmitted, Is.GreaterThan(0), "particles were emitted");
    }

    [UnityTest]
    public IEnumerator Impact_OnATarget_MakesAPuffButNoBulletHole()
    {
        SpawnWeapon("M4A1");
        MeleeTestReceiver receiver = SpawnTarget(6f);
        yield return Ready(weapon);

        Shoot(weapon);

        Assert.That(receiver.HitCount, Is.GreaterThan(0), "the shot still damages the target");
        Assert.That(ShotEffects.ImpactsPlayed, Is.EqualTo(1));
        Assert.That(ShotEffects.DecalsPlayed, Is.EqualTo(0), "no bullet holes on things that take damage");
        Assert.That(Active<ImpactFx>().First().LastEmitted, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator Effects_DoNotChangeWhatTheShotHits()
    {
        SpawnWeapon("M4A1");
        MeleeTestReceiver receiver = SpawnTarget(6f);
        yield return Ready(weapon);
        ShotEffects.Enabled = false;
        Shoot(weapon);
        int hitsWithoutEffects = receiver.HitCount;
        Assert.That(ShotEffects.TracersPlayed, Is.EqualTo(0));

        yield return new WaitForSeconds(0.3f);
        ShotEffects.Enabled = true;
        Shoot(weapon);
        int hitsWithEffects = receiver.HitCount - hitsWithoutEffects;

        Assert.That(hitsWithoutEffects, Is.GreaterThan(0));
        Assert.That(hitsWithEffects, Is.EqualTo(hitsWithoutEffects), "tracers and impacts are visual only");
        Assert.That(ShotEffects.TracersPlayed, Is.EqualTo(1));
    }

    // --- Muzzle flash -----------------------------------------------------------------------

    [UnityTest]
    public IEnumerator MuzzleFlash_FollowsTheMuzzleAndLightsTheScene()
    {
        SpawnWeapon("M4A1");
        yield return Ready(weapon);

        Shoot(weapon);

        Assert.That(ShotEffects.FlashesPlayed, Is.EqualTo(1));
        MuzzleFlashFx flash = Active<MuzzleFlashFx>().Single();
        Assert.That(flash.HasLight, Is.True);
        Assert.That(Vector3.Distance(flash.transform.position, weapon.MuzzleTransform.position), Is.LessThan(0.2f));

        yield return new WaitForSeconds(0.4f);
        Assert.That(ShotEffects.ActiveFlashes, Is.EqualTo(0), "the flash lasts only a few frames");
    }

    [UnityTest]
    public IEnumerator MuzzleFlash_OnAHiddenLayerGun_StaysOnThatLayerWithoutALight()
    {
        int hiddenLayer = LayerMask.NameToLayer("PlayerLocalHide");
        Assert.That(hiddenLayer, Is.GreaterThanOrEqualTo(0));
        var muzzle = new GameObject("HiddenMuzzle") { layer = hiddenLayer };
        muzzle.transform.position = FarAway;
        try
        {
            MuzzleFlashFx flash = ShotEffects.PlayMuzzleFlash(muzzle.transform, null, false);
            ShellCasingFx casing = ShotEffects.PlayCasing(muzzle.transform, null);

            Assert.That(flash, Is.Not.Null);
            Assert.That(flash.gameObject.layer, Is.EqualTo(hiddenLayer), "invisible to the first-person camera");
            Assert.That(flash.HasLight, Is.False, "and no light leaks into the first-person view");
            Assert.That(casing, Is.Not.Null);
            Assert.That(casing.gameObject.layer, Is.EqualTo(hiddenLayer));
        }
        finally
        {
            Object.DestroyImmediate(muzzle);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator MuzzleFlash_BiggerWeaponsFlashBigger()
    {
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.position = FarAway;
        var smg = ScriptableObject.CreateInstance<WeaponConfig>();
        smg.weaponType = WeaponType.SMG;
        var shotgun = ScriptableObject.CreateInstance<WeaponConfig>();
        shotgun.weaponType = WeaponType.Shotgun;
        try
        {
            float small = ShotEffects.PlayMuzzleFlash(muzzle.transform, smg, true).Scale;
            float big = ShotEffects.PlayMuzzleFlash(muzzle.transform, shotgun, true).Scale;
            Assert.That(big, Is.GreaterThan(small * 1.5f));
        }
        finally
        {
            Object.DestroyImmediate(muzzle);
            Object.DestroyImmediate(smg);
            Object.DestroyImmediate(shotgun);
        }

        yield return null;
    }

    // --- Casings ----------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Casing_FallsLandsRestsAndFadesAway()
    {
        SpawnWeapon("M4A1");
        SpawnGround(-1.5f);
        yield return Ready(weapon);

        Shoot(weapon);

        ShellCasingFx casing = Active<ShellCasingFx>().Single();
        float startY = casing.transform.position.y;
        Assert.That(casing.AtRest, Is.False);

        float deadline = Time.realtimeSinceStartup + 4f;
        while (!casing.AtRest && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.That(casing.AtRest, Is.True, "the casing lands and stops");
        Assert.That(casing.transform.position.y, Is.LessThan(startY - 0.8f), "it fell to the ground");
        Assert.That(casing.transform.position.y, Is.GreaterThan(FarAway.y - 1.6f), "and did not fall through it");

        deadline = Time.realtimeSinceStartup + 6f;
        while (ShotEffects.ActiveCasings > 0 && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.That(ShotEffects.ActiveCasings, Is.EqualTo(0), "it fades away and goes back to the pool");
    }

    [UnityTest]
    public IEnumerator Casing_Shotgun_EjectsOneHullPerShotNotOnePerPellet()
    {
        SpawnWeapon("Shotgun");
        yield return Ready(weapon);

        Shoot(weapon);

        Assert.That(ShotEffects.CasingsPlayed, Is.EqualTo(1));
        Assert.That(ShotEffects.FlashesPlayed, Is.EqualTo(1), "one flash for the whole blast");
        Assert.That(Active<ShellCasingFx>().Single().Age, Is.LessThan(0.1f));
    }

    // --- Pools ------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Pools_NeverGrowPastTheirCaps()
    {
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.position = FarAway;
        SpawnWall(5f);
        Collider wallCollider = wall.GetComponent<Collider>();
        try
        {
            for (int i = 0; i < 300; i++)
            {
                ShotEffects.PlayTracer(FarAway, FarAway + Vector3.forward * 50f, null, i % 2 == 0);
                ShotEffects.PlayMuzzleFlash(muzzle.transform, null, true);
                ShotEffects.PlayCasing(muzzle.transform, null);
                ShotEffects.PlayImpact(FarAway + new Vector3(0f, 0f, 4.75f), Vector3.back, wallCollider);
            }

            Assert.That(ShotEffects.TracersPlayed, Is.EqualTo(300));
            Assert.That(ShotEffects.TracerObjectsCreated, Is.LessThanOrEqualTo(ShotEffects.TracerCapacity));
            Assert.That(ShotEffects.ActiveTracers, Is.LessThanOrEqualTo(ShotEffects.TracerCapacity));
            Assert.That(ShotEffects.FlashObjectsCreated, Is.LessThanOrEqualTo(ShotEffects.FlashCapacity));
            Assert.That(ShotEffects.CasingObjectsCreated, Is.LessThanOrEqualTo(ShotEffects.CasingCapacity));
            Assert.That(ShotEffects.ImpactObjectsCreated, Is.LessThanOrEqualTo(ShotEffects.ImpactCapacity));
            Assert.That(ShotEffects.DecalObjectsCreated, Is.LessThanOrEqualTo(ShotEffects.DecalCapacity));
            Assert.That(ShotEffects.ActiveDecals, Is.LessThanOrEqualTo(ShotEffects.DecalCapacity));
        }
        finally
        {
            Object.DestroyImmediate(muzzle);
        }

        yield return null;
        Assert.That(Active<Light>().Count(l => l.enabled && l.gameObject.name.Contains("Flash")), Is.LessThanOrEqualTo(ShotEffects.FlashCapacity));
    }

    // --- First-person kick and bolt cycle ---------------------------------------------------

    [UnityTest]
    public IEnumerator Kick_EveryGunKicksOnFire_ThenSettles()
    {
        foreach (string id in KickWeaponIds)
        {
            TearDown();
            SpawnWeapon(id);
            yield return Ready(weapon);
            Assert.That(weapon.FireKickDisplacement, Is.EqualTo(0f).Within(0.001f), id + " starts at rest");

            Shoot(weapon);
            float peak = 0f;
            float zAtPeak = 0f;
            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
                if (weapon.FireKickDisplacement > peak)
                {
                    peak = weapon.FireKickDisplacement;
                    zAtPeak = weapon.FireKickPositionOffset.z;
                }
            }

            Assert.That(peak, Is.GreaterThan(0.2f), id + " visibly kicks");
            Assert.That(zAtPeak, Is.LessThan(0f), id + " kicks the gun back");

            yield return new WaitForSeconds(2.5f);
            Assert.That(weapon.FireKickDisplacement, Is.EqualTo(0f).Within(0.02f), id + " settles");
        }
    }

    [UnityTest]
    public IEnumerator Kick_Karambit_DoesNotUseTheGunKick()
    {
        SpawnWeapon("Karambit");
        yield return Ready(weapon);

        Shoot(weapon);
        yield return new WaitForSeconds(0.1f);

        Assert.That(weapon.FireKickDisplacement, Is.EqualTo(0f).Within(0.001f));
        Assert.That(ShotEffects.TracersPlayed, Is.EqualTo(0), "a knife shows no bullet tracer");
        Assert.That(ShotEffects.FlashesPlayed, Is.EqualTo(0), "and no muzzle flash");
    }

    [UnityTest]
    public IEnumerator BoltSniper_CyclesTheBoltAfterTheShot()
    {
        SpawnWeapon("BoltActionSniper");
        yield return Ready(weapon);
        Transform bolt = typeof(WeaponController).GetField("boltTransform", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon) as Transform;
        Assume.That(bolt, Is.Not.Null, "this model has no bolt part");
        Vector3 rest = bolt.localPosition;

        Shoot(weapon);
        float deepest = 0f;
        float firstMove = -1f;
        float start = Time.time;
        while (Time.time - start < 1.6f)
        {
            yield return null;
            float shift = bolt.localPosition.z - rest.z;
            deepest = Mathf.Min(deepest, shift);
            if (firstMove < 0f && Mathf.Abs(shift) > 0.002f)
                firstMove = Time.time - start;
        }

        Assert.That(deepest, Is.LessThan(-0.04f), "the bolt is drawn back");
        Assert.That(firstMove, Is.GreaterThan(0.25f), "after the shot has left the barrel");
        Assert.That(Vector3.Distance(bolt.localPosition, rest), Is.LessThan(0.002f), "and driven home again");
    }
}
