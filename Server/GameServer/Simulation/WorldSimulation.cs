using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GameServer.Configuration;
using GameServer.Content;
using GameServer.Domain;
using GameServer.Domain.Classes;
using GameServer.Domain.Combat;
using GameServer.Domain.Creatures;
using GameServer.Domain.Items;
using GameServer.Domain.Movement;
using GameServer.Domain.Npcs;
using GameServer.Domain.Quests;
using GameServer.Domain.Spatial;
using GameServer.Persistence.Model;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>
    /// État du monde. Seul le thread de la boucle de tick appelle <see cref="Tick"/> :
    /// aucun verrou n'est nécessaire dans les règles de jeu.
    /// Découpé en fichiers partiels : combat de base (WorldSimulation.Combat.cs) et compétences (WorldSimulation.Skills.cs).
    /// </summary>
    public sealed partial class WorldSimulation
    {
        private const float SpawnSpacing = 2f;
        private const string DefaultName = "Voyageur";

        /// <summary>Intervalle entre deux sauvegardes automatiques des personnages persistants en ligne.</summary>
        private const float AutosaveIntervalSeconds = 60f;

        private readonly ConcurrentQueue<WorldCommand> _commands = new ConcurrentQueue<WorldCommand>();
        private readonly Dictionary<int, PlayerSlot> _players = new Dictionary<int, PlayerSlot>();
        private readonly List<CreatureInstance> _creatures = new List<CreatureInstance>();
        private readonly List<Player> _roster = new List<Player>(WorldSnapshotMessage.MaxEntities);
        private readonly List<IPlacedEntity> _placed = new List<IPlacedEntity>(WorldSnapshotMessage.MaxEntities);
        private readonly List<IPlacedEntity> _visible = new List<IPlacedEntity>(WorldSnapshotMessage.MaxEntities);
        private readonly List<EntityState> _visibleStates = new List<EntityState>(WorldSnapshotMessage.MaxEntities);
        private readonly List<CombatEventMessage> _events = new List<CombatEventMessage>();
        private readonly List<uint> _rewardRecipients = new List<uint>();
        private readonly InterestManager<IPlacedEntity> _interest;
        private readonly CombatResolver _resolver;
        private readonly Random _random;
        private readonly PacketWriter _writer = new PacketWriter(512);
        private readonly WorldContent _content;
        private readonly GameServerOptions _options;
        private readonly ILogger<WorldSimulation> _logger;
        private readonly IWorldDirectory _directory;
        private readonly ICharacterSaveQueue _saveQueue;
        private readonly bool _isInstance;

        private uint _tick;
        private float _autosaveCountdown = AutosaveIntervalSeconds;

        /// <summary>Compte à rebours avant fermeture d'une instance terminée, ou négatif si l'instance est ouverte.</summary>
        private float _closeCountdown = -1f;

        public WorldSimulation(
            GameServerOptions options,
            WorldContent content,
            ILogger<WorldSimulation> logger,
            IWorldDirectory directory,
            ICharacterSaveQueue saveQueue,
            bool isInstance)
        {
            _options = options;
            _content = content;
            _logger = logger;
            _directory = directory;
            _saveQueue = saveQueue;
            _isInstance = isInstance;
            _interest = new InterestManager<IPlacedEntity>(options.InterestCellSize, options.InterestRadius);

            _random = options.RandomSeed.HasValue ? new Random(options.RandomSeed.Value) : new Random();
            _resolver = new CombatResolver(options.Combat, _random);

            foreach (CreatureSpawn spawn in content.CreatureSpawns)
            {
                CreatureTemplate template = content.Creatures[spawn.TemplateId];
                var creature = new CreatureInstance(_directory.AllocateEntityId(), template, spawn.X, spawn.Z);
                if (template.IsBoss)
                {
                    creature.ShockwaveCooldown = template.ShockwaveIntervalSeconds;
                }

                _creatures.Add(creature);
            }

            InitializeNpcs();
            InitializeGatheringNodes();
            _logger.LogInformation("WorldLoaded {ZoneId} {Creatures} {Npcs}", content.ZoneId, _creatures.Count, _npcs.Count);
        }

        public int PlayerCount => _players.Count;

        public string ZoneId => _content.ZoneId;

        public float SpawnX => _content.PlayerSpawnX;

        public float SpawnZ => _content.PlayerSpawnZ;

        /// <summary>Appelé depuis les threads réseau. Thread-safe.</summary>
        public void Enqueue(WorldCommand command)
        {
            _commands.Enqueue(command);
        }

        /// <summary>Un tick de simulation : commandes, joueurs, lancers, créatures, puis diffusion.</summary>
        public void Tick(float deltaSeconds)
        {
            DrainCommands();

            foreach (PlayerSlot slot in _players.Values)
            {
                Player player = slot.Player;
                player.Tick(deltaSeconds);

                if (player.IsAlive)
                {
                    if (player.AdvanceCast(deltaSeconds))
                    {
                        ResolveCast(player);
                    }

                    player.Motor.Advance(deltaSeconds);
                    CheckGoToObjectives(player);
                    TryAutoAttackSwing(player);
                }
                else if (player.IsReadyToRespawn)
                {
                    RespawnPlayer(player);
                }
            }

            _roster.Clear();
            foreach (PlayerSlot slot in _players.Values)
            {
                _roster.Add(slot.Player);
            }

            foreach (CreatureInstance creature in _creatures)
            {
                uint? target = CreatureAi.Update(creature, _roster, deltaSeconds);
                if (target.HasValue)
                {
                    ResolveCreatureAttack(creature, target.Value);
                }
            }

            UpdateBosses(deltaSeconds);
            UpdateInstanceLifetime(deltaSeconds);
            AutosavePlayers(deltaSeconds);

            foreach (GatheringNodeInstance node in _nodes)
            {
                node.Tick(deltaSeconds);
            }

            _tick++;
            BroadcastSnapshots();
            _events.Clear();

            // Les renforts morts disparaissent après la diffusion de leur événement de mort.
            _creatures.RemoveAll(creature => creature.Transient && !creature.IsAlive);
        }

        public bool IsInstance => _isInstance;

        /// <summary>Retire un joueur de ce monde et le rend à l'appelant (transfert vers un autre monde).</summary>
        public Player? ReleasePlayer(IWorldClient client)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return null;
            }

            // Le groupe est conservé : un transfert (donjon) ne sort pas du groupe.
            _players.Remove(client.ClientId);
            return slot.Player;
        }

        /// <summary>Accueille un joueur transféré depuis un autre monde, à la position donnée.</summary>
        public void AdoptPlayer(IWorldClient client, Player player, float x, float z)
        {
            player.Motor.Teleport(x, z);
            _players.Add(client.ClientId, new PlayerSlot(client, player));
            client.SetWorld(this);
            SendZoneInfo(client);
            SendInventory(client, player);
            SendStats(client, player);
            SendQuestLog(client, player);
            SendProfessions(client, player);
            SendPartyTo(player.EntityId);
        }

        private void DrainCommands()
        {
            while (_commands.TryDequeue(out WorldCommand command))
            {
                switch (command.Kind)
                {
                    case WorldCommandKind.Enter:
                        AddPlayer(command.Client, command.Name, command.ClassId);
                        break;

                    case WorldCommandKind.EnterWithCharacter:
                        AddPlayerFromSnapshot(command.Client, command.Snapshot!);
                        break;

                    case WorldCommandKind.Move:
                        HandleMove(command);
                        break;

                    case WorldCommandKind.Attack:
                        HandleAttack(command.Client, command.TargetEntityId);
                        break;

                    case WorldCommandKind.CastSkill:
                        HandleCast(command.Client, command.SkillId, command.TargetEntityId);
                        break;

                    case WorldCommandKind.InventoryAction:
                        HandleInventoryAction(command.Client, command.Action);
                        break;

                    case WorldCommandKind.Loot:
                        HandleLoot(command.Client, command.TargetEntityId);
                        break;

                    case WorldCommandKind.NpcAction:
                        HandleNpcAction(command.Client, command.NpcAction);
                        break;

                    case WorldCommandKind.PartyAction:
                        HandlePartyAction(command.Client, command.PartyAction);
                        break;

                    case WorldCommandKind.DungeonAction:
                        HandleDungeonAction(command.Client, command.DungeonAction);
                        break;

                    case WorldCommandKind.Chat:
                        HandleChat(command.Client, command.Chat);
                        break;

                    case WorldCommandKind.ProfessionAction:
                        HandleProfessionAction(command.Client, command.ProfessionAction);
                        break;

                    case WorldCommandKind.DebugGiveTestGear:
                        GiveTestGear(command.Client);
                        break;

                    case WorldCommandKind.Disconnect:
                        if (_players.TryGetValue(command.Client.ClientId, out PlayerSlot? leaving))
                        {
                            uint leavingId = leaving.Player.EntityId;
                            SaveIfPersistent(leaving.Player);
                            _players.Remove(command.Client.ClientId);
                            _directory.UnregisterOnline(leavingId);
                            NotifyPartyChange(_directory.Parties.Forget(leavingId));
                            _logger.LogInformation("PlayerLeftWorld {ClientId}", command.Client.ClientId);
                        }
                        break;
                }
            }
        }

        private void HandleMove(WorldCommand command)
        {
            if (!_players.TryGetValue(command.Client.ClientId, out PlayerSlot? slot) || !slot.Player.IsAlive)
            {
                return;
            }

            Player player = slot.Player;
            bool moving = command.DirectionX != 0 || command.DirectionZ != 0;

            // Se déplacer pendant un lancer l'interrompt, comme en MMO classique.
            if (moving && player.Cast != null)
            {
                player.CancelCast();
                Reject(command.Client, ErrorCode.CastInterrupted, "Lancer interrompu par le déplacement.");
            }

            player.Motor.SetInput(command.Sequence, command.DirectionX, command.DirectionZ);
        }

        private void AddPlayer(IWorldClient client, string name, byte classId)
        {
            if (_players.ContainsKey(client.ClientId))
            {
                return;
            }

            if (_players.Count >= _options.MaxPlayers)
            {
                client.TrySend(OutboundFrame.Rent(Opcode.Error, new ErrorMessage(ErrorCode.WorldFull, "Monde plein."), _writer));
                _logger.LogWarning("WorldFull {ClientId} {MaxPlayers}", client.ClientId, _options.MaxPlayers);
                return;
            }

            if (!_content.Classes.TryGetValue((CharacterClass)classId, out ClassDefinition? definition))
            {
                Reject(client, ErrorCode.InvalidArgument, "Classe inconnue.");
                return;
            }

            // Points d'apparition alignés sur l'axe X, pour éviter que deux joueurs se superposent.
            float spawnX = _content.PlayerSpawnX + _players.Count * SpawnSpacing;
            string playerName = string.IsNullOrWhiteSpace(name) ? DefaultName : name;
            var player = new Player(_directory.AllocateEntityId(), playerName, definition, _options.Movement, spawnX, _content.PlayerSpawnZ);
            _players.Add(client.ClientId, new PlayerSlot(client, player));
            _directory.RegisterOnline(player.EntityId, client, playerName);
            _logger.LogInformation("PlayerEnteredWorld {ClientId} {EntityId} {Class}", client.ClientId, player.EntityId, definition.Key);
            SendZoneInfo(client);
            SendInventory(client, player);
            SendStats(client, player);
            SendQuestLog(client, player);
            SendProfessions(client, player);
        }

        /// <summary>Entrée dans le monde avec un personnage persistant : hydrate le joueur depuis le snapshot chargé.</summary>
        private void AddPlayerFromSnapshot(IWorldClient client, CharacterSnapshot snapshot)
        {
            if (_players.ContainsKey(client.ClientId))
            {
                return;
            }

            if (_players.Count >= _options.MaxPlayers)
            {
                client.TrySend(OutboundFrame.Rent(Opcode.Error, new ErrorMessage(ErrorCode.WorldFull, "Monde plein."), _writer));
                _logger.LogWarning("WorldFull {ClientId} {MaxPlayers}", client.ClientId, _options.MaxPlayers);
                return;
            }

            if (!_content.Classes.TryGetValue((CharacterClass)snapshot.ClassId, out ClassDefinition? definition))
            {
                Reject(client, ErrorCode.InvalidArgument, "Classe inconnue.");
                return;
            }

            var player = new Player(
                _directory.AllocateEntityId(),
                snapshot.Name,
                definition,
                _options.Movement,
                snapshot.X,
                snapshot.Z,
                snapshot.Level,
                snapshot.Experience,
                snapshot.Copper,
                snapshot.CharacterId,
                snapshot.RaceId);

            HydrateInventory(player, snapshot.Inventory);
            HydrateQuestLog(player, snapshot.Quests);
            HydrateProfessions(player, snapshot.Professions);

            _players.Add(client.ClientId, new PlayerSlot(client, player));
            _directory.RegisterOnline(player.EntityId, client, player.Name);
            _logger.LogInformation("PlayerEnteredWorld {ClientId} {EntityId} {CharacterId}", client.ClientId, player.EntityId, snapshot.CharacterId);
            SendZoneInfo(client);
            SendInventory(client, player);
            SendStats(client, player);
            SendQuestLog(client, player);
            SendProfessions(client, player);
        }

        private void HydrateInventory(Player player, InventorySnapshot snapshot)
        {
            foreach (BagSlotSnapshot slot in snapshot.Bag)
            {
                if (_content.Items.TryGetValue(slot.ItemId, out ItemDefinition? item))
                {
                    player.Inventory.RestoreBagSlot(slot.Slot, item, slot.Quantity);
                }
                else
                {
                    _logger.LogWarning("UnknownItemOnLoad {ItemId} {CharacterId}", slot.ItemId, player.CharacterId);
                }
            }

            foreach (EquippedItemSnapshot equipped in snapshot.Equipment)
            {
                if (_content.Items.TryGetValue(equipped.ItemId, out ItemDefinition? item))
                {
                    player.Inventory.RestoreEquipped((EquipSlot)equipped.EquipSlot, item, equipped.Quantity);
                }
                else
                {
                    _logger.LogWarning("UnknownItemOnLoad {ItemId} {CharacterId}", equipped.ItemId, player.CharacterId);
                }
            }

            player.SetBonuses(player.Inventory.ComputeBonuses());
        }

        private void HydrateQuestLog(Player player, QuestLogSnapshot snapshot)
        {
            foreach (ActiveQuestSnapshot active in snapshot.Active)
            {
                if (_content.Quests.ContainsKey(active.QuestId))
                {
                    player.Quests.RestoreActive(active.QuestId, new List<int>(active.Progress).ToArray());
                }
                else
                {
                    _logger.LogWarning("UnknownQuestOnLoad {QuestId} {CharacterId}", active.QuestId, player.CharacterId);
                }
            }

            foreach (ushort questId in snapshot.Completed)
            {
                player.Quests.RestoreCompleted(questId);
            }
        }

        /// <summary>Sauvegarde toutes les 60s les personnages persistants en ligne (perte de données limitée en cas de crash).</summary>
        private void AutosavePlayers(float deltaSeconds)
        {
            _autosaveCountdown -= deltaSeconds;
            if (_autosaveCountdown > 0f)
            {
                return;
            }

            _autosaveCountdown = AutosaveIntervalSeconds;
            foreach (PlayerSlot slot in _players.Values)
            {
                SaveIfPersistent(slot.Player);
            }
        }

        private void SaveIfPersistent(Player player)
        {
            if (player.CharacterId is not long characterId)
            {
                return;
            }

            _saveQueue.TryEnqueue(BuildSnapshot(characterId, player));
        }

        /// <summary>
        /// Construit un snapshot à partir de l'état en mémoire (copie pure, pas d'I/O). Si le joueur est dans
        /// une instance de donjon, la position sauvegardée est celle du point d'apparition du monde principal :
        /// une instance disparaît, inutile d'y laisser un personnage coincé à la reconnexion.
        /// </summary>
        private CharacterSnapshot BuildSnapshot(long characterId, Player player)
        {
            WorldSimulation home = _isInstance ? _directory.Main : this;
            float x = _isInstance ? home.SpawnX : player.X;
            float z = _isInstance ? home.SpawnZ : player.Z;

            var bag = new List<BagSlotSnapshot>();
            for (int i = 0; i < Inventory.BagSize; i++)
            {
                ItemStack? stack = player.Inventory.GetBag(i);
                if (stack != null)
                {
                    bag.Add(new BagSlotSnapshot(i, stack.Item.Id, stack.Quantity));
                }
            }

            var equipment = new List<EquippedItemSnapshot>();
            foreach (EquipSlot slot in Enum.GetValues<EquipSlot>())
            {
                ItemStack? stack = player.Inventory.GetEquipped(slot);
                if (stack != null)
                {
                    equipment.Add(new EquippedItemSnapshot((byte)slot, stack.Item.Id, stack.Quantity));
                }
            }

            var active = new List<ActiveQuestSnapshot>();
            foreach (ushort questId in player.Quests.ActiveIds)
            {
                QuestDefinition definition = _content.Quests[questId];
                var progress = new int[definition.Objectives.Count];
                for (int i = 0; i < progress.Length; i++)
                {
                    progress[i] = player.Quests.Progress(questId, i);
                }

                active.Add(new ActiveQuestSnapshot(questId, progress));
            }

            var completed = new List<ushort>(player.Quests.CompletedIds);

            return new CharacterSnapshot(
                characterId,
                accountId: 0, // non utilisé à l'écriture : la ligne existe déjà, l'identifiant de compte ne change pas.
                player.Name,
                (byte)player.Class.Id,
                player.Level,
                player.Experience,
                player.Copper,
                home.ZoneId,
                x,
                z,
                new InventorySnapshot(bag, equipment),
                new QuestLogSnapshot(active, completed),
                BuildProfessionSnapshots(player),
                player.RaceId);
        }

        private void RespawnPlayer(Player player)
        {
            // Le joueur réapparaît au point de départ de la zone.
            player.Respawn(_content.PlayerSpawnX, _content.PlayerSpawnZ);
            _events.Add(new CombatEventMessage(CombatEventKind.Respawn, player.EntityId, player.EntityId, 0));
        }

        /// <summary>
        /// Envoie à chaque joueur les entités dans son rayon d'intérêt, lui compris, puis les événements de combat
        /// qui concernent une entité visible. Les apparitions et disparitions sont déduites côté client.
        /// </summary>
        private void BroadcastSnapshots()
        {
            if (_players.Count == 0)
            {
                return;
            }

            _placed.Clear();
            foreach (PlayerSlot slot in _players.Values)
            {
                _placed.Add(slot.Player);
            }

            // Les créatures mortes restent indexées ce tick : l'événement de mort doit pouvoir les atteindre.
            // Elles sont retirées des entités affichées ci-dessous.
            foreach (CreatureInstance creature in _creatures)
            {
                _placed.Add(creature);
            }

            foreach (NpcInstance npc in _npcs)
            {
                _placed.Add(npc);
            }

            foreach (GatheringNodeInstance node in _nodes)
            {
                _placed.Add(node);
            }

            _interest.Rebuild(_placed);

            foreach (PlayerSlot slot in _players.Values)
            {
                _visible.Clear();
                _interest.QueryVisible(slot.Player, _visible);

                _visibleStates.Clear();
                foreach (IPlacedEntity entity in _visible)
                {
                    // Un cadavre reste visible tant qu'il contient du butin ; sinon il disparaît.
                    if (entity is CreatureInstance { IsAlive: false, IsLootable: false })
                    {
                        continue;
                    }

                    _visibleStates.Add(ToEntityState(entity));
                }

                // Borne stricte du protocole : au-delà, on ne garde que le joueur lui-même et les premières entités.
                if (_visibleStates.Count > WorldSnapshotMessage.MaxEntities)
                {
                    _visibleStates.RemoveRange(WorldSnapshotMessage.MaxEntities, _visibleStates.Count - WorldSnapshotMessage.MaxEntities);
                }

                var snapshot = new WorldSnapshotMessage(
                    _tick,
                    slot.Player.EntityId,
                    slot.Player.Motor.LastProcessedInput,
                    _visibleStates);

                OutboundFrame frame = OutboundFrame.Rent(Opcode.WorldSnapshot, snapshot, _writer);
                if (!slot.Client.TrySend(frame))
                {
                    _logger.LogWarning("SnapshotDropped {ClientId}", slot.Client.ClientId);
                }

                foreach (CombatEventMessage combatEvent in _events)
                {
                    if (!IsVisible(combatEvent.SourceEntityId) && !IsVisible(combatEvent.TargetEntityId))
                    {
                        continue;
                    }

                    OutboundFrame eventFrame = OutboundFrame.Rent(Opcode.CombatEvent, combatEvent, _writer);
                    slot.Client.TrySend(eventFrame);
                }
            }
        }

        private bool IsVisible(uint entityId)
        {
            foreach (IPlacedEntity entity in _visible)
            {
                if (entity.EntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static EntityState ToEntityState(IPlacedEntity entity)
        {
            if (entity is Player player)
            {
                return new EntityState(
                    player.EntityId,
                    EntityKind.Player,
                    player.RaceId,
                    player.X,
                    player.Z,
                    player.Motor.YawDegrees,
                    (ushort)player.Vitals.Current,
                    (ushort)player.Vitals.MaxHealth,
                    (ushort)player.Resource.Current,
                    (ushort)player.Resource.Max,
                    (ushort)player.Level);
            }

            if (entity is NpcInstance npc)
            {
                return new EntityState(npc.EntityId, EntityKind.Npc, npc.Definition.Id, npc.X, npc.Z, 0f, 0, 0);
            }

            if (entity is GatheringNodeInstance node)
            {
                ushort available = node.IsAvailable ? (ushort)1 : (ushort)0;
                return new EntityState(node.EntityId, EntityKind.GatheringNode, node.Definition.Id, node.X, node.Z, 0f, available, 1);
            }

            var creature = (CreatureInstance)entity;
            return new EntityState(
                creature.EntityId,
                EntityKind.Creature,
                creature.Template.Id,
                creature.X,
                creature.Z,
                creature.YawDegrees,
                (ushort)creature.Vitals.Current,
                (ushort)creature.Vitals.MaxHealth);
        }

        private void Reject(IWorldClient client, ErrorCode code, string text)
        {
            client.TrySend(OutboundFrame.Rent(Opcode.Error, new ErrorMessage(code, text), _writer));
        }

        private Player? FindPlayer(uint entityId)
        {
            foreach (PlayerSlot slot in _players.Values)
            {
                if (slot.Player.EntityId == entityId)
                {
                    return slot.Player;
                }
            }

            return null;
        }

        private CreatureInstance? FindLivingCreature(uint entityId)
        {
            foreach (CreatureInstance creature in _creatures)
            {
                if (creature.EntityId == entityId && creature.IsAlive)
                {
                    return creature;
                }
            }

            return null;
        }

        private static float DistanceXZ(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax;
            float dz = bz - az;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        private sealed class PlayerSlot
        {
            public PlayerSlot(IWorldClient client, Player player)
            {
                Client = client;
                Player = player;
            }

            public IWorldClient Client { get; }

            public Player Player { get; }
        }
    }
}
