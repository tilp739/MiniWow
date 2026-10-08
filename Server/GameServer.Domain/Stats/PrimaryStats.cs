namespace GameServer.Domain.Stats
{
    /// <summary>Caractéristiques primaires. Les valeurs dérivées sont calculées par <see cref="StatCalculator"/>.</summary>
    public readonly struct PrimaryStats
    {
        public PrimaryStats(int strength, int agility, int intelligence, int stamina, int spirit)
        {
            Strength = strength;
            Agility = agility;
            Intelligence = intelligence;
            Stamina = stamina;
            Spirit = spirit;
        }

        public int Strength { get; }

        public int Agility { get; }

        public int Intelligence { get; }

        public int Stamina { get; }

        public int Spirit { get; }

        public PrimaryStats Add(PrimaryStats other) =>
            new PrimaryStats(
                Strength + other.Strength,
                Agility + other.Agility,
                Intelligence + other.Intelligence,
                Stamina + other.Stamina,
                Spirit + other.Spirit);
    }

    /// <summary>Bonus de combat fournis par l'équipement (M6). Zéro tant qu'aucun objet n'est équipé.</summary>
    public readonly struct CombatBonuses
    {
        public CombatBonuses(int armor, PrimaryStats stats)
        {
            Armor = armor;
            Stats = stats;
        }

        public int Armor { get; }

        public PrimaryStats Stats { get; }

        public static CombatBonuses None => new CombatBonuses(0, new PrimaryStats(0, 0, 0, 0, 0));
    }

    /// <summary>Statistiques dérivées : toutes les formules de statistiques sont ici, nulle part ailleurs.</summary>
    public readonly struct DerivedStats
    {
        public DerivedStats(int maxHealth, int maxResource, float attackPower, float spellPower, int armor, float criticalChance)
        {
            MaxHealth = maxHealth;
            MaxResource = maxResource;
            AttackPower = attackPower;
            SpellPower = spellPower;
            Armor = armor;
            CriticalChance = criticalChance;
        }

        public int MaxHealth { get; }

        public int MaxResource { get; }

        public float AttackPower { get; }

        public float SpellPower { get; }

        public int Armor { get; }

        public float CriticalChance { get; }
    }

    public static class StatCalculator
    {
        private const int BaseHealth = 50;
        private const int HealthPerStamina = 10;
        private const int BaseResource = 100;
        private const float AttackPowerPerStrength = 2f;
        private const float AttackPowerPerAgility = 1f;
        private const float SpellPowerPerIntelligence = 2f;
        private const float CriticalPerAgility = 0.001f;
        private const float MaxCriticalChance = 0.5f;

        /// <summary>Calcule les statistiques dérivées à partir des primaires et des bonus d'équipement.</summary>
        public static DerivedStats Derive(PrimaryStats primary, CombatBonuses bonuses)
        {
            PrimaryStats total = primary.Add(bonuses.Stats);

            int maxHealth = BaseHealth + total.Stamina * HealthPerStamina;
            int maxResource = BaseResource;
            float attackPower = total.Strength * AttackPowerPerStrength + total.Agility * AttackPowerPerAgility;
            float spellPower = total.Intelligence * SpellPowerPerIntelligence;
            float critical = System.Math.Min(MaxCriticalChance, total.Agility * CriticalPerAgility);

            return new DerivedStats(maxHealth, maxResource, attackPower, spellPower, bonuses.Armor, critical);
        }
    }
}
