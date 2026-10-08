using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>
    /// Zone courante du joueur, envoyée une fois à l'entrée dans un monde (login, transfert vers/depuis une
    /// instance). Permet au client de savoir quel décor construire (Hub, Aventure, donjon, ...) sans avoir à
    /// le déduire d'autre chose.
    /// </summary>
    public readonly struct ZoneInfoMessage : IWireMessage
    {
        public const int MaxZoneIdByteLength = 32;

        public ZoneInfoMessage(string zoneId)
        {
            ZoneId = zoneId;
        }

        public string ZoneId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteString(ZoneId, MaxZoneIdByteLength);
        }

        public static ZoneInfoMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            string zoneId = reader.ReadString(MaxZoneIdByteLength);
            reader.ExpectEnd();
            return new ZoneInfoMessage(zoneId);
        }
    }
}
