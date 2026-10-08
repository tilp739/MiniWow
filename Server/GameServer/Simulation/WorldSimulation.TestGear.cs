using System.Collections.Generic;
using GameServer.Domain;
using GameServer.Domain.Items;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>
    /// Cheat de test déclenché par le client (touche F1) : donne un objet par emplacement d'équipement, pour
    /// tester rapidement l'équipement de chaque slot sans dépendre du butin ou des quêtes. Jamais utile en
    /// production, mais sans risque particulier : aucune donnée sensible, juste des objets clairement
    /// nommés "d'essai" (cf. Content/items.json).
    /// </summary>
    public sealed partial class WorldSimulation
    {
        private static readonly (EquipSlot Slot, string KeyA, string KeyB)[] TestGearBySlot =
        {
            (EquipSlot.Head, "test_head_a", "test_head_b"),
            (EquipSlot.Neck, "test_neck_a", "test_neck_b"),
            (EquipSlot.Shoulder, "test_shoulder_a", "test_shoulder_b"),
            (EquipSlot.Back, "test_back_a", "test_back_b"),
            (EquipSlot.Chest, "test_chest_a", "test_chest_b"),
            (EquipSlot.Shirt, "test_shirt_a", "test_shirt_b"),
            (EquipSlot.Tabard, "test_tabard_a", "test_tabard_b"),
            (EquipSlot.Wrist, "test_wrist_a", "test_wrist_b"),
            (EquipSlot.Hands, "test_hands_a", "test_hands_b"),
            (EquipSlot.Waist, "test_waist_a", "test_waist_b"),
            (EquipSlot.Legs, "test_legs_a", "test_legs_b"),
            (EquipSlot.Feet, "test_feet_a", "test_feet_b"),
            (EquipSlot.Finger1, "test_finger1_a", "test_finger1_b"),
            (EquipSlot.Finger2, "test_finger2_a", "test_finger2_b"),
            (EquipSlot.Trinket1, "test_trinket1_a", "test_trinket1_b"),
            (EquipSlot.Trinket2, "test_trinket2_a", "test_trinket2_b"),
            (EquipSlot.MainHand, "test_mainhand_a", "test_mainhand_b"),
            (EquipSlot.OffHand, "test_offhand_a", "test_offhand_b"),
            (EquipSlot.Ranged, "test_ranged_a", "test_ranged_b"),
        };

        /// <summary>
        /// Pour chaque emplacement : donne la variante A si le joueur n'a ni A ni B (équipé ou dans le sac),
        /// sinon la variante B s'il n'a que A, sinon rien. Permet de tester les deux variantes de chaque
        /// emplacement sans remplir le sac inutilement (ré-appuyer sur F1 après avoir équipé la variante A).
        /// </summary>
        private void GiveTestGear(IWorldClient client)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            Inventory inventory = slot.Player.Inventory;

            var itemsByKey = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition item in _content.Items.Values)
            {
                itemsByKey[item.Key] = item;
            }

            int given = 0;
            foreach ((EquipSlot equipSlot, string keyA, string keyB) in TestGearBySlot)
            {
                if (!itemsByKey.TryGetValue(keyA, out ItemDefinition? itemA) || !itemsByKey.TryGetValue(keyB, out ItemDefinition? itemB))
                {
                    continue; // Content/items.json ne définit pas (encore) cette paire : on l'ignore plutôt que de planter.
                }

                bool hasA = HasTestItem(inventory, equipSlot, itemA.Id);
                bool hasB = HasTestItem(inventory, equipSlot, itemB.Id);

                ItemDefinition? toGive = !hasA ? itemA : (!hasB ? itemB : null);
                if (toGive != null && inventory.TryAdd(toGive, 1))
                {
                    given++;
                }
            }

            if (given > 0)
            {
                SendInventory(client, slot.Player);
            }

            _logger.LogInformation("DebugGiveTestGear {ClientId} {Given}", client.ClientId, given);
        }

        private static bool HasTestItem(Inventory inventory, EquipSlot slot, ushort itemId) =>
            inventory.GetEquipped(slot)?.Item.Id == itemId || inventory.CountInBag(itemId) > 0;
    }
}
