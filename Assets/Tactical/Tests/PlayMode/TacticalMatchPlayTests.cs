using System;
using System.Collections;
using FpsMaps.Bazaar;
using FpsTactical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Drives the tactical round loop on the generated Bazaar scene with a manual clock: squads and freeze locks, going live,
/// no respawns inside a round, every win condition, bomb plant/defuse/drop/pickup, friendly fire and the half-time swap.
/// </summary>
public class TacticalMatchPlayTests
{
    private PlayerHealth player;
    private InputHandler input;
    private PlayerWeaponBridge weapons;
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
        Assert.IsTrue(match.Begin(), "Bazaar should provide plant zones and spawns");
        yield return null;

        player = match.Player;
        Assert.IsNotNull(player, "the scene's player should join squad 0");
        input = player.GetComponent<InputHandler>();
        weapons = player.GetComponent<PlayerWeaponBridge>();
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

    private void Run(float seconds, float dt = 0.1f)
    {
        for (float t = 0f; t < seconds; t += dt)
            match.Step(dt);
    }

    private static HitDamageInfo Shot(int sourceId, float amount)
    {
        return new HitDamageInfo(1, sourceId, amount, Vector3.zero, Vector3.up, Vector3.forward, HitZoneType.Body, null);
    }

    [UnityTest]
    public IEnumerator Begin_CreatesTwoSquadsAndFreezesTheRound()
    {
        yield return Boot();

        Assert.AreEqual(TacticalPhase.ClassSelect, Model.Phase);
        Assert.AreEqual(4, match.TotalCount(CombatTeam.Attackers));
        Assert.AreEqual(4, match.TotalCount(CombatTeam.Defenders));
        Assert.AreEqual(4, match.AliveCount(CombatTeam.Attackers));
        Assert.AreEqual(4, match.AliveCount(CombatTeam.Defenders));
        Assert.AreEqual(CombatTeam.Attackers, player.Team);
        Assert.AreEqual(CombatTeam.Attackers, match.PlayerSide);
        Assert.IsNotNull(match.TargetSite);
        Assert.IsTrue(input.GameplayLocked, "movement is frozen during class selection");
        Assert.IsTrue(weapons.FireLocked, "firing is off during class selection");
        Assert.IsFalse(player.AutoRespawn);
        foreach (BotController bot in match.BotsOf(0))
            Assert.IsTrue(bot.Frozen);
        foreach (BotController bot in match.BotsOf(1))
        {
            Assert.IsTrue(bot.Frozen);
            Assert.AreEqual(CombatTeam.Defenders, bot.Team);
            Assert.IsFalse(bot.AutoRespawn);
        }
    }

    [UnityTest]
    public IEnumerator SquadsStartInTheirOwnYards()
    {
        yield return Boot();

        foreach (BotController bot in match.BotsOf(0))
            Assert.IsTrue(InSpawnArea(match.SpawnsOf(CombatTeam.Attackers), bot.transform.position), bot.name + " should start at an attacker spawn");
        foreach (BotController bot in match.BotsOf(1))
            Assert.IsTrue(InSpawnArea(match.SpawnsOf(CombatTeam.Defenders), bot.transform.position), bot.name + " should start at a defender spawn");
        Assert.IsTrue(InSpawnArea(match.SpawnsOf(CombatTeam.Attackers), player.transform.position), "player should start at an attacker spawn");
    }

    private static bool InSpawnArea(System.Collections.Generic.IReadOnlyList<Transform> spawns, Vector3 position)
    {
        for (int i = 0; i < spawns.Count; i++)
        {
            if (TacticalObjectives.Planar(spawns[i].position, position) < 6f)
                return true;
        }

        return false;
    }

    [UnityTest]
    public IEnumerator FreezeEnds_RoundGoesLive_AndOneAttackerCarriesTheBomb()
    {
        yield return Boot();

        match.Step(match.Rules.freezeSeconds - 1f);
        Assert.AreEqual(TacticalPhase.ClassSelect, Model.Phase);
        Assert.IsTrue(input.GameplayLocked);

        match.Step(1.2f);
        Assert.AreEqual(TacticalPhase.Live, Model.Phase);
        Assert.IsFalse(input.GameplayLocked, "movement unlocks when the round goes live");
        Assert.IsFalse(weapons.FireLocked);
        Assert.IsTrue(match.RoundReady);
        Assert.IsTrue(match.PlayerIsCarrier ^ (match.CarrierBot != null), "exactly one attacker carries the bomb");
        foreach (BotController bot in match.BotsOf(1))
            Assert.IsFalse(bot.Frozen);
    }

    [UnityTest]
    public IEnumerator NobodyRespawnsInsideARound()
    {
        yield return Boot();
        GoLive();

        BotController victim = match.BotsOf(1)[0];
        victim.TakeDamage(9999f, Vector3.forward, false);
        player.TakeDamage(9999f, player.transform.position + Vector3.forward);
        Assert.IsTrue(victim.IsDead);
        Assert.IsTrue(player.IsDead);

        Run(10f);
        yield return null;
        yield return null;
        Assert.IsTrue(victim.IsDead, "dead bots stay dead until the next round");
        Assert.IsTrue(player.IsDead, "the player stays dead until the next round");
        Assert.AreEqual(3, match.AliveCount(CombatTeam.Defenders));
    }

    [UnityTest]
    public IEnumerator EliminatingTheDefenders_EndsTheRoundForTheAttackers_AndTheNextRoundRevivesEveryone()
    {
        yield return Boot();
        GoLive();

        player.TakeDamage(9999f, player.transform.position + Vector3.forward);
        KillAll(CombatTeam.Defenders);
        match.Step(0.1f);

        Assert.AreEqual(TacticalPhase.RoundEnd, Model.Phase);
        Assert.AreEqual(RoundEndReason.DefendersEliminated, Model.LastResult.Reason);
        Assert.AreEqual(1, Model.Score(0));
        Assert.IsTrue(weapons.FireLocked, "firing is off between rounds");

        match.Step(match.Rules.roundEndSeconds + 0.1f);
        Assert.AreEqual(TacticalPhase.ClassSelect, Model.Phase);
        Assert.AreEqual(2, Model.RoundNumber);
        Assert.IsFalse(player.IsDead, "the player is revived for the next round");
        Assert.AreEqual(player.MaxHealth, player.CurrentHealth, 0.01f);
        Assert.AreEqual(4, match.AliveCount(CombatTeam.Attackers));
        Assert.AreEqual(4, match.AliveCount(CombatTeam.Defenders));
        Assert.IsTrue(input.GameplayLocked);
    }

    [UnityTest]
    public IEnumerator EliminatingTheAttackers_BeforeAPlant_WinsForTheDefenders()
    {
        yield return Boot();
        GoLive();

        KillAll(CombatTeam.Attackers);
        match.Step(0.1f);

        Assert.AreEqual(TacticalPhase.RoundEnd, Model.Phase);
        Assert.AreEqual(RoundEndReason.AttackersEliminated, Model.LastResult.Reason);
        Assert.AreEqual(1, Model.Score(1));
        Assert.AreEqual(0, Model.Score(0));
    }

    [UnityTest]
    public IEnumerator TimeOut_WithoutAPlant_GivesTheRoundToTheDefenders()
    {
        yield return Boot();
        GoLive();

        match.Step(match.Rules.roundSeconds + 1f);
        Assert.AreEqual(TacticalPhase.RoundEnd, Model.Phase);
        Assert.AreEqual(RoundEndReason.TimeExpired, Model.LastResult.Reason);
        Assert.AreEqual(CombatTeam.Defenders, Model.LastResult.WinnerSide);
    }

    [UnityTest]
    public IEnumerator PlayerCarrier_PlantsByHoldingTheActionInsideASite()
    {
        yield return Boot();
        GoLive();
        match.GiveBomb(null);
        Teleport(SiteCentre("A"));
        match.ActionInput = () => true;

        Run(1.5f);
        Assert.AreEqual(BombState.Carried, Model.Bomb.State, "planting takes the full hold");
        Assert.IsTrue(Model.Bomb.IsPlanting);
        Assert.IsTrue(input.GameplayLocked, "the player stands still while planting");
        Assert.IsTrue(weapons.FireLocked);

        Run(2.5f);
        Assert.AreEqual(BombState.Planted, Model.Bomb.State);
        Assert.AreEqual("A", Model.Bomb.Site);
        Assert.IsNotNull(match.BombProp, "a bomb prop is placed on the ground");
        Assert.IsFalse(match.PlayerIsCarrier);
        Assert.IsFalse(input.GameplayLocked, "movement returns after the plant");
    }

    [UnityTest]
    public IEnumerator PlantingOutsideASite_DoesNothing_AndReleasingRestartsTheHold()
    {
        yield return Boot();
        GoLive();
        match.GiveBomb(null);
        match.ActionInput = () => true;

        Run(5f);
        Assert.AreEqual(BombState.Carried, Model.Bomb.State, "the spawn yard is not a bomb site");

        Teleport(SiteCentre("B"));
        Run(2f);
        match.ActionInput = () => false;
        Run(0.3f);
        Assert.AreEqual(0f, Model.Bomb.PlantProgress);
        match.ActionInput = () => true;
        Run(2f);
        Assert.AreEqual(BombState.Carried, Model.Bomb.State, "an interrupted hold does not carry over");
    }

    [UnityTest]
    public IEnumerator PlayerDefender_DefusesByHoldingTheActionNextToTheBomb()
    {
        yield return Boot(r => r.playerAttacksFirst = false);
        Assert.AreEqual(CombatTeam.Defenders, player.Team);
        GoLive();

        match.PlantNow("B", SiteCentre("B"));
        Assert.AreEqual(BombState.Planted, Model.Bomb.State);
        Teleport(match.BombPosition + Vector3.right * 0.8f);
        match.ActionInput = () => true;

        Run(match.Rules.defuseSeconds - 1.5f);
        Assert.AreEqual(BombState.Planted, Model.Bomb.State);
        Assert.Greater(Model.Bomb.DefuseProgress, 0f);

        Run(2f);
        Assert.AreEqual(BombState.Defused, Model.Bomb.State);
        Assert.AreEqual(TacticalPhase.RoundEnd, Model.Phase);
        Assert.AreEqual(RoundEndReason.BombDefused, Model.LastResult.Reason);
        Assert.AreEqual(1, Model.Score(0), "the player's squad defends and wins the round");
    }

    [UnityTest]
    public IEnumerator AttackersCannotDefuseTheirOwnBomb()
    {
        yield return Boot();
        GoLive();
        match.PlantNow("A", SiteCentre("A"));
        Teleport(match.BombPosition + Vector3.right * 0.8f);
        match.ActionInput = () => true;

        Run(match.Rules.defuseSeconds + 1f);
        Assert.AreEqual(BombState.Planted, Model.Bomb.State);
    }

    [UnityTest]
    public IEnumerator Bomb_ExplodesAfterTheFuse_AndWinsTheRoundForTheAttackers()
    {
        yield return Boot();
        GoLive();
        match.PlantNow("A", SiteCentre("A"));

        float clockBefore = Model.PhaseRemaining;
        Run(10f);
        Assert.AreEqual(clockBefore, Model.PhaseRemaining, 0.01f, "the round clock stops once the bomb is down");
        Assert.AreEqual(TacticalPhase.Live, Model.Phase);

        for (int i = 0; i < 1000 && Model.Bomb.State == BombState.Planted; i++)
            match.Step(0.1f);
        Assert.AreEqual(BombState.Exploded, Model.Bomb.State);
        Assert.AreEqual(TacticalPhase.RoundEnd, Model.Phase);
        Assert.AreEqual(RoundEndReason.BombExploded, Model.LastResult.Reason);
        Assert.AreEqual(1, Model.Score(0));
    }

    [UnityTest]
    public IEnumerator PlantedBomb_KeepsTheRoundGoingWhenTheAttackersAllDie()
    {
        yield return Boot();
        GoLive();
        match.PlantNow("A", SiteCentre("A"));

        KillAll(CombatTeam.Attackers);
        Run(2f);
        Assert.AreEqual(TacticalPhase.Live, Model.Phase, "defenders still have to defuse");

        KillAll(CombatTeam.Defenders);
        match.Step(0.1f);
        Assert.AreEqual(TacticalPhase.RoundEnd, Model.Phase);
        Assert.AreEqual(CombatTeam.Attackers, Model.LastResult.WinnerSide, "with the bomb down, a wiped defence loses");
    }

    [UnityTest]
    public IEnumerator DeadCarrier_DropsTheBomb_AndATeammateCanPickItUp()
    {
        yield return Boot();
        GoLive();
        BotController carrier = match.BotsOf(0)[0];
        match.GiveBomb(carrier);
        Assert.AreEqual(carrier, match.CarrierBot);

        carrier.transform.position += Vector3.up * 0.01f;
        carrier.TakeDamage(9999f, Vector3.forward, false);
        match.Step(0.1f);

        Assert.AreEqual(BombState.Dropped, Model.Bomb.State);
        Assert.IsNotNull(match.BombProp, "the dropped bomb is visible on the floor");
        Assert.IsNull(match.CarrierBot);

        Teleport(match.BombPosition + Vector3.right * 0.5f);
        match.Step(0.1f);
        Assert.AreEqual(BombState.Carried, Model.Bomb.State);
        Assert.IsTrue(match.PlayerIsCarrier);
        Assert.IsNull(match.BombProp);
    }

    [UnityTest]
    public IEnumerator Defenders_CannotPickUpTheDroppedBomb()
    {
        yield return Boot();
        GoLive();
        BotController carrier = match.BotsOf(0)[0];
        match.GiveBomb(carrier);
        carrier.TakeDamage(9999f, Vector3.forward, false);
        match.Step(0.1f);
        Assert.AreEqual(BombState.Dropped, Model.Bomb.State);

        BotController defender = match.BotsOf(1)[0];
        defender.transform.position = match.BombPosition;
        match.Step(0.1f);
        Assert.AreEqual(BombState.Dropped, Model.Bomb.State);
    }

    [UnityTest]
    public IEnumerator FriendlyFire_IsOff_TeammatesAreImmune_EnemiesAreNot()
    {
        yield return Boot();
        GoLive();
        BotController a = match.BotsOf(0)[0];
        BotController b = match.BotsOf(0)[1];
        BotController enemy = match.BotsOf(1)[0];

        float before = b.Health;
        b.ApplyHitDamage(Shot(a.SourceId, 30f));
        Assert.AreEqual(before, b.Health, 0.001f, "bots cannot hurt teammates");

        float enemyBefore = enemy.Health;
        enemy.ApplyHitDamage(Shot(a.SourceId, 30f));
        Assert.Less(enemy.Health, enemyBefore);

        float playerBefore = player.CurrentHealth;
        player.ApplyHitDamage(Shot(a.SourceId, 30f));
        Assert.AreEqual(playerBefore, player.CurrentHealth, 0.001f, "the player's bot teammates cannot hurt them");
        player.ApplyHitDamage(Shot(enemy.SourceId, 30f));
        Assert.Less(player.CurrentHealth, playerBefore);

        float teammateBefore = b.Health;
        b.ApplyHitDamage(Shot(player.GetHashCode(), 30f));
        Assert.AreEqual(teammateBefore, b.Health, 0.001f, "the player cannot hurt their own squad");
    }

    [UnityTest]
    public IEnumerator FriendlyFire_CanBeSwitchedOn()
    {
        yield return Boot(r => r.friendlyFire = true);
        GoLive();
        BotController a = match.BotsOf(0)[0];
        BotController b = match.BotsOf(0)[1];

        float before = b.Health;
        b.ApplyHitDamage(Shot(a.SourceId, 30f));
        Assert.Less(b.Health, before);
        float afterFirst = b.Health;
        b.ApplyHitDamage(Shot(b.SourceId, 30f));
        Assert.AreEqual(afterFirst, b.Health, 0.001f, "self damage is still ignored");
    }

    [UnityTest]
    public IEnumerator SidesSwapAtHalfTime_AndTheMatchEndsAtTheScoreLimit()
    {
        yield return Boot(r => r.roundsToWin = 2);
        GoLive();

        KillAll(CombatTeam.Defenders);
        match.Step(0.1f);
        Assert.AreEqual(1, Model.Score(0));
        match.Step(match.Rules.roundEndSeconds + 0.1f);

        Assert.AreEqual(TacticalPhase.ClassSelect, Model.Phase);
        Assert.AreEqual(CombatTeam.Defenders, match.PlayerSide, "the player's squad defends after half time");
        Assert.AreEqual(CombatTeam.Defenders, player.Team);
        foreach (BotController bot in match.BotsOf(0))
            Assert.AreEqual(CombatTeam.Defenders, bot.Team);
        foreach (BotController bot in match.BotsOf(1))
            Assert.AreEqual(CombatTeam.Attackers, bot.Team);
        Assert.IsFalse(match.PlayerIsCarrier);

        GoLive();
        Assert.IsNotNull(match.CarrierBot, "the enemy squad now carries the bomb");
        Assert.AreEqual(CombatTeam.Attackers, match.CarrierBot.Team);

        KillAll(CombatTeam.Attackers);
        match.Step(0.1f);
        Assert.AreEqual(2, Model.Score(0));
        match.Step(match.Rules.roundEndSeconds + 0.1f);
        Assert.AreEqual(TacticalPhase.MatchEnd, Model.Phase);
        Assert.AreEqual(0, Model.MatchWinnerSquad);

        match.Step(match.Rules.matchEndSeconds + 0.1f);
        Assert.IsTrue(Model.MatchFinished);
        Assert.IsTrue(weapons.FireLocked);
    }

    [UnityTest]
    public IEnumerator Restart_StartsAFreshMatch()
    {
        yield return Boot(r => r.roundsToWin = 1);
        GoLive();
        KillAll(CombatTeam.Defenders);
        match.Step(0.1f);
        match.Step(match.Rules.roundEndSeconds + 0.1f);
        Assert.AreEqual(TacticalPhase.MatchEnd, Model.Phase);

        match.Restart();
        Assert.AreEqual(TacticalPhase.ClassSelect, Model.Phase);
        Assert.AreEqual(0, Model.Score(0));
        Assert.AreEqual(1, Model.RoundNumber);
        Assert.AreEqual(4, match.AliveCount(CombatTeam.Defenders));
        Assert.IsFalse(player.IsDead);
    }

    [UnityTest]
    public IEnumerator DestroyingTheMatch_ReturnsPracticeBehaviourToThePlayerAndBots()
    {
        yield return Boot();
        GoLive();
        UnityEngine.Object.DestroyImmediate(match.gameObject);
        match = null;
        yield return null;

        Assert.AreEqual(CombatTeam.None, player.Team);
        Assert.IsTrue(player.AutoRespawn);
        Assert.IsFalse(input.GameplayLocked);
        Assert.IsFalse(weapons.FireLocked);
        Assert.IsFalse(CombatTeams.FriendlyFire);
        Assert.AreEqual(0, BotManager.Instance.Bots.Count);
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator Bots_CarryTheBombToASite_AndPlantItWithoutThePlayer()
    {
        yield return Boot();
        match.ManualClock = false;
        GoLive();

        player.TakeDamage(9999f, player.transform.position + Vector3.forward);
        foreach (BotController bot in match.BotsOf(1))
            bot.Frozen = true;
        foreach (BotController bot in match.BotsOf(1))
            bot.transform.position = new Vector3(2000f, 0f, 2000f);
        Assert.IsNotNull(match.CarrierBot, "with the player down a bot must carry the bomb");

        Time.timeScale = 4f;
        float limit = 90f;
        while (Model.Bomb.State == BombState.Carried && limit > 0f && Model.Phase == TacticalPhase.Live)
        {
            limit -= Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = 1f;
        BotController carrier = match.CarrierBot;
        string diagnostics = carrier == null ? "no carrier" : string.Format(
            "carrier {0} state {1} objective {2} atObjective {3} speed {4:F2} dist {5:F1} nav {6} phase {7} remaining {8:F1} sees {9}",
            carrier.name, carrier.Brain.State, carrier.HasObjective, carrier.AtObjective, carrier.Speed,
            TacticalObjectives.Planar(carrier.transform.position, match.TargetSite.ZoneBounds.center), BotNavigation.IsReady, Model.Phase, Model.PhaseRemaining, carrier.SeesEnemy);
        Assert.AreEqual(BombState.Planted, Model.Bomb.State, "attacking bots should plant at " + match.TargetSite.ZoneSite + ": " + diagnostics);
        Assert.AreEqual(match.TargetSite.ZoneSite, Model.Bomb.Site);
    }
}
