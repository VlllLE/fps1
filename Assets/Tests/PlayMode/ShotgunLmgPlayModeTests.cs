using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ShotgunLmgPlayModeTests
{
    private GameObject root;
    private GameObject targetObject;
    private WeaponConfig runtimeConfig;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.Destroy(root);
        if (targetObject != null)
            Object.Destroy(targetObject);
        if (runtimeConfig != null)
            Object.Destroy(runtimeConfig);
        Time.timeScale = 1f;
    }

    private static LoadoutWeaponEntry CatalogEntry(string id)
    {
        WeaponLoadoutCatalog catalog = WeaponLoadoutCatalog.LoadFromResources();
        Assert.That(catalog, Is.Not.Null);
        LoadoutWeaponEntry entry = catalog.FindWeapon(id);
        Assert.That(entry, Is.Not.Null, id + " missing from the weapon catalog");
        return entry;
    }

    private WeaponController SpawnWeapon(string id, out Camera camera)
    {
        root = new GameObject("WeaponTestRoot");
        root.AddComponent<HitDetection>();

        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        camera = cameraObject.AddComponent<Camera>();

        LoadoutWeaponEntry entry = CatalogEntry(id);
        GameObject instance = Object.Instantiate(entry.prefab, root.transform);
        WeaponController weapon = instance.GetComponent<WeaponController>();
        Assert.That(weapon, Is.Not.Null);
        weapon.SetPlayerCamera(camera);
        return weapon;
    }

    private void SpawnTarget(float distance, out MeleeTestReceiver receiver)
    {
        targetObject = new GameObject("Target");
        targetObject.transform.position = new Vector3(0f, 0f, distance);
        var box = targetObject.AddComponent<BoxCollider>();
        box.size = new Vector3(8f, 8f, 0.5f);
        receiver = targetObject.AddComponent<MeleeTestReceiver>();
        Physics.SyncTransforms();
    }

    [UnityTest]
    public IEnumerator Shotgun_Prefab_FiresOnePelletRayPerPelletThroughHitDetection()
    {
        WeaponController weapon = SpawnWeapon("Shotgun", out _);
        SpawnTarget(4f, out MeleeTestReceiver receiver);
        int expectedPellets = weapon.Config.pelletCount;

        List<HitResult> pelletResults = null;
        int resolvedEvents = 0;
        weapon.OnShotgunHitsResolved += results => pelletResults = results;
        weapon.OnHitResolved += _ => resolvedEvents++;

        yield return null;
        weapon.Equip();
        yield return new WaitForSeconds(1.0f);

        int ammoBefore = weapon.CurrentAmmo;
        weapon.StartFiring();
        weapon.StopFiring();
        yield return null;

        Assert.That(expectedPellets, Is.GreaterThan(1));
        Assert.That(pelletResults, Is.Not.Null, "Shotgun must route through the multi-pellet path.");
        Assert.That(pelletResults.Count, Is.EqualTo(expectedPellets));
        Assert.That(resolvedEvents, Is.EqualTo(0), "Single-ray path must not also fire.");
        Assert.That(receiver.HitCount, Is.EqualTo(expectedPellets), "Each pellet lands on the wide target.");
        Assert.That(receiver.LastAmount, Is.EqualTo(weapon.Config.damage).Within(weapon.Config.damage * 0.25f));
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(ammoBefore - 1), "One shell per trigger pull, not per pellet.");
    }

    [UnityTest]
    public IEnumerator Shotgun_PelletsSpreadAcrossDifferentDirections()
    {
        WeaponController weapon = SpawnWeapon("Shotgun", out _);
        SpawnTarget(10f, out _);

        List<HitResult> pelletResults = null;
        weapon.OnShotgunHitsResolved += results => pelletResults = results;

        yield return null;
        weapon.Equip();
        yield return new WaitForSeconds(1.0f);
        weapon.StartFiring();
        weapon.StopFiring();
        yield return null;

        Assert.That(pelletResults, Is.Not.Null);
        var distinct = new HashSet<Vector3>();
        foreach (HitResult result in pelletResults)
            distinct.Add(result.Direction);
        Assert.That(distinct.Count, Is.GreaterThan(1), "Pellets should not all travel along one ray.");
    }

    [UnityTest]
    public IEnumerator Shotgun_PerShellReload_AddsOneRoundAtATime()
    {
        WeaponController weapon = SpawnWeapon("Shotgun", out _);
        runtimeConfig = Object.Instantiate(weapon.Config);
        runtimeConfig.reloadAmmoTime = 0.05f;
        runtimeConfig.shellReloadTime = 0.1f;
        runtimeConfig.shellReloadFinishTime = 0.05f;
        weapon.Initialize(runtimeConfig);

        yield return null;
        weapon.Equip();
        weapon.SetAmmo(2, 10);

        var ammoSteps = new List<int>();
        bool completed = false;
        weapon.OnAmmoChanged += (magazine, reserve) => ammoSteps.Add(magazine);
        weapon.OnReloadComplete += () => completed = true;

        weapon.TryReload();
        Assert.That(weapon.IsReloading, Is.True);

        float timeout = Time.time + 3f;
        while (!completed && Time.time < timeout)
            yield return null;

        Assert.That(completed, Is.True);
        Assert.That(weapon.IsReloading, Is.False);
        Assert.That(ammoSteps, Is.EqualTo(new List<int> { 3, 4, 5, 6 }), "Rounds must be inserted one by one.");
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(runtimeConfig.magazineSize));
        Assert.That(weapon.ReserveAmmo, Is.EqualTo(10 - (runtimeConfig.magazineSize - 2)));
    }

    [UnityTest]
    public IEnumerator Shotgun_PerShellReload_CanBeInterruptedByFiring()
    {
        WeaponController weapon = SpawnWeapon("Shotgun", out _);
        SpawnTarget(4f, out _);
        runtimeConfig = Object.Instantiate(weapon.Config);
        runtimeConfig.reloadAmmoTime = 0.05f;
        runtimeConfig.shellReloadTime = 0.6f;
        runtimeConfig.fireRate = 600f;
        weapon.Initialize(runtimeConfig);

        yield return null;
        weapon.Equip();
        yield return new WaitForSeconds(0.3f);
        weapon.SetAmmo(3, 10);

        weapon.TryReload();
        yield return new WaitForSeconds(0.2f);
        Assert.That(weapon.IsReloading, Is.True);
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(3), "No shell has been loaded yet.");

        weapon.StartFiring();
        weapon.StopFiring();
        yield return null;

        Assert.That(weapon.IsReloading, Is.False, "Firing cancels the shell reload.");
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(2));
        yield return new WaitForSeconds(0.8f);
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(2), "A cancelled reload must not keep loading in the background.");
    }

    [UnityTest]
    public IEnumerator Shotgun_EmptyChamber_CannotInterruptReloadToFire()
    {
        WeaponController weapon = SpawnWeapon("Shotgun", out _);
        runtimeConfig = Object.Instantiate(weapon.Config);
        runtimeConfig.reloadAmmoTime = 0.05f;
        runtimeConfig.shellReloadTime = 0.5f;
        weapon.Initialize(runtimeConfig);

        yield return null;
        weapon.Equip();
        weapon.SetAmmo(0, 10);
        weapon.TryReload();
        yield return new WaitForSeconds(0.1f);

        weapon.StartFiring();
        weapon.StopFiring();
        yield return null;

        Assert.That(weapon.IsReloading, Is.True, "With an empty tube the reload keeps going.");
    }

    [UnityTest]
    public IEnumerator Lmg_Prefab_HasLargeMagazine_AndAutoFireSpendsAmmo()
    {
        WeaponController weapon = SpawnWeapon("LMG", out _);
        SpawnTarget(10f, out MeleeTestReceiver receiver);

        yield return null;
        weapon.Equip();
        yield return new WaitForSeconds(0.3f);

        int start = weapon.CurrentAmmo;
        Assert.That(start, Is.GreaterThanOrEqualTo(75));

        weapon.StartFiring();
        float end = Time.time + 0.4f;
        while (Time.time < end)
        {
            weapon.UpdateFiring();
            yield return null;
        }
        weapon.StopFiring();

        int spent = start - weapon.CurrentAmmo;
        Assert.That(spent, Is.GreaterThanOrEqualTo(3), "Auto fire should chain several rounds.");
        Assert.That(spent, Is.LessThan(start / 2));
        Assert.That(receiver.HitCount, Is.EqualTo(spent), "Every round is a single hitscan ray.");
    }

    [UnityTest]
    public IEnumerator Lmg_Reload_IsSlowerThanRifleReload()
    {
        WeaponController weapon = SpawnWeapon("LMG", out _);
        runtimeConfig = Object.Instantiate(weapon.Config);
        weapon.Initialize(runtimeConfig);
        yield return null;
        weapon.Equip();
        weapon.SetAmmo(10, 100);

        weapon.TryReload();
        yield return new WaitForSeconds(2.0f);

        Assert.That(weapon.IsReloading, Is.True, "LMG reload takes several seconds.");
        Assert.That(weapon.CurrentAmmo, Is.EqualTo(10), "Belt is not refilled until the late reload step.");
    }

    [UnityTest]
    public IEnumerator SpawnedWeapons_ApplyTheirDefaultSkinOnAwake()
    {
        foreach (string id in new[] { "Shotgun", "LMG" })
        {
            WeaponController weapon = SpawnWeapon(id, out _);
            yield return null;

            var applier = weapon.GetComponent<WeaponSkinApplier>();
            Assert.That(applier, Is.Not.Null, id);
            Assert.That(applier.CurrentSkin, Is.Not.Null, id + " should start with its default skin applied.");
            Assert.That(applier.CurrentSkin, Is.SameAs(applier.DefaultSkin), id);

            Object.Destroy(root);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator LoadoutMenuService_CanEquipShotgunAndLmg_OnTestRange()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);

        var service = Object.FindAnyObjectByType<WeaponLoadoutService>();
        Assert.That(service, Is.Not.Null);

        Assert.That(service.SelectPrimary(service.Catalog.FindWeapon("Shotgun")), Is.True);
        yield return null;
        Assert.That(service.SelectSecondary(service.Catalog.FindWeapon("LMG")), Is.True);
        yield return null;

        WeaponController primary = service.GetEquippedWeapon(SlotType.Primary);
        WeaponController secondary = service.GetEquippedWeapon(SlotType.Secondary);
        Assert.That(primary, Is.Not.Null);
        Assert.That(secondary, Is.Not.Null);
        Assert.That(primary.Config.weaponType, Is.EqualTo(WeaponType.Shotgun));
        Assert.That(secondary.Config.weaponType, Is.EqualTo(WeaponType.LMG));
        Assert.That(service.Selection.PrimaryId, Is.EqualTo("Shotgun"));
        Assert.That(service.Selection.SecondaryId, Is.EqualTo("LMG"));

        PlayerPrefs.DeleteKey(PlayerPrefsLoadoutStore.DefaultKey);
    }
}
