using System;

namespace GameServer.Domain.Progression
{
    /// <summary>Courbe d'expérience. Le niveau maximal est fixé ici ; les valeurs de classe sont dans les données.</summary>
    public static class LevelTable
    {
        public const int MaxLevel = 10;

        /// <summary>Expérience nécessaire pour passer du niveau donné au suivant. Zéro au niveau maximal.</summary>
        public static long ExperienceToNext(int level)
        {
            if (level < 1 || level >= MaxLevel)
            {
                return 0;
            }

            return (long)(100d * Math.Pow(level, 1.5d));
        }
    }
}
