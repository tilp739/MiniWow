using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Construit une trame complète à partir d'un message, en réutilisant un écrivain fourni.</summary>
    public static class MessageFrame
    {
        public static byte[] Create<T>(Opcode opcode, T message, PacketWriter writer) where T : IWireMessage
        {
            writer.Reset();
            message.Write(writer);
            return FrameCodec.CreateFrame(opcode, 0, writer.WrittenSpan);
        }
    }
}
