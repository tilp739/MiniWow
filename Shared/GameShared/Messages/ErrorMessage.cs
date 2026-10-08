using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Erreur signalée au client. Le message est informatif ; le code fait foi.</summary>
    public readonly struct ErrorMessage : IWireMessage
    {
        public const int MaxTextByteLength = 128;

        public ErrorMessage(ErrorCode code, string text)
        {
            Code = code;
            Text = text;
        }

        public ErrorCode Code { get; }

        public string Text { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt16((ushort)Code);
            writer.WriteString(Text, MaxTextByteLength);
        }

        public static ErrorMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            var code = (ErrorCode)reader.ReadUInt16();
            string text = reader.ReadString(MaxTextByteLength);
            reader.ExpectEnd();

            return new ErrorMessage(code, text);
        }
    }
}
