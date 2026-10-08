using System.Collections.Generic;
using System.Text.Json;
using GameServer.Persistence.Model;

namespace GameServer.Persistence.Characters
{
    /// <summary>
    /// (Dé)sérialisation JSON de l'inventaire et du journal de quêtes, pour les colonnes <c>jsonb</c>.
    /// Les DTOs de <c>GameServer.Persistence.Model</c> n'ont qu'un constructeur public : System.Text.Json
    /// l'utilise directement (appariement par nom de paramètre), aucun convertisseur n'est nécessaire.
    /// </summary>
    internal static class CharacterStateJson
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public static string Serialize(InventorySnapshot inventory) => JsonSerializer.Serialize(inventory, Options);

        public static string Serialize(QuestLogSnapshot quests) => JsonSerializer.Serialize(quests, Options);

        public static InventorySnapshot DeserializeInventory(string json) =>
            string.IsNullOrWhiteSpace(json) || json == "{}"
                ? InventorySnapshot.Empty
                : JsonSerializer.Deserialize<InventorySnapshot>(json, Options) ?? InventorySnapshot.Empty;

        public static QuestLogSnapshot DeserializeQuests(string json) =>
            string.IsNullOrWhiteSpace(json) || json == "{}"
                ? QuestLogSnapshot.Empty
                : JsonSerializer.Deserialize<QuestLogSnapshot>(json, Options) ?? QuestLogSnapshot.Empty;

        public static string Serialize(IReadOnlyList<ProfessionSnapshot> professions) => JsonSerializer.Serialize(professions, Options);

        public static IReadOnlyList<ProfessionSnapshot> DeserializeProfessions(string json) =>
            string.IsNullOrWhiteSpace(json) || json == "[]"
                ? System.Array.Empty<ProfessionSnapshot>()
                : JsonSerializer.Deserialize<List<ProfessionSnapshot>>(json, Options) ?? new List<ProfessionSnapshot>();
    }
}
