namespace GameShared.Messages
{
    /// <summary>
    /// Résumé d'une aventure affiché au tableau des aventures d'un PNJ (cf. <see cref="NpcMessage"/>).
    /// Purement descriptif ; le lancement réel passe par <see cref="DungeonActionMessage"/> avec
    /// l'<see cref="Id"/> correspondant.
    /// </summary>
    public readonly struct AdventureSummary
    {
        public AdventureSummary(
            ushort id, string name, byte recommendedLevel, byte minPlayers, byte maxPlayers,
            byte difficulty, byte bossCount, byte dungeonCount)
        {
            Id = id;
            Name = name;
            RecommendedLevel = recommendedLevel;
            MinPlayers = minPlayers;
            MaxPlayers = maxPlayers;
            Difficulty = difficulty;
            BossCount = bossCount;
            DungeonCount = dungeonCount;
        }

        public ushort Id { get; }

        public string Name { get; }

        public byte RecommendedLevel { get; }

        public byte MinPlayers { get; }

        public byte MaxPlayers { get; }

        /// <summary>1 à 5 étoiles, purement indicatif.</summary>
        public byte Difficulty { get; }

        public byte BossCount { get; }

        public byte DungeonCount { get; }
    }
}
