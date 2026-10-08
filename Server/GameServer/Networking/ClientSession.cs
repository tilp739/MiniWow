using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using GameServer.Configuration;
using GameServer.Persistence.Accounts;
using GameServer.Persistence.Characters;
using GameServer.Persistence.Model;
using GameServer.Simulation;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking
{
    /// <summary>
    /// Connexion d'un client. Lit et valide les trames, traduit les messages en commandes de simulation,
    /// et envoie les trames sortantes via une file bornée.
    /// </summary>
    public sealed class ClientSession : IWorldClient
    {
        private const int ReadBufferSize = 2 * (ProtocolConstants.LengthPrefixSize + ProtocolConstants.MaxClientBodySize);
        private const int OutboxCapacity = 256;
        private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(2);

        private readonly TcpClient _tcp;
        private volatile WorldSimulation _world;
        private readonly IWorldDirectory _directory;
        private readonly GameServerOptions _options;
        private readonly IAccountRepository _accounts;
        private readonly ICharacterRepository _characters;
        private readonly ILogger _logger;
        private readonly Channel<OutboundFrame> _outbox;
        private readonly TokenBucket _messageLimiter;
        private readonly PacketWriter _replyWriter = new PacketWriter(256);

        private SessionState _state = SessionState.AwaitingHello;
        private string _name = string.Empty;
        private long _accountId;
        private int _violations;

        public ClientSession(
            int clientId,
            TcpClient tcp,
            IWorldDirectory directory,
            GameServerOptions options,
            IAccountRepository accounts,
            ICharacterRepository characters,
            TimeProvider time,
            ILogger logger)
        {
            ClientId = clientId;
            _tcp = tcp;
            _directory = directory;
            _world = directory.Main;
            _options = options;
            _accounts = accounts;
            _characters = characters;
            _logger = logger;
            _outbox = Channel.CreateBounded<OutboundFrame>(new BoundedChannelOptions(OutboxCapacity) { SingleReader = true });
            _messageLimiter = new TokenBucket(time, options.MessagesPerSecond, options.MessageBurst);
        }

        public int ClientId { get; }

        public bool TrySend(OutboundFrame frame)
        {
            if (_outbox.Writer.TryWrite(frame))
            {
                return true;
            }

            frame.Return();
            return false;
        }

        public void SetWorld(WorldSimulation world)
        {
            _world = world;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ConnectionOpened {ClientId}", ClientId);

            NetworkStream stream = _tcp.GetStream();
            Task writer = WriteLoopAsync(stream, cancellationToken);
            string closeReason = "client_closed";

            try
            {
                await ReadLoopAsync(stream, cancellationToken);
            }
            catch (ProtocolException ex)
            {
                closeReason = "protocol_violation";
                _logger.LogWarning("SuspiciousPacket {ClientId} {Reason}", ClientId, ex.Message);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                closeReason = "io_error";
            }
            catch (OperationCanceledException)
            {
                closeReason = "server_stopping";
            }
            finally
            {
                _outbox.Writer.TryComplete();
                _world.Enqueue(WorldCommand.Disconnect(this));
                await FlushAsync(writer);
                _tcp.Dispose();
                _logger.LogInformation("ConnectionClosed {ClientId} {Reason}", ClientId, closeReason);
            }
        }

        private async Task ReadLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[ReadBufferSize];
            int filled = 0;

            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken);
                if (read == 0)
                {
                    return;
                }

                filled += read;

                int offset = 0;
                while (FrameCodec.TryReadFrame(
                    buffer.AsSpan(offset, filled - offset),
                    ProtocolConstants.MaxClientBodySize,
                    out FrameHeader header,
                    out ReadOnlySpan<byte> payload,
                    out int consumed))
                {
                    // HandleFrame reste synchrone (une ReadOnlySpan ne peut pas franchir un await) : il ne fait
                    // que l'analyse du message, et renvoie une tâche à attendre pour les opérations asynchrones
                    // (comptes/personnages, qui touchent la base) une fois le span hors de portée.
                    Task? pending = HandleFrame(header, payload);
                    if (pending != null)
                    {
                        await pending;
                    }

                    offset += consumed;
                }

                // Conserve uniquement la fin d'une trame incomplète.
                int remaining = filled - offset;
                if (offset > 0)
                {
                    Buffer.BlockCopy(buffer, offset, buffer, 0, remaining);
                    filled = remaining;
                }
            }
        }

        private async Task WriteLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            await foreach (OutboundFrame frame in _outbox.Reader.ReadAllAsync(cancellationToken))
            {
                try { await stream.WriteAsync(frame.Buffer.AsMemory(0, frame.Length), cancellationToken); } finally { frame.Return(); }
            }
        }

        /// <summary>
        /// Analyse la trame (synchrone : une <see cref="ReadOnlySpan{T}"/> ne peut pas franchir un <c>await</c>).
        /// Renvoie une tâche à attendre par l'appelant pour les opérations asynchrones (comptes/personnages) ;
        /// <c>null</c> si la trame a déjà été traitée entièrement de façon synchrone.
        /// </summary>
        private Task? HandleFrame(FrameHeader header, ReadOnlySpan<byte> payload)
        {
            if (!_messageLimiter.TryConsume())
            {
                RegisterViolation("rate_limited");
                return null;
            }

            switch (header.Opcode)
            {
                case Opcode.Hello:
                    HandleHello(payload);
                    break;

                case Opcode.EnterWorld:
                    HandleEnterWorld(payload);
                    break;

                case Opcode.MoveInput:
                    HandleMoveInput(payload);
                    break;

                case Opcode.AttackRequest:
                    HandleAttackRequest(payload);
                    break;

                case Opcode.CastSkill:
                    HandleCastSkill(payload);
                    break;

                case Opcode.InventoryAction:
                    HandleInventoryAction(payload);
                    break;

                case Opcode.LootRequest:
                    HandleLootRequest(payload);
                    break;

                case Opcode.NpcAction:
                    HandleNpcAction(payload);
                    break;

                case Opcode.PartyAction:
                    HandlePartyAction(payload);
                    break;

                case Opcode.DungeonAction:
                    HandleDungeonAction(payload);
                    break;

                case Opcode.ChatSend:
                    HandleChatSend(payload);
                    break;

                case Opcode.ProfessionAction:
                    HandleProfessionAction(payload);
                    break;

                case Opcode.DebugGiveTestGear:
                    HandleDebugGiveTestGear(payload);
                    break;

                case Opcode.Register:
                    return HandleRegisterAsync(RegisterMessage.Read(payload));

                case Opcode.Login:
                    return HandleLoginAsync(LoginMessage.Read(payload));

                case Opcode.CreateCharacter:
                    return HandleCreateCharacterAsync(CreateCharacterMessage.Read(payload));

                case Opcode.EnterWorldByCharacter:
                    return HandleEnterWorldByCharacterAsync(EnterWorldByCharacterMessage.Read(payload));

                default:
                    throw new ProtocolException($"Opcode inattendu : {(ushort)header.Opcode}.");
            }

            return null;
        }

        private void HandleHello(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.AwaitingHello)
            {
                throw new ProtocolException("Hello reçu deux fois.");
            }

            HelloMessage hello = HelloMessage.Read(payload);
            if (!hello.IsCompatible)
            {
                Reply(Opcode.HelloAck, new HelloAckMessage(ProtocolConstants.Version, false));
                Reply(Opcode.Error, new ErrorMessage(
                    ErrorCode.UnsupportedVersion,
                    $"Version {hello.ProtocolVersion} non supportée, serveur {ProtocolConstants.Version}."));
                throw new ProtocolException($"Version de protocole incompatible : {hello.ProtocolVersion}.");
            }

            _state = SessionState.Authenticated;
            _name = hello.ClientName;
            Reply(Opcode.HelloAck, new HelloAckMessage(ProtocolConstants.Version, true));
            _logger.LogInformation("HelloAccepted {ClientId} {ClientName}", ClientId, hello.ClientName);
        }

        private void HandleEnterWorld(ReadOnlySpan<byte> payload)
        {
            EnterWorldMessage request = EnterWorldMessage.Read(payload);

            if (_state == SessionState.InWorld)
            {
                throw new ProtocolException("EnterWorld reçu deux fois.");
            }

            if (_state != SessionState.Authenticated)
            {
                Reject(ErrorCode.NotAuthenticated, "Hello requis avant EnterWorld.");
                return;
            }

            _state = SessionState.InWorld;
            _world.Enqueue(WorldCommand.Enter(this, _name, request.ClassId));
        }

        private void HandleMoveInput(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant MoveInput.");
                return;
            }

            MoveInputMessage input = MoveInputMessage.Read(payload);
            _world.Enqueue(WorldCommand.Move(this, input.Sequence, input.DirectionX, input.DirectionZ));
        }

        private void HandleAttackRequest(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant AttackRequest.");
                return;
            }

            AttackRequestMessage request = AttackRequestMessage.Read(payload);
            _world.Enqueue(WorldCommand.Attack(this, request.TargetEntityId));
        }

        private void HandleCastSkill(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant CastSkill.");
                return;
            }

            CastSkillMessage request = CastSkillMessage.Read(payload);
            _world.Enqueue(WorldCommand.Cast(this, request.SkillId, request.TargetEntityId));
        }

        private void HandleInventoryAction(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant une action d'inventaire.");
                return;
            }

            _world.Enqueue(WorldCommand.InventoryAction(this, InventoryActionMessage.Read(payload)));
        }

        private void HandleLootRequest(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant un ramassage.");
                return;
            }

            _world.Enqueue(WorldCommand.Loot(this, LootRequestMessage.Read(payload).CorpseEntityId));
        }

        private void HandleDebugGiveTestGear(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant ce test.");
                return;
            }

            DebugGiveTestGearMessage.Read(payload);
            _world.Enqueue(WorldCommand.DebugGiveTestGear(this));
        }

        private void HandleNpcAction(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant une action de PNJ.");
                return;
            }

            _world.Enqueue(WorldCommand.Npc(this, NpcActionMessage.Read(payload)));
        }

        private void HandlePartyAction(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant une action de groupe.");
                return;
            }

            _world.Enqueue(WorldCommand.Party(this, PartyActionMessage.Read(payload)));
        }

        private void HandleDungeonAction(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant une action de donjon.");
                return;
            }

            _world.Enqueue(WorldCommand.Dungeon(this, DungeonActionMessage.Read(payload)));
        }

        private void HandleChatSend(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant le chat.");
                return;
            }

            _world.Enqueue(WorldCommand.ChatSend(this, ChatSendMessage.Read(payload)));
        }

        private void HandleProfessionAction(ReadOnlySpan<byte> payload)
        {
            if (_state != SessionState.InWorld)
            {
                Reject(ErrorCode.NotInWorld, "EnterWorld requis avant une action de métier.");
                return;
            }

            _world.Enqueue(WorldCommand.Profession(this, ProfessionActionMessage.Read(payload)));
        }

        private async Task HandleRegisterAsync(RegisterMessage request)
        {
            if (_state != SessionState.Authenticated)
            {
                Reject(ErrorCode.NotAuthenticated, "Hello requis avant Register.");
                return;
            }

            CreateAccountResult result = await _accounts.CreateAccountAsync(request.Username, request.Password);
            bool success = result.Outcome == CreateAccountOutcome.Created;
            Reply(Opcode.RegisterResult, new RegisterResultMessage(success, success ? string.Empty : Describe(result.Outcome)));
        }

        private async Task HandleLoginAsync(LoginMessage request)
        {
            if (_state != SessionState.Authenticated)
            {
                Reject(ErrorCode.NotAuthenticated, "Hello requis avant Login.");
                return;
            }

            AuthenticateResult result = await _accounts.TryAuthenticateAsync(request.Username, request.Password);
            if (result.Outcome != AuthenticateOutcome.Success)
            {
                Reply(Opcode.LoginResult, new LoginResultMessage(false, Describe(result.Outcome)));
                return;
            }

            _state = SessionState.LoggedIn;
            _accountId = result.AccountId;
            Reply(Opcode.LoginResult, new LoginResultMessage(true, string.Empty));
            await SendCharacterListAsync();
        }

        private async Task HandleCreateCharacterAsync(CreateCharacterMessage request)
        {
            if (_state != SessionState.LoggedIn)
            {
                Reject(ErrorCode.NotAuthenticated, "Login requis avant CreateCharacter.");
                return;
            }

            if (!Enum.IsDefined(typeof(CharacterRace), request.RaceId))
            {
                Reject(ErrorCode.InvalidArgument, "Race inconnue.");
                return;
            }

            CreateCharacterResult result = await _characters.CreateCharacterAsync(
                _accountId, request.Name, request.ClassId, _world.ZoneId, _world.SpawnX, _world.SpawnZ, request.RaceId);

            bool success = result.Outcome == CreateCharacterOutcome.Created;
            Reply(Opcode.CreateCharacterResult, new CreateCharacterResultMessage(success, success ? string.Empty : Describe(result.Outcome)));

            if (success)
            {
                await SendCharacterListAsync();
            }
        }

        private async Task HandleEnterWorldByCharacterAsync(EnterWorldByCharacterMessage request)
        {
            if (_state == SessionState.InWorld)
            {
                throw new ProtocolException("EnterWorld reçu deux fois.");
            }

            if (_state != SessionState.LoggedIn)
            {
                Reject(ErrorCode.NotAuthenticated, "Login requis avant EnterWorldByCharacter.");
                return;
            }

            CharacterSnapshot? snapshot = await _characters.LoadCharacterAsync(request.CharacterId);
            if (snapshot == null || snapshot.AccountId != _accountId)
            {
                Reject(ErrorCode.InvalidArgument, "Personnage introuvable.");
                return;
            }

            _state = SessionState.InWorld;
            _name = snapshot.Name;
            _world.Enqueue(WorldCommand.EnterWithCharacter(this, snapshot));
        }

        private async Task SendCharacterListAsync()
        {
            IReadOnlyList<CharacterSummary> characters = await _characters.ListCharactersAsync(_accountId);
            var entries = new List<(long CharacterId, string Name, byte ClassId, ushort Level)>(characters.Count);
            foreach (CharacterSummary character in characters)
            {
                entries.Add((character.CharacterId, character.Name, character.ClassId, (ushort)character.Level));
            }

            Reply(Opcode.CharacterList, new CharacterListMessage(entries));
        }

        private static string Describe(CreateAccountOutcome outcome) => outcome switch
        {
            CreateAccountOutcome.UsernameTaken => "Identifiant déjà utilisé.",
            CreateAccountOutcome.InvalidUsername => "Identifiant invalide (3 à 20 lettres ou chiffres).",
            CreateAccountOutcome.InvalidPassword => "Mot de passe invalide (6 à 72 caractères).",
            _ => "Échec de la création de compte.",
        };

        private static string Describe(AuthenticateOutcome outcome) => outcome switch
        {
            AuthenticateOutcome.UnknownUsername => "Identifiant ou mot de passe incorrect.",
            AuthenticateOutcome.WrongPassword => "Identifiant ou mot de passe incorrect.",
            _ => "Échec de la connexion.",
        };

        private static string Describe(CreateCharacterOutcome outcome) => outcome switch
        {
            CreateCharacterOutcome.NameTaken => "Ce nom est déjà pris.",
            CreateCharacterOutcome.InvalidName => "Nom invalide (3 à 16 lettres).",
            CreateCharacterOutcome.LimitReached => "Nombre maximal de personnages atteint.",
            _ => "Échec de la création du personnage.",
        };

        private void Reject(ErrorCode code, string text)
        {
            Reply(Opcode.Error, new ErrorMessage(code, text));
            RegisterViolation(code.ToString());
        }

        private void RegisterViolation(string reason)
        {
            _violations++;
            if (_violations > _options.MaxViolations)
            {
                throw new ProtocolException($"Trop d'anomalies ({_violations}), dernière : {reason}.");
            }
        }

        private void Reply<T>(Opcode opcode, T message) where T : IWireMessage
        {
            OutboundFrame frame = OutboundFrame.Rent(opcode, message, _replyWriter);
            if (!TrySend(frame))
            {
                throw new ProtocolException("File d'envoi pleine : client trop lent.");
            }
        }

        private static async Task FlushAsync(Task writer)
        {
            await Task.WhenAny(writer, Task.Delay(FlushTimeout));
            if (writer.IsCompleted)
            {
                try
                {
                    await writer;
                }
                catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
                {
                    // Le client était déjà parti : rien à envoyer.
                }
            }
        }

        private enum SessionState
        {
            AwaitingHello,
            Authenticated,
            LoggedIn,
            InWorld,
        }
    }
}
