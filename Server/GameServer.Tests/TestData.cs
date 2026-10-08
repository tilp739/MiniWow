using GameServer.Domain;
using GameServer.Domain.Classes;
using GameServer.Domain.Combat;
using GameServer.Domain.Movement;

namespace GameServer.Tests
{
    /// <summary>Données de test partagées : une classe de référence et une fabrique de joueurs.</summary>
    public static class TestData
    {
        /// <summary>Mage : 100 PV au niveau 1, ce qui permet de tuer un joueur d'un seul coup de 100 dégâts.</summary>
        public static ClassDefinition Mage() => new ClassDefinition
        {
            Id = CharacterClass.Mage,
            Key = "mage",
            Name = "Mage",
            Resource = ResourceKind.Mana,
            BaseStrength = 2,
            BaseAgility = 4,
            BaseIntelligence = 12,
            BaseStamina = 5,
            BaseSpirit = 6,
            GrowthIntelligence = 3,
            GrowthStamina = 1,
            GrowthSpirit = 1,
            ResourceRegenPerSecond = 3,
        };

        public static Player NewPlayer(uint id, float x, float z) =>
            new Player(id, "test", Mage(), new MovementSettings(), x, z);
    }
}
