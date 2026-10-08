namespace GameServer.Persistence.Model
{
    /// <summary>Ligne de la liste de personnages d'un compte (écran de sélection).</summary>
    public sealed class CharacterSummary
    {
        public CharacterSummary(long characterId, string name, byte classId, int level)
        {
            CharacterId = characterId;
            Name = name;
            ClassId = classId;
            Level = level;
        }

        public long CharacterId { get; }

        public string Name { get; }

        public byte ClassId { get; }

        public int Level { get; }
    }
}
