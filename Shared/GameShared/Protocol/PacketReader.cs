using System;
using System.Buffers.Binary;
using System.Text;

namespace GameShared.Protocol
{
    /// <summary>
    /// Lit les champs d'un payload. Toute lecture hors bornes lève <see cref="ProtocolException"/>
    /// au lieu de lire des données invalides : un paquet tronqué ou malformé ne doit jamais crasher le serveur.
    /// </summary>
    public ref struct PacketReader
    {
        private readonly ReadOnlySpan<byte> _buffer;
        private int _position;

        public PacketReader(ReadOnlySpan<byte> payload)
        {
            _buffer = payload;
            _position = 0;
        }

        public int Remaining => _buffer.Length - _position;

        public bool ReadBoolean()
        {
            byte raw = ReadByte();
            if (raw > 1)
            {
                throw new ProtocolException($"Booléen invalide : {raw}.");
            }

            return raw == 1;
        }

        public byte ReadByte()
        {
            EnsureRemaining(1);
            return _buffer[_position++];
        }

        public sbyte ReadSByte()
        {
            return unchecked((sbyte)ReadByte());
        }

        public ushort ReadUInt16()
        {
            EnsureRemaining(sizeof(ushort));
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(_position));
            _position += sizeof(ushort);
            return value;
        }

        public uint ReadUInt32()
        {
            EnsureRemaining(sizeof(uint));
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(_position));
            _position += sizeof(uint);
            return value;
        }

        public int ReadInt32()
        {
            EnsureRemaining(sizeof(int));
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_position));
            _position += sizeof(int);
            return value;
        }

        public long ReadInt64()
        {
            EnsureRemaining(sizeof(long));
            long value = BinaryPrimitives.ReadInt64LittleEndian(_buffer.Slice(_position));
            _position += sizeof(long);
            return value;
        }

        public float ReadSingle()
        {
            return BitConverter.Int32BitsToSingle(ReadInt32());
        }

        /// <summary>
        /// Lit une chaîne UTF-8 préfixée par sa longueur.
        /// Lève <see cref="ProtocolException"/> si elle dépasse <paramref name="maxByteLength"/>.
        /// </summary>
        public string ReadString(int maxByteLength)
        {
            int byteCount = ReadUInt16();
            if (byteCount > maxByteLength)
            {
                throw new ProtocolException($"Chaîne trop longue : {byteCount} > {maxByteLength} octets.");
            }

            EnsureRemaining(byteCount);
            string value = Encoding.UTF8.GetString(_buffer.Slice(_position, byteCount));
            _position += byteCount;
            return value;
        }

        /// <summary>Vérifie qu'aucun octet n'a été laissé de côté : un message doit être lu intégralement.</summary>
        public void ExpectEnd()
        {
            if (Remaining != 0)
            {
                throw new ProtocolException($"{Remaining} octet(s) inattendus en fin de message.");
            }
        }

        private void EnsureRemaining(int byteCount)
        {
            if (Remaining < byteCount)
            {
                throw new ProtocolException($"Message tronqué : {byteCount} octet(s) attendus, {Remaining} disponibles.");
            }
        }
    }
}
