using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>Point d'entrée statique pratique : créer un Orc complet en un seul appel.</summary>
    public static class OrcGenerator
    {
        /// <summary>
        /// Crée un nouvel Orc sous <paramref name="parent"/> (ou à la racine de la scène si null).
        /// Reproductible : la même <paramref name="seed"/> donne toujours exactement le même Orc.
        /// </summary>
        public static OrcCharacter Generate(int seed, Transform parent = null, string name = "Orc")
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            OrcCharacter character = go.AddComponent<OrcCharacter>();
            character.GenerateRandom(seed);
            return character;
        }

        /// <summary>Crée un Orc avec les proportions par défaut (pas d'aléatoire).</summary>
        public static OrcCharacter GenerateDefault(Transform parent = null, string name = "Orc")
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            OrcCharacter character = go.AddComponent<OrcCharacter>();
            character.Generate();
            return character;
        }
    }
}
