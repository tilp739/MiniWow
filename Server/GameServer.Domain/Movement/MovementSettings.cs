namespace GameServer.Domain.Movement
{
    /// <summary>Paramètres de mouvement autoritaires du serveur.</summary>
    public sealed class MovementSettings
    {
        /// <summary>Vitesse de marche, en unités par seconde.</summary>
        public float WalkSpeed { get; set; } = 6f;

        /// <summary>Demi-côté du monde carré centré sur l'origine. Le joueur ne peut pas sortir de ce carré.</summary>
        public float WorldHalfExtent { get; set; } = 500f;

        /// <summary>Nombre de ticks sans nouvelle entrée avant de considérer le joueur à l'arrêt.</summary>
        public int InputTimeoutTicks { get; set; } = 10;
    }
}
