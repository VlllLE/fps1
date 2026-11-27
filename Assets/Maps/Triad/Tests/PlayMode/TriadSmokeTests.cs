using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FpsMaps.Triad;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Loads the generated Triad scene (run Tools/Maps/Build Triad first): player, spawns, lighting, collision layers,
/// runtime NavMesh reachability across lanes and buildings, and bots walking every lane and building end to end.
/// </summary>
public class TriadSmokeTests
{
    private GameObject player;
    private MovementController movement;
    private TriadLayoutData layout;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        layout = TriadLayout.Create();
#if UNITY_EDITOR
        var sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(TriadLayout.ScenePath);
        if (sceneAsset == null)
            Assert.Ignore("Triad scene not generated yet. Run Tools/Maps/Build Triad.");

        var loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            TriadLayout.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        while (!loading.isDone)
            yield return null;
#else
        SceneManager.LoadScene(TriadLayout.SceneName, LoadSceneMode.Single);
        yield return null;
#endif
        yield return new WaitForSeconds(0.5f);

        player = GameObject.Find("Player");
        if (player != null)
            movement = player.GetComponent<MovementController>();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (BotManager.Instance != null)
            Object.DestroyImmediate(BotManager.Instance.gameObject);
        BotNavigation.Reset();
    }

    private IEnumerator BuildNavigation()
    {
        yield return BotNavigation.BuildRoutine(new List<Transform> { player.transform });
        Assert.IsTrue(BotNavigation.IsReady, "Navigation should be ready");
    }

    private static bool Reaches(Vector3 from, Vector3 to, List<Vector3> corners, out float endGap)
    {
        endGap = float.MaxValue;
        if (!BotNavigation.TryCalculatePath(from, to, corners, out bool complete) || !complete || corners.Count == 0)
            return false;
        endGap = Vector3.Distance(corners[corners.Count - 1], to);
        return endGap < 1.0f;
    }

    [UnityTest]
    public IEnumerator Player_ExistsWithMovementStack_AndSettlesOnTheFloor()
    {
        Assert.IsNotNull(player, "Player should exist in Triad");
        Assert.IsNotNull(movement, "Player should have MovementController");
        var cc = player.GetComponent<CharacterController>();
        Assert.IsNotNull(cc);
        Assert.IsNotNull(player.GetComponent<InputHandler>());
        Assert.IsNotNull(Camera.main, "A main camera should exist");

        for (int i = 0; i < 45; i++)
        {
            if (!cc.isGrounded)
                cc.Move(Vector3.down * 0.2f);
            yield return null;
        }

        Assert.Less(player.transform.position.y, 1f, "Player should settle on the street");
        Assert.Greater(player.transform.position.y, -0.5f, "Player should not fall through the floor");
        Assert.AreEqual(layout.Spawns[0].Position.z, player.transform.position.z, 1.5f, "Player starts at PlayerSpawn");
    }

    [UnityTest]
    public IEnumerator Spawns_AreTaggedValidAndNotInsideGeometry()
    {
        GameObject[] spawns = GameObject.FindGameObjectsWithTag("Respawn");
        Assert.AreEqual(layout.Spawns.Count, spawns.Length, "Scene spawns should match the layout");
        Assert.IsNotNull(GameObject.Find("PlayerSpawn"));

        int teamA = 0;
        int teamB = 0;
        foreach (GameObject spawn in spawns)
        {
            Vector3 p = spawn.transform.position;
            if (p.z < 0f)
                teamA++;
            else
                teamB++;

            bool inside = Physics.OverlapCapsule(p + Vector3.up * 0.55f, p + Vector3.up * 1.45f, 0.4f, ~0, QueryTriggerInteraction.Ignore).Any(c => !c.transform.IsChildOf(player.transform));
            Assert.IsFalse(inside, spawn.name + " is inside geometry");
            Assert.IsTrue(Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore),
                spawn.name + " has no ground");
            Assert.AreEqual(p.y, hit.point.y, 0.3f, spawn.name + " floats above the floor");
        }

        Assert.GreaterOrEqual(teamA, 9);
        Assert.GreaterOrEqual(teamB, 9);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Lighting_IsDuskDaylight_NotNeon()
    {
        Assert.IsNotNull(RenderSettings.skybox, "Skybox missing");
        Assert.AreEqual("Skybox/Procedural", RenderSettings.skybox.shader.name);
        Assert.IsNotNull(RenderSettings.sun, "Sun missing");
        Assert.AreEqual(LightType.Directional, RenderSettings.sun.type);
        float elevation = Mathf.Asin(-RenderSettings.sun.transform.forward.y) * Mathf.Rad2Deg;
        Assert.That(elevation, Is.InRange(5f, 30f), "Dusk sun should sit low on the horizon");
        Assert.Greater(RenderSettings.sun.color.r, RenderSettings.sun.color.b, "Sun should be warm");
        Assert.IsTrue(RenderSettings.fog);
        Assert.AreEqual(UnityEngine.Rendering.AmbientMode.Trilight, RenderSettings.ambientMode);
        yield return null;
    }

    [UnityTest]
    public IEnumerator WorldColliders_AreOnWallLayers_AndSkylineIsDecor()
    {
        Assert.IsNotNull(movement.Config, "MovementController should have a config");
        int wallMask = movement.Config.wallLayers.value;
        int checkedColliders = 0;
        foreach (var collider in Object.FindObjectsByType<Collider>())
        {
            if (collider.transform.IsChildOf(player.transform))
                continue;

            checkedColliders++;
            Assert.AreNotEqual(0, wallMask & (1 << collider.gameObject.layer),
                collider.name + " is on layer " + LayerMask.LayerToName(collider.gameObject.layer));
        }

        Assert.Greater(checkedColliders, 800, "Triad should be full of colliders");
        foreach (BlockSpec b in layout.Blocks.Where(x => x.Surface == TriadSurface.Skyline).Take(10))
            Assert.IsNull(GameObject.Find(b.Name).GetComponent<Collider>(), b.Name + " is visual only");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Navigation_ConnectsBothSpawnSides_AndEveryWaypointOnEveryTier()
    {
        yield return BuildNavigation();

        var corners = new List<Vector3>();
        Vector3 start = layout.Spawns[0].Position;
        foreach (SpawnSpec spawn in layout.Spawns.Where(s => s.Team == 1))
            Assert.IsTrue(Reaches(start, spawn.Position, corners, out float gap), "No path from PlayerSpawn to " + spawn.Name + " (gap " + gap + ")");

        foreach (SpawnSpec spawn in layout.Spawns.Where(s => s.Team == 1))
            Assert.IsTrue(Reaches(spawn.Position, layout.Spawns[1].Position, corners, out float gap), "No path from " + spawn.Name + " back to team A (gap " + gap + ")");

        var unreachable = new List<string>();
        foreach (WaypointSpec w in layout.Waypoints)
        {
            if (!Reaches(start, w.Position, corners, out _))
                unreachable.Add(w.Name + "@" + w.Position);
        }

        Assert.IsEmpty(unreachable, "Unreachable by bots from PlayerSpawn: " + string.Join(", ", unreachable));
    }

    [UnityTest]
    public IEnumerator Navigation_RunsThroughEachLaneAndBuilding()
    {
        yield return BuildNavigation();

        var corners = new List<Vector3>();
        foreach (RouteSpec route in layout.Routes)
        {
            Vector3 from = route.Start;
            foreach (Vector3 point in route.Points)
            {
                Assert.IsTrue(Reaches(from, point, corners, out float gap), route.Name + ": no path " + from + " -> " + point + " (gap " + gap + ")");
                from = point;
            }
        }
    }

    public static readonly string[] RouteNames = { "LaneWest", "LaneMiddle", "LaneEast", "Warehouse", "Office", "Depot" };

    [UnityTest]
    public IEnumerator Bots_PlayThroughEveryLaneAndBuilding([ValueSource(nameof(RouteNames))] string routeName)
    {
        RouteSpec route = layout.Routes.First(r => r.Name == routeName);

        var sceneMarkers = GameObject.Find("Waypoints");
        if (sceneMarkers != null)
            sceneMarkers.SetActive(false);

        var markerRoot = new GameObject("RouteMarkers");
        foreach (Vector3 point in route.Points)
        {
            var marker = new GameObject("Marker");
            marker.transform.SetParent(markerRoot.transform, false);
            marker.transform.position = point;
            marker.AddComponent<BotWaypoint>();
        }

        BotManager manager = BotManager.Create();
        manager.AutoSpawnCount = 0;
        manager.KeysEnabled = false;
        float wait = 15f;
        while (!manager.IsReady && wait > 0f)
        {
            wait -= Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(manager.IsReady, "Bot manager not ready");
        manager.Settings.viewDistance = 1f;
        manager.Settings.closeAwarenessRadius = 0f;
        manager.Settings.hearingScale = 0f;
        manager.Settings.patrolPauseMin = 0f;
        manager.Settings.patrolPauseMax = 0f;
        manager.Settings.patrolWaypointCount = Mathf.Max(2, route.Points.Count);
        manager.Settings.patrolRadius = 60f;

        BotController bot = manager.SpawnBot(route.Start);
        Assert.IsNotNull(bot);

        var visited = new bool[route.Points.Count];
        Time.timeScale = 3f;
        float simTime = 0f;
        while (simTime < 240f && visited.Any(v => !v))
        {
            simTime += Time.deltaTime;
            Vector3 p = bot.transform.position;
            for (int i = 0; i < route.Points.Count; i++)
            {
                Vector3 target = route.Points[i];
                float planar = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(target.x, target.z));
                if (planar < 1.6f && Mathf.Abs(p.y - target.y) < 1.5f)
                    visited[i] = true;
            }

            yield return null;
        }

        Time.timeScale = 1f;
        var missed = new List<string>();
        for (int i = 0; i < visited.Length; i++)
        {
            if (!visited[i])
                missed.Add(route.Points[i].ToString());
        }

        Assert.IsEmpty(missed, routeName + ": bot never reached " + string.Join(", ", missed) + " (bot ended at " + bot.transform.position
            + ", stuck recoveries " + bot.StuckRecoveries + ")");
    }
}
