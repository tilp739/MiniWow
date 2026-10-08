using UnityEngine;

namespace MiniWow.Client.UI
{
    /// <summary>
    /// Thème visuel IMGUI : remplace le skin gris par défaut d'Unity par des panneaux sombres à bordure
    /// dorée et du texte crème/or, pour se rapprocher d'un MMORPG classique sans dépendre d'assets
    /// graphiques externes (tout généré par code, cohérent avec le reste du projet — mêmes textures
    /// procédurales 1 pixel que les barres de vie).
    ///
    /// Limite réelle de l'IMGUI : pas de coins arrondis, pas d'icônes, police par défaut. Un rendu
    /// pixel-perfect "à la WoW" demanderait de vraies textures/polices et une UI Canvas dédiée ; ce thème
    /// rapproche l'ambiance (sombre, doré, lisible) dans ce que l'IMGUI permet raisonnablement.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color PanelBackground = new Color(0.07f, 0.06f, 0.05f, 0.92f);
        public static readonly Color PanelBorder = new Color(0.63f, 0.49f, 0.18f);
        public static readonly Color TitleColor = new Color(1f, 0.82f, 0.4f);
        public static readonly Color TextColor = new Color(0.92f, 0.88f, 0.78f);
        public static readonly Color ButtonFill = new Color(0.16f, 0.13f, 0.09f, 0.95f);
        public static readonly Color ButtonHoverFill = new Color(0.3f, 0.23f, 0.11f, 0.95f);
        public static readonly Color FieldFill = new Color(0.03f, 0.03f, 0.02f, 0.9f);

        private static GUISkin _skin;

        /// <summary>Construit (une seule fois, mis en cache) le skin sombre/or qui remplace le gris par défaut.</summary>
        public static GUISkin Build()
        {
            if (_skin != null)
            {
                return _skin;
            }

            GUISkin source = GUI.skin;
            _skin = ScriptableObject.CreateInstance<GUISkin>();
            _skin.font = source.font;

            // On part d'une copie de chaque style par défaut (évite tout champ null non prévu par l'IMGUI),
            // puis on ne personnalise que ce qui compte visuellement.
            _skin.box = new GUIStyle(source.box);
            _skin.label = new GUIStyle(source.label);
            _skin.button = new GUIStyle(source.button);
            _skin.textField = new GUIStyle(source.textField);
            _skin.textArea = new GUIStyle(source.textArea);
            _skin.toggle = new GUIStyle(source.toggle);
            _skin.window = new GUIStyle(source.window);
            _skin.horizontalSlider = new GUIStyle(source.horizontalSlider);
            _skin.horizontalSliderThumb = new GUIStyle(source.horizontalSliderThumb);
            _skin.verticalSlider = new GUIStyle(source.verticalSlider);
            _skin.verticalSliderThumb = new GUIStyle(source.verticalSliderThumb);
            _skin.horizontalScrollbar = new GUIStyle(source.horizontalScrollbar);
            _skin.horizontalScrollbarThumb = new GUIStyle(source.horizontalScrollbarThumb);
            _skin.horizontalScrollbarLeftButton = new GUIStyle(source.horizontalScrollbarLeftButton);
            _skin.horizontalScrollbarRightButton = new GUIStyle(source.horizontalScrollbarRightButton);
            _skin.verticalScrollbar = new GUIStyle(source.verticalScrollbar);
            _skin.verticalScrollbarThumb = new GUIStyle(source.verticalScrollbarThumb);
            _skin.verticalScrollbarUpButton = new GUIStyle(source.verticalScrollbarUpButton);
            _skin.verticalScrollbarDownButton = new GUIStyle(source.verticalScrollbarDownButton);
            _skin.scrollView = new GUIStyle(source.scrollView);

            Texture2D panelTex = BorderedTexture(PanelBackground, PanelBorder);
            Texture2D buttonTex = BorderedTexture(ButtonFill, PanelBorder);
            Texture2D buttonHoverTex = BorderedTexture(ButtonHoverFill, TitleColor);
            Texture2D fieldTex = BorderedTexture(FieldFill, PanelBorder);

            _skin.box.richText = true;
            _skin.box.fontStyle = FontStyle.Bold;
            _skin.box.alignment = TextAnchor.UpperCenter;
            _skin.box.padding = new RectOffset(8, 8, 22, 8);
            _skin.box.border = new RectOffset(4, 4, 4, 4);
            _skin.box.normal.background = panelTex;
            _skin.box.normal.textColor = TitleColor;

            _skin.label.richText = true;
            _skin.label.wordWrap = true;
            _skin.label.normal.textColor = TextColor;

            _skin.button.border = new RectOffset(3, 3, 3, 3);
            _skin.button.normal.background = buttonTex;
            _skin.button.normal.textColor = TextColor;
            _skin.button.hover.background = buttonHoverTex;
            _skin.button.hover.textColor = TitleColor;
            _skin.button.active.background = buttonHoverTex;
            _skin.button.active.textColor = TitleColor;

            _skin.textField.border = new RectOffset(2, 2, 2, 2);
            _skin.textField.normal.background = fieldTex;
            _skin.textField.normal.textColor = TextColor;

            _skin.toggle.normal.textColor = TextColor;
            _skin.toggle.onNormal.textColor = TitleColor;

            return _skin;
        }

        /// <summary>
        /// Petite texture carrée : bordure de <paramref name="borderPx"/> pixels, remplissage uni au centre.
        /// Étirable proprement via GUIStyle.border (les coins ne se déforment pas).
        /// </summary>
        private static Texture2D BorderedTexture(Color fill, Color border, int size = 16, int borderPx = 2)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onBorder = x < borderPx || y < borderPx || x >= size - borderPx || y >= size - borderPx;
                    tex.SetPixel(x, y, onBorder ? border : fill);
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
