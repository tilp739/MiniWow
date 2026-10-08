using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    /// <summary>Visibilité et synchronisation entre plusieurs clients réels.</summary>
    public sealed class InterestIntegrationTests
    {
        private static CancellationToken Timeout(int seconds = 15) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task TwoPlayers_NearEachOther_SeeEachOther()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            CancellationToken ct = Timeout();
            await using TestClient a = await JoinAsync(server, "alice", ct);
            await using TestClient b = await JoinAsync(server, "bob", ct);

            WorldSnapshotMessage seenByA = await a.WaitForSnapshotAsync(s => s.Entities.Count == 2, ct);
            WorldSnapshotMessage seenByB = await b.WaitForSnapshotAsync(s => s.Entities.Count == 2, ct);

            Assert.NotEqual(seenByA.YourEntityId, seenByB.YourEntityId);
            Assert.Contains(seenByA.Entities, e => e.EntityId == seenByB.YourEntityId);
            Assert.Contains(seenByB.Entities, e => e.EntityId == seenByA.YourEntityId);
        }

        [Fact]
        public async Task Player_LeavingInterestRadius_DisappearsFromOtherSnapshots()
        {
            // Rayon de 5 : les deux joueurs apparaissent à 2 unités, puis B s'éloigne.
            await using ServerHarness server = await ServerHarness.StartAsync(new Dictionary<string, string?>
            {
                ["GameServer:InterestRadius"] = "5",
                ["GameServer:InterestCellSize"] = "5",
            });
            CancellationToken ct = Timeout();
            await using TestClient a = await JoinAsync(server, "alice", ct);
            await using TestClient b = await JoinAsync(server, "bob", ct);

            WorldSnapshotMessage initial = await a.WaitForSnapshotAsync(s => s.Entities.Count == 2, ct);
            WorldSnapshotMessage bobView = await b.WaitForSnapshotAsync(s => s.Entities.Count == 2, ct);
            uint bobId = bobView.YourEntityId;

            // B court vers +X pendant environ 2 s, soit 12 unités : bien au-delà de 5.
            for (int i = 0; i < 40; i++)
            {
                await b.SendAsync(Opcode.MoveInput, new MoveInputMessage(sequence: (uint)(i + 1), 127, 0), ct);
                await Task.Delay(50, ct);
            }

            WorldSnapshotMessage later = await a.WaitForSnapshotAsync(
                s => s.Entities.Count == 1 && s.YourEntityId == initial.YourEntityId,
                ct);
            Assert.DoesNotContain(later.Entities, e => e.EntityId == bobId);
        }

        [Fact]
        public async Task Player_Disconnecting_DisappearsFromOtherSnapshots()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            CancellationToken ct = Timeout();
            await using TestClient a = await JoinAsync(server, "alice", ct);
            TestClient b = await JoinAsync(server, "bob", ct);
            await a.WaitForSnapshotAsync(s => s.Entities.Count == 2, ct);

            await b.DisposeAsync();

            await a.WaitForSnapshotAsync(s => s.Entities.Count == 1, ct);
        }

        [Fact]
        public async Task EnterWorld_IsRefused_WhenWorldIsFull()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(new Dictionary<string, string?>
            {
                ["GameServer:MaxPlayers"] = "2",
            });
            CancellationToken ct = Timeout();
            await using TestClient a = await JoinAsync(server, "alice", ct);
            await using TestClient b = await JoinAsync(server, "bob", ct);

            await using var c = new TestClient(server.Endpoint);
            await c.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "carol"), ct);
            await c.ReceiveAsync(ct); // HelloAck
            await c.SendRawAsync(FrameCodec.CreateFrame(Opcode.EnterWorld, 0, new byte[] { 2 }), ct);

            (Opcode opcode, byte[] payload) = await c.ReceiveAsync(ct);
            Assert.Equal(Opcode.Error, opcode);
            Assert.Equal(ErrorCode.WorldFull, ErrorMessage.Read(payload).Code);
        }

        private static async Task<TestClient> JoinAsync(ServerHarness server, string name, CancellationToken ct)
        {
            var client = new TestClient(server.Endpoint);
            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, name), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            await client.SendRawAsync(FrameCodec.CreateFrame(Opcode.EnterWorld, 0, new byte[] { 2 }), ct);
            return client;
        }
    }
}
