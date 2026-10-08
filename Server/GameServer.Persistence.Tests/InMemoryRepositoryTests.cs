using System.Threading.Tasks;
using GameServer.Persistence.Accounts;
using GameServer.Persistence.Characters;
using GameServer.Persistence.Model;

namespace GameServer.Persistence.Tests
{
    /// <summary>Les doubles en mémoire sont réels (pas des mocks) : testés comme n'importe quelle implémentation.</summary>
    public sealed class InMemoryRepositoryTests
    {
        [Fact]
        public async Task Account_CreateThenAuthenticate_Succeeds()
        {
            var repository = new InMemoryAccountRepository();

            CreateAccountResult created = await repository.CreateAccountAsync("Shiraal", "motdepasse1");
            Assert.Equal(CreateAccountOutcome.Created, created.Outcome);

            AuthenticateResult authenticated = await repository.TryAuthenticateAsync("Shiraal", "motdepasse1");
            Assert.Equal(AuthenticateOutcome.Success, authenticated.Outcome);
        }

        [Fact]
        public async Task Account_WrongPassword_Fails()
        {
            var repository = new InMemoryAccountRepository();
            await repository.CreateAccountAsync("Vostra", "motdepasse1");

            AuthenticateResult result = await repository.TryAuthenticateAsync("Vostra", "autre-chose");

            Assert.Equal(AuthenticateOutcome.WrongPassword, result.Outcome);
        }

        [Fact]
        public async Task Character_CreateThenLoad_RoundTrips()
        {
            var repository = new InMemoryCharacterRepository();

            CreateCharacterResult created = await repository.CreateCharacterAsync(1, "Loxanne", classId: 2, "greenhaven", 5f, 6f);
            Assert.Equal(CreateCharacterOutcome.Created, created.Outcome);

            CharacterSnapshot? loaded = await repository.LoadCharacterAsync(created.Summary!.CharacterId);
            Assert.NotNull(loaded);
            Assert.Equal("Loxanne", loaded!.Name);
            Assert.Equal(2, loaded.ClassId);
        }

        [Fact]
        public async Task Character_BeyondLimit_IsRejected()
        {
            var repository = new InMemoryCharacterRepository();
            string[] names = { "Aldar", "Brondu", "Corvan", "Daenys", "Eolan", "Fendra", "Goric", "Halvar" };
            Assert.Equal(CharacterRules.MaxCharactersPerAccount, names.Length);
            foreach (string name in names)
            {
                await repository.CreateCharacterAsync(1, name, classId: 1, "greenhaven", 0f, 0f);
            }

            CreateCharacterResult overLimit = await repository.CreateCharacterAsync(1, "DeTrop", classId: 1, "greenhaven", 0f, 0f);

            Assert.Equal(CreateCharacterOutcome.LimitReached, overLimit.Outcome);
        }
    }
}
