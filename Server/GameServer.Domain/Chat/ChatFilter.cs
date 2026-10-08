using System;
using System.Collections.Generic;
using System.Text;

namespace GameServer.Domain.Chat
{
    /// <summary>
    /// Filtre du chat : refuse le texte vide ou contenant des caractères de contrôle, et masque les mots interdits
    /// (insensible à la casse). Les mots sont configurables ; la liste est vide par défaut.
    /// </summary>
    public sealed class ChatFilter
    {
        private readonly IReadOnlyList<string> _bannedWords;

        public ChatFilter(IEnumerable<string> bannedWords)
        {
            var words = new List<string>();
            foreach (string word in bannedWords)
            {
                if (!string.IsNullOrWhiteSpace(word))
                {
                    words.Add(word.Trim());
                }
            }

            _bannedWords = words;
        }

        /// <summary>Retourne le texte filtré, ou null si le message doit être refusé.</summary>
        public string? Apply(string text)
        {
            string trimmed = text.Trim();
            if (trimmed.Length == 0 || HasControlCharacters(trimmed))
            {
                return null;
            }

            string result = trimmed;
            foreach (string word in _bannedWords)
            {
                result = MaskWord(result, word);
            }

            return result;
        }

        private static bool HasControlCharacters(string text)
        {
            foreach (char c in text)
            {
                if (char.IsControl(c))
                {
                    return true;
                }
            }

            return false;
        }

        private static string MaskWord(string text, string word)
        {
            var builder = new StringBuilder(text.Length);
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    builder.Append(text, index, text.Length - index);
                    break;
                }

                builder.Append(text, index, found - index);
                builder.Append('*', word.Length);
                index = found + word.Length;
            }

            return builder.ToString();
        }
    }
}
