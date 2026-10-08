using System;
using System.Buffers.Binary;

namespace GameShared.Protocol
{
    /// <summary>
    /// Encodage et décodage des trames.
    /// Format : [u32 length][u16 opcode][u32 requestId][payload]
    /// où length = taille de (opcode + requestId + payload). Little-endian partout.
    /// Aucune allocation : travaille sur des Span.
    /// </summary>
    public static class FrameCodec
    {
        /// <summary>Taille totale d'une trame pour un payload donné.</summary>
        public static int GetFrameSize(int payloadLength)
        {
            return ProtocolConstants.LengthPrefixSize + ProtocolConstants.BodyHeaderSize + payloadLength;
        }

        /// <summary>
        /// Écrit une trame dans <paramref name="destination"/> et retourne le nombre d'octets écrits.
        /// </summary>
        public static int WriteFrame(Span<byte> destination, Opcode opcode, uint requestId, ReadOnlySpan<byte> payload)
        {
            int frameSize = GetFrameSize(payload.Length);
            if (destination.Length < frameSize)
            {
                throw new ArgumentException($"Buffer trop petit : {destination.Length} < {frameSize}.", nameof(destination));
            }

            uint bodyLength = (uint)(ProtocolConstants.BodyHeaderSize + payload.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(destination, bodyLength);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(ProtocolConstants.LengthPrefixSize), (ushort)opcode);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(ProtocolConstants.LengthPrefixSize + sizeof(ushort)), requestId);
            payload.CopyTo(destination.Slice(ProtocolConstants.LengthPrefixSize + ProtocolConstants.BodyHeaderSize));

            return frameSize;
        }

        /// <summary>Crée une trame dans un nouveau tableau. Pour l'envoi, préférer <see cref="WriteFrame"/> avec un buffer réutilisé.</summary>
        public static byte[] CreateFrame(Opcode opcode, uint requestId, ReadOnlySpan<byte> payload)
        {
            byte[] frame = new byte[GetFrameSize(payload.Length)];
            WriteFrame(frame, opcode, requestId, payload);
            return frame;
        }

        /// <summary>
        /// Tente de lire une trame complète au début de <paramref name="buffer"/>.
        /// Retourne false si la trame est encore incomplète (rien n'est consommé).
        /// Lève <see cref="ProtocolException"/> si la longueur annoncée est invalide.
        /// </summary>
        public static bool TryReadFrame(
            ReadOnlySpan<byte> buffer,
            int maxBodySize,
            out FrameHeader header,
            out ReadOnlySpan<byte> payload,
            out int consumed)
        {
            header = default;
            payload = default;
            consumed = 0;

            if (buffer.Length < ProtocolConstants.LengthPrefixSize)
            {
                return false;
            }

            uint bodyLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            if (bodyLength < ProtocolConstants.BodyHeaderSize)
            {
                throw new ProtocolException($"Longueur de trame invalide : {bodyLength} octets.");
            }

            if (bodyLength > maxBodySize)
            {
                throw new ProtocolException($"Trame trop grande : {bodyLength} > {maxBodySize} octets.");
            }

            int frameSize = ProtocolConstants.LengthPrefixSize + (int)bodyLength;
            if (buffer.Length < frameSize)
            {
                return false;
            }

            ReadOnlySpan<byte> body = buffer.Slice(ProtocolConstants.LengthPrefixSize, (int)bodyLength);
            ushort rawOpcode = BinaryPrimitives.ReadUInt16LittleEndian(body);
            uint requestId = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(sizeof(ushort)));

            header = new FrameHeader((Opcode)rawOpcode, requestId);
            payload = body.Slice(ProtocolConstants.BodyHeaderSize);
            consumed = frameSize;
            return true;
        }
    }
}
