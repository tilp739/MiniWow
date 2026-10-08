using System.Threading.Tasks;
using GameServer.Persistence.Accounts;
using Npgsql;

namespace GameServer.Persistence.Tests
{
    /// <summary>
    /// Contre le Postgres réel du <c>docker-compose.yml</c>. Si injoignable, chaque test se termine
    /// silencieusement (pas de framework de "skip" en xUnit v2 ici, et on ne veut pas ajouter de dépendance
    /// juste pour ça) plutôt que d'échouer la suite entière en l'absence de Docker.
    /// Collection partagée avec <see cref="NpgsqlCharacterRepositoryTests"/> : xUnit exécute les classes de
    /// test en parallèle par défaut, mais <see cref="PostgresFixture.TryCreateAsync"/> vide la base à chaque
    /// test (TRUNCATE) — sans cette collection commune, deux tests de classes différentes se marchent dessus.
    /// </summary>
    [Collection("Postgres")]
    public sealed class NpgsqlAccountRepositoryTests
    {
        [Fact]
        public async Task CreateAccount_ThenAuthenticate_Succeeds()
        {
            await using NpgsqlDataSource? dataSource = await PostgresFixture.TryCreateAsync();
            if (dataSource == null)
            {
                return;
            }

            var repository = new NpgsqlAccountRepository(dataSource);

            CreateAccountResult created = await repository.CreateAccountAsync("Oggrim", "motdepasse1");
            Assert.Equal(CreateAccountOutcome.Created, created.Outcome);

            AuthenticateResult authenticated = await repository.TryAuthenticateAsync("oggrim", "motdepasse1");
            Assert.Equal(AuthenticateOutcome.Success, authenticated.Outcome);
            Assert.Equal(created.AccountId, authenticated.AccountId);
        }

        [Fact]
        public async Task CreateAccount_DuplicateUsername_IsRejected()
        {
            await using NpgsqlDataSource? dataSource = await PostgresFixture.TryCreateAsync();
            if (dataSource == null)
            {
                return;
            }

            var repository = new NpgsqlAccountRepository(dataSource);

            await repository.CreateAccountAsync("Teremus", "motdepasse1");
            CreateAccountResult duplicate = await repository.CreateAccountAsync("teremus", "autremotdepasse");

            Assert.Equal(CreateAccountOutcome.UsernameTaken, duplicate.Outcome);
        }

        [Fact]
        public async Task Authenticate_WrongPassword_Fails()
        {
            await using NpgsqlDataSource? dataSource = await PostgresFixture.TryCreateAsync();
            if (dataSource == null)
            {
                return;
            }

            var repository = new NpgsqlAccountRepository(dataSource);
            await repository.CreateAccountAsync("Bariss", "motdepasse1");

            AuthenticateResult result = await repository.TryAuthenticateAsync("Bariss", "mauvais-mot-de-passe");

            Assert.Equal(AuthenticateOutcome.WrongPassword, result.Outcome);
        }
    }
}
