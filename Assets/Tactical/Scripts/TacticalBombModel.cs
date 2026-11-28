using System;

namespace FpsTactical
{
    public enum BombState
    {
        /// <summary>An attacker holds the bomb (or nobody has been given it yet).</summary>
        Carried,
        /// <summary>Lying on the floor after its carrier died.</summary>
        Dropped,
        Planted,
        Defused,
        Exploded
    }

    /// <summary>
    /// Plant, defuse and fuse timing of the bomb without any scene object. Planting needs an uninterrupted hold inside a
    /// site, defusing an uninterrupted hold next to the planted bomb, and the fuse runs once the bomb is planted.
    /// </summary>
    public sealed class TacticalBombModel
    {
        private readonly float plantSeconds;
        private readonly float defuseSeconds;
        private readonly float fuseSeconds;

        public TacticalBombModel(float plantSeconds, float defuseSeconds, float fuseSeconds)
        {
            this.plantSeconds = Math.Max(0.01f, plantSeconds);
            this.defuseSeconds = Math.Max(0.01f, defuseSeconds);
            this.fuseSeconds = Math.Max(0.01f, fuseSeconds);
            Reset();
        }

        public BombState State { get; private set; }
        public string Site { get; private set; }
        public float PlantProgress { get; private set; }
        public float DefuseProgress { get; private set; }
        public float FuseRemaining { get; private set; }
        public bool IsPlanting { get; private set; }
        public bool IsDefusing { get; private set; }

        public float PlantProgress01 => PlantProgress / plantSeconds;
        public float DefuseProgress01 => DefuseProgress / defuseSeconds;
        public float FuseElapsed01 => State == BombState.Planted || State == BombState.Exploded || State == BombState.Defused
            ? 1f - Math.Max(0f, FuseRemaining) / fuseSeconds
            : 0f;

        public bool IsPlanted => State == BombState.Planted;
        public bool IsResolved => State == BombState.Defused || State == BombState.Exploded;

        /// <summary>Seconds a defuse started right now would need; compared with <see cref="FuseRemaining"/> by the bots.</summary>
        public float DefuseSeconds => defuseSeconds;

        public event Action<string> Planted;
        public event Action Defused;
        public event Action Exploded;

        public void Reset()
        {
            State = BombState.Carried;
            Site = null;
            PlantProgress = 0f;
            DefuseProgress = 0f;
            FuseRemaining = fuseSeconds;
            IsPlanting = false;
            IsDefusing = false;
        }

        /// <summary>Plants the bomb at once, skipping the hold (tests and scripted events).</summary>
        public void Plant(string site)
        {
            if (State != BombState.Carried && State != BombState.Dropped)
                return;
            State = BombState.Planted;
            Site = site;
            PlantProgress = plantSeconds;
            FuseRemaining = fuseSeconds;
            IsPlanting = false;
            Planted?.Invoke(site);
        }

        public void Drop()
        {
            if (State != BombState.Carried)
                return;
            State = BombState.Dropped;
            PlantProgress = 0f;
            IsPlanting = false;
        }

        public void PickUp()
        {
            if (State == BombState.Dropped)
                State = BombState.Carried;
        }

        /// <summary>
        /// Advances the bomb by <paramref name="dt"/>. <paramref name="plantSite"/> is the site the carrier stands in
        /// (null outside any site), <paramref name="planting"/> and <paramref name="defusing"/> say whether the action is held.
        /// </summary>
        public void Tick(float dt, string plantSite, bool planting, bool defusing)
        {
            dt = Math.Max(0f, dt);
            IsPlanting = false;
            IsDefusing = false;

            switch (State)
            {
                case BombState.Carried:
                    if (planting && !string.IsNullOrEmpty(plantSite))
                    {
                        IsPlanting = true;
                        PlantProgress += dt;
                        if (PlantProgress >= plantSeconds)
                        {
                            PlantProgress = plantSeconds;
                            State = BombState.Planted;
                            Site = plantSite;
                            FuseRemaining = fuseSeconds;
                            IsPlanting = false;
                            Planted?.Invoke(plantSite);
                        }
                    }
                    else
                    {
                        PlantProgress = 0f;
                    }

                    break;

                case BombState.Planted:
                    FuseRemaining -= dt;
                    if (defusing)
                    {
                        IsDefusing = true;
                        DefuseProgress += dt;
                        if (DefuseProgress >= defuseSeconds)
                        {
                            DefuseProgress = defuseSeconds;
                            State = BombState.Defused;
                            IsDefusing = false;
                            Defused?.Invoke();
                            break;
                        }
                    }
                    else
                    {
                        DefuseProgress = 0f;
                    }

                    if (FuseRemaining <= 0f)
                    {
                        FuseRemaining = 0f;
                        State = BombState.Exploded;
                        IsDefusing = false;
                        Exploded?.Invoke();
                    }

                    break;
            }
        }
    }
}
