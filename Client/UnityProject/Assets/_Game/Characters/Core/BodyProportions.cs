using System;
using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Paramètres morphologiques d'un personnage humanoïde. Communs à toutes les races futures
    /// (Orc, Human, Elf, ...) : seules les valeurs par défaut et les plages typiques changent
    /// d'une race à l'autre, pas la structure des données ni le générateur.
    /// </summary>
    [Serializable]
    public struct BodyProportions
    {
        [Range(0.7f, 1.4f)] public float Height;
        [Range(0.7f, 1.6f)] public float ShoulderWidth;
        [Range(0.7f, 1.4f)] public float HeadSize;
        [Range(0.7f, 1.4f)] public float ArmLength;
        [Range(0.7f, 1.4f)] public float LegLength;
        [Range(0.7f, 1.5f)] public float BodyWidth;
        public Color SkinColor;

        /// <summary>Gabarit par défaut d'un Orc : massif, épaules larges, tête forte.</summary>
        public static BodyProportions OrcDefault => new BodyProportions
        {
            Height = 1.15f,
            ShoulderWidth = 1.3f,
            HeadSize = 1.1f,
            ArmLength = 1.1f,
            LegLength = 1.0f,
            BodyWidth = 1.25f,
            SkinColor = new Color(0.36f, 0.47f, 0.24f)
        };

        /// <summary>Gabarit par défaut d'un Humain : proportions normales, sans exagération.</summary>
        public static BodyProportions HumanDefault => new BodyProportions
        {
            Height = 1.0f,
            ShoulderWidth = 1.0f,
            HeadSize = 0.9f,
            ArmLength = 1.0f,
            LegLength = 1.0f,
            BodyWidth = 0.95f,
            SkinColor = new Color(0.86f, 0.68f, 0.54f)
        };

        /// <summary>
        /// Variante aléatoire d'un Orc, entièrement déterminée par <paramref name="seed"/> :
        /// la même seed reproduit toujours exactement le même Orc.
        /// </summary>
        public static BodyProportions RandomOrc(int seed)
        {
            var rng = new System.Random(seed);
            BodyProportions p = OrcDefault;

            float Vary(float value, float range) => value + (float)(rng.NextDouble() * 2.0 - 1.0) * range;

            p.Height = Vary(p.Height, 0.08f);
            p.ShoulderWidth = Vary(p.ShoulderWidth, 0.15f);
            p.HeadSize = Vary(p.HeadSize, 0.08f);
            p.ArmLength = Vary(p.ArmLength, 0.1f);
            p.LegLength = Vary(p.LegLength, 0.08f);
            p.BodyWidth = Vary(p.BodyWidth, 0.12f);

            float hue = 0.24f + (float)rng.NextDouble() * 0.12f; // verts / olive
            float saturation = 0.45f + (float)rng.NextDouble() * 0.3f;
            float value = 0.3f + (float)rng.NextDouble() * 0.25f;
            p.SkinColor = Color.HSVToRGB(hue, saturation, value);

            return p;
        }
    }
}
