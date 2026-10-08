namespace GameServer.Domain.Professions
{
    /// <summary>Modèle de nœud de récolte (veine, plante…), chargé depuis <c>Content/gathering_nodes.json</c>.</summary>
    public sealed class GatheringNodeDefinition
    {
        public ushort Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public byte ProfessionId { get; set; }

        public int RequiredSkill { get; set; }

        public ushort ResultItemId { get; set; }

        public int ResultQuantityMin { get; set; } = 1;

        public int ResultQuantityMax { get; set; } = 1;

        public float RespawnSeconds { get; set; } = 30f;

        /// <summary>Distance maximale à laquelle le joueur peut récolter ce nœud.</summary>
        public const float InteractRange = 5f;
    }
}
