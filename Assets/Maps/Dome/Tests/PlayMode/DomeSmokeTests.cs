using System.Collections;
using FpsMaps.Dome;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Loads the generated Dome scene (run Tools/Maps/Build Dome first) and checks the player, spawn points,
/// dome structure and the collision layers that wall-run / wall-bounce / slide / bhop rely on.
/// </summary>
public class DomeSmokeTests
{
    private GameObject player;
    private MovementController movement;
    private CharacterController characterController;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        var sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(DomeLayout.ScenePath);
        if (sceneAsset == null)
            Assert.Ignore("Dome scene not generated yet. Run Tools/Maps/Build Dome.");

        var loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            DomeLayout.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        while (!loading.isDone)
            yield return null;
#else
        SceneManager.LoadScene(DomeLayout.SceneName, LoadSceneMode.Single);
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
        Assert.IsNotNull(player, "Player should exist in Dome");
        Assert.IsNotNull(movement, "Player should have MovementController");
        Assert.IsNotNull(characterController, "Player should have CharacterController");
        Assert.IsNotNull(player.GetComponent<InputHandler>(), "Player should have InputHandler");
        Assert.IsNotNull(Camera.main, "A main camera should exist");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Player_SettlesOnFloor()
    {
        Assert.IsNotNull(characterController, "CharacterController should exist");
        for (int i = 0; i < 45; i++)
        {
            if (!characterController.isGrounded)
                characterController.Move(Vector3.down * 0.2f);
            yield return null;
        }

        Assert.Less(player.transform.position.y, 1.5f, "Player should settle on the floor, not float");
        Assert.Greater(player.transform.position.y, -0.5f, "Player should not fall through the floor");
    }

    [UnityTest]
    public IEnumerator Scene_HasSpawnPointsAroundTheMap()
    {
        GameObject[] spawns = GameObject.FindGameObjectsWithTag("Respawn");
        Assert.GreaterOrEqual(spawns.Length, 8, "Dome should have spawn points spread around the map");

        float farthest = 0f;
        foreach (GameObject spawn in spawns)
            farthest = Mathf.Max(farthest, new Vector2(spawn.transform.position.x, spawn.transform.position.z).magnitude);
        Assert.Greater(farthest, 25f, "Some spawns should be in the outer yards");
        yield return null;
    }

    [UnityTest]
    public IEnumerator WorldColliders_AreOnWallLayers()
    {
        Assert.IsNotNull(movement, "Player should have MovementController");
        Assert.IsNotNull(movement.Config, "MovementController should have a config");

        int wallMask = movement.Config.wallLayers.value;
        int checkedColliders = 0;
        foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (collider.transform.IsChildOf(player.transform))
                continue;

            checkedColliders++;
            Assert.AreNotEqual(0, wallMask & (1 << collider.gameObject.layer),
                collider.name + " is on layer " + LayerMask.LayerToName(collider.gameObject.layer) +
                " which is not in MovementConfig.wallLayers");
        }

        Assert.Greater(checkedColliders, 60, "Dome should be full of colliders");
        yield return null;
    }

    [UnityTest]
    public IEnumerator DomeStructure_HasShellGlassAndTrim()
    {
        var shell = GameObject.Find("DomeShell");
        Assert.IsNotNull(shell, "Dome collision shell should exist");
        Assert.IsNotNull(shell.GetComponent<MeshCollider>(), "Dome shell should have a MeshCollider");

        var glass = GameObject.Find("DomeGlass");
        Assert.IsNotNull(glass, "Energy glass should exist");
        Assert.IsNull(glass.GetComponent<Collider>(), "Glass is visual only");
        Assert.IsNotNull(GameObject.Find("DomeStruts"), "Hex trim should exist");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Scene_HasLightingAndTargets()
    {
        Assert.IsNotNull(RenderSettings.skybox, "Skybox should be assigned");
        Assert.IsTrue(RenderSettings.fog, "Fog should be enabled");

        int lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
        Assert.GreaterOrEqual(lights, 6, "Dome should be lit by directional and coloured point lights");

        int targets = Object.FindObjectsByType<Target>(FindObjectsSortMode.None).Length;
        Assert.GreaterOrEqual(targets, 6, "Dome should have practice targets");
        yield return null;
    }

    [UnityTest]
    public IEnumerator CatwalkTop_IsReachableByRaycastFromAbove()
    {
        Vector3 onCatwalk = DomeLayout.Direction(60f) * DomeLayout.CatwalkRadius;
        Vector3 origin = new Vector3(onCatwalk.x, DomeLayout.CatwalkTopY + 4f, onCatwalk.z);
        Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f), "Ray should hit the catwalk");
        Assert.AreEqual(DomeLayout.CatwalkTopY, hit.point.y, 0.05f);
        yield return null;
    }
}
