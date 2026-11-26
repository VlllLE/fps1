using System;
using UnityEngine;

public enum BotState
{
    Patrol = 0,
    Investigate,
    Chase,
    Attack,
    TakeCover,
    Dead
}

/// <summary>Everything the brain needs to know this tick. Plain data so the state machine is testable without a scene.</summary>
public struct BotSenses
{
    /// <summary>A living target exists.</summary>
    public bool targetValid;
    /// <summary>Target is inside the view cone/range and not blocked.</summary>
    public bool seesTarget;
    public float targetDistance;
    public Vector3 targetPosition;
    /// <summary>The bot heard the target or was hit this tick; <see cref="heardPosition"/> is where to look.</summary>
    public bool heardSomething;
    public Vector3 heardPosition;
    public float health01;
    public bool needsReload;
    public bool isReloading;
    /// <summary>The bot reached the position its current state wanted to go to.</summary>
    public bool atDestination;
    public bool hasCoverPoint;
}

/// <summary>
/// Pure state machine: Patrol, Investigate, Chase, Attack, TakeCover, Dead. It decides what the bot wants to do;
/// <see cref="BotController"/> performs movement, aiming and shooting.
/// </summary>
public sealed class BotBrain
{
    private readonly BotSettings settings;
    private float investigateWait;
    private float coverCooldownLeft;
    private float lostSightTime;

    public BotBrain(BotSettings settings)
    {
        this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
    }

    public BotState State { get; private set; } = BotState.Patrol;
    public float TimeInState { get; private set; }
    public bool HasLastKnownPosition { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    /// <summary>Seconds since the target was last visible (large when never seen).</summary>
    public float TimeSinceSeen { get; private set; } = 999f;
    /// <summary>Seconds of uninterrupted line of sight.</summary>
    public float TimeSeeingTarget { get; private set; }

    public bool IsAlive => State != BotState.Dead;
    public bool IsEngaged => State == BotState.Chase || State == BotState.Attack || State == BotState.TakeCover;

    /// <summary>True once the reaction time has elapsed while attacking with line of sight.</summary>
    public bool CanFire => State == BotState.Attack && TimeSeeingTarget >= settings.reactionTime;

    public event Action<BotState, BotState> StateChanged;

    public void Tick(float dt, BotSenses senses)
    {
        if (State == BotState.Dead)
            return;

        dt = Mathf.Max(0f, dt);
        TimeInState += dt;
        coverCooldownLeft = Mathf.Max(0f, coverCooldownLeft - dt);

        bool sees = senses.targetValid && senses.seesTarget;
        if (sees)
        {
            LastKnownPosition = senses.targetPosition;
            HasLastKnownPosition = true;
            TimeSinceSeen = 0f;
            TimeSeeingTarget += dt;
            lostSightTime = 0f;
        }
        else
        {
            TimeSinceSeen += dt;
            TimeSeeingTarget = 0f;
            lostSightTime += dt;
        }

        if (!senses.targetValid)
        {
            if (IsEngaged || State == BotState.Investigate)
                SetState(BotState.Patrol);
            return;
        }

        bool heard = senses.heardSomething && !sees;

        switch (State)
        {
            case BotState.Patrol:
                if (sees)
                    Engage(senses);
                else if (heard)
                    BeginInvestigate(senses.heardPosition);
                break;

            case BotState.Investigate:
                if (sees)
                {
                    Engage(senses);
                }
                else if (heard)
                {
                    BeginInvestigate(senses.heardPosition);
                }
                else if (senses.atDestination)
                {
                    investigateWait += dt;
                    if (investigateWait >= settings.investigateTime)
                        SetState(BotState.Patrol);
                }
                break;

            case BotState.Chase:
                if (sees)
                {
                    if (senses.targetDistance <= settings.maxEngageRange)
                        SetState(BotState.Attack);
                }
                else if (TimeSinceSeen > settings.memoryTime)
                {
                    SetState(BotState.Patrol);
                }
                else if (senses.atDestination)
                {
                    BeginInvestigate(LastKnownPosition);
                }
                break;

            case BotState.Attack:
                if (lostSightTime > settings.lostSightGrace)
                {
                    SetState(BotState.Chase);
                }
                else if (sees && senses.targetDistance > settings.maxEngageRange * 1.15f)
                {
                    SetState(BotState.Chase);
                }
                else if (ShouldTakeCover(senses))
                {
                    SetState(BotState.TakeCover);
                }
                break;

            case BotState.TakeCover:
                bool coverDone = TimeInState >= settings.coverDuration && !senses.isReloading;
                if (!senses.hasCoverPoint && TimeInState >= 0.4f)
                    coverDone = true;
                if (coverDone)
                {
                    coverCooldownLeft = settings.coverCooldown;
                    if (sees)
                        Engage(senses);
                    else
                        SetState(BotState.Chase);
                }
                break;
        }
    }

    /// <summary>Marks the bot dead; it stays dead until <see cref="Revive"/>.</summary>
    public void Kill()
    {
        SetState(BotState.Dead);
    }

    public void Revive()
    {
        HasLastKnownPosition = false;
        TimeSinceSeen = 999f;
        TimeSeeingTarget = 0f;
        lostSightTime = 0f;
        coverCooldownLeft = 0f;
        investigateWait = 0f;
        SetState(BotState.Patrol, true);
    }

    /// <summary>Sends a living bot to look at a position (a gunshot, a hit from an unseen shooter).</summary>
    public void Investigate(Vector3 position)
    {
        if (State == BotState.Dead || State == BotState.Attack || State == BotState.TakeCover)
            return;
        BeginInvestigate(position);
    }

    private bool ShouldTakeCover(BotSenses senses)
    {
        if (!settings.useCover || coverCooldownLeft > 0f || !senses.hasCoverPoint)
            return false;
        if (senses.health01 <= settings.coverHealthFraction)
            return true;
        return senses.needsReload && !senses.isReloading;
    }

    private void Engage(BotSenses senses)
    {
        SetState(senses.targetDistance <= settings.maxEngageRange ? BotState.Attack : BotState.Chase);
    }

    private void BeginInvestigate(Vector3 position)
    {
        LastKnownPosition = position;
        HasLastKnownPosition = true;
        investigateWait = 0f;
        SetState(BotState.Investigate, true);
    }

    private void SetState(BotState next, bool force = false)
    {
        if (next == State && !force)
            return;

        BotState previous = State;
        State = next;
        TimeInState = 0f;
        if (next != BotState.Investigate)
            investigateWait = 0f;
        if (next == BotState.Dead)
        {
            TimeSeeingTarget = 0f;
            lostSightTime = 0f;
        }

        if (previous != next)
            StateChanged?.Invoke(previous, next);
    }
}
