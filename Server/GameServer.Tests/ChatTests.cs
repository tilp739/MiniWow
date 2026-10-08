using System;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Domain.Chat;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    public sealed class ChatFilterTests
    {
        [Fact]
        public void Apply_MasksBannedWords_CaseInsensitively()
        {
            var filter = new ChatFilter(new[] { "idiot" });

            Assert.Equal("tu es *****", filter.Apply("tu es IDIOT")); // un astérisque par lettre
        }

        [Fact]
        public void Apply_RejectsEmptyText()
        {
            var filter = new ChatFilter(Array.Empty<string>());

            Assert.Null(filter.Apply("   "));
        }

        [Fact]
        public void Apply_RejectsControlCharacters()
        {
            var filter = new ChatFilter(Array.Empty<string>());

            Assert.Null(filter.Apply("bonjour\u0007"));
        }
    }

    /// <summary>Chat de bout en bout : zone, chuchotement, groupe.</summary>
    public sealed class ChatIntegrationTests
    {
        private static CancellationToken Timeout(int seconds = 15) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task Say_IsDeliveredToNearbyPlayers()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            CancellationToken ct = Timeout();
            await using TestClient speaker = await JoinAsync(server, "parleur", ct);
            await using TestClient listener = await JoinAsync(server, "auditeur", ct);
            await speaker.WaitForSnapshotAsync(s => s.Entities.Count >= 2, ct);

            await speaker.SendAsync(Opcode.ChatSend, new ChatSendMessage(ChatChannel.Say, string.Empty, "bonjour"), ct);

            ChatLineMessage line = await WaitForChatAsync(listener, ct);
            Assert.Equal(ChatChannel.Say, line.Channel);
            Assert.Equal("parleur", line.Sender);
            Assert.Equal("bonjour", line.Text);
        }

        [Fact]
        public async Task Whisper_ReachesTheNamedPlayer()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            CancellationToken ct = Timeout();
            await using TestClient sender = await JoinAsync(server, "emetteur", ct);
            await using TestClient target = await JoinAsync(server, "destinataire", ct);
            await sender.WaitForSnapshotAsync(s => s.Entities.Count >= 2, ct);

            await sender.SendAsync(Opcode.ChatSend, new ChatSendMessage(ChatChannel.Whisper, "destinataire", "secret"), ct);

            ChatLineMessage line = await WaitForChatAsync(target, ct);
            Assert.Equal(ChatChannel.Whisper, line.Channel);
            Assert.Equal("secret", line.Text);
        }

        [Fact]
        public async Task PartyChat_WithoutParty_IsRefused()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, "solo", ct);
            await client.WaitForSnapshotAsync(s => s.Entities.Count >= 1, ct);

            await client.SendAsync(Opcode.ChatSend, new ChatSendMessage(ChatChannel.Party, string.Empty, "allô ?"), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.PartyError, error.Code);
        }

        private static async Task<TestClient> JoinAsync(ServerHarness server, string name, CancellationToken ct)
        {
            var client = new TestClient(server.Endpoint);
            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, name), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            await client.SendAsync(Opcode.EnterWorld, new EnterWorldMessage(2), ct);
            return client;
        }

        private static async Task<ChatLineMessage> WaitForChatAsync(TestClient client, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode == Opcode.Chat)
                {
                    return ChatLineMessage.Read(payload);
                }
            }
        }

        private static async Task<ErrorMessage> WaitForErrorAsync(TestClient client, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode == Opcode.Error)
                {
                    return ErrorMessage.Read(payload);
                }
            }
        }
    }
}
