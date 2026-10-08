using GameServer.Domain.Classes;
using GameServer.Domain.Combat;

namespace GameServer.Domain.Skills
{
    public enum SkillKind : byte
    {
        Damage = 1,
        Heal = 2,
        /// <summary>Force la cible à se concentrer sur le lanceur (menace).</summary>
        Taunt = 3,
    }

    public enum SpellSchool : byte
    {
        Physical = 1,
        Arcane = 2,
        Holy = 3,
    }

    /// <summary>
    /// Compétence data-driven. La puissance est <c>BasePower + Coefficient × puissance du lanceur</c>
    /// (puissance d'attaque pour l'école physique, puissance des sorts sinon).
    /// </summary>
    public sealed class SkillDefinition
    {
        public ushort Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public CharacterClass Class { get; set; }

        public SkillKind Kind { get; set; }

        public SpellSchool School { get; set; }

        /// <summary>Cible hostile (créature ou joueur ennemi). Sinon : alliée ou soi-même.</summary>
        public bool TargetsEnemy { get; set; }

        /// <summary>Portée en unités. 0 : soi-même uniquement.</summary>
        public float Range { get; set; }

        public float CastSeconds { get; set; }

        public float CooldownSeconds { get; set; }

        public int ResourceCost { get; set; }

        public ResourceKind ResourceCostKind { get; set; }

        public float BasePower { get; set; }

        public float Coefficient { get; set; }
    }
}
