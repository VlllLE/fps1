using System.Collections.Generic;
using FpsTactical;
using NUnit.Framework;
using UnityEngine;

public class TacticalBombModelTests
{
    private static TacticalBombModel NewBomb() => new TacticalBombModel(3f, 7f, 40f);

    [Test]
    public void Plant_NeedsAnUninterruptedHoldInsideASite()
    {
        var bomb = NewBomb();
        bomb.Tick(2.9f, "A", true, false);
        Assert.AreEqual(BombState.Carried, bomb.State);
        Assert.IsTrue(bomb.IsPlanting);

        bomb.Tick(0.2f, "A", true, false);
        Assert.AreEqual(BombState.Planted, bomb.State);
        Assert.AreEqual("A", bomb.Site);
        Assert.AreEqual(40f, bomb.FuseRemaining, 0.001f);
    }

    [Test]
    public void Plant_ReleasingTheActionOrLeavingTheSiteRestartsIt()
    {
        var bomb = NewBomb();
        bomb.Tick(2f, "A", true, false);
        bomb.Tick(0.1f, "A", false, false);
        Assert.AreEqual(0f, bomb.PlantProgress);
        bomb.Tick(2f, "A", true, false);
        bomb.Tick(0.1f, null, true, false);
        Assert.AreEqual(0f, bomb.PlantProgress);
        bomb.Tick(2.5f, "B", true, false);
        Assert.AreEqual(BombState.Carried, bomb.State, "restarted holds must not add up");
    }

    [Test]
    public void Plant_IsImpossibleWhileTheBombLiesOnTheFloor()
    {
        var bomb = NewBomb();
        bomb.Tick(1f, "A", true, false);
        bomb.Drop();
        Assert.AreEqual(BombState.Dropped, bomb.State);
        Assert.AreEqual(0f, bomb.PlantProgress);
        bomb.Tick(10f, "A", true, false);
        Assert.AreEqual(BombState.Dropped, bomb.State);
        bomb.PickUp();
        Assert.AreEqual(BombState.Carried, bomb.State);
    }

    [Test]
    public void Defuse_NeedsAFullUninterruptedHold()
    {
        var bomb = NewBomb();
        bomb.Plant("B");
        bomb.Tick(6f, null, false, true);
        Assert.AreEqual(BombState.Planted, bomb.State);
        bomb.Tick(0.5f, null, false, false);
        Assert.AreEqual(0f, bomb.DefuseProgress, "letting go restarts the defuse");
        bomb.Tick(6.9f, null, false, true);
        Assert.AreEqual(BombState.Planted, bomb.State);
        bomb.Tick(0.2f, null, false, true);
        Assert.AreEqual(BombState.Defused, bomb.State);
    }

    [Test]
    public void Fuse_ExplodesTheBombUnlessDefusedFirst()
    {
        var bomb = NewBomb();
        bool exploded = false;
        bomb.Exploded += () => exploded = true;
        bomb.Plant("A");
        bomb.Tick(39.9f, null, false, false);
        Assert.AreEqual(BombState.Planted, bomb.State);
        bomb.Tick(0.2f, null, false, false);
        Assert.AreEqual(BombState.Exploded, bomb.State);
        Assert.IsTrue(exploded);
    }

    [Test]
    public void Defuse_FinishingOnTheSameTickAsTheFuseWins()
    {
        var bomb = NewBomb();
        bomb.Plant("A");
        bomb.Tick(33f, null, false, false);
        bomb.Tick(7f, null, false, true);
        Assert.AreEqual(BombState.Defused, bomb.State);
    }

    [Test]
    public void FuseProgress_ReportsHowFarTheFuseBurned()
    {
        var bomb = NewBomb();
        Assert.AreEqual(0f, bomb.FuseElapsed01);
        bomb.Plant("A");
        bomb.Tick(10f, null, false, false);
        Assert.AreEqual(0.25f, bomb.FuseElapsed01, 0.001f);
    }
}

public class TacticalMatchModelTests
{
    private TacticalRules rules;
    private TacticalMatchModel model;
    private readonly List<RoundResult> results = new List<RoundResult>();

    [SetUp]
    public void SetUp()
    {
        rules = new TacticalRules
        {
            roundsToWin = 4,
            freezeSeconds = 10f,
            roundSeconds = 60f,
            roundEndSeconds = 4f,
            matchEndSeconds = 6f
        };
        model = new TacticalMatchModel(rules);
        results.Clear();
        model.RoundEnded += r => results.Add(r);
        model.Start();
    }

    private void GoLive()
    {
        model.Tick(rules.freezeSeconds + 0.1f, 4, 4, null, false, false);
        Assert.AreEqual(TacticalPhase.Live, model.Phase);
    }

    private void FinishRoundEndPhase()
    {
        model.Tick(rules.roundEndSeconds + 0.1f, 4, 4, null, false, false);
    }

    private void PlayRound(CombatTeam winner)
    {
        GoLive();
        if (winner == CombatTeam.Attackers)
            model.Tick(0.1f, 4, 0, null, false, false);
        else
            model.Tick(0.1f, 0, 4, null, false, false);
        Assert.AreEqual(TacticalPhase.RoundEnd, model.Phase);
        FinishRoundEndPhase();
    }

    [Test]
    public void Start_BeginsWithFreezeTimeAndSquadZeroAttacking()
    {
        Assert.AreEqual(TacticalPhase.ClassSelect, model.Phase);
        Assert.AreEqual(1, model.RoundNumber);
        Assert.AreEqual(CombatTeam.Attackers, model.SideOf(0));
        Assert.AreEqual(CombatTeam.Defenders, model.SideOf(1));
        Assert.AreEqual(rules.freezeSeconds, model.PhaseRemaining, 0.001f);
    }

    [Test]
    public void PlayerCanDefendFirst()
    {
        rules.playerAttacksFirst = false;
        model.Start();
        Assert.AreEqual(CombatTeam.Defenders, model.SideOf(0));
        Assert.AreEqual(0, model.SquadOf(CombatTeam.Defenders));
    }

    [Test]
    public void FreezeTime_ThenTheRoundGoesLive_AndSkipFreezeShortensIt()
    {
        model.Tick(5f, 4, 4, null, false, false);
        Assert.AreEqual(TacticalPhase.ClassSelect, model.Phase);
        model.SkipFreeze();
        Assert.AreEqual(TacticalPhase.Live, model.Phase);
        Assert.AreEqual(rules.roundSeconds, model.PhaseRemaining, 0.001f);
    }

    [Test]
    public void TimeOut_WithoutAPlant_IsADefenderWin()
    {
        GoLive();
        model.Tick(59f, 4, 4, null, false, false);
        Assert.AreEqual(TacticalPhase.Live, model.Phase);
        model.Tick(1.5f, 4, 4, null, false, false);
        Assert.AreEqual(TacticalPhase.RoundEnd, model.Phase);
        Assert.AreEqual(RoundEndReason.TimeExpired, results[0].Reason);
        Assert.AreEqual(CombatTeam.Defenders, results[0].WinnerSide);
        Assert.AreEqual(1, model.Score(1));
    }

    [Test]
    public void EliminatingTheDefenders_WinsForTheAttackers()
    {
        GoLive();
        model.Tick(0.1f, 3, 0, null, false, false);
        Assert.AreEqual(RoundEndReason.DefendersEliminated, results[0].Reason);
        Assert.AreEqual(CombatTeam.Attackers, results[0].WinnerSide);
        Assert.AreEqual(1, model.Score(0));
    }

    [Test]
    public void EliminatingTheAttackers_BeforeAPlant_WinsForTheDefenders()
    {
        GoLive();
        model.Tick(0.1f, 0, 2, null, false, false);
        Assert.AreEqual(RoundEndReason.AttackersEliminated, results[0].Reason);
        Assert.AreEqual(1, model.Score(1));
    }

    [Test]
    public void BothSidesDyingAtOnce_FavoursTheDefendersUnlessTheBombIsDown()
    {
        GoLive();
        model.Tick(0.1f, 0, 0, null, false, false);
        Assert.AreEqual(CombatTeam.Defenders, results[0].WinnerSide);

        FinishRoundEndPhase();
        GoLive();
        model.Bomb.Plant("A");
        model.Tick(0.1f, 0, 0, null, false, false);
        Assert.AreEqual(CombatTeam.Attackers, results[1].WinnerSide);
    }

    [Test]
    public void PlantedBomb_KeepsTheRoundAliveAfterTheAttackersDie_AndSwitchesTheClockOff()
    {
        GoLive();
        model.Bomb.Plant("B");
        float before = model.PhaseRemaining;
        model.Tick(5f, 0, 3, null, false, false);
        Assert.AreEqual(TacticalPhase.Live, model.Phase, "dead attackers cannot lose a planted round before the fuse ends");
        Assert.AreEqual(before, model.PhaseRemaining, 0.001f, "round clock stops once the bomb is planted");

        model.Tick(100f, 0, 3, null, false, false);
        Assert.AreEqual(RoundEndReason.BombExploded, results[0].Reason);
        Assert.AreEqual(CombatTeam.Attackers, results[0].WinnerSide);
    }

    [Test]
    public void PlantedBomb_WinsForTheAttackersWhenTheDefendersAreAllDead()
    {
        GoLive();
        model.Bomb.Plant("A");
        model.Tick(0.1f, 2, 0, null, false, false);
        Assert.AreEqual(RoundEndReason.DefendersEliminated, results[0].Reason);
    }

    [Test]
    public void Defusing_IsADefenderWin()
    {
        GoLive();
        model.Bomb.Plant("A");
        model.Tick(7.1f, 2, 2, null, false, true);
        Assert.AreEqual(RoundEndReason.BombDefused, results[0].Reason);
        Assert.AreEqual(CombatTeam.Defenders, results[0].WinnerSide);
    }

    [Test]
    public void HoldingThePlantActionInASite_PlantsDuringTheLiveRound()
    {
        GoLive();
        model.Tick(3.3f, 4, 4, "B", true, false);
        Assert.AreEqual(BombState.Planted, model.Bomb.State);
        Assert.AreEqual("B", model.Bomb.Site);
    }

    [Test]
    public void BombCannotBePlantedDuringFreezeTime()
    {
        model.Tick(5f, 4, 4, "A", true, false);
        Assert.AreEqual(BombState.Carried, model.Bomb.State);
        Assert.AreEqual(0f, model.Bomb.PlantProgress);
    }

    [Test]
    public void RoundEnd_ThenNextRoundStartsWithFreezeTime()
    {
        PlayRound(CombatTeam.Attackers);
        Assert.AreEqual(TacticalPhase.ClassSelect, model.Phase);
        Assert.AreEqual(2, model.RoundNumber);
        Assert.AreEqual(BombState.Carried, model.Bomb.State);
    }

    [Test]
    public void SidesSwapAtHalfTime_AfterThreeRounds_AndOnlyOnce()
    {
        PlayRound(CombatTeam.Attackers);
        PlayRound(CombatTeam.Defenders);
        Assert.AreEqual(CombatTeam.Attackers, model.SideOf(0));
        PlayRound(CombatTeam.Attackers);
        Assert.AreEqual(3, model.RoundsPlayed);
        Assert.AreEqual(CombatTeam.Defenders, model.SideOf(0), "squad 0 defends after the swap");
        Assert.IsTrue(model.SidesSwapped);

        PlayRound(CombatTeam.Attackers);
        Assert.AreEqual(CombatTeam.Defenders, model.SideOf(0), "no second swap");
    }

    [Test]
    public void ScoresFollowTheSquads_NotTheSides_AcrossTheSwap()
    {
        PlayRound(CombatTeam.Attackers);
        PlayRound(CombatTeam.Attackers);
        PlayRound(CombatTeam.Attackers);
        Assert.AreEqual(3, model.Score(0));
        PlayRound(CombatTeam.Attackers);
        Assert.AreEqual(3, model.Score(0));
        Assert.AreEqual(1, model.Score(1), "squad 1 attacked in round four and won it");
    }

    [Test]
    public void FirstSquadToFourRounds_WinsTheMatch()
    {
        PlayRound(CombatTeam.Attackers);
        PlayRound(CombatTeam.Attackers);
        PlayRound(CombatTeam.Attackers);
        PlayRound(CombatTeam.Attackers);
        Assert.AreEqual(3, model.Score(0));
        Assert.AreEqual(1, model.Score(1));
        Assert.AreEqual(TacticalPhase.ClassSelect, model.Phase);

        GoLive();
        model.Tick(0.1f, 0, 4, null, false, false);
        Assert.AreEqual(4, model.Score(0), "squad 0 defends after the swap and wins the deciding round");
        FinishRoundEndPhase();
        Assert.AreEqual(TacticalPhase.MatchEnd, model.Phase);
        Assert.AreEqual(0, model.MatchWinnerSquad);
    }

    [Test]
    public void MatchEnd_IsReachedAtTheScoreLimit_AndFinishesAfterThePause()
    {
        rules.roundsToWin = 2;
        model = new TacticalMatchModel(rules);
        model.Start();
        PlayRound(CombatTeam.Attackers);
        Assert.AreEqual(CombatTeam.Defenders, model.SideOf(0), "roundsToWin 2 swaps after one round");
        GoLive();
        model.Tick(0.1f, 0, 4, null, false, false);
        FinishRoundEndPhase();
        Assert.AreEqual(TacticalPhase.MatchEnd, model.Phase);
        Assert.AreEqual(0, model.MatchWinnerSquad);
        Assert.IsFalse(model.MatchFinished);
        model.Tick(rules.matchEndSeconds + 0.1f, 4, 4, null, false, false);
        Assert.IsTrue(model.MatchFinished);

        model.Start();
        Assert.AreEqual(0, model.Score(0));
        Assert.AreEqual(1, model.RoundNumber);
        Assert.AreEqual(TacticalPhase.ClassSelect, model.Phase);
    }

    [Test]
    public void SingleRoundMatch_NeverSwaps()
    {
        rules.roundsToWin = 1;
        model = new TacticalMatchModel(rules);
        model.Start();
        PlayRound(CombatTeam.Attackers);
        Assert.AreEqual(TacticalPhase.MatchEnd, model.Phase);
        Assert.IsFalse(model.SidesSwapped);
    }

    [Test]
    public void ARoundCanOnlyBeScoredOnce()
    {
        GoLive();
        model.Tick(0.1f, 3, 0, null, false, false);
        model.Tick(0.1f, 3, 0, null, false, false);
        model.Tick(0.1f, 0, 3, null, false, false);
        Assert.AreEqual(1, model.RoundsPlayed);
    }
}

public class CombatTeamTests
{
    [TearDown]
    public void TearDown()
    {
        CombatTeams.FriendlyFire = false;
    }

    [Test]
    public void OppositeAndEnemies()
    {
        Assert.AreEqual(CombatTeam.Defenders, CombatTeams.Opposite(CombatTeam.Attackers));
        Assert.AreEqual(CombatTeam.Attackers, CombatTeams.Opposite(CombatTeam.Defenders));
        Assert.AreEqual(CombatTeam.None, CombatTeams.Opposite(CombatTeam.None));
        Assert.IsTrue(CombatTeams.AreEnemies(CombatTeam.Attackers, CombatTeam.Defenders));
        Assert.IsFalse(CombatTeams.AreEnemies(CombatTeam.Attackers, CombatTeam.Attackers));
        Assert.IsFalse(CombatTeams.AreEnemies(CombatTeam.None, CombatTeam.Defenders));
    }

    [Test]
    public void FriendlyFire_IsOffByDefault_AndTeammatesCannotHurtEachOther()
    {
        Assert.IsFalse(CombatTeams.FriendlyFire);
        Assert.IsFalse(CombatTeams.CanDamage(CombatTeam.Attackers, CombatTeam.Attackers));
        Assert.IsTrue(CombatTeams.CanDamage(CombatTeam.Attackers, CombatTeam.Defenders));
        CombatTeams.FriendlyFire = true;
        Assert.IsTrue(CombatTeams.CanDamage(CombatTeam.Attackers, CombatTeam.Attackers));
    }

    [Test]
    public void NoTeam_KeepsThePracticeRules()
    {
        Assert.IsTrue(CombatTeams.CanDamage(CombatTeam.None, CombatTeam.None));
        Assert.IsTrue(CombatTeams.CanDamage(CombatTeam.None, CombatTeam.Defenders));
    }
}

public class TacticalObjectivesTests
{
    [Test]
    public void HoldPoints_PreferWaypointsInsideTheZoneNearestTheCentreFirst()
    {
        var zone = new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(14f, 3f, 14f));
        var waypoints = new List<Vector3>
        {
            new Vector3(5f, 0.05f, 0f),
            new Vector3(1f, 0.05f, 1f),
            new Vector3(60f, 0.05f, 0f),
            new Vector3(-3f, 8f, 0f)
        };

        List<Vector3> points = TacticalObjectives.HoldPoints(zone, waypoints, 2);
        Assert.AreEqual(2, points.Count);
        Assert.AreEqual(new Vector3(1f, 0.05f, 1f), points[0]);
        Assert.AreEqual(new Vector3(5f, 0.05f, 0f), points[1]);
    }

    [Test]
    public void HoldPoints_PadWithARingWhenTheMapHasTooFewWaypoints()
    {
        var zone = new Bounds(new Vector3(10f, 1.5f, 20f), new Vector3(14f, 3f, 14f));
        List<Vector3> points = TacticalObjectives.HoldPoints(zone, new List<Vector3>(), 4);
        Assert.AreEqual(4, points.Count);
        foreach (Vector3 p in points)
            Assert.LessOrEqual(TacticalObjectives.Planar(p, zone.center), 7f);
        Assert.AreNotEqual(points[0], points[1]);
    }

    [Test]
    public void YawToward_FollowsTheCompass()
    {
        Assert.AreEqual(0f, TacticalObjectives.YawToward(Vector3.zero, new Vector3(0f, 0f, 5f)), 0.01f);
        Assert.AreEqual(90f, TacticalObjectives.YawToward(Vector3.zero, new Vector3(5f, 0f, 0f)), 0.01f);
        Assert.AreEqual(180f, Mathf.Abs(TacticalObjectives.YawToward(Vector3.zero, new Vector3(0f, 0f, -5f))), 0.01f);
    }

    [Test]
    public void RingPoint_SpreadsEvenlyAroundTheCentre()
    {
        Vector3 a = TacticalObjectives.RingPoint(Vector3.zero, 5f, 0, 4);
        Vector3 b = TacticalObjectives.RingPoint(Vector3.zero, 5f, 2, 4);
        Assert.AreEqual(5f, a.magnitude, 0.001f);
        Assert.AreEqual(0f, (a + b).magnitude, 0.001f);
    }
}
