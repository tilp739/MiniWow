namespace GameServer.Simulation
{
    /// <summary>
    /// Vue de la simulation sur une connexion. Permet à la simulation d'envoyer des trames
    /// sans dépendre de la couche réseau concrète.
    /// </summary>
    public interface IWorldClient
    {
        int ClientId { get; }

        /// <summary>Met une trame complète en file d'envoi. Retourne false si la file est pleine ou fermée.</summary>
        bool TrySend(OutboundFrame frame);

        /// <summary>Indique au réseau vers quel monde envoyer les commandes de ce client (changé lors d'un transfert).</summary>
        void SetWorld(WorldSimulation world);
    }
}
