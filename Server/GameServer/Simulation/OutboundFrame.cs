using System;
using System.Buffers;
using GameShared.Messages;
using GameShared.Protocol;

namespace GameServer.Simulation
{
    /// <summary>
    /// Trame sortante dont le tampon vient d'un pool. La session le rend après écriture sur le socket,
    /// ce qui évite une allocation par trame et par joueur à chaque tick.
    /// </summary>
    public readonly struct OutboundFrame
    {
        private OutboundFrame(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }

        public byte[] Buffer { get; }

        public int Length { get; }

        /// <summary>Encode un message dans un tampon loué. Le message est écrit dans <paramref name="writer"/>, réutilisé.</summary>
        public static OutboundFrame Rent<T>(Opcode opcode, T message, PacketWriter writer) where T : IWireMessage
        {
            writer.Reset();
            message.Write(writer);

            int size = FrameCodec.GetFrameSize(writer.Length);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
            FrameCodec.WriteFrame(buffer, opcode, 0, writer.WrittenSpan);
            return new OutboundFrame(buffer, size);
        }

        /// <summary>Rend le tampon au pool. À appeler une seule fois, après l'envoi ou l'abandon de la trame.</summary>
        public void Return()
        {
            if (Buffer != null)
            {
                ArrayPool<byte>.Shared.Return(Buffer);
            }
        }
    }
}
