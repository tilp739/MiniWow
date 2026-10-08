using System.Collections.Generic;
using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Matériaux low-poly simples, créés une seule fois puis mis en cache par clé. Pas de textures :
    /// des couleurs plates, adaptées au style low-poly et peu coûteuses à l'affichage.
    /// </summary>
    public static class CharacterMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _litShader;

        private static Shader LitShader
        {
            get
            {
                if (_litShader == null)
                {
                    _litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                }

                return _litShader;
            }
        }

        /// <summary>Matériau partagé pour une clé donnée ; créé au premier appel, réutilisé ensuite.</summary>
        public static Material GetOrCreate(string key, Color color)
        {
            if (Cache.TryGetValue(key, out Material existing) && existing != null)
            {
                return existing;
            }

            var material = new Material(LitShader) { name = key, color = color };
            Cache[key] = material;
            return material;
        }

        /// <summary>Peau de l'Orc : une couleur par teinte, pour permettre des Orcs de couleurs différentes.</summary>
        public static Material SkinMaterial(Color skinColor) =>
            GetOrCreate($"OrcSkin_{ColorUtility.ToHtmlStringRGB(skinColor)}", skinColor);

        public static Material Armor => GetOrCreate("Armor", new Color(0.32f, 0.33f, 0.36f));

        public static Material Leather => GetOrCreate("Leather", new Color(0.45f, 0.28f, 0.16f));

        public static Material Metal => GetOrCreate("Metal", new Color(0.55f, 0.56f, 0.58f));

        public static Material Boots => GetOrCreate("Boots", new Color(0.26f, 0.18f, 0.12f));
    }
}
