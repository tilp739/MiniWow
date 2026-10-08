using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Réponse du serveur au Hello : version serveur et verdict de compatibilité.</summary>
    public readonly struct HelloAckMessage : IWireMessage
    {
        public HelloAckMessage(ushort serverProtocolVersion, bool accepted)
        {
            ServerProtocolVersion = serverProtocolVersion;
            Accepted = accepted;
        }

        public ushort ServerProtocolVersion { get; }

        public bool Accepted { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt16(ServerProtocolVersion);
            writer.WriteBoolean(Accepted);
        }

        public static HelloAckMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            ushort version = reader.ReadUInt16();
            bool accepted = reader.ReadBoolean();
            reader.ExpectEnd();

            return new HelloAckMessage(version, accepted);
        }
    }
}
