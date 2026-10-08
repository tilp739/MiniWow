using System.Linq;
using GameServer.Domain;
using Microsoft.Extensions.Logging;
using GameServer.Domain.Items;

namespace GameServer.Simulation
{
    /// <summary>Commandes d'administration, déclenchées depuis la console du serveur uniquement (jamais par le réseau).</summary>
    public sealed partial class WorldSimulation
    {
        /// <summary>Donne un objet à un joueur de ce monde, par son nom. Retourne false si le joueur ou l'objet est introuvable.</summary>
        public bool AdminGiveItem(string playerName, string itemKey, int quantity)
        {
            Player? target = _players.Values
                .Select(slot => slot.Player)
                .FirstOrDefault(p => string.Equals(p.Name, playerName, System.StringComparison.OrdinalIgnoreCase));
            ItemDefinition? item = _content.Items.Values.FirstOrDefault(i => i.Key == itemKey);

            if (target == null || item == null || quantity <= 0 || !target.Inventory.TryAdd(item, quantity))
            {
                return false;
            }

            foreach (PlayerSlot slot in _players.Values.Where(s => s.Player == target))
            {
                SendInventory(slot.Client, target);
            }

            _logger.LogInformation("AdminGiveItem {Player} {Item} {Quantity}", playerName, itemKey, quantity);
            return true;
        }

        /// <summary>Nombre de joueurs dans ce monde, pour les commandes d'information.</summary>
        public int CountPlayers() => _players.Count;
    }
}
