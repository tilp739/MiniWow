using System;
using System.Collections.Generic;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>
    /// Race jouable. Purement cosmétique à ce stade (aucun effet sur les statistiques) : détermine
    /// uniquement le modèle 3D affiché (cf. <c>EntityState.TemplateId</c> pour un joueur).
    /// </summary>
    public enum CharacterRace : byte
    {
        Human = 0,
        Orc = 1,
    }

    /// <summary>Liste des personnages du compte connecté (écran de sélection).</summary>
    public sealed class CharacterListMessage : IWireMessage
    {
        public const int MaxCharacters = 8;
        public const int MaxNameByteLength = 32;

        public CharacterListMessage(IReadOnlyList<(long CharacterId, string Name, byte ClassId, ushort Level)> characters)
        {
            Characters = characters;
        }

        public IReadOnlyList<(long CharacterId, string Name, byte ClassId, ushort Level)> Characters { get; }

        public void Write(PacketWriter writer)
        {
            if (Characters.Count > MaxCharacters)
            {
                throw new ArgumentException("Trop de personnages pour un seul compte.");
            }

            writer.WriteByte((byte)Characters.Count);
            foreach ((long characterId, string name, byte classId, ushort level) in Characters)
            {
                writer.WriteInt64(characterId);
                writer.WriteString(name, MaxNameByteLength);
                writer.WriteByte(classId);
                writer.WriteUInt16(level);
            }
        }

        public static CharacterListMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            int count = reader.ReadByte();
            if (count > MaxCharacters)
            {
                throw new ProtocolException("Trop de personnages pour un seul compte.");
            }

            var characters = new List<(long CharacterId, string Name, byte ClassId, ushort Level)>(count);
            for (int i = 0; i < count; i++)
            {
                long characterId = reader.ReadInt64();
                string name = reader.ReadString(MaxNameByteLength);
                byte classId = reader.ReadByte();
                ushort level = reader.ReadUInt16();
                characters.Add((characterId, name, classId, level));
            }

            reader.ExpectEnd();
            return new CharacterListMessage(characters);
        }
    }

    /// <summary>Demande de création d'un nouveau personnage sur le compte connecté.</summary>
    public readonly struct CreateCharacterMessage : IWireMessage
    {
        public CreateCharacterMessage(string name, byte classId, byte raceId)
        {
            Name = name;
            ClassId = classId;
            RaceId = raceId;
        }

        public string Name { get; }

        public byte ClassId { get; }

        public byte RaceId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteString(Name, CharacterListMessage.MaxNameByteLength);
            writer.WriteByte(ClassId);
            writer.WriteByte(RaceId);
        }

        public static CreateCharacterMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            string name = reader.ReadString(CharacterListMessage.MaxNameByteLength);
            byte classId = reader.ReadByte();
            byte raceId = reader.ReadByte();
            reader.ExpectEnd();
            return new CreateCharacterMessage(name, classId, raceId);
        }
    }

    /// <summary>Résultat d'une création de personnage. En cas de succès, le serveur envoie ensuite <see cref="CharacterListMessage"/>.</summary>
    public readonly struct CreateCharacterResultMessage : IWireMessage
    {
        public const int MaxErrorTextByteLength = 128;

        public CreateCharacterResultMessage(bool success, string errorText)
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

        public static CreateCharacterResultMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            bool success = reader.ReadBoolean();
            string errorText = reader.ReadString(MaxErrorTextByteLength);
            reader.ExpectEnd();
            return new CreateCharacterResultMessage(success, errorText);
        }
    }

    /// <summary>Demande d'entrée dans le monde avec un personnage précis du compte connecté.</summary>
    public readonly struct EnterWorldByCharacterMessage : IWireMessage
    {
        public EnterWorldByCharacterMessage(long characterId)
        {
            CharacterId = characterId;
        }

        public long CharacterId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteInt64(CharacterId);
        }

        public static EnterWorldByCharacterMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            long characterId = reader.ReadInt64();
            reader.ExpectEnd();
            return new EnterWorldByCharacterMessage(characterId);
        }
    }
}
