using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    public sealed class GameServerIntegrationTests
    {
        private static CancellationToken Timeout(int seconds = 10)
        {
            return new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;
        }

        [Fact]
        public async Task Hello_IsAccepted_WhenVersionMatches()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "tester"), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);

            Assert.Equal(Opcode.HelloAck, opcode);
            HelloAckMessage ack = HelloAckMessage.Read(payload);
            Assert.True(ack.Accepted);
            Assert.Equal(ProtocolConstants.Version, ack.ServerProtocolVersion);
        }

        [Fact]
        public async Task Hello_IsRejectedAndClosed_WhenVersionDiffers()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            await client.SendAsync(Opcode.Hello, new HelloMessage(999, "tester"), ct);

            (Opcode ackOpcode, byte[] ackPayload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, ackOpcode);
            Assert.False(HelloAckMessage.Read(ackPayload).Accepted);

            (Opcode errorOpcode, byte[] errorPayload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.Error, errorOpcode);
            Assert.Equal(ErrorCode.UnsupportedVersion, ErrorMessage.Read(errorPayload).Code);

            await Assert.ThrowsAnyAsync<Exception>(() => client.ReceiveAsync(ct));
        }

        [Fact]
        public async Task EnterWorld_BeforeHello_IsRefused()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            await client.SendRawAsync(FrameCodec.CreateFrame(Opcode.EnterWorld, 0, new byte[] { 2 }), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);

            Assert.Equal(Opcode.Error, opcode);
            Assert.Equal(ErrorCode.NotAuthenticated, ErrorMessage.Read(payload).Code);
        }

        [Fact]
        public async Task EnterWorld_SpawnsPlayerAtOrigin()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = await ConnectAndEnterWorldAsync(server);
            CancellationToken ct = Timeout();

            WorldSnapshotMessage snapshot = await client.WaitForSnapshotAsync(s => s.Entities.Count > 0, ct);

            EntityState self = FindEntity(snapshot, snapshot.YourEntityId);
            Assert.Equal(0f, self.X, 3);
            Assert.Equal(0f, self.Z, 3);
        }

        [Fact]
        public async Task MoveInput_MovesPlayer_ButNeverFasterThanServerSpeed()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = await ConnectAndEnterWorldAsync(server);
            CancellationToken ct = Timeout(15);
            WorldSnapshotMessage first = await client.WaitForSnapshotAsync(s => s.Entities.Count > 0, ct);
            uint entityId = first.YourEntityId;

            const uint lastSequence = 40;
            var stopwatch = Stopwatch.StartNew();
            for (uint sequence = 1; sequence <= lastSequence; sequence++)
            {
                await client.SendAsync(Opcode.MoveInput, new MoveInputMessage(sequence, 127, 0), ct);
                await Task.Delay(50, ct);
            }
            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            WorldSnapshotMessage latest = await client.WaitForSnapshotAsync(
                s => s.LastProcessedInput >= lastSequence,
                ct);

            EntityState self = FindEntity(latest, entityId);
            Assert.True(self.X > 0.5f, $"Le joueur doit avancer (X = {self.X}).");

            // Le serveur n'intègre jamais plus vite que la vitesse configurée, quel que soit le client.
            const float walkSpeed = 6f;
            Assert.True(self.X <= walkSpeed * elapsedSeconds + 0.5f, $"Vitesse trop élevée : X = {self.X} en {elapsedSeconds:F2} s.");
            Assert.InRange(self.Z, -0.01f, 0.01f);
            Assert.InRange(self.YawDegrees, 89f, 91f);
        }

        [Fact]
        public async Task MalformedLength_ClosesConnection()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            await using var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            // Longueur annoncée : 0xFFFFFFFF, bien au-delà de la limite.
            await client.SendRawAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, ct);

            await Assert.ThrowsAnyAsync<Exception>(() => client.ReceiveAsync(ct));
        }

        [Fact]
        public async Task RepeatedProtocolViolations_CloseConnection()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(new Dictionary<string, string?>
            {
                ["GameServer:MaxViolations"] = "3",
            });
            await using var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            // Cinq EnterWorld sans Hello : chacun est une anomalie.
            byte[] enterWorld = FrameCodec.CreateFrame(Opcode.EnterWorld, 0, new byte[] { 2 });
            for (int i = 0; i < 5; i++)
            {
                await client.SendRawAsync(enterWorld, ct);
            }

            int errors = 0;
            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                while (true)
                {
                    (Opcode opcode, _) = await client.ReceiveAsync(ct);
                    if (opcode == Opcode.Error)
                    {
                        errors++;
                    }
                }
            });

            Assert.True(errors >= 1 && errors <= 4, $"Erreurs reçues avant fermeture : {errors}.");
        }

        private static async Task<TestClient> ConnectAndEnterWorldAsync(ServerHarness server)
        {
            var client = new TestClient(server.Endpoint);
            CancellationToken ct = Timeout();

            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "tester"), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            await client.SendRawAsync(FrameCodec.CreateFrame(Opcode.EnterWorld, 0, new byte[] { 2 }), ct);
            return client;
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
    }
}
