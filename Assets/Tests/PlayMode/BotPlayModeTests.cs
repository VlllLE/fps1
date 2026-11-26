using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Bots on a generated flat arena: navigation, perception, shooting, damage, death and respawn.</summary>
public class BotPlayModeTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private GameObject playerObject;
    private PlayerHealth playerHealth;
    private BotManager manager;
    private WeaponConfig weapon;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Scene leftover = SceneManager.GetActiveScene();
        if (System.Array.IndexOf(BotMapPlayModeTests.Maps, leftover.name) >= 0)
        {
            Scene arena = SceneManager.CreateScene("BotArena");
            SceneManager.SetActiveScene(arena);
            yield return SceneManager.UnloadSceneAsync(leftover);
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "BotArenaGround";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(120f, 1f, 120f);
        created.Add(ground);

        playerObject = new GameObject("BotTestPlayer");
        playerObject.transform.position = new Vector3(0f, 0.05f, -20f);
        var controller = playerObject.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 1f, 0f);
        playerHealth = playerObject.AddComponent<PlayerHealth>();
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

        Assert.That(manager.IsReady, Is.True, "BotManager should finish building navigation");
    }

    [TearDown]
    public void TearDown()
    {
        var bots = new List<BotController>(BotController.All);
        for (int i = 0; i < bots.Count; i++)
        {
            if (bots[i] != null)
                Object.DestroyImmediate(bots[i].gameObject);
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
        Time.timeScale = 1f;
    }

    private BotController SpawnBotFacingPlayer(Vector3 position)
    {
        Vector3 toPlayer = playerObject.transform.position - position;
        toPlayer.y = 0f;
        BotController bot = BotController.Create("TestBot", position, Quaternion.LookRotation(toPlayer), manager.Settings, weapon,
            Color.red, 1234);
        return bot;
    }

    private static void MakeBotsDeadly(BotSettings settings)
    {
        settings.reactionTime = 0.1f;
        settings.aimErrorDegrees = 0.5f;
        settings.aimErrorWhileMoving = 0f;
        settings.aimErrorPerTargetSpeed = 0f;
        settings.aimTurnSpeed = 720f;
        settings.damageMultiplier = 0.5f;
        settings.strafe = false;
        settings.useCover = false;
    }

    [UnityTest]
    public IEnumerator Navigation_IsGeneratedForTheArena()
    {
        Assert.That(BotNavigation.IsReady, Is.True);
        Assert.That(BotNavigation.Mode, Is.EqualTo(BotNavMode.NavMesh).Or.EqualTo(BotNavMode.Fallback));
        if (BotNavigation.Mode == BotNavMode.NavMesh)
            Assert.That(NavMesh.SamplePosition(new Vector3(10f, 0f, 10f), out _, 1.5f, NavMesh.AllAreas), Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Manager_AddsHealthToThePlayerAndSpawnsBotsAwayFromIt()
    {
        BotController bot = manager.SpawnBot();
        yield return null;

        Assert.That(PlayerHealth.Local, Is.EqualTo(playerHealth));
        Assert.That(manager.Bots.Count, Is.EqualTo(1));
        Assert.That(bot.Brain.State, Is.EqualTo(BotState.Patrol).Or.EqualTo(BotState.Attack).Or.EqualTo(BotState.Investigate)
            .Or.EqualTo(BotState.Chase));
        Assert.That(Vector3.Distance(bot.transform.position, playerObject.transform.position), Is.GreaterThan(3f));
    }

    [UnityTest]
    public IEnumerator Bot_HasAHumanoidBodyWithHitboxes()
    {
        BotController bot = manager.SpawnBot(new Vector3(20f, 0f, 20f));
        yield return null;

        Assert.That(bot.Body, Is.Not.Null);
        Assert.That(bot.Body.IsBuilt, Is.True);
        Assert.That(bot.Body.Hitboxes.Count, Is.GreaterThan(0));
        Assert.That(bot.Health, Is.EqualTo(manager.Settings.maxHealth));
        Assert.That(bot.GetComponent<Rigidbody>().isKinematic, Is.True);
    }

    [UnityTest]
    public IEnumerator Bot_PatrolsBetweenWaypoints()
    {
        manager.Settings.viewDistance = 1f;
        manager.Settings.closeAwarenessRadius = 0f;
        manager.Settings.hearingScale = 0f;
        BotController bot = manager.SpawnBot(new Vector3(30f, 0f, 30f));
        yield return null;

        Vector3 start = bot.transform.position;
        float farthest = 0f;
        float elapsed = 0f;
        while (elapsed < 8f)
        {
            elapsed += Time.deltaTime;
            farthest = Mathf.Max(farthest, Vector3.Distance(start, bot.transform.position));
            yield return null;
        }

        Assert.That(bot.Brain.State, Is.EqualTo(BotState.Patrol));
        Assert.That(farthest, Is.GreaterThan(2f), "a patrolling bot should walk somewhere");
        Assert.That(bot.PatrolWaypoints.Count, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator Bot_SeesShootsAndDamagesThePlayer()
    {
        MakeBotsDeadly(manager.Settings);
        playerHealth.GodMode = false;
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, 0f));
        yield return null;

        bool attacked = false;
        float elapsed = 0f;
        while (elapsed < 8f && playerHealth.TotalDamageTaken <= 0f)
        {
            elapsed += Time.deltaTime;
            attacked |= bot.Brain.State == BotState.Attack;
            yield return null;
        }

        Assert.That(attacked, Is.True, "the bot should switch to Attack when it sees the player");
        Assert.That(bot.ShotsFired, Is.GreaterThan(0));
        Assert.That(playerHealth.TotalDamageTaken, Is.GreaterThan(0f), "bot bullets should reach the player through HitDetection");
    }

    [UnityTest]
    public IEnumerator Bot_ShotsReportTheirSourceToThePlayerHealth()
    {
        MakeBotsDeadly(manager.Settings);
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, 0f));
        Vector3 reportedSource = Vector3.zero;
        bool hit = false;
        playerHealth.OnDamaged += (amount, source, fatal) =>
        {
            hit = true;
            reportedSource = source;
        };

        float elapsed = 0f;
        while (elapsed < 8f && !hit)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.That(hit, Is.True);
        Assert.That(Vector3.Distance(reportedSource, bot.transform.position), Is.LessThan(1.5f));
    }

    [UnityTest]
    public IEnumerator Bot_CannotSeeThroughAWall()
    {
        MakeBotsDeadly(manager.Settings);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(0f, 2f, -10f);
        wall.transform.localScale = new Vector3(30f, 4f, 0.5f);
        created.Add(wall);

        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, 0f));
        yield return new WaitForSeconds(2.5f);

        Assert.That(bot.Brain.State, Is.Not.EqualTo(BotState.Attack));
        Assert.That(playerHealth.TotalDamageTaken, Is.Zero);
    }

    [UnityTest]
    public IEnumerator Bot_TakesDamageFromRealShotsAndDiesAndRespawns()
    {
        manager.Settings.respawnDelay = 0.6f;
        manager.Settings.corpseTime = 0.3f;
        BotController bot = manager.SpawnBot(new Vector3(25f, 0f, 25f));
        bot.Frozen = true;
        yield return null;
        Physics.SyncTransforms();

        Vector3 chest = bot.transform.position + Vector3.up * 1.1f;
        HitDetection resolver = BotManager.HitResolver;
        HitRequest request = HitRequest.Create(chest + Vector3.back * 4f, Vector3.forward, 10f, 30f);
        request.SourceId = -1;
        request.ShotId = 1;
        resolver.Resolve(request);

        Assert.That(bot.Health, Is.LessThan(manager.Settings.maxHealth), "a hit on the hitbox must damage the bot");
        Assert.That(bot.IsDead, Is.False);

        request.BaseDamage = 500f;
        request.ShotId = 2;
        resolver.Resolve(request);

        Assert.That(bot.IsDead, Is.True);
        Assert.That(bot.Deaths, Is.EqualTo(1));

        yield return new WaitForSeconds(1.2f);

        Assert.That(bot.IsDead, Is.False, "the bot respawns after the respawn delay");
        Assert.That(bot.Health, Is.EqualTo(manager.Settings.maxHealth));
        Assert.That(bot.Gun.Ammo, Is.EqualTo(bot.Gun.MagazineSize));
    }

    [UnityTest]
    public IEnumerator Bot_IgnoresDamageFromOtherBots()
    {
        BotController victim = manager.SpawnBot(new Vector3(25f, 0f, 25f));
        BotController shooter = manager.SpawnBot(new Vector3(-25f, 0f, 25f));
        yield return null;

        victim.ApplyHitDamage(new HitDamageInfo(1, shooter.SourceId, 50f, Vector3.zero, Vector3.up, Vector3.forward, HitZoneType.Body, null));

        Assert.That(victim.Health, Is.EqualTo(manager.Settings.maxHealth));
    }

    [UnityTest]
    public IEnumerator Freeze_StopsBotsAndAppliesToNewBots()
    {
        manager.Settings.viewDistance = 1f;
        manager.Settings.closeAwarenessRadius = 0f;
        manager.Settings.hearingScale = 0f;
        BotController first = manager.SpawnBot(new Vector3(30f, 0f, 30f));
        manager.SetFrozen(true);
        BotController second = manager.SpawnBot(new Vector3(-30f, 0f, 30f));
        yield return null;
        Vector3 firstStart = first.transform.position;
        Vector3 secondStart = second.transform.position;

        yield return new WaitForSeconds(1.5f);

        Assert.That(first.Frozen, Is.True);
        Assert.That(second.Frozen, Is.True);
        Assert.That(Vector3.Distance(firstStart, first.transform.position), Is.LessThan(0.01f));
        Assert.That(Vector3.Distance(secondStart, second.transform.position), Is.LessThan(0.01f));

        manager.SetFrozen(false);
        Assert.That(first.Frozen, Is.False);
    }

    [UnityTest]
    public IEnumerator Difficulty_SwitchUpdatesTheSharedSettings()
    {
        BotController bot = manager.SpawnBot(new Vector3(30f, 0f, 30f));
        yield return null;

        manager.SetDifficulty(BotDifficulty.Veteran);

        Assert.That(manager.Difficulty, Is.EqualTo(BotDifficulty.Veteran));
        Assert.That(bot.Settings, Is.SameAs(manager.Settings));
        Assert.That(bot.Settings.reactionTime, Is.EqualTo(0.15f).Within(0.0001f));
        Assert.That(bot.Health, Is.EqualTo(bot.Settings.maxHealth));
    }

    [UnityTest]
    public IEnumerator PlayerHealth_RespawnsAfterBeingKilledByBots()
    {
        playerHealth.TakeDamage(1000f, new Vector3(0f, 0f, 10f));
        Assert.That(playerHealth.IsDead, Is.True);
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, 0f));
        yield return new WaitForSeconds(0.5f);

        Assert.That(bot.Brain.State, Is.Not.EqualTo(BotState.Attack), "bots stop attacking a dead player");
        yield return new WaitForSeconds(3.2f);

        Assert.That(playerHealth.IsDead, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.GreaterThan(0f));
    }
}

/// <summary>Smoke test on the real maps: the manager adds player health, builds navigation and bots walk.</summary>
public class BotMapPlayModeTests
{
    [UnityTest]
    public IEnumerator Map_BotsSpawnNavigateAndMove([ValueSource(nameof(Maps))] string map)
    {
        SceneManager.LoadScene(map, LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(0.5f);

        BotManager manager = BotManager.Create();
        manager.AutoSpawnCount = 2;
        manager.KeysEnabled = false;
        float timeout = 15f;
        while (!manager.IsReady && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        try
        {
            Assert.That(manager.IsReady, Is.True, map + ": manager not ready");
            Assert.That(PlayerHealth.Local, Is.Not.Null, map + ": player should get PlayerHealth");
            Assert.That(BotNavigation.IsReady, Is.True, map + ": navigation not built");
            Assert.That(manager.Bots.Count, Is.EqualTo(2), map + ": bots not spawned");

            manager.Settings.viewDistance = 1f;
            manager.Settings.closeAwarenessRadius = 0f;
            manager.Settings.hearingScale = 0f;
            Vector3[] starts = new Vector3[manager.Bots.Count];
            for (int i = 0; i < starts.Length; i++)
                starts[i] = manager.Bots[i].transform.position;

            yield return new WaitForSeconds(6f);

            float moved = 0f;
            for (int i = 0; i < starts.Length; i++)
                moved = Mathf.Max(moved, Vector3.Distance(starts[i], manager.Bots[i].transform.position));
            Assert.That(moved, Is.GreaterThan(1f), map + ": no bot moved (navigation mode " + BotNavigation.Mode + ")");
        }
        finally
        {
            if (manager != null)
                Object.DestroyImmediate(manager.gameObject);
            BotNavigation.Reset();
        }
    }

    public static readonly string[] Maps = { "TestRange", "Shipment", "Dome" };
}
