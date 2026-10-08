using System;

namespace GameServer.Domain.Combat
{
    /// <summary>Résultat d'un jet de dégâts.</summary>
    public readonly struct DamageRoll
    {
        public DamageRoll(int amount, bool isCritical)
        {
            Amount = amount;
            IsCritical = isCritical;
        }

        public int Amount { get; }

        public bool IsCritical { get; }
    }

    /// <summary>
    /// Formule unique des dégâts : tirage, critique, puis mitigation par l'armure.
    /// Toute modification de la formule passe par cette classe.
    /// </summary>
    public sealed class CombatResolver
    {
        private readonly CombatSettings _settings;
        private readonly Random _random;

        public CombatResolver(CombatSettings settings, Random random)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>Tire des dégâts dans [minDamage, maxDamage], applique le critique puis l'armure. Minimum 1.</summary>
        public DamageRoll RollDamage(int minDamage, int maxDamage, int armor)
        {
            if (minDamage > maxDamage)
            {
                throw new ArgumentException("Dégâts minimum supérieurs au maximum.");
            }

            int raw = _random.Next(minDamage, maxDamage + 1);
            bool critical = _random.NextDouble() < _settings.CriticalChance;

            float value = critical ? raw * _settings.CriticalMultiplier : raw;
            float reduction = Math.Max(0, armor) / (Math.Max(0, armor) + _settings.ArmorConstant);
            int amount = Math.Max(1, (int)MathF.Round(value * (1f - reduction)));

            return new DamageRoll(amount, critical);
        }
    }
}
