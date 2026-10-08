using System.Collections.Generic;
using GameServer.Content;
using GameServer.Domain;
using GameServer.Domain.Items;
using GameServer.Domain.Professions;
using GameServer.Domain.Spatial;
using GameServer.Persistence.Model;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>Métiers : nœuds de récolte, recettes d'artisanat, apprentissage et progression de compétence.</summary>
    public sealed partial class WorldSimulation
    {
        private readonly List<GatheringNodeInstance> _nodes = new List<GatheringNodeInstance>();

        private void InitializeGatheringNodes()
        {
            foreach (GatheringNodeSpawn spawn in _content.GatheringNodeSpawns)
            {
                GatheringNodeDefinition definition = _content.GatheringNodes[spawn.TemplateId];
                _nodes.Add(new GatheringNodeInstance(_directory.AllocateEntityId(), definition, spawn.X, spawn.Z));
            }
        }

        private void HandleProfessionAction(IWorldClient client, ProfessionActionMessage action)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            switch (action.Kind)
            {
                case ProfessionActionKind.Gather:
                    Gather(client, slot.Player, action.TargetEntityId);
                    break;

                case ProfessionActionKind.Craft:
                    Craft(client, slot.Player, action.RecipeId);
                    break;
            }
        }

        private void Gather(IWorldClient client, Player player, uint nodeEntityId)
        {
            GatheringNodeInstance? node = FindNode(nodeEntityId);
            if (node == null)
            {
                Reject(client, ErrorCode.NoSuchItem, "Nœud de récolte inconnu.");
                return;
            }

            if (!node.IsAvailable)
            {
                Reject(client, ErrorCode.NoSuchItem, "Ce nœud est épuisé.");
                return;
            }

            if (DistanceXZ(player.X, player.Z, node.X, node.Z) > GatheringNodeDefinition.InteractRange)
            {
                Reject(client, ErrorCode.TooFar, "Trop loin du nœud de récolte.");
                return;
            }

            GatheringNodeDefinition definition = node.Definition;
            if (!player.Professions.IsLearned(definition.ProfessionId) || player.Professions.SkillOf(definition.ProfessionId) < definition.RequiredSkill)
            {
                Reject(client, ErrorCode.NotAllowed, "Métier non appris ou compétence insuffisante.");
                return;
            }

            ItemDefinition result = _content.Items[definition.ResultItemId];
            int quantity = _random.Next(definition.ResultQuantityMin, definition.ResultQuantityMax + 1);
            if (!player.Inventory.TryAdd(result, quantity))
            {
                Reject(client, ErrorCode.InventoryFull, "Sac plein.");
                return;
            }

            node.Deplete();
            GainProfessionSkill(player, definition.ProfessionId);
            _logger.LogInformation("NodeGathered {EntityId} {NodeId}", player.EntityId, node.EntityId);
            SendInventory(client, player);
            SendProfessions(client, player);
        }

        private void Craft(IWorldClient client, Player player, ushort recipeId)
        {
            if (!_content.Recipes.TryGetValue(recipeId, out RecipeDefinition? recipe))
            {
                Reject(client, ErrorCode.InvalidArgument, "Recette inconnue.");
                return;
            }

            if (!player.Professions.IsLearned(recipe.ProfessionId) || player.Professions.SkillOf(recipe.ProfessionId) < recipe.RequiredSkill)
            {
                Reject(client, ErrorCode.NotAllowed, "Métier non appris ou compétence insuffisante.");
                return;
            }

            foreach (ReagentEntry reagent in recipe.Reagents)
            {
                if (player.Inventory.CountInBag(reagent.ItemId) < reagent.Quantity)
                {
                    Reject(client, ErrorCode.NoSuchItem, "Réactifs manquants.");
                    return;
                }
            }

            // Tout ou rien : si le résultat ne tient pas, les réactifs déjà retirés sont restitués.
            foreach (ReagentEntry reagent in recipe.Reagents)
            {
                player.Inventory.TryRemove(reagent.ItemId, reagent.Quantity);
            }

            ItemDefinition result = _content.Items[recipe.ResultItemId];
            if (!player.Inventory.TryAdd(result, recipe.ResultQuantity))
            {
                foreach (ReagentEntry reagent in recipe.Reagents)
                {
                    player.Inventory.TryAdd(_content.Items[reagent.ItemId], reagent.Quantity);
                }

                Reject(client, ErrorCode.InventoryFull, "Sac plein.");
                return;
            }

            GainProfessionSkill(player, recipe.ProfessionId);
            _logger.LogInformation("RecipeCrafted {EntityId} {RecipeId}", player.EntityId, recipeId);
            SendInventory(client, player);
            SendProfessions(client, player);
        }

        /// <summary>+1 de compétence par action réussie, jusqu'au plafond du métier. Déterministe (pas de palier aléatoire).</summary>
        private void GainProfessionSkill(Player player, byte professionId)
        {
            int maxSkill = _content.Professions[professionId].MaxSkill;
            player.Professions.GainSkill(professionId, 1, maxSkill);
        }

        private void SendProfessions(IWorldClient client, Player player)
        {
            var known = new List<(byte ProfessionId, string Name, int Skill, int MaxSkill)>();
            foreach (byte professionId in player.Professions.LearnedIds)
            {
                if (_content.Professions.TryGetValue(professionId, out ProfessionDefinition? definition))
                {
                    known.Add((professionId, definition.Name, player.Professions.SkillOf(professionId), definition.MaxSkill));
                }
            }

            client.TrySend(OutboundFrame.Rent(Opcode.Professions, new ProfessionsMessage(known), _writer));
        }

        /// <summary>Restaure les métiers connus depuis un snapshot. Un métier disparu du contenu est ignoré.</summary>
        private void HydrateProfessions(Player player, IReadOnlyList<ProfessionSnapshot> snapshot)
        {
            foreach (ProfessionSnapshot entry in snapshot)
            {
                if (_content.Professions.ContainsKey(entry.ProfessionId))
                {
                    player.Professions.RestoreLearned(entry.ProfessionId, entry.Skill);
                }
                else
                {
                    _logger.LogWarning("UnknownProfessionOnLoad {ProfessionId} {CharacterId}", entry.ProfessionId, player.CharacterId);
                }
            }
        }

        /// <summary>Construit la liste de professions à sauvegarder depuis l'état en mémoire.</summary>
        private List<ProfessionSnapshot> BuildProfessionSnapshots(Player player)
        {
            var result = new List<ProfessionSnapshot>();
            foreach (byte professionId in player.Professions.LearnedIds)
            {
                result.Add(new ProfessionSnapshot(professionId, player.Professions.SkillOf(professionId)));
            }

            return result;
        }

        private GatheringNodeInstance? FindNode(uint entityId)
        {
            foreach (GatheringNodeInstance node in _nodes)
            {
                if (node.EntityId == entityId)
                {
                    return node;
                }
            }

            return null;
        }
    }
}
