namespace GameServer.Domain.Combat
{
    /// <summary>Constantes globales de la formule de combat.</summary>
    public sealed class CombatSettings
    {
        /// <summary>Probabilité d'un coup critique, entre 0 et 1.</summary>
        public float CriticalChance { get; set; } = 0.05f;

        /// <summary>Multiplicateur de dégâts d'un coup critique.</summary>
        public float CriticalMultiplier { get; set; } = 1.5f;

        /// <summary>
        /// Constante de mitigation : réduction = armure / (armure + constante).
        /// Avec 100, 100 d'armure réduisent les dégâts de moitié.
        /// </summary>
        public float ArmorConstant { get; set; } = 100f;
    }

    /// <summary>Statistiques de combat de base d'un joueur, en attendant les classes (M5).</summary>
    public sealed class PlayerCombatSettings
    {
        public int MaxHealth { get; set; } = 100;

        public int DamageMin { get; set; } = 8;

        public int DamageMax { get; set; } = 12;

        public int Armor { get; set; } = 0;

        public float AttackRange { get; set; } = 3f;

        public float AttackCooldownSeconds { get; set; } = 1f;

        public float RespawnSeconds { get; set; } = 5f;
    }
}
