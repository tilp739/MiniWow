using GameShared.Messages;
using GameShared.Protocol;
using MiniWow.Client.Characters;
using MiniWow.Client.Data;
using MiniWow.Client.Gameplay;
using MiniWow.Client.UI;
using UnityEngine;

namespace MiniWow.Client.Network
{
    /// <summary>
    /// Point d'entrée réseau du client : connexion, handshake, entrée dans le monde, envoi des actions du joueur,
    /// et distribution des messages serveur vers l'état de jeu et l'interface.
    /// </summary>
    public sealed class GameClient : MonoBehaviour
    {
        public enum ClientState
        {
            Disconnected,
            Handshaking,
            LoggingIn,
            CharacterSelect,
            CreatingCharacter,
            EnteringWorld,
            InWorld,
        }

        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7777;
        [SerializeField] private LocalPlayerController _localPlayer;
        [SerializeField] private RemoteEntities _remoteEntities; // optionnel : sans lui, seul le joueur local est affiché
        [SerializeField] private GameUI _ui; // optionnel : interface de jeu

        private readonly ServerConnection _connection = new ServerConnection();
        private readonly PacketWriter _writer = new PacketWriter(256);
        private readonly GameState _state = new GameState();

        public ClientState State { get; private set; } = ClientState.Disconnected;

        public GameState Game => _state;

        public uint LocalEntityId { get; private set; }

        public uint LastServerTick { get; private set; }

        public bool IsInWorld => State == ClientState.InWorld;

        /// <summary>Classe du personnage en jeu (1 Guerrier, 2 Mage, 3 Prêtre), connue une fois entré dans le monde.</summary>
        public byte ClassId { get; private set; } = 1;

        // ----- Compte et personnages -----

        public void SendRegister(string username, string password)
        {
            if (State == ClientState.LoggingIn)
            {
                Send(Opcode.Register, new RegisterMessage(username, password));
            }
        }

        public void SendLogin(string username, string password)
        {
            if (State == ClientState.LoggingIn)
            {
                Send(Opcode.Login, new LoginMessage(username, password));
            }
        }

        /// <summary>Quitte l'écran de sélection pour celui de création de personnage. Purement local, pas de réseau.</summary>
        public void EnterCharacterCreation()
        {
            if (State == ClientState.CharacterSelect)
            {
                State = ClientState.CreatingCharacter;
                _state.CreateCharacterError = string.Empty;
            }
        }

        public void CancelCharacterCreation()
        {
            if (State == ClientState.CreatingCharacter)
            {
                State = ClientState.CharacterSelect;
            }
        }

        public void SendCreateCharacter(string name, byte classId, byte raceId)
        {
            if (State == ClientState.CreatingCharacter)
            {
                Send(Opcode.CreateCharacter, new CreateCharacterMessage(name, classId, raceId));
            }
        }

        public void SendEnterWorldByCharacter(long characterId, byte classId)
        {
            if (State == ClientState.CharacterSelect)
            {
                ClassId = classId;
                State = ClientState.EnteringWorld;
                Send(Opcode.EnterWorldByCharacter, new EnterWorldByCharacterMessage(characterId));
            }
        }

        // ----- Envoi des actions : chaque méthode ne fait qu'emballer une demande ; le serveur valide. -----

        public void SendMoveInput(uint sequence, sbyte directionX, sbyte directionZ)
        {
            if (IsInWorld)
            {
                Send(Opcode.MoveInput, new MoveInputMessage(sequence, directionX, directionZ));
            }
        }

        public void SendAttack(uint targetEntityId)
        {
            if (IsInWorld)
            {
                Send(Opcode.AttackRequest, new AttackRequestMessage(targetEntityId));
            }
        }

        /// <summary>Arrête l'attaque automatique en cours (0 : aucun identifiant d'entité n'est jamais nul).</summary>
        public void SendDisengage()
        {
            if (IsInWorld)
            {
                Send(Opcode.AttackRequest, new AttackRequestMessage(0));
            }
        }

        public void SendCast(ushort skillId, uint targetEntityId)
        {
            if (IsInWorld)
            {
                Send(Opcode.CastSkill, new CastSkillMessage(skillId, targetEntityId));
            }
        }

        public void SendLoot(uint corpseEntityId)
        {
            if (IsInWorld)
            {
                Send(Opcode.LootRequest, new LootRequestMessage(corpseEntityId));
            }
        }

        public void SendNpc(NpcActionKind kind, uint npcEntityId, ushort argument = 0, ushort quantity = 0)
        {
            if (IsInWorld)
            {
                Send(Opcode.NpcAction, new NpcActionMessage(kind, npcEntityId, argument, quantity));
            }
        }

        public void SendInventory(InventoryActionKind kind, byte from, byte to = 0, ushort amount = 0)
        {
            if (IsInWorld)
            {
                Send(Opcode.InventoryAction, new InventoryActionMessage(kind, from, to, amount));
            }
        }

        public void SendParty(PartyActionKind kind, uint targetEntityId = 0)
        {
            if (IsInWorld)
            {
                Send(Opcode.PartyAction, new PartyActionMessage(kind, targetEntityId));
            }
        }

        /// <summary><paramref name="adventureId"/> : 0 pour une porte simple (Crypt), ou l'aventure choisie au tableau.</summary>
        public void SendDungeonEnter(uint gateNpcEntityId, ushort adventureId = 0)
        {
            if (IsInWorld)
            {
                Send(Opcode.DungeonAction, new DungeonActionMessage(DungeonActionKind.Enter, gateNpcEntityId, adventureId));
            }
        }

        public void SendChat(ChatChannel channel, string target, string text)
        {
            if (IsInWorld)
            {
                Send(Opcode.ChatSend, new ChatSendMessage(channel, target ?? string.Empty, text));
            }
        }

        public void SendGather(uint nodeEntityId)
        {
            if (IsInWorld)
            {
                Send(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Gather, nodeEntityId, 0));
            }
        }

        public void SendCraft(ushort recipeId)
        {
            if (IsInWorld)
            {
                Send(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Craft, 0, recipeId));
            }
        }

        public void SendLearnProfession(uint npcEntityId, byte professionId)
        {
            if (IsInWorld)
            {
                Send(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, npcEntityId, professionId, 0));
            }
        }

        /// <summary>Cheat de test (touche F1) : demande un objet par emplacement d'équipement.</summary>
        public void SendDebugGiveTestGear()
        {
            if (IsInWorld)
            {
                Send(Opcode.DebugGiveTestGear, new DebugGiveTestGearMessage());
            }
        }

        // ----- Cycle de vie -----

        private void Start()
        {
            if (_localPlayer == null)
            {
                Debug.LogError("GameClient : LocalPlayer non assigné.", this);
                enabled = false;
                return;
            }

            ContentCatalog.EnsureLoaded();
            Connect();
        }

        private void Update()
        {
            while (_connection.TryDequeue(out ServerConnection.ReceivedFrame frame))
            {
                HandleFrame(frame);
            }

            if (State != ClientState.Disconnected && !_connection.IsOpen)
            {
                Debug.LogWarning($"Connexion perdue : {_connection.CloseReason}", this);
                State = ClientState.Disconnected;
            }

            // Décompte local des barres (lancer, annonce de boss) : l'autorité reste sur le serveur.
            float dt = Time.deltaTime;
            if (_state.CastRemainingSeconds > 0f)
            {
                _state.CastRemainingSeconds = Mathf.Max(0f, _state.CastRemainingSeconds - dt);
            }

            if (_state.TelegraphRemainingSeconds > 0f)
            {
                _state.TelegraphRemainingSeconds = Mathf.Max(0f, _state.TelegraphRemainingSeconds - dt);
            }
        }

        private void OnDestroy()
        {
            _connection.Dispose();
        }

        private void Connect()
        {
            try
            {
                _connection.Connect(_host, _port);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                Debug.LogError($"Impossible de joindre {_host}:{_port} : {ex.Message}", this);
                return;
            }

            State = ClientState.Handshaking;
            // Le nom de client du Hello n'identifie plus le joueur : l'identité vient du compte (Login).
            Send(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "client"));
        }

        // ----- Réception -----

        private void HandleFrame(ServerConnection.ReceivedFrame frame)
        {
            switch (frame.Opcode)
            {
                case Opcode.HelloAck:
                    HandleHelloAck(HelloAckMessage.Read(frame.Payload));
                    break;

                case Opcode.WorldSnapshot:
                    HandleSnapshot(WorldSnapshotMessage.Read(frame.Payload));
                    break;

                case Opcode.CombatEvent:
                    HandleCombatEvent(CombatEventMessage.Read(frame.Payload));
                    break;

                case Opcode.Error:
                    HandleError(ErrorMessage.Read(frame.Payload));
                    break;

                case Opcode.Inventory:
                    _state.ApplyInventory(InventoryMessage.Read(frame.Payload));
                    OrcEquipmentBridge.Apply(_localPlayer.OrcCharacter, _state);
                    break;

                case Opcode.Quests:
                    _state.ApplyQuestLog(QuestLogMessage.Read(frame.Payload));
                    break;

                case Opcode.Stats:
                    _state.ApplyStats(StatsMessage.Read(frame.Payload));
                    break;

                case Opcode.ZoneInfo:
                    _state.ZoneId = ZoneInfoMessage.Read(frame.Payload).ZoneId;
                    break;

                case Opcode.Professions:
                    _state.ApplyProfessions(ProfessionsMessage.Read(frame.Payload));
                    break;

                case Opcode.Npc:
                    _state.Dialogue = NpcMessage.Read(frame.Payload);
                    break;

                case Opcode.Party:
                    _state.ApplyParty(PartyMessage.Read(frame.Payload));
                    break;

                case Opcode.Chat:
                    HandleChat(ChatLineMessage.Read(frame.Payload));
                    break;

                case Opcode.RegisterResult:
                    HandleRegisterResult(RegisterResultMessage.Read(frame.Payload));
                    break;

                case Opcode.LoginResult:
                    HandleLoginResult(LoginResultMessage.Read(frame.Payload));
                    break;

                case Opcode.CharacterList:
                    _state.ApplyCharacterList(CharacterListMessage.Read(frame.Payload));
                    State = ClientState.CharacterSelect;
                    break;

                case Opcode.CreateCharacterResult:
                    HandleCreateCharacterResult(CreateCharacterResultMessage.Read(frame.Payload));
                    break;

                default:
                    Debug.LogWarning($"Opcode inattendu du serveur : {(ushort)frame.Opcode}", this);
                    break;
            }
        }

        private void HandleHelloAck(HelloAckMessage ack)
        {
            if (!ack.Accepted)
            {
                Debug.LogError($"Version de protocole refusée par le serveur (serveur {ack.ServerProtocolVersion}).", this);
                _connection.Dispose();
                State = ClientState.Disconnected;
                return;
            }

            // L'identité vient maintenant d'un compte : attend un Login (ou Register) venant de l'interface.
            State = ClientState.LoggingIn;
        }

        private void HandleRegisterResult(RegisterResultMessage result)
        {
            _state.RegisterError = result.Success ? string.Empty : result.ErrorText;
        }

        private void HandleLoginResult(LoginResultMessage result)
        {
            if (!result.Success)
            {
                _state.LoginError = result.ErrorText;
                return;
            }

            _state.LoginError = string.Empty;
            // CharacterSelect est fixé par le CharacterList qui suit immédiatement un Login réussi.
        }

        private void HandleCreateCharacterResult(CreateCharacterResultMessage result)
        {
            _state.CreateCharacterError = result.Success ? string.Empty : result.ErrorText;
            // En cas de succès, le CharacterList qui suit ramène à CharacterSelect.
        }

        private void HandleSnapshot(WorldSnapshotMessage snapshot)
        {
            if (State == ClientState.EnteringWorld)
            {
                State = ClientState.InWorld;
            }

            LocalEntityId = snapshot.YourEntityId;
            LastServerTick = snapshot.ServerTick;
            _localPlayer.ApplySnapshot(snapshot);

            if (_remoteEntities != null)
            {
                _remoteEntities.ApplySnapshot(snapshot);
            }
        }

        private void HandleCombatEvent(CombatEventMessage combatEvent)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.CastStart:
                    _state.CastingSkillId = 0;
                    _state.CastTotalSeconds = combatEvent.Amount / 1000f;
                    _state.CastRemainingSeconds = _state.CastTotalSeconds;
                    break;

                case CombatEventKind.Telegraph:
                    _state.TelegraphRemainingSeconds = combatEvent.Amount / 1000f;
                    break;

                case CombatEventKind.Damage:
                    PlayAnimationEvent(combatEvent.SourceEntityId, attack: true);
                    PlayAnimationEvent(combatEvent.TargetEntityId, attack: false);
                    ShowFloatingNumber(combatEvent.TargetEntityId, combatEvent.Amount, isHeal: false);
                    break;

                case CombatEventKind.Heal:
                    ShowFloatingNumber(combatEvent.TargetEntityId, combatEvent.Amount, isHeal: true);
                    break;
            }

            if (_ui != null)
            {
                _ui.AddLine(CombatTextFormatter.Format(combatEvent, LocalEntityId, NameOf));
            }
        }

        /// <summary>Nombre flottant (dégâts/soin) à la position courante de l'entité, si elle est visible.</summary>
        private void ShowFloatingNumber(uint entityId, int amount, bool isHeal)
        {
            if (_ui == null || !TryGetWorldPosition(entityId, out Vector3 position))
            {
                return;
            }

            _ui.AddFloatingNumber(position, amount, isHeal);
        }

        /// <summary>Position actuelle (approchée) d'une entité visible, locale ou distante.</summary>
        private bool TryGetWorldPosition(uint entityId, out Vector3 position)
        {
            if (entityId == LocalEntityId)
            {
                position = _localPlayer.transform.position;
                return true;
            }

            if (_remoteEntities != null && _remoteEntities.TryGetInfo(entityId, out EntityInfo info))
            {
                position = info.Position;
                return true;
            }

            position = default;
            return false;
        }

        private void HandleError(ErrorMessage error)
        {
            Debug.LogWarning($"Erreur serveur {error.Code} : {error.Text}", this);
            if (_ui != null)
            {
                _ui.AddLine($"Refusé : {error.Text}");
            }
        }

        private void HandleChat(ChatLineMessage line)
        {
            string prefix = line.Channel switch
            {
                ChatChannel.Say => "[Local]",
                ChatChannel.Whisper => "[Privé]",
                ChatChannel.Party => "[Groupe]",
                _ => "[?]",
            };
            _state.AddChat($"{prefix} {line.Sender} : {line.Text}");
        }

        /// <summary>
        /// Joue le coup (attaque) ou le flinch (touché) sur le modèle généré de l'entité, local ou distant.
        /// Purement cosmétique ; sans effet si l'entité n'a pas de modèle dédié (pas encore d'Orc, créature, etc.).
        /// </summary>
        private void PlayAnimationEvent(uint entityId, bool attack)
        {
            OrcAnimator animator = null;

            if (entityId == LocalEntityId)
            {
                animator = _localPlayer.OrcAnimator;
            }
            else if (_remoteEntities != null)
            {
                _remoteEntities.TryGetOrcAnimator(entityId, out animator);
            }

            if (animator == null)
            {
                return;
            }

            if (attack)
            {
                animator.PlayAttack();
            }
            else
            {
                animator.PlayHit();
            }
        }

        /// <summary>Nom affichable d'une entité : « Vous », nom de créature, de PNJ, ou joueur.</summary>
        private string NameOf(uint entityId)
        {
            if (entityId == LocalEntityId)
            {
                return "Vous";
            }

            if (_remoteEntities != null && (_remoteEntities.TryGetInfo(entityId, out EntityInfo info) || _remoteEntities.TryGetLastKnown(entityId, out info)))
            {
                switch (info.Kind)
                {
                    case EntityKind.Creature: return CreatureCatalog.NameOf(info.TemplateId);
                    case EntityKind.Npc: return ContentCatalog.NpcName(info.TemplateId);
                    default: return $"Joueur #{entityId}";
                }
            }

            return "Inconnu";
        }

        private void Send<T>(Opcode opcode, T message) where T : IWireMessage
        {
            _writer.Reset();
            message.Write(_writer);
            _connection.Send(FrameCodec.CreateFrame(opcode, 0, _writer.WrittenSpan));
        }
    }
}
