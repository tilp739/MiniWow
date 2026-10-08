using System;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameShared.Tests
{
    public sealed class MessagesTests
    {
        [Fact]
        public void Hello_RoundTrips()
        {
            var writer = new PacketWriter();
            new HelloMessage(ProtocolConstants.Version, "Augustin").Write(writer);

            HelloMessage decoded = HelloMessage.Read(writer.WrittenSpan);

            Assert.Equal("Augustin", decoded.ClientName);
            Assert.True(decoded.IsCompatible);
        }

        [Fact]
        public void Hello_IsNotCompatible_WhenVersionDiffers()
        {
            var writer = new PacketWriter();
            new HelloMessage(999, "client").Write(writer);

            HelloMessage decoded = HelloMessage.Read(writer.WrittenSpan);

            Assert.False(decoded.IsCompatible);
        }

        [Fact]
        public void Hello_Throws_WhenNameIsEmpty()
        {
            var writer = new PacketWriter();
            new HelloMessage(ProtocolConstants.Version, "").Write(writer);

            Assert.Throws<ProtocolException>(() => HelloMessage.Read(writer.WrittenSpan));
        }

        [Fact]
        public void Hello_Throws_WhenNameExceedsLimit()
        {
            var writer = new PacketWriter();
            writer.WriteUInt16(ProtocolConstants.Version);
            writer.WriteUInt16(HelloMessage.MaxClientNameByteLength + 1);
            for (int i = 0; i < HelloMessage.MaxClientNameByteLength + 1; i++)
            {
                writer.WriteBoolean(false);
            }

            Assert.Throws<ProtocolException>(() => HelloMessage.Read(writer.WrittenSpan));
        }

        [Fact]
        public void Hello_Throws_WhenPayloadIsTruncated()
        {
            byte[] truncated = { 1 }; // version incomplète

            Assert.Throws<ProtocolException>(() => HelloMessage.Read(truncated));
        }

        [Fact]
        public void Hello_Throws_WhenTrailingBytesExist()
        {
            var writer = new PacketWriter();
            new HelloMessage(ProtocolConstants.Version, "client").Write(writer);
            writer.WriteBoolean(true); // octet parasite

            Assert.Throws<ProtocolException>(() => HelloMessage.Read(writer.WrittenSpan));
        }

        [Fact]
        public void HelloAck_RoundTrips()
        {
            var writer = new PacketWriter();
            new HelloAckMessage(ProtocolConstants.Version, true).Write(writer);

            HelloAckMessage decoded = HelloAckMessage.Read(writer.WrittenSpan);

            Assert.Equal(ProtocolConstants.Version, decoded.ServerProtocolVersion);
            Assert.True(decoded.Accepted);
        }

        [Fact]
        public void HelloAck_Throws_WhenBooleanIsInvalid()
        {
            byte[] invalid = { 0, 1, 7 }; // version = 256, booléen = 7

            Assert.Throws<ProtocolException>(() => HelloAckMessage.Read(invalid));
        }

        [Fact]
        public void PacketWriter_Throws_WhenStringExceedsLimit()
        {
            var writer = new PacketWriter();

            Assert.Throws<ArgumentException>(() => writer.WriteString(new string('a', 10), 5));
        }

        [Fact]
        public void PacketReader_RoundTripsUtf8StringWithAccents()
        {
            var writer = new PacketWriter();
            writer.WriteString("Élodie", 32);
            writer.WriteSingle(1.5f);

            var reader = new PacketReader(writer.WrittenSpan);

            Assert.Equal("Élodie", reader.ReadString(32));
            Assert.Equal(1.5f, reader.ReadSingle());
            reader.ExpectEnd();
        }
    }
}
