using System.Collections.Generic;
using FpsTactical;
using NUnit.Framework;

public class TacticalHudTextTests
{
    [Test]
    public void ResolveScale_IsOneAtLandscape720p()
    {
        Assert.AreEqual(1f, TacticalHudText.ResolveScale(1280f, 720f), 0.001f);
    }

    [Test]
    public void ResolveScale_PortraitPhone_KeepsEnoughVirtualWidth()
    {
        float scale = TacticalHudText.ResolveScale(720f, 1280f);
        Assert.Less(scale, 1.5f);
        Assert.GreaterOrEqual(720f / scale, TacticalHudText.MinUiWidth - 0.01f);

        float narrow = TacticalHudText.ResolveScale(400f, 800f);
        Assert.GreaterOrEqual(400f / narrow, TacticalHudText.MinUiWidth - 0.01f);
        Assert.Less(narrow, scale);
    }

    [Test]
    public void ResolveScale_IsClampedAndSafeForEmptyScreens()
    {
        Assert.AreEqual(0.5f, TacticalHudText.ResolveScale(100f, 100f), 0.001f);
        Assert.AreEqual(1f, TacticalHudText.ResolveScale(0f, 0f), 0.001f);
        Assert.AreEqual(2f, TacticalHudText.ResolveScale(3840f, 1440f), 0.001f);
    }

    [TestCase(0f, "0:00")]
    [TestCase(-5f, "0:00")]
    [TestCase(0.1f, "0:01")]
    [TestCase(9.9f, "0:10")]
    [TestCase(59.2f, "1:00")]
    [TestCase(100f, "1:40")]
    [TestCase(15f, "0:15")]
    public void Clock_RoundsUpToWholeSeconds(float seconds, string expected)
    {
        Assert.AreEqual(expected, TacticalHudText.Clock(seconds));
    }

    [Test]
    public void BombLine_ReadsPerState()
    {
        Assert.AreEqual("YOU HAVE THE BOMB", TacticalHudText.BombLine(BombState.Carried, null, true, null, true, 40f));
        Assert.AreEqual("ALPHA 2 HAS THE BOMB", TacticalHudText.BombLine(BombState.Carried, null, false, "ALPHA 2", true, 40f));
        Assert.AreEqual("BOMB NOT PLANTED", TacticalHudText.BombLine(BombState.Carried, null, false, "ALPHA 2", false, 40f));
        Assert.AreEqual("BOMB DROPPED", TacticalHudText.BombLine(BombState.Dropped, null, false, null, true, 40f));
        Assert.AreEqual("BOMB PLANTED B  0:25", TacticalHudText.BombLine(BombState.Planted, "B", false, null, false, 24.2f));
        Assert.AreEqual("BOMB DEFUSED", TacticalHudText.BombLine(BombState.Defused, "A", false, null, false, 10f));
        Assert.AreEqual("BOMB EXPLODED", TacticalHudText.BombLine(BombState.Exploded, "A", false, null, false, 0f));
    }

    [Test]
    public void ActionHint_PrefersPlantingAndIsNullWithoutAnAction()
    {
        Assert.AreEqual("HOLD E TO PLANT", TacticalHudText.ActionHint(true, false));
        Assert.AreEqual("HOLD E TO DEFUSE", TacticalHudText.ActionHint(false, true));
        Assert.IsNull(TacticalHudText.ActionHint(false, false));
    }

    [Test]
    public void ProgressLabel_DistinguishesTheLocalPlayerFromOthers()
    {
        Assert.AreEqual("PLANTING", TacticalHudText.ProgressLabel(true, true));
        Assert.AreEqual("BOMB BEING PLANTED", TacticalHudText.ProgressLabel(true, false));
        Assert.AreEqual("DEFUSING", TacticalHudText.ProgressLabel(false, true));
        Assert.AreEqual("BOMB BEING DEFUSED", TacticalHudText.ProgressLabel(false, false));
    }

    [Test]
    public void Titles_AndNames()
    {
        Assert.AreEqual("ROUND WON", TacticalHudText.RoundTitle(true));
        Assert.AreEqual("ROUND LOST", TacticalHudText.RoundTitle(false));
        Assert.AreEqual("VICTORY", TacticalHudText.MatchTitle(true));
        Assert.AreEqual("DEFEAT", TacticalHudText.MatchTitle(false));
        Assert.AreEqual("ATTACK", TacticalHudText.SideName(CombatTeam.Attackers));
        Assert.AreEqual("DEFEND", TacticalHudText.SideName(CombatTeam.Defenders));
        Assert.AreEqual("ALPHA", TacticalHudText.SquadName(0));
        Assert.AreEqual("BRAVO", TacticalHudText.SquadName(1));
        Assert.AreEqual("CLASS SELECT", TacticalHudText.PhaseLabel(TacticalPhase.ClassSelect));
        Assert.AreEqual("LIVE", TacticalHudText.PhaseLabel(TacticalPhase.Live));
    }
}

public class TacticalStatsTests
{
    private static TacticalStats NewStats(out TacticalParticipant you, out TacticalParticipant mate,
        out TacticalParticipant foe, out TacticalParticipant foe2)
    {
        var stats = new TacticalStats();
        you = stats.Add("You", 0, true, null);
        mate = stats.Add("Alpha 2", 0, false, null);
        foe = stats.Add("Bravo 1", 1, false, null);
        foe2 = stats.Add("Bravo 2", 1, false, null);
        return stats;
    }

    [Test]
    public void RecordDeath_CountsKillAndDeath()
    {
        var stats = NewStats(out var you, out _, out var foe, out _);
        stats.RecordDeath(foe, you, true, 5f);

        Assert.AreEqual(1, you.Kills);
        Assert.AreEqual(1, foe.Deaths);
        Assert.AreEqual(0, foe.Kills);
        Assert.AreEqual(1, stats.KillFeed.Count);
        Assert.AreEqual("You", stats.KillFeed[0].Killer);
        Assert.AreEqual("Bravo 1", stats.KillFeed[0].Victim);
        Assert.IsTrue(stats.KillFeed[0].Headshot);
        Assert.AreEqual(5f, stats.KillFeed[0].Time);
    }

    [Test]
    public void RecordDeath_WithoutAKiller_OnlyCountsTheDeath()
    {
        var stats = NewStats(out var you, out var mate, out _, out _);
        stats.RecordDeath(mate, null, false, 1f);

        Assert.AreEqual(1, mate.Deaths);
        Assert.AreEqual(0, you.Kills);
        Assert.IsNull(stats.KillFeed[0].Killer);
    }

    [Test]
    public void RecordDeath_TeamKillSubtractsAndSuicideDoesNotScore()
    {
        var stats = NewStats(out var you, out var mate, out _, out _);
        stats.RecordDeath(mate, you, false, 1f);
        Assert.AreEqual(-1, you.Kills);

        stats.RecordDeath(you, you, false, 2f);
        Assert.AreEqual(-1, you.Kills, "a suicide is a death without a kill");
        Assert.AreEqual(1, you.Deaths);
        Assert.IsNull(stats.KillFeed[1].Killer);
    }

    [Test]
    public void KillFeed_KeepsTheLastEntriesAndFiresEvents()
    {
        var stats = NewStats(out var you, out _, out var foe, out _);
        int events = 0;
        stats.KillRecorded += _ => events++;
        for (int i = 0; i < TacticalStats.MaxFeedEntries + 3; i++)
            stats.RecordDeath(foe, you, false, i);

        Assert.AreEqual(TacticalStats.MaxFeedEntries, stats.KillFeed.Count);
        Assert.AreEqual(TacticalStats.MaxFeedEntries + 3, events);
        Assert.AreEqual(3f, stats.KillFeed[0].Time, "the oldest entries drop off");
    }

    [Test]
    public void ResetScores_KeepsFightersButClearsNumbersAndFeed()
    {
        var stats = NewStats(out var you, out _, out var foe, out _);
        stats.RecordDeath(foe, you, false, 1f);
        stats.ResetScores();

        Assert.AreEqual(4, stats.Participants.Count);
        Assert.AreEqual(0, you.Kills);
        Assert.AreEqual(0, foe.Deaths);
        Assert.AreEqual(0, stats.KillFeed.Count);
    }

    [Test]
    public void CollectSquad_SortsByKillsThenFewerDeaths()
    {
        var stats = NewStats(out var you, out var mate, out var foe, out var foe2);
        stats.RecordDeath(foe, mate, false, 1f);
        stats.RecordDeath(foe2, mate, false, 2f);
        stats.RecordDeath(foe, you, false, 3f);

        var rows = new List<TacticalParticipant>();
        stats.CollectSquad(0, rows);
        Assert.AreEqual(2, rows.Count);
        Assert.AreSame(mate, rows[0]);
        Assert.AreSame(you, rows[1]);

        stats.CollectSquad(1, rows);
        Assert.AreEqual(2, rows.Count);
        Assert.AreSame(foe2, rows[0], "equal kills, fewer deaths first");
    }

    [Test]
    public void FindPlayer_AndFindBot_ResolveParticipants()
    {
        var stats = NewStats(out var you, out _, out _, out _);
        Assert.AreSame(you, stats.FindPlayer());
        Assert.IsNull(stats.FindBot(null));
    }
}
