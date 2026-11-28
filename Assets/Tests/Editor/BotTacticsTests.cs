using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>Peek timer, peek/cover transitions of the brain, reaction jitter, flinch and the difficulty ladder.</summary>
public class BotTacticsTests
{
    private BotSettings settings;
    private BotBrain brain;

    [SetUp]
    public void SetUp()
    {
        settings = BotSettings.CreatePreset(BotDifficulty.Normal);
        settings.reactionTime = 0.5f;
        settings.minStateTime = 0.6f;
        settings.coverDuration = 2f;
        settings.coverHealthFraction = 0.3f;
        settings.usePeek = true;
        settings.peekExposeMin = 2f;
        settings.peekExposeMax = 3f;
        settings.peekHideMin = 1f;
        settings.peekHideMax = 2f;
        brain = new BotBrain(settings);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(settings);
    }

    private static BotSenses Seen(float distance = 15f)
    {
        return new BotSenses
        {
            targetValid = true,
            seesTarget = true,
            targetDistance = distance,
            targetPosition = new Vector3(0f, 0f, distance),
            health01 = 1f
        };
    }

    private static BotSenses PeekWanted(float hide = 1f, bool cover = true)
    {
        BotSenses senses = Seen();
        senses.hasCoverPoint = cover;
        senses.peekDue = true;
        senses.peekHideTime = hide;
        return senses;
    }

    #region Peek timer

    [Test]
    public void PeekTimer_BecomesDueAfterTheExposureLimit()
    {
        var timer = new BotPeekTimer(settings, new System.Random(3));
        Assert.That(timer.Limit, Is.InRange(settings.peekExposeMin, settings.peekExposeMax));
        Assert.That(timer.Due, Is.False);

        timer.Tick(timer.Limit - 0.1f, true);
        Assert.That(timer.Due, Is.False);

        timer.Tick(0.2f, true);
        Assert.That(timer.Due, Is.True);
    }

    [Test]
    public void PeekTimer_PaysExposureBackAtHalfSpeedOutOfSight()
    {
        var timer = new BotPeekTimer(settings, new System.Random(3));
        timer.Tick(1f, true);
        timer.Tick(1f, false);
        Assert.That(timer.Exposed, Is.EqualTo(0.5f).Within(0.001f));

        timer.Tick(5f, false);
        Assert.That(timer.Exposed, Is.EqualTo(0f));
    }

    [Test]
    public void PeekTimer_RestartClearsExposureAndRollsANewLimit()
    {
        var timer = new BotPeekTimer(settings, new System.Random(9));
        timer.Tick(10f, true);
        Assert.That(timer.Due, Is.True);

        timer.Restart();
        Assert.That(timer.Exposed, Is.EqualTo(0f));
        Assert.That(timer.Due, Is.False);
        Assert.That(timer.Limit, Is.InRange(settings.peekExposeMin, settings.peekExposeMax));
    }

    [Test]
    public void PeekTimer_NeverDueWhenPeekingIsOff()
    {
        settings.usePeek = false;
        var timer = new BotPeekTimer(settings, new System.Random(1));
        timer.Tick(60f, true);
        Assert.That(timer.Due, Is.False);
    }

    [Test]
    public void PeekTimer_HideTimeStaysInTheConfiguredBand()
    {
        var timer = new BotPeekTimer(settings, new System.Random(5));
        for (int i = 0; i < 50; i++)
            Assert.That(timer.RollHideTime(), Is.InRange(settings.peekHideMin, settings.peekHideMax));
    }

    [Test]
    public void PeekTimer_RejectsMissingSettings()
    {
        Assert.Throws<ArgumentNullException>(() => new BotPeekTimer(null, new System.Random(1)));
    }

    #endregion

    #region Brain peeking

    [Test]
    public void Brain_DucksBehindCoverWhenAPeekIsDue()
    {
        brain.Tick(0.1f, Seen());
        Assert.That(brain.State, Is.EqualTo(BotState.Attack));

        brain.Tick(0.7f, PeekWanted());

        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover));
        Assert.That(brain.CoverIsPeek, Is.True);
    }

    [Test]
    public void Brain_DoesNotPeekWithoutACoverPoint()
    {
        brain.Tick(0.1f, Seen());
        brain.Tick(0.7f, PeekWanted(1f, false));

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void Brain_DoesNotPeekWhenDisabledOrTooSoonAfterEngaging()
    {
        brain.Tick(0.1f, Seen());
        brain.Tick(0.3f, PeekWanted());
        Assert.That(brain.State, Is.EqualTo(BotState.Attack), "min state time not reached");

        settings.usePeek = false;
        brain.Tick(0.5f, PeekWanted());
        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void Brain_PeekEndsAfterTheHideTime_AndReturnsToTheFight()
    {
        brain.Tick(0.1f, Seen());
        brain.Tick(0.7f, PeekWanted(1f));
        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover));

        BotSenses hiding = Seen();
        hiding.hasCoverPoint = true;
        hiding.atCoverPoint = true;
        brain.Tick(0.6f, hiding);
        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover), "still hiding");

        brain.Tick(0.5f, hiding);
        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
        Assert.That(brain.CoverIsPeek, Is.False);
    }

    [Test]
    public void Brain_PeekHideTimeOnlyRunsOnceTheBotIsAtItsCover()
    {
        brain.Tick(0.1f, Seen());
        brain.Tick(0.7f, PeekWanted(1f));

        BotSenses running = Seen();
        running.hasCoverPoint = true;
        brain.Tick(2.0f, running);
        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover), "still on the way to cover");

        running.atCoverPoint = true;
        brain.Tick(0.9f, running);
        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover));
        brain.Tick(0.2f, running);
        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void Brain_PeekGivesUpWhenTheCoverIsNeverReached()
    {
        brain.Tick(0.1f, Seen());
        brain.Tick(0.7f, PeekWanted(1f));

        BotSenses lost = Seen();
        lost.hasCoverPoint = true;
        brain.Tick(1f + BotBrain.MaxPeekTravelTime + 0.1f, lost);

        Assert.That(brain.State, Is.EqualTo(BotState.Attack));
    }

    [Test]
    public void Brain_PeekEndingWithoutSightChasesTheLastKnownPosition()
    {
        brain.Tick(0.1f, Seen());
        brain.Tick(0.7f, PeekWanted(0.5f));

        var blind = new BotSenses { targetValid = true, health01 = 1f, hasCoverPoint = true, atCoverPoint = true };
        brain.Tick(0.6f, blind);

        Assert.That(brain.State, Is.EqualTo(BotState.Chase));
    }

    [Test]
    public void Brain_PeekDoesNotUseUpTheHealthCoverCooldown()
    {
        settings.coverCooldown = 30f;
        brain.Tick(0.1f, Seen());
        brain.Tick(0.7f, PeekWanted(0.5f));
        BotSenses hiding = Seen();
        hiding.hasCoverPoint = true;
        hiding.atCoverPoint = true;
        brain.Tick(0.6f, hiding);
        Assert.That(brain.State, Is.EqualTo(BotState.Attack));

        BotSenses hurt = Seen();
        hurt.hasCoverPoint = true;
        hurt.health01 = 0.1f;
        brain.Tick(0.1f, hurt);

        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover));
        Assert.That(brain.CoverIsPeek, Is.False);
    }

    [Test]
    public void Brain_HealthCoverStillLastsTheFullCoverDuration()
    {
        BotSenses hurt = Seen();
        hurt.hasCoverPoint = true;
        hurt.health01 = 0.1f;
        brain.Tick(0.1f, hurt);
        brain.Tick(0.7f, hurt);
        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover));
        Assert.That(brain.CoverIsPeek, Is.False);

        brain.Tick(1.0f, hurt);
        Assert.That(brain.State, Is.EqualTo(BotState.TakeCover), "2 s of cover, not the short peek time");
    }

    [Test]
    public void Brain_ReactionScaleStretchesTheFirstShotDelay()
    {
        brain.ReactionScale = 2f;
        brain.Tick(0.1f, Seen());
        brain.Tick(0.6f, Seen());
        Assert.That(brain.CanFire, Is.False, "0.5 s reaction is 1 s with scale 2");

        brain.Tick(0.4f, Seen());
        Assert.That(brain.CanFire, Is.True);
    }

    [Test]
    public void Brain_RevivingResetsReactionScaleAndPeekState()
    {
        brain.ReactionScale = 3f;
        brain.Kill();
        brain.Revive();

        Assert.That(brain.ReactionScale, Is.EqualTo(1f));
        Assert.That(brain.CoverIsPeek, Is.False);
    }

    #endregion

    #region Flinch

    [Test]
    public void Flinch_RaisesTheAimErrorRightAfterAHit()
    {
        settings.flinchError = 4f;
        var aim = new BotAimModel(settings);
        float calm = aim.ComputeInaccuracy(0f, 0f);

        aim.NotifyHit();

        Assert.That(aim.Flinch, Is.EqualTo(1f));
        Assert.That(aim.ComputeInaccuracy(0f, 0f), Is.EqualTo(calm + 4f).Within(0.001f));
    }

    [Test]
    public void Flinch_FadesOverTheFlinchTime()
    {
        settings.flinchError = 4f;
        settings.flinchTime = 0.5f;
        var aim = new BotAimModel(settings);
        aim.NotifyHit();

        aim.Track(Vector3.forward, 0.25f);
        Assert.That(aim.Flinch, Is.EqualTo(0.5f).Within(0.001f));

        aim.Track(Vector3.forward, 0.3f);
        Assert.That(aim.Flinch, Is.EqualTo(0f));
    }

    [Test]
    public void Flinch_FadesEvenWithoutATarget()
    {
        settings.flinchTime = 0.5f;
        var aim = new BotAimModel(settings);
        aim.NotifyHit();

        aim.Track(Vector3.zero, 0.6f);

        Assert.That(aim.Flinch, Is.EqualTo(0f));
    }

    [Test]
    public void Flinch_CanBeCleared()
    {
        var aim = new BotAimModel(settings);
        aim.NotifyHit();
        aim.ClearFlinch();
        Assert.That(aim.Flinch, Is.EqualTo(0f));
    }

    #endregion

    #region Difficulty ladder

    [Test]
    public void Difficulty_ReactionAndAimImproveFromEasyToVeteran()
    {
        BotDifficulty[] levels = { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard, BotDifficulty.Veteran };
        BotSettings previous = null;
        foreach (BotDifficulty level in levels)
        {
            BotSettings current = BotSettings.CreatePreset(level);
            try
            {
                if (previous != null)
                {
                    Assert.Less(current.reactionTime, previous.reactionTime, level + " reacts faster");
                    Assert.Less(current.reactionJitter, previous.reactionJitter, level + " is steadier");
                    Assert.Less(current.aimErrorDegrees, previous.aimErrorDegrees, level + " aims better");
                    Assert.Less(current.flinchError, previous.flinchError, level + " flinches less");
                    Assert.Less(current.flinchTime, previous.flinchTime, level + " recovers faster");
                    Assert.Greater(current.aimTurnSpeed, previous.aimTurnSpeed, level + " turns faster");
                    Assert.Greater(current.teamwork, previous.teamwork, level + " plays better with the team");
                    Assert.LessOrEqual(current.peekExposeMax, previous.peekExposeMax, level + " ducks out sooner");
                }
            }
            finally
            {
                if (previous != null)
                    UnityEngine.Object.DestroyImmediate(previous);
                previous = current;
            }
        }

        UnityEngine.Object.DestroyImmediate(previous);
    }

    [Test]
    public void Difficulty_EasyBotsDoNotPeekOrHugWallsOrCoordinate()
    {
        BotSettings easy = BotSettings.CreatePreset(BotDifficulty.Easy);
        try
        {
            Assert.That(easy.usePeek, Is.False);
            Assert.That(easy.preferWalls, Is.False);
            Assert.That(easy.teamwork, Is.EqualTo(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(easy);
        }
    }

    [TestCase(BotDifficulty.Normal)]
    [TestCase(BotDifficulty.Hard)]
    [TestCase(BotDifficulty.Veteran)]
    public void Difficulty_SkilledBotsPeekAndHugWalls(BotDifficulty level)
    {
        BotSettings preset = BotSettings.CreatePreset(level);
        try
        {
            Assert.That(preset.usePeek, Is.True);
            Assert.That(preset.preferWalls, Is.True);
            Assert.That(preset.teamwork, Is.GreaterThan(0f));
            Assert.That(preset.peekExposeMax, Is.GreaterThanOrEqualTo(preset.peekExposeMin));
            Assert.That(preset.peekHideMax, Is.GreaterThanOrEqualTo(preset.peekHideMin));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    #endregion
}
