using GameServer.Persistence.Model;

namespace GameServer.Simulation
{
    /// <summary>
    /// File de sauvegarde de personnage, remplie depuis le thread de simulation (jamais d'I/O ici).
    /// Un worker en tâche de fond écrit réellement en base ; voir <c>CharacterSaveWorker</c>.
    /// </summary>
    public interface ICharacterSaveQueue
    {
        /// <summary>Dépose une sauvegarde sans bloquer. Si la file est pleine, perdue : le prochain autosave la referra.</summary>
        bool TryEnqueue(CharacterSnapshot snapshot);
    }
}
