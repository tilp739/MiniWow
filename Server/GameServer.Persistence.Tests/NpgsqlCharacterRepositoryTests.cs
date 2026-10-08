using System.Collections.Generic;
using System.Threading.Tasks;
using GameServer.Persistence.Accounts;
using GameServer.Persistence.Characters;
using GameServer.Persistence.Model;
using Npgsql;

namespace GameServer.Persistence.Tests
{
    /// <summary>Contre le Postgres réel du <c>docker-compose.yml</c>. Voir <see cref="NpgsqlAccountRepositoryTests"/>.</summary>
    [Collection("Postgres")]
    public sealed class NpgsqlCharacterRepositoryTests
    {
        private static async Task<(NpgsqlDataSource DataSource, long AccountId)?> CreateAccountAsync()
        {
            NpgsqlDataSource? dataSource = await PostgresFixture.TryCreateAsync();
            if (dataSource == null)
            {
                return null;
            }

            CreateAccountResult account = await new NpgsqlAccountRepository(dataSource).CreateAccountAsync("Fenwick", "motdepasse1");
            return (dataSource, account.AccountId);
        }

        [Fact]
        public async Task CreateCharacter_ThenLoad_RoundTripsBasicFields()
        {
            (NpgsqlDataSource DataSource, long AccountId)? setup = await CreateAccountAsync();
            if (setup == null)
            {
                return;
            }

            await using NpgsqlDataSource dataSource = setup.Value.DataSource;
            var repository = new NpgsqlCharacterRepository(dataSource);

            CreateCharacterResult created = await repository.CreateCharacterAsync(setup.Value.AccountId, "Thrundar", classId: 1, "greenhaven", 10f, 20f);
            Assert.Equal(CreateCharacterOutcome.Created, created.Outcome);

            CharacterSnapshot? loaded = await repository.LoadCharacterAsync(created.Summary!.CharacterId);
            Assert.NotNull(loaded);
            Assert.Equal("Thrundar", loaded!.Name);
            Assert.Equal(1, loaded.Level);
            Assert.Equal("greenhaven", loaded.Zone);
            Assert.Equal(10f, loaded.X);
            Assert.Equal(20f, loaded.Z);
            Assert.Empty(loaded.Inventory.Bag);
            Assert.Empty(loaded.Quests.Active);
        }

        [Fact]
        public async Task SaveCharacter_PersistsInventoryAndQuestLog()
        {
            (NpgsqlDataSource DataSource, long AccountId)? setup = await CreateAccountAsync();
            if (setup == null)
            {
                return;
            }

            await using NpgsqlDataSource dataSource = setup.Value.DataSource;
            var repository = new NpgsqlCharacterRepository(dataSource);
            CreateCharacterResult created = await repository.CreateCharacterAsync(setup.Value.AccountId, "Myralle", classId: 2, "greenhaven", 0f, 0f, raceId: 1);

            var inventory = new InventorySnapshot(
                new List<BagSlotSnapshot> { new BagSlotSnapshot(0, itemId: 5, quantity: 3) },
                new List<EquippedItemSnapshot> { new EquippedItemSnapshot(equipSlot: 5, itemId: 12, quantity: 1) });
            var quests = new QuestLogSnapshot(
                new List<ActiveQuestSnapshot> { new ActiveQuestSnapshot(questId: 7, new List<int> { 1, 0 }) },
                new List<ushort> { 3 });
            var professions = new List<ProfessionSnapshot> { new ProfessionSnapshot(professionId: 1, skill: 12) };

            var snapshot = new CharacterSnapshot(
                created.Summary!.CharacterId, setup.Value.AccountId, "Myralle", classId: 2,
                level: 4, experience: 150, copper: 999, zone: "crypt", x: 1.5f, z: 2.5f, inventory, quests, professions);

            await repository.SaveCharacterAsync(snapshot);

            CharacterSnapshot? reloaded = await repository.LoadCharacterAsync(created.Summary.CharacterId);
            Assert.NotNull(reloaded);
            Assert.Equal(4, reloaded!.Level);
            Assert.Equal(150, reloaded.Experience);
            Assert.Equal(999, reloaded.Copper);
            Assert.Equal("crypt", reloaded.Zone);
            Assert.Single(reloaded.Inventory.Bag);
            Assert.Equal(5, (int)reloaded.Inventory.Bag[0].ItemId);
            Assert.Single(reloaded.Inventory.Equipment);
            Assert.Single(reloaded.Quests.Active);
            Assert.Equal(new[] { 1, 0 }, reloaded.Quests.Active[0].Progress);
            Assert.Single(reloaded.Quests.Completed);
            Assert.Single(reloaded.Professions);
            Assert.Equal(12, reloaded.Professions[0].Skill);
            Assert.Equal(1, reloaded.RaceId);
        }

        [Fact]
        public async Task CreateCharacter_DuplicateName_IsRejected()
        {
            (NpgsqlDataSource DataSource, long AccountId)? setup = await CreateAccountAsync();
            if (setup == null)
            {
                return;
            }

            await using NpgsqlDataSource dataSource = setup.Value.DataSource;
            var repository = new NpgsqlCharacterRepository(dataSource);

            await repository.CreateCharacterAsync(setup.Value.AccountId, "Doublette", classId: 1, "greenhaven", 0f, 0f);
            CreateCharacterResult duplicate = await repository.CreateCharacterAsync(setup.Value.AccountId, "doublette", classId: 2, "greenhaven", 0f, 0f);

            Assert.Equal(CreateCharacterOutcome.NameTaken, duplicate.Outcome);
        }

        [Fact]
        public async Task CreateCharacter_BeyondLimit_IsRejected()
        {
            (NpgsqlDataSource DataSource, long AccountId)? setup = await CreateAccountAsync();
            if (setup == null)
            {
                return;
            }

            await using NpgsqlDataSource dataSource = setup.Value.DataSource;
            var repository = new NpgsqlCharacterRepository(dataSource);

            string[] names = { "Aldar", "Brondu", "Corvan", "Daenys", "Eolan", "Fendra", "Goric", "Halvar" };
            Assert.Equal(CharacterRules.MaxCharactersPerAccount, names.Length);
            foreach (string name in names)
            {
                CreateCharacterResult ok = await repository.CreateCharacterAsync(setup.Value.AccountId, name, classId: 1, "greenhaven", 0f, 0f);
                Assert.Equal(CreateCharacterOutcome.Created, ok.Outcome);
            }

            CreateCharacterResult overLimit = await repository.CreateCharacterAsync(setup.Value.AccountId, "PersoTropLoin", classId: 1, "greenhaven", 0f, 0f);
            Assert.Equal(CreateCharacterOutcome.LimitReached, overLimit.Outcome);
        }
    }
}
