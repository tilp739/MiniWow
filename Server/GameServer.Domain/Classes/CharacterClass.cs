using GameServer.Domain.Combat;
using GameServer.Domain.Stats;

namespace GameServer.Domain.Classes
{
    /// <summary>Classes jouables. Les valeurs numériques sont dans <c>Content/classes.json</c>.</summary>
    public enum CharacterClass : byte
    {
        Warrior = 1,
        Mage = 2,
        Priest = 3,
    }

    /// <summary>Définition d'une classe : statistiques de base, croissance par niveau, ressource.</summary>
    public sealed class ClassDefinition
    {
        public CharacterClass Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public ResourceKind Resource { get; set; }

        public int BaseStrength { get; set; }

        public int BaseAgility { get; set; }

        public int BaseIntelligence { get; set; }

        public int BaseStamina { get; set; }

        public int BaseSpirit { get; set; }

        public int GrowthStrength { get; set; }

        public int GrowthAgility { get; set; }

        public int GrowthIntelligence { get; set; }

        public int GrowthStamina { get; set; }

        public int GrowthSpirit { get; set; }

        /// <summary>Régénération de ressource par seconde (négative pour la colère, qui se dissipe).</summary>
        public float ResourceRegenPerSecond { get; set; }

        /// <summary>Statistiques primaires à un niveau donné.</summary>
        public PrimaryStats StatsAtLevel(int level)
        {
            int levels = System.Math.Max(0, level - 1);
            return new PrimaryStats(
                BaseStrength + GrowthStrength * levels,
                BaseAgility + GrowthAgility * levels,
                BaseIntelligence + GrowthIntelligence * levels,
                BaseStamina + GrowthStamina * levels,
                BaseSpirit + GrowthSpirit * levels);
        }
    }
}
