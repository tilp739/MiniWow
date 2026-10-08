using System.Collections.Generic;
using GameServer.Domain;
using GameServer.Domain.Creatures;
using GameServer.Domain.Items;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>Butin, inventaire et équipement. Toutes les opérations sont validées ici, côté serveur.</summary>
    public sealed partial class WorldSimulation
    {
        /// <summary>Distance maximale pour ramasser un cadavre.</summary>
        private const float LootRange = 5f;

        /// <summary>
        /// Tire le butin d'une créature morte : la table est secrète, seul le résultat quitte le serveur.
        /// Les contributeurs, eux seuls, peuvent ramasser.
        /// </summary>
        private void PrepareLoot(CreatureInstance creature, IReadOnlyList<uint> contributors)
        {
            creature.LootOwners.Clear();
            foreach (uint contributor in contributors)
            {
                creature.LootOwners.Add(contributor);
            }

            foreach (LootEntry entry in creature.Template.Loot)
            {
                if (_random.NextDouble() >= entry.Chance)
                {
                    continue;
                }

                int quantity = _random.Next(entry.Min, entry.Max + 1);
                if (quantity > 0)
                {
                    creature.PendingLoot.Add((entry.ItemId, quantity));
                }
            }

            int copper = creature.Template.CopperMax > 0
                ? _random.Next(creature.Template.CopperMin, creature.Template.CopperMax + 1)
                : 0;
            creature.SetPendingCopper(copper);
        }

        private void HandleLoot(IWorldClient client, uint corpseId)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            Player looter = slot.Player;
            CreatureInstance? corpse = FindCorpse(corpseId);

            if (corpse == null || !corpse.IsLootable)
            {
                Reject(client, ErrorCode.NoSuchItem, "Rien à ramasser ici.");
                return;
            }

            if (!corpse.LootOwners.Contains(looter.EntityId))
            {
                Reject(client, ErrorCode.NotAllowed, "Ce butin ne vous appartient pas.");
                return;
            }

            if (DistanceXZ(looter.X, looter.Z, corpse.X, corpse.Z) > LootRange)
            {
                Reject(client, ErrorCode.TooFar, "Trop loin du cadavre.");
                return;
            }

            // Tout ou rien : si un objet ne tient pas, les précédents sont retirés.
            var added = new List<(ushort ItemId, int Quantity)>();
            foreach ((ushort itemId, int quantity) in corpse.PendingLoot)
            {
                ItemDefinition item = _content.Items[itemId];
                if (!looter.Inventory.TryAdd(item, quantity))
                {
                    foreach ((ushort doneId, int doneQuantity) in added)
                    {
                        looter.Inventory.TryRemove(doneId, doneQuantity);
                    }

                    Reject(client, ErrorCode.InventoryFull, "Sac plein.");
                    return;
                }

                added.Add((itemId, quantity));
            }

            looter.AddCopper(corpse.PendingCopper);
            corpse.ClearLoot();
            SendInventory(client, looter);
            _logger.LogInformation("LootTaken {EntityId} {CorpseId}", looter.EntityId, corpseId);
        }

        private void HandleInventoryAction(IWorldClient client, InventoryActionMessage action)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            Player owner = slot.Player;
            Inventory inventory = owner.Inventory;
            bool changed;

            switch (action.Kind)
            {
                case InventoryActionKind.Move:
                    changed = inventory.TryMove(action.From, action.To);
                    break;

                case InventoryActionKind.Split:
                    changed = inventory.TrySplit(action.From, action.To, action.Amount);
                    break;

                case InventoryActionKind.Destroy:
                    changed = inventory.TryDestroy(action.From);
                    break;

                case InventoryActionKind.Equip:
                    changed = inventory.TryEquip(action.From);
                    if (changed)
                    {
                        owner.SetBonuses(inventory.ComputeBonuses());
                    }
                    break;

                case InventoryActionKind.Unequip:
                    changed = inventory.TryUnequip((Domain.Items.EquipSlot)action.From);
                    if (changed)
                    {
                        owner.SetBonuses(inventory.ComputeBonuses());
                    }
                    break;

                default:
                    changed = false;
                    break;
            }

            if (!changed)
            {
                Reject(client, ErrorCode.InvalidArgument, "Action d'inventaire impossible.");
                return;
            }

            SendInventory(client, owner);
        }

        /// <summary>Envoie le contenu complet du sac et de l'équipement au propriétaire.</summary>
        private void SendInventory(IWorldClient client, Player owner)
        {
            var bag = new List<(ushort ItemId, ushort Quantity)>(InventoryMessage.BagSize);
            for (int i = 0; i < InventoryMessage.BagSize; i++)
            {
                ItemStack? stack = owner.Inventory.GetBag(i);
                bag.Add(stack == null ? ((ushort)0, (ushort)0) : (stack.Item.Id, (ushort)stack.Quantity));
            }

            var equipment = new List<ushort>(InventoryMessage.EquipmentSize);
            for (int i = 0; i < InventoryMessage.EquipmentSize; i++)
            {
                ItemStack? stack = owner.Inventory.GetEquipped((EquipSlot)i);
                equipment.Add(stack?.Item.Id ?? 0);
            }

            client.TrySend(OutboundFrame.Rent(Opcode.Inventory, new InventoryMessage(bag, equipment), _writer));

            // Les objectifs « collecter » suivent le contenu du sac : on les resynchronise à chaque changement.
            SyncCollectProgress(owner);
            SendQuestLog(client, owner);
        }

        private CreatureInstance? FindCorpse(uint entityId)
        {
            foreach (CreatureInstance creature in _creatures)
            {
                if (creature.EntityId == entityId)
                {
                    return creature;
                }
            }

            return null;
        }
    }
}
