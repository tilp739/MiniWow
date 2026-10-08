using System;
using System.Buffers.Binary;
using System.Text;

namespace GameShared.Protocol
{
    /// <summary>
    /// Écrit les champs d'un payload en little-endian.
    /// Les chaînes sont préfixées par leur longueur en octets UTF-8 (u16).
    /// </summary>
    public sealed class PacketWriter
    {
        private byte[] _buffer;
        private int _position;

        public PacketWriter(int initialCapacity = 256)
        {
            _buffer = new byte[initialCapacity];
        }

        public int Length => _position;

        /// <summary>Contenu écrit jusqu'ici. Valide jusqu'au prochain appel d'écriture ou Reset.</summary>
        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _position);

        public void Reset()
        {
            _position = 0;
        }

        public void WriteBoolean(bool value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value ? (byte)1 : (byte)0;
        }

        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value;
        }

        public void WriteSByte(sbyte value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = unchecked((byte)value);
        }

        public void WriteUInt16(ushort value)
        {
            EnsureCapacity(sizeof(ushort));
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_position), value);
            _position += sizeof(ushort);
        }

        public void WriteUInt32(uint value)
        {
            EnsureCapacity(sizeof(uint));
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_position), value);
            _position += sizeof(uint);
        }

        public void WriteInt32(int value)
        {
            EnsureCapacity(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position), value);
            _position += sizeof(int);
        }

        public void WriteInt64(long value)
        {
            EnsureCapacity(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position), value);
            _position += sizeof(long);
        }

        public void WriteSingle(float value)
        {
            WriteInt32(BitConverter.SingleToInt32Bits(value));
        }

        /// <summary>
        /// Écrit une chaîne UTF-8 préfixée par sa longueur.
        /// Lève <see cref="ArgumentException"/> si elle dépasse <paramref name="maxByteLength"/>.
        /// </summary>
        public void WriteString(string value, int maxByteLength)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            int byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount > maxByteLength)
            {
                throw new ArgumentException($"Chaîne trop longue : {byteCount} > {maxByteLength} octets.", nameof(value));
            }

            WriteUInt16((ushort)byteCount);
            EnsureCapacity(byteCount);
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _position);
            _position += byteCount;
        }

        private void EnsureCapacity(int additionalBytes)
        {
            int required = _position + additionalBytes;
            if (required <= _buffer.Length)
            {
                return;
            }

            int newCapacity = Math.Max(_buffer.Length * 2, required);
            Array.Resize(ref _buffer, newCapacity);
        }
    }
}
