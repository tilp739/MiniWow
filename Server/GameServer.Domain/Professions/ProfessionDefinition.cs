namespace GameServer.Domain.Professions
{
    /// <summary>Métier jouable (récolte ou artisanat), chargé depuis <c>Content/professions.json</c>.</summary>
    public sealed class ProfessionDefinition
    {
        public byte Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Compétence maximale atteignable dans ce métier.</summary>
        public int MaxSkill { get; set; }
    }
}
