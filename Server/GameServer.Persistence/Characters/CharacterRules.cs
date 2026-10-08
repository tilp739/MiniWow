using System.Linq;

namespace GameServer.Persistence.Characters
{
    /// <summary>Règles de validation partagées par toutes les implémentations de <see cref="ICharacterRepository"/>.</summary>
    public static class CharacterRules
    {
        public const int MinNameLength = 3;
        public const int MaxNameLength = 16;

        /// <summary>Nombre maximal de personnages par compte (comme un plafond de royaume Classic, réduit).</summary>
        public const int MaxCharactersPerAccount = 8;

        public static bool IsValidName(string name) =>
            name.Length >= MinNameLength
            && name.Length <= MaxNameLength
            && name.All(char.IsLetter);
    }
}
