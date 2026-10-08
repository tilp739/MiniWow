using System;

namespace GameServer.Domain.Combat
{
    public enum ResourceKind : byte
    {
        /// <summary>Colère : générée par le combat, se dissipe hors combat.</summary>
        Rage = 1,

        /// <summary>Mana : régénère avec le temps, dépensée par les sorts.</summary>
        Mana = 2,
    }

    /// <summary>Ressource de classe (mana ou colère), toujours bornée entre 0 et le maximum.</summary>
    public sealed class ResourcePool
    {
        private readonly float _regenPerSecond;

        public ResourcePool(ResourceKind kind, int max, float regenPerSecond, int initial)
        {
            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max));
            }

            Kind = kind;
            Max = max;
            _regenPerSecond = regenPerSecond;
            Current = Math.Clamp(initial, 0, max);
        }

        public ResourceKind Kind { get; }

        public int Max { get; private set; }

        public int Current { get; private set; }

        public bool CanSpend(int amount) => amount >= 0 && Current >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            Current -= amount;
            return true;
        }

        public void Gain(int amount)
        {
            if (amount > 0)
            {
                Current = Math.Min(Max, Current + amount);
            }
        }

        /// <summary>Régénération (mana) ou dissipation (colère) sur un intervalle.</summary>
        public void Tick(float deltaSeconds)
        {
            float delta = _regenPerSecond * deltaSeconds;
            if (delta == 0f)
            {
                return;
            }

            float next = Current + delta;
            Current = (int)Math.Clamp(MathF.Floor(next), 0f, Max);
        }

        /// <summary>Met à jour le maximum (après un changement d'équipement ou de niveau) sans dépasser la nouvelle borne.</summary>
        public void SetMax(int max)
        {
            Max = Math.Max(1, max);
            Current = Math.Min(Current, Max);
        }

        public void Fill()
        {
            Current = Max;
        }
    }
}
