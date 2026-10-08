namespace GameServer.Domain.Adventures
{
    /// <summary>
    /// Aventure proposée au tableau des aventures du Hub, chargée depuis <c>Content/adventures.json</c>.
    /// Purement descriptive côté contenu : le lancement réel instancie <see cref="ZoneId"/> via le même
    /// mécanisme que le donjon Crypt (<c>WorldSimulation.Dungeon.cs</c>), généralisé pour résoudre la zone
    /// depuis une aventure choisie plutôt que depuis le champ fixe d'un seul PNJ.
    /// </summary>
    public sealed class AdventureDefinition
    {
        public ushort Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Identifiant de la zone (Content/zones/{ZoneId}.json) instanciée au lancement.</summary>
        public string ZoneId { get; set; } = string.Empty;

        public int RecommendedLevel { get; set; } = 1;

        public int MinPlayers { get; set; } = 1;

        public int MaxPlayers { get; set; } = 5;

        /// <summary>1 à 5 étoiles, purement indicatif pour l'affichage.</summary>
        public int Difficulty { get; set; } = 1;

        public int BossCount { get; set; }

        public int DungeonCount { get; set; }

        public string Description { get; set; } = string.Empty;
    }
}
