using System.Collections.Generic;

namespace GameServer.Domain.Npcs
{
    public sealed class ShopEntry
    {
        public ushort ItemId { get; set; }

        /// <summary>Prix d'achat unitaire, en copper. Le rachat se fait au prix de vente de l'objet.</summary>
        public int Price { get; set; }
    }

    /// <summary>PNJ, chargé depuis <c>Content/npcs.json</c> : texte, quêtes proposées, boutique éventuelle.</summary>
    public sealed class NpcDefinition
    {
        public ushort Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public float X { get; set; }

        public float Z { get; set; }

        public List<ushort> OfferedQuests { get; set; } = new List<ushort>();

        public List<ShopEntry> Shop { get; set; } = new List<ShopEntry>();

        /// <summary>Métiers que ce PNJ peut enseigner.</summary>
        public List<byte> TeachesProfessions { get; set; } = new List<byte>();

        /// <summary>Zone du donjon proposé par ce PNJ, ou null s'il n'en propose pas (porte simple, un seul donjon fixe).</summary>
        public string? DungeonZone { get; set; }

        /// <summary>
        /// Aventures (cf. Content/adventures.json) que ce PNJ propose au joueur (tableau des aventures du Hub).
        /// Un PNJ peut cumuler <see cref="DungeonZone"/> (porte fixe) et des aventures (tableau à choix) ;
        /// les deux utilisent le même mécanisme d'instanciation, juste une résolution de zone différente.
        /// </summary>
        public List<ushort> OfferedAdventureIds { get; set; } = new List<ushort>();

        /// <summary>Distance maximale à laquelle le joueur peut interagir avec le PNJ.</summary>
        public const float InteractRange = 5f;
    }
}
