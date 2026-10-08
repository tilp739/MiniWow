using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    public enum ChatChannel : byte
    {
        /// <summary>Local : joueurs proches.</summary>
        Say = 1,

        /// <summary>Privé : un joueur précis, désigné par son nom.</summary>
        Whisper = 2,

        /// <summary>Groupe.</summary>
        Party = 3,
    }

    /// <summary>Message envoyé par le client. <c>Target</c> : nom du destinataire, utilisé pour Whisper uniquement.</summary>
    public readonly struct ChatSendMessage : IWireMessage
    {
        public const int MaxTextByteLength = 200;
        public const int MaxNameByteLength = 32;

        public ChatSendMessage(ChatChannel channel, string target, string text)
        {
            Channel = channel;
            Target = target;
            Text = text;
        }

        public ChatChannel Channel { get; }

        public string Target { get; }

        public string Text { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte((byte)Channel);
            writer.WriteString(Target, MaxNameByteLength);
            writer.WriteString(Text, MaxTextByteLength);
        }

        public static ChatSendMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte channel = reader.ReadByte();
            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ProtocolException($"Canal de chat inconnu : {channel}.");
            }

            string target = reader.ReadString(MaxNameByteLength);
            string text = reader.ReadString(MaxTextByteLength);
            reader.ExpectEnd();
            return new ChatSendMessage((ChatChannel)channel, target, text);
        }
    }

    /// <summary>Message reçu par le client : canal, auteur et texte.</summary>
    public readonly struct ChatLineMessage : IWireMessage
    {
        public ChatLineMessage(ChatChannel channel, string sender, string text)
        {
            Channel = channel;
            Sender = sender;
            Text = text;
        }

        public ChatChannel Channel { get; }

        public string Sender { get; }

        public string Text { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte((byte)Channel);
            writer.WriteString(Sender, ChatSendMessage.MaxNameByteLength);
            writer.WriteString(Text, ChatSendMessage.MaxTextByteLength);
        }

        public static ChatLineMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte channel = reader.ReadByte();
            if (!Enum.IsDefined(typeof(ChatChannel), channel))
            {
                throw new ProtocolException($"Canal de chat inconnu : {channel}.");
            }

            string sender = reader.ReadString(ChatSendMessage.MaxNameByteLength);
            string text = reader.ReadString(ChatSendMessage.MaxTextByteLength);
            reader.ExpectEnd();
            return new ChatLineMessage((ChatChannel)channel, sender, text);
        }
    }
}
