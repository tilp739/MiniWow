using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniWow.Client.Data
{
    /// <summary>
    /// Noms des créatures, lus depuis la copie de <c>Content/creatures.json</c> dans Resources.
    /// Une seule source de vérité : le fichier du serveur, copié au build.
    /// </summary>
    public static class CreatureCatalog
    {
        private const string ResourcePath = "Content/creatures";
        private const string UnknownName = "Créature";

        private static Dictionary<int, string> _names;

        public static string NameOf(int templateId)
        {
            EnsureLoaded();
            return _names.TryGetValue(templateId, out string name) ? name : UnknownName;
        }

        private static void EnsureLoaded()
        {
            if (_names != null)
            {
                return;
            }

            _names = new Dictionary<int, string>();

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"Catalogue des créatures introuvable : Resources/{ResourcePath}. Lancez le build du serveur.");
                return;
            }

            CreatureFile file = JsonUtility.FromJson<CreatureFile>(asset.text);
            if (file?.creatures == null)
            {
                return;
            }

            foreach (CreatureEntry entry in file.creatures)
            {
                _names[entry.id] = entry.name;
            }
        }

        // Champs publics et noms identiques au JSON : exigence de JsonUtility.
        [Serializable]
        public sealed class CreatureFile
        {
            public CreatureEntry[] creatures;
        }

        [Serializable]
        public sealed class CreatureEntry
        {
            public int id;
            public string name;
        }
    }
}
