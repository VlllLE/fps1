using System;
using System.Collections;
using FpsMaps.Bazaar;
using FpsTactical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The tactical HUD layer on the Bazaar scene: banners, class-select state, kill / death stats and the kill feed,
/// the bomb action prompt, the scoreboard flags and the spectator camera after the player dies.
/// </summary>
public class TacticalHudPlayTests
{
    private PlayerHealth player;
    private CharacterController controller;
    private TacticalMatch match;

    private IEnumerator Boot(Action<TacticalRules> tweak = null)
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

        var rules = new TacticalRules();
        tweak?.Invoke(rules);
        match = TacticalMatch.Create(rules);
        match.AutoStart = false;
        match.AutoRestart = false;
        match.ManualClock = true;
        match.ActionInput = () => false;
        match.Seed(7);
        Assert.IsTrue(match.Begin());
        yield return null;

        player = match.Player;
        Assert.IsNotNull(player);
        controller = player.GetComponent<CharacterController>();
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

    private TacticalMatchModel Model => match.Model;

    private void GoLive()
    {
        Model.SkipFreeze();
        match.Step(0.01f);
        Assert.AreEqual(TacticalPhase.Live, Model.Phase);
    }

    private void KillAll(CombatTeam side, bool includePlayer = true)
    {
        for (int squad = 0; squad < 2; squad++)
        {
            foreach (BotController bot in match.BotsOf(squad))
            {
                if (bot != null && !bot.IsDead && bot.Team == side)
                    bot.TakeDamage(9999f, Vector3.forward, false);
            }
        }

        if (includePlayer && !player.IsDead && player.Team == side)
            player.TakeDamage(9999f, player.transform.position + Vector3.forward);
    }

    private void Teleport(Vector3 position)
    {
        controller.enabled = false;
        player.transform.position = position;
        controller.enabled = true;
    }

    private Vector3 SiteCentre(string site)
    {
        Bounds bounds = match.ZoneBySite(site).ZoneBounds;
        return new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
    }

    private static HitDamageInfo Shot(int sourceId, float amount, HitZoneType zone = HitZoneType.Body)
    {
        return new HitDamageInfo(1, sourceId, amount, Vector3.zero, Vector3.up, Vector3.forward, zone, null);
    }

    [UnityTest]
    public IEnumerator Begin_AttachesTheHudAndTheSpectator()
    {
        yield return Boot();

        Assert.IsNotNull(match.Hud);
        Assert.IsNotNull(match.Spectator);
        Assert.AreEqual(8, match.Stats.Participants.Count, "four a side");
        Assert.AreEqual("ROUND 1", match.Hud.BannerTitle);
        StringAssert.Contains("ATTACK", match.Hud.BannerSubtitle);
        Assert.IsTrue(match.Hud.BannerActive);
        Assert.IsTrue(match.Hud.IsClassSelectOpen, "the class-select panel is open during freeze time");
        Assert.IsFalse(match.Hud.ScoreboardVisible);
    }

    [UnityTest]
    public IEnumerator ShowHudOff_SkipsTheHud()
    {
        yield return Boot();
        UnityEngine.Object.DestroyImmediate(match.gameObject);
        match = null;
        yield return null;

        var rules = new TacticalRules();
        match = TacticalMatch.Create(rules);
        match.AutoStart = false;
        match.ShowHud = false;
        match.ManualClock = true;
        Assert.IsTrue(match.Begin());
        Assert.IsNull(match.Hud);
        Assert.IsNotNull(match.Spectator);
    }

    [UnityTest]
    public IEnumerator ClassSelect_ReadyStartsTheRoundAndClosesThePanel()
    {
        yield return Boot();
        yield return null;
        Assert.IsTrue(match.Hud.IsClassSelectOpen);

        match.Hud.Ready();
        match.Step(0.01f);

        Assert.AreEqual(TacticalPhase.Live, Model.Phase);
        Assert.IsFalse(match.Hud.IsClassSelectOpen);
        Assert.AreEqual("GO", match.Hud.BannerTitle);
    }

    [UnityTest]
    public IEnumerator ClassSelect_ReopensBetweenRounds()
    {
        yield return Boot();
        GoLive();
        KillAll(CombatTeam.Defenders);
        match.Step(0.1f);
        Assert.AreEqual(TacticalPhase.RoundEnd, Model.Phase);
        Assert.IsFalse(match.Hud.IsClassSelectOpen);

        match.Step(match.Rules.roundEndSeconds + 0.1f);
        Assert.AreEqual(TacticalPhase.ClassSelect, Model.Phase);
        Assert.IsTrue(match.Hud.IsClassSelectOpen);
        Assert.AreEqual("ROUND 2", match.Hud.BannerTitle);
    }

    [UnityTest]
    public IEnumerator Banners_ReportWinsAndLossesFromThePlayersView()
    {
        yield return Boot();
        GoLive();
        KillAll(CombatTeam.Defenders);
        match.Step(0.1f);
        Assert.AreEqual("ROUND WON", match.Hud.BannerTitle);
        StringAssert.Contains("eliminated", match.Hud.BannerSubtitle.ToLowerInvariant());

        match.Step(match.Rules.roundEndSeconds + 0.1f);
        GoLive();
        KillAll(CombatTeam.Attackers);
        match.Step(0.1f);
        Assert.AreEqual("ROUND LOST", match.Hud.BannerTitle);
    }

    [UnityTest]
    public IEnumerator Banners_ShowSidesSwappedAtHalfTimeAndTheMatchResult()
    {
        yield return Boot(r => r.roundsToWin = 2);
        GoLive();
        KillAll(CombatTeam.Defenders);
        match.Step(0.1f);
        match.Step(match.Rules.roundEndSeconds + 0.1f);

        Assert.AreEqual("SIDES SWAPPED", match.Hud.BannerTitle);
        StringAssert.Contains("DEFEND", match.Hud.BannerSubtitle);

        GoLive();
        KillAll(CombatTeam.Attackers);
        match.Step(0.1f);
        match.Step(match.Rules.roundEndSeconds + 0.1f);
        Assert.AreEqual(TacticalPhase.MatchEnd, Model.Phase);
        Assert.AreEqual("VICTORY", match.Hud.BannerTitle);
        StringAssert.Contains("2 - 0", match.Hud.BannerSubtitle);
        Assert.IsTrue(match.Hud.ScoreboardVisible, "the scoreboard stays up at the end of the match");
    }

    [UnityTest]
    public IEnumerator Stats_AttributeKillsToThePlayerAndToBots()
    {
        yield return Boot();
        GoLive();
        BotController enemy = match.BotsOf(1)[0];
        BotController enemy2 = match.BotsOf(1)[1];

        enemy.ApplyHitDamage(Shot(player.GetHashCode(), 9999f, HitZoneType.Head));
        Assert.IsTrue(enemy.IsDead);

        TacticalParticipant you = match.Stats.FindPlayer();
        TacticalParticipant foe = match.Stats.FindBot(enemy);
        Assert.AreEqual(1, you.Kills);
        Assert.AreEqual(1, foe.Deaths);
        Assert.AreEqual(1, match.Stats.KillFeed.Count);
        Assert.AreEqual("You", match.Stats.KillFeed[0].Killer);
        Assert.IsTrue(match.Stats.KillFeed[0].Headshot);

        enemy2.ApplyHitDamage(Shot(match.BotsOf(0)[0].SourceId, 9999f));
        Assert.AreEqual(1, match.Stats.FindBot(match.BotsOf(0)[0]).Kills);
        Assert.IsFalse(match.Stats.KillFeed[1].Headshot);

        player.ApplyHitDamage(Shot(match.BotsOf(1)[2].SourceId, 9999f));
        Assert.IsTrue(player.IsDead);
        Assert.AreEqual(1, you.Deaths);
        Assert.AreEqual(1, match.Stats.FindBot(match.BotsOf(1)[2]).Kills);
        Assert.AreEqual(3, match.Stats.KillFeed.Count);
    }

    [UnityTest]
    public IEnumerator Stats_DirectDamageHasNoKiller_AndRestartClearsEverything()
    {
        yield return Boot(r => r.roundsToWin = 1);
        GoLive();
        BotController enemy = match.BotsOf(1)[0];
        enemy.TakeDamage(9999f, Vector3.forward, false);

        Assert.AreEqual(1, match.Stats.FindBot(enemy).Deaths);
        Assert.IsNull(match.Stats.KillFeed[0].Killer);
        Assert.AreEqual(0, match.Stats.FindPlayer().Kills);

        match.Restart();
        Assert.AreEqual(0, match.Stats.FindBot(enemy).Deaths);
        Assert.AreEqual(0, match.Stats.KillFeed.Count);
    }

    [UnityTest]
    public IEnumerator Stats_AreResetWhenTheMatchAutoRestarts()
    {
        yield return Boot(r => r.roundsToWin = 1);
        match.AutoRestart = true;
        GoLive();
        match.BotsOf(1)[0].ApplyHitDamage(Shot(player.GetHashCode(), 9999f));
        KillAll(CombatTeam.Defenders);
        match.Step(0.1f);
        match.Step(match.Rules.roundEndSeconds + 0.1f);
        Assert.AreEqual(TacticalPhase.MatchEnd, Model.Phase);
        Assert.AreEqual(1, match.Stats.FindPlayer().Kills);

        match.Step(match.Rules.matchEndSeconds + 0.1f);
        Assert.AreEqual(TacticalPhase.ClassSelect, Model.Phase);
        Assert.AreEqual(0, match.Stats.FindPlayer().Kills);
    }

    [UnityTest]
    public IEnumerator Participants_ReportAliveStateAndTheBombCarrier()
    {
        yield return Boot();
        GoLive();

        int carriers = 0;
        foreach (TacticalParticipant participant in match.Stats.Participants)
        {
            Assert.IsTrue(match.IsAlive(participant));
            if (match.CarriesBomb(participant))
            {
                carriers++;
                Assert.AreEqual(0, participant.Squad, "the player's squad attacks and carries the bomb");
            }
        }

        Assert.AreEqual(1, carriers);

        match.GiveBomb(null);
        Assert.IsTrue(match.CarriesBomb(match.Stats.FindPlayer()));
        match.BotsOf(1)[0].TakeDamage(9999f, Vector3.forward, false);
        Assert.IsFalse(match.IsAlive(match.Stats.FindBot(match.BotsOf(1)[0])));
    }

    [UnityTest]
    public IEnumerator ActionPrompt_OffersPlantInsideASiteAndDefuseNextToThePlantedBomb()
    {
        yield return Boot();
        GoLive();
        match.GiveBomb(null);

        Teleport(SiteCentre("A"));
        Assert.AreEqual("HOLD E TO PLANT", match.ActionPrompt);

        Teleport(match.SpawnsOf(CombatTeam.Attackers)[0].position);
        Assert.IsNull(match.ActionPrompt, "outside a site there is nothing to do");
    }

    [UnityTest]
    public IEnumerator ActionPrompt_DefendersGetTheDefusePrompt()
    {
        yield return Boot(r => r.playerAttacksFirst = false);
        GoLive();
        Assert.AreEqual(CombatTeam.Defenders, match.PlayerSide);
        Assert.IsNull(match.ActionPrompt);

        Vector3 site = SiteCentre("B");
        match.PlantNow("B", site);
        Teleport(site + new Vector3(0.5f, 0f, 0f));
        Assert.AreEqual("HOLD E TO DEFUSE", match.ActionPrompt);

        Teleport(match.SpawnsOf(CombatTeam.Defenders)[0].position);
        Assert.IsNull(match.ActionPrompt);
    }

    [UnityTest]
    public IEnumerator Spectator_FollowsATeammateAfterTheDelayAndMovesOnWhenItDies()
    {
        yield return Boot();
        GoLive();
        Assert.IsFalse(match.Spectator.IsSpectating);

        player.TakeDamage(9999f, player.transform.position + Vector3.forward);
        Assert.IsTrue(player.IsDead);
        match.Step(0.5f);
        Assert.IsFalse(match.Spectator.IsSpectating, "the view stays on the body for a moment");

        match.Step(TacticalSpectator.StartDelaySeconds);
        Assert.IsTrue(match.Spectator.IsSpectating);
        BotController first = match.Spectator.Target;
        Assert.IsNotNull(first);
        Assert.AreEqual(CombatTeam.Attackers, first.Team, "teammates are watched first");
        Assert.IsNotEmpty(match.Spectator.TargetName);

        match.Step(0.2f);
        Assert.Less(Vector3.Distance(match.Spectator.ViewCamera.transform.position, first.transform.position), 8f);

        first.TakeDamage(9999f, Vector3.forward, false);
        match.Step(0.1f);
        Assert.IsNotNull(match.Spectator.Target);
        Assert.AreNotSame(first, match.Spectator.Target);
        Assert.IsFalse(match.Spectator.Target.IsDead);
    }

    [UnityTest]
    public IEnumerator Spectator_PickCyclesThroughLivingPlayers()
    {
        yield return Boot();
        GoLive();
        player.TakeDamage(9999f, player.transform.position + Vector3.forward);
        match.Step(TacticalSpectator.StartDelaySeconds + 0.1f);

        BotController a = match.Spectator.Target;
        match.Spectator.Pick(1);
        BotController b = match.Spectator.Target;
        Assert.AreNotSame(a, b);
        match.Spectator.Pick(-1);
        Assert.AreSame(a, match.Spectator.Target);
        match.Spectator.Pick(0);
        Assert.AreSame(a, match.Spectator.Target, "a valid target is kept");
    }

    [UnityTest]
    public IEnumerator Spectator_WatchesEnemiesWhenTheWholeSquadIsDown_AndStopsWithTheNextRound()
    {
        yield return Boot();
        GoLive();
        foreach (BotController bot in match.BotsOf(0))
            bot.TakeDamage(9999f, Vector3.forward, false);
        player.TakeDamage(9999f, player.transform.position + Vector3.forward);
        match.Step(TacticalSpectator.StartDelaySeconds + 0.1f);

        Assert.AreEqual(TacticalPhase.RoundEnd, Model.Phase, "the defenders won the round");
        Assert.IsTrue(match.Spectator.IsSpectating);
        Assert.AreEqual(CombatTeam.Defenders, match.Spectator.Target.Team);

        match.Step(match.Rules.roundEndSeconds + 0.1f);
        Assert.AreEqual(TacticalPhase.ClassSelect, Model.Phase);
        Assert.IsFalse(player.IsDead, "the player is back for the next round");
        Assert.IsFalse(match.Spectator.IsSpectating);
    }
}
