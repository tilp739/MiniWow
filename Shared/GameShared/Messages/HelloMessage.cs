using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Premier message envoyé par le client : version du protocole et nom du client.</summary>
    public readonly struct HelloMessage : IWireMessage
    {
        public const int MaxClientNameByteLength = 32;

        public HelloMessage(ushort protocolVersion, string clientName)
        {
            ProtocolVersion = protocolVersion;
            ClientName = clientName;
        }

        public ushort ProtocolVersion { get; }

        public string ClientName { get; }

        public bool IsCompatible => ProtocolVersion == ProtocolConstants.Version;

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt16(ProtocolVersion);
            writer.WriteString(ClientName, MaxClientNameByteLength);
        }

        public static HelloMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            ushort version = reader.ReadUInt16();
            string name = reader.ReadString(MaxClientNameByteLength);
            reader.ExpectEnd();

            if (name.Length == 0)
            {
                throw new ProtocolException("Nom de client vide.");
            }

            return new HelloMessage(version, name);
        }
    }
}
