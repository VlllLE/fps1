using System;
using System.Collections;
using System.Collections.Generic;
using FpsMaps.Bazaar;
using FpsTactical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The bot team play on Bazaar: roles, flank routes, a carrier that trails its entries, defenders rotating to contact, the
/// retake (defuser, cover, safe and hopeless defuses) and the post-plant guard. The match runs on a manual clock.
/// </summary>
public class TacticalTeamPlayTests
{
    private PlayerHealth player;
    private CharacterController controller;
    private TacticalMatch match;

    private IEnumerator Boot(Action<TacticalRules> tweak = null, float teamwork = 1f)
    {
#if UNITY_EDITOR
        var sceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(BazaarLayout.ScenePath);
        if (sceneAsset == null)
            Assert.Ignore("Bazaar scene not generated yet. Run Tools/Maps/Build Bazaar.");

        var loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            BazaarLayout.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        while (!loading.isDone)
            yield return null;
#else
        SceneManager.LoadScene(BazaarLayout.SceneName, LoadSceneMode.Single);
        yield return null;
#endif
        yield return null;

        BotManager manager = BotManager.Create();
        manager.AutoSpawnCount = 0;
        manager.KeysEnabled = false;
        float wait = 20f;
        while ((!manager.IsReady || !BotNavigation.IsReady) && wait > 0f)
        {
            wait -= Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(manager.IsReady, "bot manager should be ready");
        manager.Settings.teamwork = teamwork;

        var rules = new TacticalRules { roundSeconds = 900f };
        tweak?.Invoke(rules);
        match = TacticalMatch.Create(rules);
        match.AutoStart = false;
        match.AutoRestart = false;
        match.ManualClock = true;
        match.ActionInput = () => false;
        match.Seed(7);
        Assert.IsTrue(match.Begin(), "Bazaar should provide plant zones and spawns");
        yield return null;

        player = match.Player;
        controller = player.GetComponent<CharacterController>();
        Assume.That(BotNavigation.Mode, Is.EqualTo(BotNavMode.NavMesh), "team routes need the runtime NavMesh");
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        CombatTeams.FriendlyFire = false;
        if (player != null && player.IsDead)
            player.Respawn();
        if (match != null)
            UnityEngine.Object.DestroyImmediate(match.gameObject);
        if (BotManager.Instance != null)
            UnityEngine.Object.DestroyImmediate(BotManager.Instance.gameObject);
        BotNavigation.Reset();
        match = null;
    }

    private TacticalBotDirector Director => match.Director;

    private void GoLive()
    {
        match.Model.SkipFreeze();
        match.Step(0.01f);
        Assert.AreEqual(TacticalPhase.Live, match.Model.Phase);
    }

    private void Run(float seconds, float dt = 0.1f)
    {
        for (float t = 0f; t < seconds; t += dt)
            match.Step(dt);
    }

    private List<BotController> Alive(CombatTeam side)
    {
        var list = new List<BotController>();
        match.CollectAliveBots(side, list);
        return list;
    }

    private void SendDefendersHome()
    {
        IReadOnlyList<Transform> spawns = match.SpawnsOf(CombatTeam.Defenders);
        List<BotController> defenders = Alive(CombatTeam.Defenders);
        for (int i = 0; i < defenders.Count; i++)
            defenders[i].transform.position = spawns[i % spawns.Count].position;
    }

    private Vector3 SiteCentre(ITacticalZone zone)
    {
        Bounds bounds = zone.ZoneBounds;
        return new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
    }

    private static float Planar(Vector3 a, Vector3 b)
    {
        return TacticalObjectives.Planar(a, b);
    }

    [UnityTest]
    public IEnumerator RoundStart_GivesEveryBotARole()
    {
        yield return Boot();
        GoLive();
        Run(0.6f);

        List<BotController> attackers = Alive(CombatTeam.Attackers);
        List<BotController> defenders = Alive(CombatTeam.Defenders);
        Assert.AreEqual(3, attackers.Count, "the player is the fourth attacker");
        Assert.AreEqual(4, defenders.Count);

        int entries = 0;
        foreach (BotController bot in attackers)
        {
            TacticalBotRole role = Director.RoleOf(bot);
            Assert.AreNotEqual(TacticalBotRole.None, role, bot.name + " should have a job");
            if (role == TacticalBotRole.Entry)
                entries++;
        }

        Assert.GreaterOrEqual(entries, 1, "somebody has to push the site");

        var perZone = new int[match.PlantZones.Count];
        foreach (BotController bot in defenders)
        {
            Assert.AreEqual(TacticalBotRole.Anchor, Director.RoleOf(bot));
            Assert.GreaterOrEqual(Director.ZoneOf(bot), 0);
            perZone[Director.ZoneOf(bot)]++;
        }

        foreach (int count in perZone)
            Assert.GreaterOrEqual(count, 1, "every site has a defender");
    }

    [UnityTest]
    public IEnumerator CarryingBot_HasTheCarrierRole()
    {
        yield return Boot(r => r.playerAttacksFirst = false);
        GoLive();
        Run(0.6f);

        Assert.IsNotNull(match.CarrierBot);
        Assert.AreEqual(TacticalBotRole.Carrier, Director.RoleOf(match.CarrierBot));
    }

    [UnityTest]
    public IEnumerator SkilledAttackers_SendAFlankerAlongAnAlternativeRoute()
    {
        yield return Boot(r => r.playerAttacksFirst = false);
        GoLive();
        Run(0.6f);

        Assert.GreaterOrEqual(Director.FlankerCount, 1, "with full teamwork a flanker takes another way");
        Vector3 centre = SiteCentre(match.TargetSite);
        BotController flanker = null;
        foreach (BotController bot in Alive(CombatTeam.Attackers))
        {
            if (Director.RoleOf(bot) == TacticalBotRole.Flanker)
                flanker = bot;
        }

        Assert.IsNotNull(flanker);
        Assert.IsTrue(Director.TryGetRouteVia(flanker, out Vector3 via));
        Assert.GreaterOrEqual(Planar(via, centre), TacticalRoutePlanner.MinFromEnds, "the waypoint is not on top of the site");
        Assert.IsTrue(flanker.HasObjective);
        Assert.Less(Planar(flanker.ObjectivePosition, via), 3f, "first the flanker heads for the waypoint");

        flanker.transform.position = via;
        Run(1f);

        Assert.Greater(Planar(flanker.ObjectivePosition, via), 2f, "having reached the waypoint, the flanker moves on");
        Assert.LessOrEqual(Planar(flanker.ObjectivePosition, centre), 8f, "and ends up at the site, on the waypoint's side");
    }

    [UnityTest]
    public IEnumerator Attackers_WithoutTeamwork_NeverFlank()
    {
        yield return Boot(r => r.playerAttacksFirst = false, 0f);
        GoLive();
        Run(0.6f);

        Assert.AreEqual(0, Director.FlankerCount);
        foreach (BotController bot in Alive(CombatTeam.Attackers))
            Assert.AreNotEqual(TacticalBotRole.Flanker, Director.RoleOf(bot));
    }

    [UnityTest]
    public IEnumerator Carrier_TrailsItsEntriesBeforeGoingIn()
    {
        yield return Boot(r => r.playerAttacksFirst = false);
        GoLive();
        Run(0.6f);

        BotController carrier = match.CarrierBot;
        Assert.IsNotNull(carrier);
        Vector3 centre = SiteCentre(match.TargetSite);
        Assert.GreaterOrEqual(Planar(carrier.ObjectivePosition, centre), 7f, "the carrier waits short of the site at first");

        Run(15f);
        Assert.LessOrEqual(Planar(carrier.ObjectivePosition, centre), 3f, "when the entries are in (or time is up) the carrier goes to the site");
    }

    [UnityTest]
    public IEnumerator Defenders_RotateToASiteWhereAMateMadeContact()
    {
        yield return Boot();
        GoLive();
        Run(0.6f);

        int contested = 0;
        Director.ReportContact(match.PlantZones[contested].ZoneBounds.center);
        Run(0.6f);

        Assert.AreEqual(1, Director.Rotations);
        int rotators = 0;
        foreach (BotController bot in Alive(CombatTeam.Defenders))
        {
            if (Director.RoleOf(bot) == TacticalBotRole.Rotator)
            {
                rotators++;
                Assert.AreEqual(contested, Director.ZoneOf(bot));
            }
        }

        Assert.AreEqual(1, rotators);

        Director.ReportContact(match.PlantZones[1].ZoneBounds.center);
        Run(0.6f);
        Assert.AreEqual(1, Director.Rotations, "the defence does not flip-flop while the first contact is fresh");
    }

    [UnityTest]
    public IEnumerator Defenders_WithoutTeamwork_StayAtTheirSites()
    {
        yield return Boot(null, 0f);
        GoLive();
        Run(0.6f);

        Director.ReportContact(match.PlantZones[0].ZoneBounds.center);
        Run(1f);

        Assert.AreEqual(0, Director.Rotations);
        foreach (BotController bot in Alive(CombatTeam.Defenders))
            Assert.AreEqual(TacticalBotRole.Anchor, Director.RoleOf(bot));
    }

    [UnityTest]
    public IEnumerator PlantedBomb_SendsOneDefenderToDefuse_AndTheOthersCover()
    {
        yield return Boot();
        GoLive();
        Run(0.6f);

        SendDefendersHome();
        ITacticalZone site = match.PlantZones[0];
        match.PlantNow(site.ZoneSite, SiteCentre(site));
        Run(1f);

        BotController defuser = Director.Defuser;
        Assert.IsNotNull(defuser);
        Assert.AreEqual(CombatTeam.Defenders, defuser.Team);
        Assert.AreEqual(TacticalBotRole.Defuser, Director.RoleOf(defuser));
        Assert.IsFalse(Director.DefuseHopeless);

        int cover = 0;
        foreach (BotController bot in Alive(CombatTeam.Defenders))
        {
            if (bot != defuser && Director.RoleOf(bot) == TacticalBotRole.Cover)
                cover++;
        }

        Assert.AreEqual(3, cover);
        foreach (BotController bot in Alive(CombatTeam.Attackers))
            Assert.AreEqual(TacticalBotRole.PostPlant, Director.RoleOf(bot));
    }

    [UnityTest]
    public IEnumerator Retake_SendsCoverBotsAroundBySideRoutes()
    {
        yield return Boot();
        GoLive();
        Run(0.6f);

        SendDefendersHome();
        ITacticalZone site = match.PlantZones[0];
        match.PlantNow(site.ZoneSite, SiteCentre(site));
        Run(1f);

        int routed = 0;
        foreach (BotController bot in Alive(CombatTeam.Defenders))
        {
            if (bot != Director.Defuser && Director.TryGetRouteVia(bot, out Vector3 via))
            {
                routed++;
                Assert.GreaterOrEqual(Planar(via, match.BombPosition), TacticalRoutePlanner.MinFromEnds);
            }
        }

        Assert.GreaterOrEqual(routed, 1, "at least one covering defender comes from the side");
    }

    [UnityTest]
    public IEnumerator Defuser_WaitsWhileAttackersHoldTheBomb_ThenGoesIn()
    {
        yield return Boot();
        GoLive();
        Run(0.6f);

        SendDefendersHome();
        ITacticalZone site = match.PlantZones[0];
        match.PlantNow(site.ZoneSite, SiteCentre(site));
        Vector3 bomb = match.BombPosition;
        List<BotController> attackers = Alive(CombatTeam.Attackers);
        attackers[0].transform.position = bomb + new Vector3(3f, 0f, 0f);
        Run(1f);

        BotController defuser = Director.Defuser;
        Assert.IsNotNull(defuser);
        Assert.IsTrue(Director.DefuseHeld, "an attacker is on the bomb and there is time to spare");
        Assert.GreaterOrEqual(Planar(defuser.ObjectivePosition, bomb), 10f, "the defuser holds short of the bomb");

        foreach (BotController attacker in Alive(CombatTeam.Attackers))
            attacker.TakeDamage(9999f, Vector3.forward, false);
        Run(1f);

        Assert.IsFalse(Director.DefuseHeld);
        Assert.LessOrEqual(Planar(Director.Defuser.ObjectivePosition, bomb), 3f, "with the bomb clear the defuser goes in");
    }

    [UnityTest]
    public IEnumerator Defuse_IsAbandonedWhenTheFuseIsTooShort()
    {
        yield return Boot();
        GoLive();
        Run(0.6f);

        SendDefendersHome();
        ITacticalZone site = match.PlantZones[0];
        match.PlantNow(site.ZoneSite, SiteCentre(site));
        Run(1f);
        Assert.IsNotNull(Director.Defuser);
        Assert.IsFalse(Director.DefuseHopeless);

        Run(34f);

        Assert.Less(match.Model.Bomb.FuseRemaining, match.Model.Bomb.DefuseSeconds + 1f);
        Assert.IsTrue(Director.DefuseHopeless);
        Assert.IsNull(Director.Defuser, "nobody wastes the last seconds on a defuse that cannot finish");
    }

    [UnityTest]
    public IEnumerator PostPlantAttackers_GuardTheBombFromSpotsThatSeeIt()
    {
        yield return Boot();
        GoLive();
        Run(0.6f);

        SendDefendersHome();
        ITacticalZone site = match.PlantZones[0];
        match.PlantNow(site.ZoneSite, SiteCentre(site));
        Run(1f);

        Vector3 bomb = match.BombPosition;
        foreach (BotController bot in Alive(CombatTeam.Attackers))
        {
            Assert.IsTrue(bot.HasObjective);
            Vector3 spot = bot.ObjectivePosition;
            Assert.LessOrEqual(Planar(spot, bomb), 9f, "guards stay near the bomb");
            Assert.IsTrue(BotPerception.HasLineOfSight(spot + Vector3.up * 1.5f, bomb + Vector3.up * 0.9f, null, null),
                bot.name + " should hold a spot with a view of the bomb");
        }
    }

    [UnityTest]
    public IEnumerator DefuseInProgress_MakesTheAttackersRushTheBomb()
    {
        yield return Boot(r => r.playerAttacksFirst = false);
        GoLive();
        Run(0.6f);

        SendDefendersHome();
        ITacticalZone site = match.PlantZones[0];
        match.PlantNow(site.ZoneSite, SiteCentre(site));
        Vector3 bomb = match.BombPosition;

        controller.enabled = false;
        player.transform.position = bomb + Vector3.right * 0.8f;
        controller.enabled = true;
        match.ActionInput = () => true;
        Run(2f);

        Assert.IsTrue(match.Model.Bomb.IsDefusing);
        foreach (BotController bot in Alive(CombatTeam.Attackers))
            Assert.LessOrEqual(Planar(bot.ObjectivePosition, bomb), 5f, bot.name + " should contest the defuse");
    }
}
