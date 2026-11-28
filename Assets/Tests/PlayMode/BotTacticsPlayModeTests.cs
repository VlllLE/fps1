using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Shoot-hide-peek, wall-hugging holds and the difficulty-scaled reaction on a flat arena with one wall, using real bots and
/// the runtime NavMesh.
/// </summary>
public class BotTacticsPlayModeTests
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
            Scene arena = SceneManager.CreateScene("BotTacticsArena");
            SceneManager.SetActiveScene(arena);
            yield return SceneManager.UnloadSceneAsync(leftover);
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "BotTacticsGround";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(120f, 1f, 120f);
        created.Add(ground);

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "BotTacticsWall";
        wall.transform.position = new Vector3(0f, 1.5f, 6f);
        wall.transform.localScale = new Vector3(8f, 3f, 1f);
        created.Add(wall);

        playerObject = new GameObject("BotTacticsPlayer");
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
        Assume.That(BotNavigation.Mode, Is.EqualTo(BotNavMode.NavMesh), "the tactics need the runtime NavMesh");
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
        return BotController.Create("TacticsBot", position, Quaternion.LookRotation(toPlayer), manager.Settings, weapon, Color.red, 77);
    }

    [UnityTest]
    public IEnumerator Bot_DucksBehindTheWall_AndPeeksBackOut()
    {
        BotSettings settings = manager.Settings;
        settings.usePeek = true;
        settings.peekExposeMin = 0.6f;
        settings.peekExposeMax = 0.9f;
        settings.peekHideMin = 0.6f;
        settings.peekHideMax = 0.8f;
        settings.damageMultiplier = 0f;
        settings.useCover = true;
        settings.reactionTime = 0.1f;

        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, 2f));
        Time.timeScale = 2f;

        bool sawPeekCover = false;
        bool hiddenAtCover = false;
        bool cameBack = false;
        Vector3 playerEye = playerObject.transform.position + Vector3.up * 1.6f;
        for (float t = 0f; t < 40f && !cameBack; t += Time.unscaledDeltaTime)
        {
            if (bot.Brain.State == BotState.TakeCover && bot.Brain.CoverIsPeek)
            {
                sawPeekCover = true;
                float planar = Vector2.Distance(
                    new Vector2(bot.transform.position.x, bot.transform.position.z),
                    new Vector2(bot.CoverPoint.x, bot.CoverPoint.z));
                if (bot.HasCoverPoint && planar < 1.2f
                    && !BotPerception.HasLineOfSight(playerEye, bot.AimPoint, null, bot.transform))
                    hiddenAtCover = true;
            }
            else if (sawPeekCover && (bot.Brain.State == BotState.Attack || bot.Brain.State == BotState.Chase))
            {
                cameBack = true;
            }

            yield return null;
        }

        Assert.That(bot.Peeks, Is.GreaterThanOrEqualTo(1), "an exposed bot should duck out of sight");
        Assert.That(sawPeekCover, Is.True);
        Assert.That(hiddenAtCover, Is.True, "the cover spot hides the bot from the enemy");
        Assert.That(cameBack, Is.True, "after hiding the bot goes back to the fight");
    }

    [UnityTest]
    public IEnumerator EasyBot_NeverPeeks()
    {
        BotSettings settings = manager.Settings;
        settings.ApplyPreset(BotDifficulty.Easy);
        settings.damageMultiplier = 0f;
        settings.peekExposeMin = 0.3f;
        settings.peekExposeMax = 0.4f;

        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, 2f));
        Time.timeScale = 2f;
        for (float t = 0f; t < 10f; t += Time.unscaledDeltaTime)
            yield return null;

        Assert.That(bot.Peeks, Is.EqualTo(0));
        Assert.That(bot.Brain.CoverIsPeek, Is.False);
    }

    [UnityTest]
    public IEnumerator HoldingBot_StandsAgainstANearbyWall()
    {
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, -4f));
        manager.Settings.viewDistance = 1f;
        manager.Settings.closeAwarenessRadius = 0f;
        manager.Settings.hearingScale = 0f;

        Vector3 hold = new Vector3(1f, 0f, 3.4f);
        bot.SetObjective(hold, 1.5f, 1f, 0f, true);
        Time.timeScale = 3f;

        for (float t = 0f; t < 30f && !bot.HasHoldPoint; t += Time.unscaledDeltaTime)
            yield return null;

        Assert.That(bot.HasHoldPoint, Is.True, "a wall point should be found next to the objective");
        Assert.That(Vector3.Distance(bot.HoldPoint, hold), Is.LessThanOrEqualTo(4.1f));
        Assert.That(NavMesh.FindClosestEdge(bot.HoldPoint, out NavMeshHit edge, NavMesh.AllAreas), Is.True);
        Assert.That(edge.distance, Is.LessThanOrEqualTo(1.2f), "the hold spot is close to the wall");

        for (float t = 0f; t < 30f; t += Time.unscaledDeltaTime)
        {
            Vector2 a = new Vector2(bot.transform.position.x, bot.transform.position.z);
            Vector2 b = new Vector2(bot.HoldPoint.x, bot.HoldPoint.z);
            if (Vector2.Distance(a, b) < 1.2f)
                break;
            yield return null;
        }

        Assert.That(Vector2.Distance(new Vector2(bot.transform.position.x, bot.transform.position.z),
            new Vector2(bot.HoldPoint.x, bot.HoldPoint.z)), Is.LessThan(1.2f), "the bot walks to its hold spot");
    }

    [UnityTest]
    public IEnumerator HoldingBot_WithoutWallPreference_StaysOnTheObjective()
    {
        manager.Settings.preferWalls = false;
        manager.Settings.viewDistance = 1f;
        manager.Settings.closeAwarenessRadius = 0f;
        manager.Settings.hearingScale = 0f;
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, -4f));

        bot.SetObjective(new Vector3(1f, 0f, 3.4f), 1.5f, 1f, 0f, true);
        Time.timeScale = 3f;
        for (float t = 0f; t < 12f; t += Time.unscaledDeltaTime)
            yield return null;

        Assert.That(bot.HasHoldPoint, Is.False);
        Assert.That(bot.AtObjective, Is.True);
    }

    [UnityTest]
    public IEnumerator Objective_WithoutHugWalls_NeverUsesAHoldPoint()
    {
        manager.Settings.viewDistance = 1f;
        manager.Settings.closeAwarenessRadius = 0f;
        manager.Settings.hearingScale = 0f;
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, -4f));

        bot.SetObjective(new Vector3(1f, 0f, 3.4f), 1.5f, 1f, 0f);
        Time.timeScale = 3f;
        for (float t = 0f; t < 12f; t += Time.unscaledDeltaTime)
            yield return null;

        Assert.That(bot.HasHoldPoint, Is.False);
        Assert.That(bot.AtObjective, Is.True);
    }

    [UnityTest]
    public IEnumerator GettingHit_FlinchesTheAim()
    {
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, -4f));
        yield return null;

        float calm = bot.Aim.ComputeInaccuracy(0f, 0f);
        bot.TakeDamage(1f, Vector3.forward, false);

        Assert.That(bot.Aim.Flinch, Is.EqualTo(1f));
        Assert.That(bot.Aim.ComputeInaccuracy(0f, 0f), Is.GreaterThan(calm));

        bot.Respawn(new Vector3(0f, 0f, -4f));
        Assert.That(bot.Aim.Flinch, Is.EqualTo(0f), "a fresh life starts calm");
    }

    [UnityTest]
    public IEnumerator NewEngagement_RollsAReactionScaleWithinTheJitter()
    {
        manager.Settings.reactionJitter = 0.5f;
        BotController bot = SpawnBotFacingPlayer(new Vector3(0f, 0f, -8f));
        Time.timeScale = 2f;

        for (float t = 0f; t < 10f && bot.Brain.State != BotState.Attack; t += Time.unscaledDeltaTime)
            yield return null;

        Assert.That(bot.Brain.State, Is.EqualTo(BotState.Attack));
        Assert.That(bot.Brain.ReactionScale, Is.InRange(1f, 1.5f));
    }
}
