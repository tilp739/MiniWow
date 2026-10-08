using System.Collections.Generic;
using GameServer.Domain;
using GameServer.Domain.Adventures;
using GameServer.Domain.Items;
using GameServer.Domain.Npcs;
using GameServer.Domain.Quests;
using GameServer.Domain.Stats;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>PNJ, quêtes, boutique et progression (expérience, niveau, monnaie).</summary>
    public sealed partial class WorldSimulation
    {
        private readonly List<NpcInstance> _npcs = new List<NpcInstance>();

        private void InitializeNpcs()
        {
            foreach (NpcDefinition definition in _content.Npcs.Values)
            {
                _npcs.Add(new NpcInstance(_directory.AllocateEntityId(), definition));
            }
        }

        private void HandleNpcAction(IWorldClient client, NpcActionMessage action)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            Player player = slot.Player;
            NpcInstance? npc = FindNpc(action.NpcEntityId);
            if (npc == null)
            {
                Reject(client, ErrorCode.NoSuchNpc, "PNJ inconnu.");
                return;
            }

            if (DistanceXZ(player.X, player.Z, npc.X, npc.Z) > NpcDefinition.InteractRange)
            {
                Reject(client, ErrorCode.TooFar, "Trop loin du PNJ.");
                return;
            }

            switch (action.Kind)
            {
                case NpcActionKind.Talk:
                    CreditTalk(player, npc.Definition.Id);
                    SendQuestLog(client, player);
                    SendNpc(client, player, npc);
                    break;

                case NpcActionKind.AcceptQuest:
                    AcceptQuest(client, player, npc, action.Argument);
                    break;

                case NpcActionKind.TurnInQuest:
                    TurnInQuest(client, player, npc, action.Argument);
                    break;

                case NpcActionKind.Buy:
                    Buy(client, player, npc, action.Argument, action.Quantity);
                    break;

                case NpcActionKind.Sell:
                    Sell(client, player, action.Argument, action.Quantity);
                    break;

                case NpcActionKind.LearnProfession:
                    LearnProfession(client, player, npc, (byte)action.Argument);
                    break;
            }
        }

        private void LearnProfession(IWorldClient client, Player player, NpcInstance npc, byte professionId)
        {
            if (!npc.Definition.TeachesProfessions.Contains(professionId) || !_content.Professions.ContainsKey(professionId))
            {
                Reject(client, ErrorCode.InvalidArgument, "Ce PNJ n'enseigne pas ce métier.");
                return;
            }

            if (!player.Professions.Learn(professionId))
            {
                Reject(client, ErrorCode.NotAllowed, "Métier déjà appris.");
                return;
            }

            _logger.LogInformation("ProfessionLearned {EntityId} {ProfessionId}", player.EntityId, professionId);
            SendProfessions(client, player);
            SendNpc(client, player, npc);
        }

        private void AcceptQuest(IWorldClient client, Player player, NpcInstance npc, ushort questId)
        {
            if (!_content.Quests.TryGetValue(questId, out QuestDefinition? quest) || quest.GiverNpcId != npc.Definition.Id || !npc.Definition.OfferedQuests.Contains(questId))
            {
                Reject(client, ErrorCode.QuestError, "Ce PNJ ne propose pas cette quête.");
                return;
            }

            if (!player.Quests.Accept(quest))
            {
                Reject(client, ErrorCode.QuestError, "Quête déjà acceptée ou terminée.");
                return;
            }

            SyncCollectProgress(player);
            SendQuestLog(client, player);
            SendNpc(client, player, npc);
        }

        private void TurnInQuest(IWorldClient client, Player player, NpcInstance npc, ushort questId)
        {
            if (!_content.Quests.TryGetValue(questId, out QuestDefinition? quest) || quest.TurnInNpcId != npc.Definition.Id || !player.Quests.IsActive(questId))
            {
                Reject(client, ErrorCode.QuestError, "Cette quête ne se rend pas ici.");
                return;
            }

            SyncCollectProgress(player);
            if (!player.Quests.IsSatisfied(quest))
            {
                Reject(client, ErrorCode.QuestError, "Objectifs non atteints.");
                return;
            }

            // Les objets à remettre sont retirés, puis la récompense est ajoutée. Si elle ne tient pas, tout est annulé.
            RemoveCollectItems(player, quest);
            if (quest.Reward.ItemId != 0)
            {
                ItemDefinition rewardItem = _content.Items[quest.Reward.ItemId];
                if (!player.Inventory.TryAdd(rewardItem, quest.Reward.ItemCount))
                {
                    RestoreCollectItems(player, quest);
                    Reject(client, ErrorCode.InventoryFull, "Sac plein : libérez de la place.");
                    return;
                }
            }

            player.Quests.Complete(questId);
            player.AddCopper(quest.Reward.Copper);
            GrantExperience(player, quest.Reward.Experience);

            _logger.LogInformation("QuestCompleted {EntityId} {Quest}", player.EntityId, quest.Key);
            SendInventory(client, player);
            SendStats(client, player);
            SendQuestLog(client, player);
            SendNpc(client, player, npc);
        }

        private void Buy(IWorldClient client, Player player, NpcInstance npc, ushort itemId, ushort quantity)
        {
            ShopEntry? entry = npc.Definition.Shop.Find(e => e.ItemId == itemId);
            if (entry == null || quantity == 0)
            {
                Reject(client, ErrorCode.InvalidArgument, "Cet objet n'est pas à vendre ici.");
                return;
            }

            ItemDefinition item = _content.Items[itemId];
            if (!player.Inventory.CanAdd(item, quantity))
            {
                Reject(client, ErrorCode.InventoryFull, "Sac plein.");
                return;
            }

            long total = (long)entry.Price * quantity;
            if (!player.TrySpendCopper(total))
            {
                Reject(client, ErrorCode.NotEnoughCopper, "Pas assez de cuivre.");
                return;
            }

            player.Inventory.TryAdd(item, quantity);
            SyncCollectProgress(player);
            SendInventory(client, player);
            SendStats(client, player);
        }

        private void Sell(IWorldClient client, Player player, ushort itemId, ushort quantity)
        {
            if (!_content.Items.TryGetValue(itemId, out ItemDefinition? item) || item.SellPrice <= 0 || quantity == 0)
            {
                Reject(client, ErrorCode.InvalidArgument, "Cet objet ne se vend pas.");
                return;
            }

            if (!player.Inventory.TryRemove(itemId, quantity))
            {
                Reject(client, ErrorCode.NoSuchItem, "Objet absent du sac.");
                return;
            }

            player.AddCopper((long)item.SellPrice * quantity);
            SyncCollectProgress(player);
            SendInventory(client, player);
            SendStats(client, player);
        }

        /// <summary>Un objectif « parler » progresse quand le joueur ouvre le dialogue du PNJ visé.</summary>
        private void CreditTalk(Player player, ushort npcDefinitionId)
        {
            foreach (ushort questId in new List<ushort>(player.Quests.ActiveIds))
            {
                QuestDefinition quest = _content.Quests[questId];
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    QuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.Kind == ObjectiveKind.Talk && objective.TargetId == npcDefinitionId)
                    {
                        player.Quests.Advance(quest, i, 1);
                    }
                }
            }
        }

        /// <summary>Un objectif « tuer » progresse pour chaque contributeur d'une créature de ce modèle.</summary>
        private void CreditKill(Player player, ushort templateId)
        {
            bool changed = false;
            foreach (ushort questId in new List<ushort>(player.Quests.ActiveIds))
            {
                QuestDefinition quest = _content.Quests[questId];
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    QuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.Kind == ObjectiveKind.Kill && objective.TargetId == templateId)
                    {
                        changed |= player.Quests.Advance(quest, i, 1);
                    }
                }
            }

            if (changed && TryGetClient(player, out IWorldClient? client))
            {
                SendQuestLog(client, player);
            }
        }

        /// <summary>Les objectifs « atteindre une zone » se vérifient à chaque tick.</summary>
        private void CheckGoToObjectives(Player player)
        {
            if (!player.IsAlive)
            {
                return;
            }

            bool changed = false;
            foreach (ushort questId in new List<ushort>(player.Quests.ActiveIds))
            {
                QuestDefinition quest = _content.Quests[questId];
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    QuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.Kind != ObjectiveKind.GoTo || player.Quests.Progress(questId, i) >= objective.Count)
                    {
                        continue;
                    }

                    if (DistanceXZ(player.X, player.Z, objective.X, objective.Z) <= objective.Radius)
                    {
                        changed |= player.Quests.Advance(quest, i, 1);
                    }
                }
            }

            if (changed && TryGetClient(player, out IWorldClient? client))
            {
                SendQuestLog(client, player);
            }
        }

        /// <summary>Met à jour les objectifs « collecter » d'après le contenu réel du sac.</summary>
        private void SyncCollectProgress(Player player)
        {
            foreach (ushort questId in new List<ushort>(player.Quests.ActiveIds))
            {
                QuestDefinition quest = _content.Quests[questId];
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    QuestObjectiveDefinition objective = quest.Objectives[i];
                    if (objective.Kind == ObjectiveKind.Collect)
                    {
                        int owned = player.Inventory.CountInBag(objective.TargetId);
                        player.Quests.SetProgress(questId, i, System.Math.Min(objective.Count, owned));
                    }
                }
            }
        }

        private void RemoveCollectItems(Player player, QuestDefinition quest)
        {
            foreach (QuestObjectiveDefinition objective in quest.Objectives)
            {
                if (objective.Kind == ObjectiveKind.Collect)
                {
                    player.Inventory.TryRemove(objective.TargetId, objective.Count);
                }
            }
        }

        private void RestoreCollectItems(Player player, QuestDefinition quest)
        {
            foreach (QuestObjectiveDefinition objective in quest.Objectives)
            {
                if (objective.Kind == ObjectiveKind.Collect)
                {
                    player.Inventory.TryAdd(_content.Items[objective.TargetId], objective.Count);
                }
            }
        }

        /// <summary>Ajoute de l'expérience et annonce les montées de niveau au joueur.</summary>
        private void GrantExperience(Player player, int amount)
        {
            int levels = player.GrantExperience(amount);
            if (levels > 0)
            {
                _logger.LogInformation("LevelUp {EntityId} {Level}", player.EntityId, player.Level);
            }

            if (TryGetClient(player, out IWorldClient? client))
            {
                SendStats(client, player);
            }
        }

        /// <summary>Retrouve la connexion d'un joueur, pour lui envoyer une mise à jour ciblée.</summary>
        private bool TryGetClient(Player player, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IWorldClient? client)
        {
            foreach (PlayerSlot slot in _players.Values)
            {
                if (slot.Player == player)
                {
                    client = slot.Client;
                    return true;
                }
            }

            client = null;
            return false;
        }

        private NpcInstance? FindNpc(uint entityId)
        {
            foreach (NpcInstance npc in _npcs)
            {
                if (npc.EntityId == entityId)
                {
                    return npc;
                }
            }

            return null;
        }

        /// <summary>Zone courante, envoyée une fois à l'entrée dans ce monde (login ou transfert d'instance).</summary>
        private void SendZoneInfo(IWorldClient client)
        {
            client.TrySend(OutboundFrame.Rent(Opcode.ZoneInfo, new ZoneInfoMessage(_content.ZoneId), _writer));
        }

        private void SendStats(IWorldClient client, Player player)
        {
            PrimaryStats total = player.Class.StatsAtLevel(player.Level).Add(player.Bonuses.Stats);

            var stats = new StatsMessage(
                (ushort)player.Level,
                (uint)player.Experience,
                (uint)player.ExperienceToNextLevel,
                player.Copper,
                total.Strength,
                total.Agility,
                total.Intelligence,
                total.Stamina,
                total.Spirit,
                player.Derived.AttackPower,
                player.Derived.SpellPower,
                player.Derived.CriticalChance,
                player.Derived.Armor);
            client.TrySend(OutboundFrame.Rent(Opcode.Stats, stats, _writer));
        }

        private void SendQuestLog(IWorldClient client, Player player)
        {
            var active = new List<(ushort QuestId, string Title, IReadOnlyList<QuestObjectiveView> Objectives)>();
            foreach (ushort questId in player.Quests.ActiveIds)
            {
                QuestDefinition quest = _content.Quests[questId];
                var objectives = new List<QuestObjectiveView>();
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    QuestObjectiveDefinition objective = quest.Objectives[i];
                    objectives.Add(new QuestObjectiveView(
                        (byte)objective.Kind,
                        objective.TargetId,
                        (ushort)player.Quests.Progress(questId, i),
                        (ushort)objective.Count));
                }

                active.Add((questId, quest.Title, objectives));
            }

            var completed = new List<ushort>(player.Quests.CompletedIds);
            client.TrySend(OutboundFrame.Rent(Opcode.Quests, new QuestLogMessage(active, completed), _writer));
        }

        /// <summary>Dialogue du PNJ, vu par ce joueur : quêtes proposées, quêtes à rendre, boutique.</summary>
        private void SendNpc(IWorldClient client, Player player, NpcInstance npc)
        {
            var offered = new List<ushort>();
            var turnIn = new List<ushort>();

            foreach (ushort questId in npc.Definition.OfferedQuests)
            {
                if (player.Quests.IsAvailable(questId) && _content.Quests.ContainsKey(questId))
                {
                    offered.Add(questId);
                }
            }

            foreach (ushort questId in player.Quests.ActiveIds)
            {
                QuestDefinition quest = _content.Quests[questId];
                if (quest.TurnInNpcId == npc.Definition.Id)
                {
                    turnIn.Add(questId);
                }
            }

            var shop = new List<(ushort ItemId, int Price)>();
            foreach (ShopEntry entry in npc.Definition.Shop)
            {
                shop.Add((entry.ItemId, entry.Price));
            }

            var teachable = new List<byte>();
            foreach (byte professionId in npc.Definition.TeachesProfessions)
            {
                if (!player.Professions.IsLearned(professionId))
                {
                    teachable.Add(professionId);
                }
            }

            var adventures = new List<AdventureSummary>();
            foreach (ushort adventureId in npc.Definition.OfferedAdventureIds)
            {
                if (_content.Adventures.TryGetValue(adventureId, out AdventureDefinition? adventure))
                {
                    adventures.Add(new AdventureSummary(
                        adventure.Id,
                        adventure.Name,
                        (byte)adventure.RecommendedLevel,
                        (byte)adventure.MinPlayers,
                        (byte)adventure.MaxPlayers,
                        (byte)adventure.Difficulty,
                        (byte)adventure.BossCount,
                        (byte)adventure.DungeonCount));
                }
            }

            var message = new NpcMessage(npc.EntityId, npc.Definition.Name, npc.Definition.Text, offered, turnIn, shop, teachable, adventures);
            client.TrySend(OutboundFrame.Rent(Opcode.Npc, message, _writer));
        }
    }
}
