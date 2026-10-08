using System.Collections.Generic;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameShared.Tests
{
    public sealed class QuestMessagesTests
    {
        [Fact]
        public void Npc_RoundTripsWithoutShop()
        {
            var writer = new PacketWriter();
            new NpcMessage(7, "Mira", "Bonjour", new List<ushort> { 1, 2 }, new List<ushort>(), new List<(ushort, int)>(), new List<byte>(), new List<AdventureSummary>()).Write(writer);

            NpcMessage decoded = NpcMessage.Read(writer.WrittenSpan);

            Assert.Equal(7u, decoded.NpcEntityId);
            Assert.Equal("Bonjour", decoded.Text);
            Assert.Equal(2, decoded.OfferedQuests.Count);
            Assert.Empty(decoded.Shop);
            Assert.Empty(decoded.TeachesProfessions);
            Assert.Empty(decoded.Adventures);
        }

        [Fact]
        public void Npc_RoundTripsWithShop()
        {
            var writer = new PacketWriter();
            new NpcMessage(8, "Tom", "Achetez", new List<ushort>(), new List<ushort> { 3 }, new List<(ushort, int)> { (6, 5), (4, 12) }, new List<byte> { 1, 3 }, new List<AdventureSummary>()).Write(writer);

            NpcMessage decoded = NpcMessage.Read(writer.WrittenSpan);

            Assert.Equal(3, decoded.TurnInQuests[0]);
            Assert.Equal(2, decoded.Shop.Count);
            Assert.Equal(12, decoded.Shop[1].Price);
            Assert.Equal(new byte[] { 1, 3 }, decoded.TeachesProfessions);
        }

        [Fact]
        public void Npc_RoundTripsWithAdventures()
        {
            var writer = new PacketWriter();
            var adventures = new List<AdventureSummary>
            {
                new AdventureSummary(1, "Forêt Oubliée", recommendedLevel: 5, minPlayers: 1, maxPlayers: 5, difficulty: 2, bossCount: 2, dungeonCount: 1),
            };
            new NpcMessage(9, "Héraut", "Quelles aventures sont disponibles ?", new List<ushort>(), new List<ushort>(), new List<(ushort, int)>(), new List<byte>(), adventures).Write(writer);

            NpcMessage decoded = NpcMessage.Read(writer.WrittenSpan);

            Assert.Single(decoded.Adventures);
            Assert.Equal("Forêt Oubliée", decoded.Adventures[0].Name);
            Assert.Equal(5, decoded.Adventures[0].RecommendedLevel);
            Assert.Equal(2, decoded.Adventures[0].BossCount);
        }
    }
}
