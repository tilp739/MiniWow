using System.Collections.Generic;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameShared.Tests
{
    public sealed class AccountAndCharacterMessagesTests
    {
        [Fact]
        public void Register_RoundTrips()
        {
            var writer = new PacketWriter();
            new RegisterMessage("Oggrim", "motdepasse1").Write(writer);

            RegisterMessage decoded = RegisterMessage.Read(writer.WrittenSpan);

            Assert.Equal("Oggrim", decoded.Username);
            Assert.Equal("motdepasse1", decoded.Password);
        }

        [Fact]
        public void RegisterResult_RoundTrips_WithError()
        {
            var writer = new PacketWriter();
            new RegisterResultMessage(false, "Identifiant déjà utilisé.").Write(writer);

            RegisterResultMessage decoded = RegisterResultMessage.Read(writer.WrittenSpan);

            Assert.False(decoded.Success);
            Assert.Equal("Identifiant déjà utilisé.", decoded.ErrorText);
        }

        [Fact]
        public void Login_RoundTrips()
        {
            var writer = new PacketWriter();
            new LoginMessage("Oggrim", "motdepasse1").Write(writer);

            LoginMessage decoded = LoginMessage.Read(writer.WrittenSpan);

            Assert.Equal("Oggrim", decoded.Username);
            Assert.Equal("motdepasse1", decoded.Password);
        }

        [Fact]
        public void CharacterList_RoundTrips_WithSeveralEntries()
        {
            var entries = new List<(long CharacterId, string Name, byte ClassId, ushort Level)>
            {
                (1, "Thrundar", 1, 1),
                (2, "Myralle", 2, 12),
            };

            var writer = new PacketWriter();
            new CharacterListMessage(entries).Write(writer);

            CharacterListMessage decoded = CharacterListMessage.Read(writer.WrittenSpan);

            Assert.Equal(2, decoded.Characters.Count);
            Assert.Equal("Thrundar", decoded.Characters[0].Name);
            Assert.Equal(12, decoded.Characters[1].Level);
        }

        [Fact]
        public void CharacterList_Throws_WhenTooManyCharacters()
        {
            var entries = new List<(long CharacterId, string Name, byte ClassId, ushort Level)>();
            for (int i = 0; i < CharacterListMessage.MaxCharacters + 1; i++)
            {
                entries.Add((i, $"Perso{i}", 1, 1));
            }

            var writer = new PacketWriter();
            Assert.Throws<System.ArgumentException>(() => new CharacterListMessage(entries).Write(writer));
        }

        [Fact]
        public void CreateCharacter_RoundTrips()
        {
            var writer = new PacketWriter();
            new CreateCharacterMessage("Thrundar", 1, 1).Write(writer);

            CreateCharacterMessage decoded = CreateCharacterMessage.Read(writer.WrittenSpan);

            Assert.Equal("Thrundar", decoded.Name);
            Assert.Equal(1, decoded.ClassId);
            Assert.Equal(1, decoded.RaceId);
        }

        [Fact]
        public void EnterWorldByCharacter_RoundTrips()
        {
            var writer = new PacketWriter();
            new EnterWorldByCharacterMessage(42).Write(writer);

            EnterWorldByCharacterMessage decoded = EnterWorldByCharacterMessage.Read(writer.WrittenSpan);

            Assert.Equal(42, decoded.CharacterId);
        }
    }
}
