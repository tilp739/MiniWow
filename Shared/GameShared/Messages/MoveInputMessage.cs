using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>
    /// Direction voulue par le joueur, envoyée à chaque tick client (≈ 20 Hz).
    /// Le client n'envoie ni position ni durée : le serveur intègre le mouvement lui-même.
    /// Les composantes sont encodées de -127 à 127 (1 = pleine poussée).
    /// </summary>
    public readonly struct MoveInputMessage : IWireMessage
    {
        public const sbyte MaxComponent = 127;

        public MoveInputMessage(uint sequence, sbyte directionX, sbyte directionZ)
        {
            Sequence = sequence;
            DirectionX = directionX;
            DirectionZ = directionZ;
        }

        /// <summary>Numéro croissant, permet au client de savoir quelles entrées le serveur a traitées.</summary>
        public uint Sequence { get; }

        public sbyte DirectionX { get; }

        public sbyte DirectionZ { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt32(Sequence);
            writer.WriteSByte(DirectionX);
            writer.WriteSByte(DirectionZ);
        }

        public static MoveInputMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            uint sequence = reader.ReadUInt32();
            sbyte directionX = reader.ReadSByte();
            sbyte directionZ = reader.ReadSByte();
            reader.ExpectEnd();

            // -128 n'est pas une valeur valide : on reste symétrique.
            if (directionX == sbyte.MinValue || directionZ == sbyte.MinValue)
            {
                throw new ProtocolException("Composante de direction hors limites.");
            }

            return new MoveInputMessage(sequence, directionX, directionZ);
        }
    }
}
