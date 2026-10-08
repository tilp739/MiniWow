using System;
using System.Threading.Tasks;
using GameServer.Persistence.Migrations;
using Npgsql;

namespace GameServer.Persistence.Tests
{
    /// <summary>
    /// Connexion au Postgres du <c>docker-compose.yml</c> de développement. Seul point de la suite
    /// à dépendre d'un process externe (comme <c>ContentLoaderTests</c> dépend du disque) : si la base
    /// est injoignable, les tests qui l'utilisent se signalent "ignorés" plutôt que d'échouer.
    /// </summary>
    public static class PostgresFixture
    {
        private static readonly string ConnectionString = BuildConnectionString();

        public static async Task<NpgsqlDataSource?> TryCreateAsync()
        {
            NpgsqlDataSource dataSource = NpgsqlDataSource.Create(ConnectionString);
            try
            {
                await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
                await new MigrationRunner(dataSource).RunAsync();
                await using var truncate = new NpgsqlCommand("TRUNCATE characters, accounts RESTART IDENTITY CASCADE", connection);
                await truncate.ExecuteNonQueryAsync();
                return dataSource;
            }
            catch (Exception)
            {
                await dataSource.DisposeAsync();
                return null;
            }
        }

        private static string BuildConnectionString()
        {
            string password = Environment.GetEnvironmentVariable("MINIWOW_DB_PASSWORD") ?? "change-me-in-local-env";
            return $"Host=127.0.0.1;Port=5432;Database=miniwow_dev;Username=miniwow;Password={password};Timeout=2";
        }
    }
}
