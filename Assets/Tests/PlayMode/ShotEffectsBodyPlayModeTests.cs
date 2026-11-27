using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>The local player's third-person body reacts to its own shots, in step with the first-person weapon.</summary>
public class ShotEffectsLocalBodyPlayModeTests
{
    private GameObject player;
    private HumanoidBody body;
    private WeaponSwitcher switcher;
    private int hiddenLayer;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        SceneManager.LoadScene("TestRange", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);

        var movement = Object.FindAnyObjectByType<MovementController>();
        Assert.That(movement, Is.Not.Null);
        player = movement.gameObject;
        body = player.GetComponent<HumanoidBody>();
        switcher = player.GetComponentInChildren<WeaponSwitcher>();
        hiddenLayer = LayerMask.NameToLayer("PlayerLocalHide");
        yield return new WaitForSeconds(1.5f);
        ShotEffects.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        ShotEffects.Clear();
        Time.timeScale = 1f;
    }

    private IEnumerator Equip(SlotType slot)
    {
        switcher.SwitchToSlotType(slot);
        yield return new WaitForSeconds(1.2f);
        ShotEffects.Clear();
    }

    [UnityTest]
    public IEnumerator Firing_RecoilsTheBody_AndFlashesOnBothTheViewmodelAndTheHiddenBodyGun()
    {
        Assert.That(body, Is.Not.Null);
        yield return Equip(SlotType.Primary);
        WeaponController weapon = switcher.CurrentWeapon;
        Assume.That(weapon, Is.Not.Null);
        Assume.That(weapon.IsMelee, Is.False);

        weapon.StartFiring();
        weapon.StopFiring();

        MuzzleFlashFx[] flashes = Object.FindObjectsByType<MuzzleFlashFx>();
        Assert.That(flashes.Count(f => f.gameObject.layer != hiddenLayer && f.HasLight), Is.EqualTo(1), "the viewmodel flash lights the scene");
        Assert.That(flashes.Count(f => f.gameObject.layer == hiddenLayer && !f.HasLight), Is.EqualTo(1),
            "the third-person gun flashes on the hidden layer and without a light, so the player's own camera never sees it");
        Assert.That(ShotEffects.CasingsPlayed, Is.GreaterThanOrEqualTo(2), "the viewmodel and the body's gun each eject a casing");
        Assert.That(body.Animator.FireCycleTime, Is.LessThan(0.3f), "the body started its fire reaction");

        yield return null;
        Assert.That(Mathf.Abs(body.Animator.Recoil), Is.GreaterThan(0.001f), "the body recoils");
    }

    [UnityTest]
    public IEnumerator FirstPersonCamera_StillCullsTheLocalBodyLayer()
    {
        yield return Equip(SlotType.Primary);
        Camera camera = Camera.main;
        Assert.That(camera, Is.Not.Null);

        switcher.CurrentWeapon.StartFiring();
        switcher.CurrentWeapon.StopFiring();

        Assert.That(camera.cullingMask & (1 << hiddenLayer), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator Knife_SwingsTheBody_WithoutAMuzzleFlashOrTracer()
    {
        float giveUp = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < giveUp && (switcher.CurrentWeapon == null || !switcher.CurrentWeapon.IsMelee))
        {
            switcher.SwitchToSlotType(SlotType.Melee);
            yield return new WaitForSeconds(0.6f);
            if (switcher.CurrentWeapon != null && switcher.CurrentWeapon.IsMelee) break;
            switcher.SwitchToSlotType(SlotType.Secondary);
            yield return new WaitForSeconds(0.6f);
        }
        yield return new WaitForSeconds(0.8f);
        ShotEffects.Clear();
        WeaponController knife = switcher.CurrentWeapon;
        Assume.That(knife, Is.Not.Null);
        Assume.That(knife.IsMelee, Is.True);

        knife.StartFiring();
        knife.StopFiring();

        bool swung = false;
        float deadline = Time.realtimeSinceStartup + 0.5f;
        while (!swung && Time.realtimeSinceStartup < deadline)
        {
            swung = body.Animator.IsSwinging;
            yield return null;
        }

        Assert.That(swung, Is.True, "the third-person body swings the blade");
        Assert.That(ShotEffects.FlashesPlayed, Is.EqualTo(0));
        Assert.That(ShotEffects.TracersPlayed, Is.EqualTo(0));
        Assert.That(ShotEffects.CasingsPlayed, Is.EqualTo(0));
    }
}

/// <summary>Bots show tracers from the muzzle of the gun they hold and react with the same fire animation.</summary>
public class ShotEffectsBotPlayModeTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private GameObject playerObject;
    private BotManager manager;
    private WeaponConfig weapon;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Scene leftover = SceneManager.GetActiveScene();
        if (System.Array.IndexOf(BotMapPlayModeTests.Maps, leftover.name) >= 0)
        {
            Scene arena = SceneManager.CreateScene("BotFxArena");
            SceneManager.SetActiveScene(arena);
            yield return SceneManager.UnloadSceneAsync(leftover);
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "BotFxGround";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(120f, 1f, 120f);
        created.Add(ground);

        playerObject = new GameObject("BotFxPlayer");
        playerObject.transform.position = new Vector3(0f, 0.05f, -20f);
        var controller = playerObject.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 1f, 0f);
        playerObject.AddComponent<PlayerHealth>();
        created.Add(playerObject);

        weapon = BotGun.CreateFallbackConfig();
        manager = BotManager.Create();
        manager.AutoSpawnCount = 0;
        manager.KeysEnabled = false;
        manager.StartDifficulty = BotDifficulty.Normal;
        float timeout = 10f;
        while (!manager.IsReady && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        Assert.That(manager.IsReady, Is.True);
        ShotEffects.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (BotController bot in new List<BotController>(BotController.All))
        {
            if (bot != null)
                Object.DestroyImmediate(bot.gameObject);
        }

        if (manager != null)
            Object.DestroyImmediate(manager.gameObject);
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                Object.DestroyImmediate(created[i]);
        }

        created.Clear();
        if (weapon != null)
            Object.DestroyImmediate(weapon);
        BotNavigation.Reset();
        ShotEffects.Clear();
        Time.timeScale = 1f;
    }

    private BotController SpawnBotFacingPlayer(Vector3 position)
    {
        Vector3 toPlayer = playerObject.transform.position - position;
        toPlayer.y = 0f;
        return BotController.Create("FxBot", position, Quaternion.LookRotation(toPlayer), manager.Settings, weapon, Color.red, 4321);
    }

    [UnityTest]
    public IEnumerator Bot_Shooting_DrawsATracerFromItsGunMuzzle()
    {
        BotSettings s = manager.Settings;
        s.reactionTime = 0.1f;
        s.aimErrorDegrees = 0.5f;
        s.aimErrorWhileMoving = 0f;
        s.aimErrorPerTargetSpeed = 0f;
        s.aimTurnSpeed = 720f;
        s.damageMultiplier = 0.2f;
        s.strafe = false;
        s.useCover = false;
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, -5f));
        yield return null;

        float deadline = Time.realtimeSinceStartup + 6f;
        while (ShotEffects.TracersPlayed == 0 && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.That(ShotEffects.TracersPlayed, Is.GreaterThan(0), "the bot's bullets are visible");
        TracerFx tracer = Object.FindObjectsByType<TracerFx>().FirstOrDefault();
        Assert.That(tracer, Is.Not.Null);
        Assert.That(bot.Body.Muzzle, Is.Not.Null);
        Assert.That(Vector3.Distance(tracer.Start, bot.Body.Muzzle.position), Is.LessThan(0.6f), "the streak leaves the held gun's muzzle");
        Assert.That(ShotEffects.FlashesPlayed, Is.GreaterThan(0));
        Assert.That(ShotEffects.CasingsPlayed, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator Bot_FireReaction_FlashesEjectsAndKicksTheBody()
    {
        manager.Settings.viewDistance = 1f;
        manager.Settings.closeAwarenessRadius = 0f;
        manager.Settings.hearingScale = 0f;
        BotController bot = SpawnBotFacingPlayer(new Vector3(30f, 0f, 30f));
        yield return new WaitForSeconds(0.5f);
        ShotEffects.Clear();

        bot.Body.NotifyFired(true);

        MuzzleFlashFx flash = Object.FindObjectsByType<MuzzleFlashFx>().Single();
        Assert.That(flash.HasLight, Is.True, "bots light up their surroundings");
        Assert.That(Vector3.Distance(flash.transform.position, bot.Body.Muzzle.position), Is.LessThan(0.2f));
        Assert.That(ShotEffects.CasingsPlayed, Is.EqualTo(1));
        Assert.That(bot.Body.Animator.FireCycleTime, Is.LessThan(0.1f));

        yield return null;
        Assert.That(Mathf.Abs(bot.Body.Animator.Recoil), Is.GreaterThan(0.001f), "the soldier's body recoils");
    }
}
