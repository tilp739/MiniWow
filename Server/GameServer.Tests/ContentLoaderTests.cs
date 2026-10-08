using System;
using System.IO;
using GameServer.Content;
using Xunit;

namespace GameServer.Tests
{
    public sealed class ContentLoaderTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "miniwow-content-" + Guid.NewGuid().ToString("N"));

        public ContentLoaderTests()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "zones"));

            // Classes et compétences minimales, requises par le chargeur.
            File.WriteAllText(Path.Combine(_directory, "classes.json"),
                @"{ ""classes"": [ { ""id"": ""Mage"", ""key"": ""mage"", ""name"": ""Mage"", ""resource"": ""Mana"" } ] }");
            File.WriteAllText(Path.Combine(_directory, "skills.json"), @"{ ""skills"": [] }");
            File.WriteAllText(Path.Combine(_directory, "items.json"), @"{ ""items"": [] }");
            File.WriteAllText(Path.Combine(_directory, "npcs.json"), @"{ ""npcs"": [] }");
            File.WriteAllText(Path.Combine(_directory, "quests.json"), @"{ ""quests"": [] }");
            File.WriteAllText(Path.Combine(_directory, "professions.json"), @"{ ""professions"": [] }");
            File.WriteAllText(Path.Combine(_directory, "recipes.json"), @"{ ""recipes"": [] }");
            File.WriteAllText(Path.Combine(_directory, "gathering_nodes.json"), @"{ ""gatheringNodes"": [] }");
            File.WriteAllText(Path.Combine(_directory, "adventures.json"), @"{ ""adventures"": [] }");
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Fact]
        public void Load_ReadsRealGameContent()
        {
            string real = Path.Combine(AppContext.BaseDirectory, "Content");

            WorldContent content = ContentLoader.Load(real, "greenhaven");

            Assert.Equal("greenhaven", content.ZoneId);
            Assert.True(content.Creatures.ContainsKey(1));
            Assert.Equal("grey_wolf", content.Creatures[1].Key);
            Assert.NotEmpty(content.CreatureSpawns);
        }

        [Fact]
        public void Load_ReadsRealCryptExpedition()
        {
            string real = Path.Combine(AppContext.BaseDirectory, "Content");

            WorldContent content = ContentLoader.Load(real, "crypt");

            Assert.Equal("crypt", content.ZoneId);
            Assert.True(content.Creatures.ContainsKey(5));
            Assert.Equal("renegade_necromancer", content.Creatures[5].Key);
            Assert.False(content.Creatures[5].IsBoss, "Le mini-boss ne doit pas terminer l'expédition à sa mort.");
            Assert.True(content.Creatures[3].IsBoss);
            Assert.Equal(6, content.CreatureSpawns.Count);
        }

        [Fact]
        public void Load_ReadsRealSandboxZone()
        {
            string real = Path.Combine(AppContext.BaseDirectory, "Content");

            WorldContent content = ContentLoader.Load(real, "sandbox");

            Assert.Equal("sandbox", content.ZoneId);
            Assert.True(content.Creatures.ContainsKey(6));
            Assert.Equal("alpha_wolf", content.Creatures[6].Key);
            Assert.True(content.Creatures.ContainsKey(7));
            Assert.Equal("savage_boar_elite", content.Creatures[7].Key);
            Assert.Equal(15, content.CreatureSpawns.Count);
            Assert.Equal(8, content.GatheringNodeSpawns.Count);
            Assert.Equal(3, content.Npcs.Count);
            Assert.True(content.Quests.ContainsKey(5));
        }

        [Fact]
        public void Load_ReadsCreatures_AndSpawns()
        {
            WriteCreatures(
                @"{ ""creatures"": [ { ""id"": 1, ""key"": ""wolf"", ""name"": ""Loup"", ""maxHealth"": 10, ""damageMin"": 1, ""damageMax"": 2, ""leashRange"": 20, ""aggroRange"": 10 } ] }");
            WriteZone(@"{ ""id"": ""test"", ""playerSpawn"": { ""x"": 1, ""z"": 2 }, ""creatures"": [ { ""template"": 1, ""x"": 5, ""z"": 6 } ] }");

            WorldContent content = ContentLoader.Load(_directory, "test");

            Assert.Equal(1f, content.PlayerSpawnX);
            Assert.Equal(2f, content.PlayerSpawnZ);
            Assert.Equal(10, content.Creatures[1].MaxHealth);
            Assert.Single(content.CreatureSpawns);
            Assert.Equal(5f, content.CreatureSpawns[0].X);
        }

        [Fact]
        public void Load_Throws_WhenDamageRangeIsInverted()
        {
            WriteCreatures(
                @"{ ""creatures"": [ { ""id"": 1, ""key"": ""wolf"", ""name"": ""Loup"", ""damageMin"": 9, ""damageMax"": 2 } ] }");
            WriteZone(@"{ ""id"": ""test"", ""creatures"": [] }");

            Assert.Throws<InvalidDataException>(() => ContentLoader.Load(_directory, "test"));
        }

        [Fact]
        public void Load_Throws_WhenIdsAreDuplicated()
        {
            WriteCreatures(
                @"{ ""creatures"": [ { ""id"": 1, ""key"": ""a"", ""name"": ""A"" }, { ""id"": 1, ""key"": ""b"", ""name"": ""B"" } ] }");
            WriteZone(@"{ ""id"": ""test"", ""creatures"": [] }");

            Assert.Throws<InvalidDataException>(() => ContentLoader.Load(_directory, "test"));
        }

        [Fact]
        public void Load_Throws_WhenSpawnReferencesUnknownTemplate()
        {
            WriteCreatures(@"{ ""creatures"": [ { ""id"": 1, ""key"": ""a"", ""name"": ""A"" } ] }");
            WriteZone(@"{ ""id"": ""test"", ""creatures"": [ { ""template"": 99, ""x"": 0, ""z"": 0 } ] }");

            Assert.Throws<InvalidDataException>(() => ContentLoader.Load(_directory, "test"));
        }

        [Fact]
        public void Load_Throws_WhenLeashIsSmallerThanAggro()
        {
            WriteCreatures(
                @"{ ""creatures"": [ { ""id"": 1, ""key"": ""a"", ""name"": ""A"", ""aggroRange"": 10, ""leashRange"": 5 } ] }");
            WriteZone(@"{ ""id"": ""test"", ""creatures"": [] }");

            Assert.Throws<InvalidDataException>(() => ContentLoader.Load(_directory, "test"));
        }

        [Fact]
        public void Load_Throws_WhenJsonIsMalformed()
        {
            WriteCreatures("{ not json");
            WriteZone(@"{ ""id"": ""test"" }");

            Assert.Throws<InvalidDataException>(() => ContentLoader.Load(_directory, "test"));
        }

        private void WriteCreatures(string json) => File.WriteAllText(Path.Combine(_directory, "creatures.json"), json);

        private void WriteZone(string json) => File.WriteAllText(Path.Combine(_directory, "zones", "test.json"), json);
    }
}
