using System;
using UnityEngine;

/// <summary>
/// Pure timer behind the shoot-hide-peek rhythm: it adds up the seconds a bot stands in the enemy's sight while attacking
/// and becomes <see cref="Due"/> once the (randomised) exposure limit is reached. Time spent out of sight pays the
/// exposure back at half speed so a short break does not reset a long fight.
/// </summary>
public sealed class BotPeekTimer
{
    private readonly BotSettings settings;
    private readonly System.Random rng;
    private float limit;

    public BotPeekTimer(BotSettings settings, System.Random rng)
    {
        this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        this.rng = rng ?? new System.Random();
        Restart();
    }

    /// <summary>Seconds in the open since the last peek.</summary>
    public float Exposed { get; private set; }

    /// <summary>Exposure at which the next ducking is due.</summary>
    public float Limit => limit;

    public bool Due => settings.usePeek && Exposed >= limit;

    public void Tick(float dt, bool exposedNow)
    {
        dt = Mathf.Max(0f, dt);
        Exposed = exposedNow ? Exposed + dt : Mathf.Max(0f, Exposed - dt * 0.5f);
    }

    /// <summary>Starts a new cycle with a fresh random exposure limit.</summary>
    public void Restart()
    {
        Exposed = 0f;
        limit = Lerp(settings.peekExposeMin, settings.peekExposeMax);
    }

    /// <summary>Random time to stay behind cover.</summary>
    public float RollHideTime()
    {
        return Lerp(settings.peekHideMin, settings.peekHideMax);
    }

    private float Lerp(float min, float max)
    {
        return Mathf.Lerp(min, Mathf.Max(min, max), (float)rng.NextDouble());
    }
}
