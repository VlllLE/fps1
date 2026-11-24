using System.Collections;
using CodMovementShooter.Maps.Shipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Loads the generated Shipment scene (run Tools/Maps/Build Shipment first) and checks the player,
/// spawn points and collision layers that wall-run / wall-bounce / slide / bhop rely on.
/// </summary>
public class ShipmentSmokeTests
{
    private GameObject player;
    private MovementController movement;
    private CharacterController characterController;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        var sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(ShipmentMapInfo.ScenePath);
        if (sceneAsset == null)
            Assert.Ignore("Shipment scene not generated yet. Run Tools/Maps/Build Shipment.");

        var loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            ShipmentMapInfo.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        while (!loading.isDone)
            yield return null;
#else
        SceneManager.LoadScene(ShipmentMapInfo.SceneName, LoadSceneMode.Single);
        yield return null;
#endif
        yield return new WaitForSeconds(0.5f);

        player = GameObject.Find("Player");
        if (player != null)
        {
            movement = player.GetComponent<MovementController>();
            characterController = player.GetComponent<CharacterController>();
        }
    }

    [UnityTest]
    public IEnumerator Player_ExistsWithMovementStack()
    {
        Assert.IsNotNull(player, "Player should exist in Shipment");
        Assert.IsNotNull(movement, "Player should have MovementController");
        Assert.IsNotNull(characterController, "Player should have CharacterController");
        Assert.IsNotNull(player.GetComponent<InputHandler>(), "Player should have InputHandler");
        Assert.IsNotNull(Camera.main, "A main camera should exist");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Player_SettlesOnFloor()
    {
        Assert.IsNotNull(player, "Player should exist");
        for (int i = 0; i < 45; i++)
        {
            if (!characterController.isGrounded)
                characterController.Move(Vector3.down * 0.2f);
            yield return null;
        }

        Assert.Less(player.transform.position.y, 1.5f, "Player should settle on the floor, not float or fall");
        Assert.Greater(player.transform.position.y, -0.5f, "Player should not fall through the floor");
    }

    [UnityTest]
    public IEnumerator Scene_HasSpawnPointsForBothTeams()
    {
        var spawns = Object.FindObjectsByType<ShipmentSpawnPoint>(FindObjectsSortMode.None);
        Assert.GreaterOrEqual(spawns.Length, 6);

        int teamA = 0;
        int teamB = 0;
        foreach (var spawn in spawns)
        {
            if (spawn.Team == SpawnTeam.TeamA) teamA++;
            if (spawn.Team == SpawnTeam.TeamB) teamB++;
        }

        Assert.GreaterOrEqual(teamA, 2);
        Assert.GreaterOrEqual(teamB, 2);
        yield return null;
    }

    [UnityTest]
    public IEnumerator WorldColliders_AreOnWallLayers()
    {
        Assert.IsNotNull(movement, "Player should have MovementController");
        Assert.IsNotNull(movement.Config, "MovementController should have a config");

        int wallMask = movement.Config.wallLayers.value;
        int checkedColliders = 0;
        foreach (var collider in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
        {
            if (collider.transform.IsChildOf(player.transform))
                continue;

            checkedColliders++;
            Assert.AreNotEqual(0, wallMask & (1 << collider.gameObject.layer),
                collider.name + " is on layer " + LayerMask.LayerToName(collider.gameObject.layer) +
                " which is not in MovementConfig.wallLayers");
        }

        Assert.Greater(checkedColliders, 40, "Shipment should be full of box colliders");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ContainerTop_IsReachableByRaycastFromAbove()
    {
        var container = GameObject.Find("Container_A2");
        Assert.IsNotNull(container, "Central container should exist");

        Vector3 origin = container.transform.position + Vector3.up * 8f;
        Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out var hit, 20f), "Ray should hit the container roof");
        Assert.AreEqual(ShipmentLayout.ContainerHeight, hit.point.y, 0.05f);
        yield return null;
    }
}
