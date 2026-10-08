using System.Collections.Generic;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameShared.Tests
{
    public sealed class ProfessionMessagesTests
    {
        [Fact]
        public void ProfessionAction_Gather_RoundTrips()
        {
            var writer = new PacketWriter();
            new ProfessionActionMessage(ProfessionActionKind.Gather, targetEntityId: 42, recipeId: 0).Write(writer);

            ProfessionActionMessage decoded = ProfessionActionMessage.Read(writer.WrittenSpan);

            Assert.Equal(ProfessionActionKind.Gather, decoded.Kind);
            Assert.Equal(42u, decoded.TargetEntityId);
        }

        [Fact]
        public void ProfessionAction_Craft_RoundTrips()
        {
            var writer = new PacketWriter();
            new ProfessionActionMessage(ProfessionActionKind.Craft, targetEntityId: 0, recipeId: 7).Write(writer);

            ProfessionActionMessage decoded = ProfessionActionMessage.Read(writer.WrittenSpan);

            Assert.Equal(ProfessionActionKind.Craft, decoded.Kind);
            Assert.Equal((ushort)7, decoded.RecipeId);
        }

        [Fact]
        public void ProfessionAction_Throws_OnUnknownKind()
        {
            var writer = new PacketWriter();
            writer.WriteByte(99);
            writer.WriteUInt32(0);
            writer.WriteUInt16(0);

            Assert.Throws<ProtocolException>(() => ProfessionActionMessage.Read(writer.WrittenSpan));
        }

        [Fact]
        public void Professions_RoundTrips_WithSeveralEntries()
        {
            var known = new List<(byte ProfessionId, string Name, int Skill, int MaxSkill)>
            {
                (1, "Minage", 12, 75),
                (3, "Forge", 1, 75),
            };

            var writer = new PacketWriter();
            new ProfessionsMessage(known).Write(writer);

            ProfessionsMessage decoded = ProfessionsMessage.Read(writer.WrittenSpan);

            Assert.Equal(2, decoded.Known.Count);
            Assert.Equal("Minage", decoded.Known[0].Name);
            Assert.Equal(12, decoded.Known[0].Skill);
            Assert.Equal(75, decoded.Known[1].MaxSkill);
        }
    }
}
