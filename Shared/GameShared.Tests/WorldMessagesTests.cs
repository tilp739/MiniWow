using System;
using System.Collections.Generic;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameShared.Tests
{
    public sealed class WorldMessagesTests
    {
        [Fact]
        public void MoveInput_RoundTripsNegativeAndPositiveDirections()
        {
            var writer = new PacketWriter();
            new MoveInputMessage(7, -127, 64).Write(writer);

            MoveInputMessage decoded = MoveInputMessage.Read(writer.WrittenSpan);

            Assert.Equal(7u, decoded.Sequence);
            Assert.Equal(-127, decoded.DirectionX);
            Assert.Equal(64, decoded.DirectionZ);
        }

        [Fact]
        public void MoveInput_Throws_WhenComponentIsMinValue()
        {
            byte[] payload = { 0, 0, 0, 0, 0x80, 0 }; // seq = 0, dirX = -128

            Assert.Throws<ProtocolException>(() => MoveInputMessage.Read(payload));
        }

        [Fact]
        public void MoveInput_Throws_WhenPayloadHasExtraBytes()
        {
            byte[] payload = { 1, 0, 0, 0, 1, 1, 99 };

            Assert.Throws<ProtocolException>(() => MoveInputMessage.Read(payload));
        }

        [Fact]
        public void WorldSnapshot_RoundTripsEntitiesAndAckFields()
        {
            var entities = new List<EntityState>
            {
                new EntityState(1, EntityKind.Player, 0, 1.5f, -2f, 90f, 100, 100),
                new EntityState(2, EntityKind.Creature, 7, 0f, 10.25f, 0f, 30, 60),
            };
            var writer = new PacketWriter();
            new WorldSnapshotMessage(120, 1, 33, entities).Write(writer);

            WorldSnapshotMessage decoded = WorldSnapshotMessage.Read(writer.WrittenSpan);

            Assert.Equal(120u, decoded.ServerTick);
            Assert.Equal(1u, decoded.YourEntityId);
            Assert.Equal(33u, decoded.LastProcessedInput);
            Assert.Equal(2, decoded.Entities.Count);
            Assert.Equal(1.5f, decoded.Entities[0].X);
            Assert.Equal(10.25f, decoded.Entities[1].Z);
            Assert.Equal(EntityKind.Creature, decoded.Entities[1].Kind);
            Assert.Equal((ushort)7, decoded.Entities[1].TemplateId);
            Assert.Equal((ushort)30, decoded.Entities[1].Health);
            Assert.Equal((ushort)60, decoded.Entities[1].MaxHealth);
        }

        [Fact]
        public void WorldSnapshot_Throws_WhenEntityKindIsUnknown()
        {
            var writer = new PacketWriter();
            writer.WriteUInt32(1);
            writer.WriteUInt32(1);
            writer.WriteUInt32(0);
            writer.WriteUInt16(1);
            writer.WriteUInt32(1);
            writer.WriteByte(99); // type inconnu

            Assert.Throws<ProtocolException>(() => WorldSnapshotMessage.Read(writer.WrittenSpan));
        }

        [Fact]
        public void AttackRequest_RoundTrips()
        {
            var writer = new PacketWriter();
            new AttackRequestMessage(42).Write(writer);

            Assert.Equal(42u, AttackRequestMessage.Read(writer.WrittenSpan).TargetEntityId);
        }

        [Fact]
        public void CombatEvent_RoundTrips()
        {
            var writer = new PacketWriter();
            new CombatEventMessage(CombatEventKind.Damage, 3, 9, -12).Write(writer);

            CombatEventMessage decoded = CombatEventMessage.Read(writer.WrittenSpan);

            Assert.Equal(CombatEventKind.Damage, decoded.Kind);
            Assert.Equal(3u, decoded.SourceEntityId);
            Assert.Equal(9u, decoded.TargetEntityId);
            Assert.Equal(-12, decoded.Amount);
        }

        [Fact]
        public void WorldSnapshot_Throws_WhenEntityCountExceedsLimit()
        {
            var writer = new PacketWriter();
            writer.WriteUInt32(1);
            writer.WriteUInt32(1);
            writer.WriteUInt32(0);
            writer.WriteUInt16(WorldSnapshotMessage.MaxEntities + 1);

            Assert.Throws<ProtocolException>(() => WorldSnapshotMessage.Read(writer.WrittenSpan));
        }

        [Fact]
        public void Error_RoundTrips()
        {
            var writer = new PacketWriter();
            new ErrorMessage(ErrorCode.RateLimited, "trop de messages").Write(writer);

            ErrorMessage decoded = ErrorMessage.Read(writer.WrittenSpan);

            Assert.Equal(ErrorCode.RateLimited, decoded.Code);
            Assert.Equal("trop de messages", decoded.Text);
        }
    }
}
