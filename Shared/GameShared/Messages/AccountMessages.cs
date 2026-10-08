using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Demande de création de compte. La validation (longueur, caractères) est faite côté serveur.</summary>
    public readonly struct RegisterMessage : IWireMessage
    {
        public const int MaxUsernameByteLength = 32;
        public const int MaxPasswordByteLength = 128;

        public RegisterMessage(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }

        public string Password { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteString(Username, MaxUsernameByteLength);
            writer.WriteString(Password, MaxPasswordByteLength);
        }

        public static RegisterMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            string username = reader.ReadString(MaxUsernameByteLength);
            string password = reader.ReadString(MaxPasswordByteLength);
            reader.ExpectEnd();
            return new RegisterMessage(username, password);
        }
    }

    /// <summary>Résultat d'une création de compte. <c>ErrorText</c> vide si réussie.</summary>
    public readonly struct RegisterResultMessage : IWireMessage
    {
        public const int MaxErrorTextByteLength = 128;

        public RegisterResultMessage(bool success, string errorText)
        {
            Success = success;
            ErrorText = errorText;
        }

        public bool Success { get; }

        public string ErrorText { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteBoolean(Success);
            writer.WriteString(ErrorText, MaxErrorTextByteLength);
        }

        public static RegisterResultMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            bool success = reader.ReadBoolean();
            string errorText = reader.ReadString(MaxErrorTextByteLength);
            reader.ExpectEnd();
            return new RegisterResultMessage(success, errorText);
        }
    }

    /// <summary>Demande de connexion à un compte existant.</summary>
    public readonly struct LoginMessage : IWireMessage
    {
        public LoginMessage(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }

        public string Password { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteString(Username, RegisterMessage.MaxUsernameByteLength);
            writer.WriteString(Password, RegisterMessage.MaxPasswordByteLength);
        }

        public static LoginMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            string username = reader.ReadString(RegisterMessage.MaxUsernameByteLength);
            string password = reader.ReadString(RegisterMessage.MaxPasswordByteLength);
            reader.ExpectEnd();
            return new LoginMessage(username, password);
        }
    }

    /// <summary>Résultat d'une connexion. En cas de succès, le serveur envoie ensuite <see cref="CharacterListMessage"/>.</summary>
    public readonly struct LoginResultMessage : IWireMessage
    {
        public LoginResultMessage(bool success, string errorText)
        {
            Success = success;
            ErrorText = errorText;
        }

        public bool Success { get; }

        public string ErrorText { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteBoolean(Success);
            writer.WriteString(ErrorText, RegisterResultMessage.MaxErrorTextByteLength);
        }

        public static LoginResultMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            bool success = reader.ReadBoolean();
            string errorText = reader.ReadString(RegisterResultMessage.MaxErrorTextByteLength);
            reader.ExpectEnd();
            return new LoginResultMessage(success, errorText);
        }
    }
}
