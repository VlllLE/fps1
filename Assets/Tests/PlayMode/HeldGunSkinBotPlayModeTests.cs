using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Bots carry the third-person gun of a catalog weapon in that weapon's skin, with the scope glass untouched.</summary>
public class HeldGunSkinBotPlayModeTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private BotManager manager;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Scene leftover = SceneManager.GetActiveScene();
        if (System.Array.IndexOf(BotMapPlayModeTests.Maps, leftover.name) >= 0)
        {
            Scene arena = SceneManager.CreateScene("HeldGunSkinBotArena");
            SceneManager.SetActiveScene(arena);
            yield return SceneManager.UnloadSceneAsync(leftover);
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "BotArenaGround";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(120f, 1f, 120f);
        created.Add(ground);

        var player = new GameObject("BotTestPlayer");
        player.transform.position = new Vector3(0f, 0.05f, -20f);
        var controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 1f, 0f);
        player.AddComponent<PlayerHealth>();
        created.Add(player);

        manager = BotManager.Create();
        manager.AutoSpawnCount = 0;
        manager.KeysEnabled = false;
        float timeout = 10f;
        while (!manager.IsReady && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        Assert.That(manager.IsReady, Is.True);
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
        BotNavigation.Reset();
    }

    [UnityTest]
    public IEnumerator BotsHeldGuns_WearTheirWeaponSkin_AndKeepGlassLenses()
    {
        var bots = new List<BotController>();
        for (int i = 0; i < 8; i++)
            bots.Add(manager.SpawnBot(new Vector3(-20f + i * 6f, 0f, 20f)));
        yield return null;
        yield return null;

        int skinned = 0;
        foreach (BotController bot in bots)
        {
            ThirdPersonGun gun = bot.Body.Gun;
            Assert.IsNotNull(gun, bot.name);
            GameObject prefab = BotManager.GetWeaponPrefab(bot.Gun.Config);
            if (prefab == null || !gun.UsesImportedModel)
                continue;

            WeaponSkinDefinition expected = prefab.GetComponentInChildren<WeaponSkinApplier>(true).DefaultSkin;
            Assert.IsNotNull(expected, prefab.name + " has a default skin");
            Assert.AreSame(expected, gun.AppliedSkin, prefab.name + " bot gun skin");
            HeldGunSkinPlayModeTests.AssertOuterPartsWearSkin(gun, expected, prefab.name);
            HeldGunSkinPlayModeTests.AssertScopeLensIsGlass(gun, prefab.name);
            skinned++;
        }

        Assert.Greater(skinned, 0, "at least one bot carries a catalog weapon with an imported model");
    }
}
