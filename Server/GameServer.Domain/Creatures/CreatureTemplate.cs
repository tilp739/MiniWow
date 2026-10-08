using System.Collections.Generic;

namespace GameServer.Domain.Creatures
{
    /// <summary>
    /// Modèle d'une créature, chargé depuis <c>Content/creatures.json</c>.
    /// Les valeurs sont équilibrables sans toucher au code.
    /// </summary>
    public sealed class CreatureTemplate
    {
        /// <summary>Identifiant numérique stable, envoyé au client dans les snapshots.</summary>
        public ushort Id { get; set; }

        /// <summary>Clé lisible, utilisée dans les données et les logs.</summary>
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int MaxHealth { get; set; } = 50;

        public int DamageMin { get; set; } = 2;

        public int DamageMax { get; set; } = 4;

        public int Armor { get; set; }

        /// <summary>Vitesse de déplacement, en unités par seconde.</summary>
        public float MoveSpeed { get; set; } = 4f;

        /// <summary>Distance à laquelle la créature repère un joueur.</summary>
        public float AggroRange { get; set; } = 10f;

        /// <summary>Distance maximale à son point d'apparition ; au-delà, elle retourne chez elle.</summary>
        public float LeashRange { get; set; } = 30f;

        public float AttackRange { get; set; } = 2f;

        public float AttackCooldownSeconds { get; set; } = 1.5f;

        public float RespawnSeconds { get; set; } = 20f;

        public int ExperienceReward { get; set; } = 10;

        /// <summary>Table de butin. Les probabilités restent secrètes : seul le résultat est envoyé au client.</summary>
        public List<LootEntry> Loot { get; set; } = new List<LootEntry>();

        public int CopperMin { get; set; }

        public int CopperMax { get; set; }

        /// <summary>Boss : attaques spéciales, invocations et phases. Voir WorldSimulation.Boss.cs.</summary>
        public bool IsBoss { get; set; }

        /// <summary>Intervalle entre deux onde de choc, en secondes (boss uniquement).</summary>
        public float ShockwaveIntervalSeconds { get; set; } = 10f;

        /// <summary>Rayon de l'onde de choc : les joueurs à moins de cette distance sont touchés.</summary>
        public float ShockwaveRadius { get; set; } = 6f;

        public int ShockwaveDamageMin { get; set; }

        public int ShockwaveDamageMax { get; set; }

        /// <summary>Délai entre le télégraphe et l'impact : laisse aux joueurs le temps de s'écarter.</summary>
        public float ShockwaveTelegraphSeconds { get; set; } = 2f;

        /// <summary>Modèle invoqué à la phase 2 (0 : aucune invocation).</summary>
        public ushort SummonTemplateId { get; set; }

        public int SummonCount { get; set; } = 2;

        /// <summary>Sous ce pourcentage de vie, le boss invoque ses renforts une fois.</summary>
        public float SummonAtHealthPercent { get; set; } = 60f;

        /// <summary>Sous ce pourcentage de vie, le boss devient enragé.</summary>
        public float EnrageAtHealthPercent { get; set; } = 25f;

        public float EnrageDamageMultiplier { get; set; } = 1.5f;
    }

    /// <summary>Une ligne de la table de butin.</summary>
    public sealed class LootEntry
    {
        public ushort ItemId { get; set; }

        /// <summary>Probabilité entre 0 et 1.</summary>
        public float Chance { get; set; }

        public int Min { get; set; } = 1;

        public int Max { get; set; } = 1;
    }
}
