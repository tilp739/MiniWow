using System;
using System.Threading;
using System.Threading.Tasks;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    /// <summary>
    /// Bout en bout : compte -> personnage -> entrée dans le monde -> déconnexion -> reconnexion.
    /// Le serveur de test (<see cref="ServerHarness"/>) utilise des dépôts en mémoire (pas de Postgres) :
    /// voir <c>GameServer.Persistence.Tests</c> pour les mêmes garanties contre le Postgres réel.
    /// </summary>
    public sealed class AccountAndCharacterIntegrationTests
    {
        private static CancellationToken Timeout(int seconds = 10) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task Register_ThenLogin_Succeeds()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            await Handshake.ConnectAsync(client, ct);

            await client.SendAsync(Opcode.Register, new RegisterMessage("Oggrim", "motdepasse1"), ct);
            (Opcode registerOpcode, byte[] registerPayload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.RegisterResult, registerOpcode);
            Assert.True(RegisterResultMessage.Read(registerPayload).Success);

            await client.SendAsync(Opcode.Login, new LoginMessage("Oggrim", "motdepasse1"), ct);
            (Opcode loginOpcode, byte[] loginPayload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.LoginResult, loginOpcode);
            Assert.True(LoginResultMessage.Read(loginPayload).Success);

            (Opcode listOpcode, byte[] listPayload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.CharacterList, listOpcode);
            Assert.Empty(CharacterListMessage.Read(listPayload).Characters);
        }

        [Fact]
        public async Task Login_WithWrongPassword_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            await Handshake.ConnectAsync(client, ct);
            await Handshake.RegisterAsync(client, "Teremus", "motdepasse1", ct);

            await client.SendAsync(Opcode.Login, new LoginMessage("Teremus", "mauvais-mot-de-passe"), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);

            Assert.Equal(Opcode.LoginResult, opcode);
            Assert.False(LoginResultMessage.Read(payload).Success);
        }

        [Fact]
        public async Task CreateCharacter_ThenEnterWorld_SpawnsAtZoneSpawnPoint()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            await Handshake.ConnectAsync(client, ct);
            await Handshake.RegisterAndLoginAsync(client, "Bariss", "motdepasse1", ct);

            long characterId = await Handshake.CreateCharacterAsync(client, "Thrundar", classId: 1, ct);

            await client.SendAsync(Opcode.EnterWorldByCharacter, new EnterWorldByCharacterMessage(characterId), ct);
            WorldSnapshotMessage snapshot = await client.WaitForSnapshotAsync(s => s.Entities.Count > 0, ct);

            Assert.Equal(0f, FindEntity(snapshot, snapshot.YourEntityId).X, 3);
        }

        [Fact]
        public async Task Disconnect_ThenReconnect_RestoresPosition()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            CancellationToken ct = Timeout(15);

            long characterId;
            await using (var firstSession = new TestClient(server.Endpoint))
            {
                await Handshake.ConnectAsync(firstSession, ct);
                await Handshake.RegisterAndLoginAsync(firstSession, "Myralle", "motdepasse1", ct);
                characterId = await Handshake.CreateCharacterAsync(firstSession, "Myralle", classId: 2, ct);

                await firstSession.SendAsync(Opcode.EnterWorldByCharacter, new EnterWorldByCharacterMessage(characterId), ct);
                await firstSession.WaitForSnapshotAsync(s => s.Entities.Count > 0, ct);

                // Avance pendant un moment : la position sauvegardée doit refléter ce déplacement.
                for (uint sequence = 1; sequence <= 10; sequence++)
                {
                    await firstSession.SendAsync(Opcode.MoveInput, new MoveInputMessage(sequence, 127, 0), ct);
                    await Task.Delay(50, ct);
                }

                await firstSession.WaitForSnapshotAsync(s => s.LastProcessedInput >= 10, ct);
            }

            // La session précédente est fermée : laisse le temps à la déconnexion d'être traitée
            // (prochain tick de simulation) et à la sauvegarde en arrière-plan de s'exécuter.
            await Task.Delay(500, ct);

            await using var secondSession = new TestClient(server.Endpoint);
            await Handshake.ConnectAsync(secondSession, ct);
            await Handshake.LoginAsync(secondSession, "Myralle", "motdepasse1", ct);

            await secondSession.SendAsync(Opcode.EnterWorldByCharacter, new EnterWorldByCharacterMessage(characterId), ct);
            WorldSnapshotMessage snapshot = await secondSession.WaitForSnapshotAsync(s => s.Entities.Count > 0, ct);

            float x = FindEntity(snapshot, snapshot.YourEntityId).X;
            Assert.True(x > 0.5f, $"La position sauvegardée doit refléter le déplacement précédent (X = {x}).");
        }

        [Fact]
        public async Task EnterWorldByCharacter_WithAnotherAccountsCharacter_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            CancellationToken ct = Timeout();

            long characterId;
            await using (var owner = new TestClient(server.Endpoint))
            {
                await Handshake.ConnectAsync(owner, ct);
                await Handshake.RegisterAndLoginAsync(owner, "Vostra", "motdepasse1", ct);
                characterId = await Handshake.CreateCharacterAsync(owner, "Vostra", classId: 1, ct);
            }

            await using var intruder = new TestClient(server.Endpoint);
            await Handshake.ConnectAsync(intruder, ct);
            await Handshake.RegisterAndLoginAsync(intruder, "Shiraal", "motdepasse1", ct);

            await intruder.SendAsync(Opcode.EnterWorldByCharacter, new EnterWorldByCharacterMessage(characterId), ct);
            (Opcode opcode, byte[] payload) = await intruder.ReceiveAsync(ct);

            Assert.Equal(Opcode.Error, opcode);
            Assert.Equal(ErrorCode.InvalidArgument, ErrorMessage.Read(payload).Code);
        }

        private static EntityState FindEntity(WorldSnapshotMessage snapshot, uint entityId)
        {
            foreach (EntityState entity in snapshot.Entities)
            {
                if (entity.EntityId == entityId)
                {
                    return entity;
                }
            }

            throw new Xunit.Sdk.XunitException($"Entité {entityId} absente du snapshot.");
        }

        /// <summary>Petites briques de protocole réutilisées par ces tests : handshake, compte, personnage.</summary>
        private static class Handshake
        {
            public static async Task ConnectAsync(TestClient client, CancellationToken ct)
            {
                await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "tester"), ct);
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                Assert.Equal(Opcode.HelloAck, opcode);
                Assert.True(HelloAckMessage.Read(payload).Accepted);
            }

            public static async Task RegisterAsync(TestClient client, string username, string password, CancellationToken ct)
            {
                await client.SendAsync(Opcode.Register, new RegisterMessage(username, password), ct);
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                Assert.Equal(Opcode.RegisterResult, opcode);
                Assert.True(RegisterResultMessage.Read(payload).Success);
            }

            public static async Task LoginAsync(TestClient client, string username, string password, CancellationToken ct)
            {
                await client.SendAsync(Opcode.Login, new LoginMessage(username, password), ct);
                (Opcode loginOpcode, byte[] loginPayload) = await client.ReceiveAsync(ct);
                Assert.Equal(Opcode.LoginResult, loginOpcode);
                Assert.True(LoginResultMessage.Read(loginPayload).Success);

                (Opcode listOpcode, _) = await client.ReceiveAsync(ct);
                Assert.Equal(Opcode.CharacterList, listOpcode);
            }

            public static async Task RegisterAndLoginAsync(TestClient client, string username, string password, CancellationToken ct)
            {
                await RegisterAsync(client, username, password, ct);
                await LoginAsync(client, username, password, ct);
            }

            public static async Task<long> CreateCharacterAsync(TestClient client, string name, byte classId, CancellationToken ct, byte raceId = 0)
            {
                await client.SendAsync(Opcode.CreateCharacter, new CreateCharacterMessage(name, classId, raceId), ct);
                (Opcode resultOpcode, byte[] resultPayload) = await client.ReceiveAsync(ct);
                Assert.Equal(Opcode.CreateCharacterResult, resultOpcode);
                Assert.True(CreateCharacterResultMessage.Read(resultPayload).Success);

                (Opcode listOpcode, byte[] listPayload) = await client.ReceiveAsync(ct);
                Assert.Equal(Opcode.CharacterList, listOpcode);
                var characters = CharacterListMessage.Read(listPayload).Characters;
                Assert.NotEmpty(characters);
                return characters[^1].CharacterId;
            }
        }
    }
}
