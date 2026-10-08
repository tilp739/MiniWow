using System.Collections.Generic;

namespace GameServer.Domain.Quests
{
    /// <summary>Types d'objectifs. Chaque type est évalué par le journal de quêtes ou par la simulation.</summary>
    public enum ObjectiveKind : byte
    {
        /// <summary>Tuer des créatures d'un modèle donné.</summary>
        Kill = 1,

        /// <summary>Posséder des objets dans le sac.</summary>
        Collect = 2,

        /// <summary>Parler à un PNJ.</summary>
        Talk = 3,

        /// <summary>Atteindre une zone.</summary>
        GoTo = 4,
    }

    public sealed class QuestObjectiveDefinition
    {
        public ObjectiveKind Kind { get; set; }

        /// <summary>Modèle de créature (Kill), objet (Collect) ou PNJ (Talk). Inutilisé pour GoTo.</summary>
        public ushort TargetId { get; set; }

        public int Count { get; set; } = 1;

        public float X { get; set; }

        public float Z { get; set; }

        public float Radius { get; set; } = 3f;
    }

    public sealed class QuestRewardDefinition
    {
        public int Experience { get; set; }

        public int Copper { get; set; }

        /// <summary>Objet offert en récompense, ou 0 pour aucun.</summary>
        public ushort ItemId { get; set; }

        public int ItemCount { get; set; } = 1;
    }

    /// <summary>Définition d'une quête, chargée depuis <c>Content/quests.json</c>.</summary>
    public sealed class QuestDefinition
    {
        public ushort Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        /// <summary>PNJ qui propose la quête.</summary>
        public ushort GiverNpcId { get; set; }

        /// <summary>PNJ auprès de qui la quête se termine.</summary>
        public ushort TurnInNpcId { get; set; }

        public List<QuestObjectiveDefinition> Objectives { get; set; } = new List<QuestObjectiveDefinition>();

        public QuestRewardDefinition Reward { get; set; } = new QuestRewardDefinition();
    }
}
