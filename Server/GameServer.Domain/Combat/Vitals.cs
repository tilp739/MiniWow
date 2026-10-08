using System;

namespace GameServer.Domain.Combat
{
    /// <summary>Points de vie d'une entité. Les dégâts sont plafonnés à la vie restante.</summary>
    public sealed class Vitals
    {
        public Vitals(int maxHealth)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), "La vie maximale doit être positive.");
            }

            MaxHealth = maxHealth;
            Current = maxHealth;
        }

        public int MaxHealth { get; private set; }

        public int Current { get; private set; }

        public bool IsAlive => Current > 0;

        /// <summary>Applique des dégâts et retourne la quantité réellement retirée.</summary>
        public int ApplyDamage(int amount)
        {
            if (!IsAlive || amount <= 0)
            {
                return 0;
            }

            int applied = Math.Min(amount, Current);
            Current -= applied;
            return applied;
        }

        public void Restore()
        {
            Current = MaxHealth;
        }

        /// <summary>Soigne sans dépasser le maximum ; retourne la quantité réellement rendue.</summary>
        public int Heal(int amount)
        {
            if (!IsAlive || amount <= 0)
            {
                return 0;
            }

            int applied = System.Math.Min(amount, MaxHealth - Current);
            Current += applied;
            return applied;
        }

        /// <summary>Change le maximum en conservant la proportion de vie restante (équipement, niveau).</summary>
        public void SetMaxHealth(int maxHealth)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            }

            float ratio = (float)Current / MaxHealth;
            MaxHealth = maxHealth;
            Current = IsAlive ? System.Math.Max(1, (int)System.MathF.Round(maxHealth * ratio)) : 0;
        }
    }
}
