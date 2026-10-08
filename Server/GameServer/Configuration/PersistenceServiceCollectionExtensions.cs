using System;
using GameServer.Persistence.Accounts;
using GameServer.Persistence.Characters;
using GameServer.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GameServer.Configuration
{
    /// <summary>
    /// Bascule la persistance de comptes/personnages sur Postgres (ADO.NET brut, via Npgsql), en remplaçant
    /// les implémentations en mémoire enregistrées par <c>AddGameServer</c>. Appelé uniquement par le serveur
    /// réel (<c>Program.cs</c>) : les tests via <c>ServerHarness</c> gardent les implémentations en mémoire.
    /// </summary>
    public static class PersistenceServiceCollectionExtensions
    {
        /// <summary>
        /// Mot de passe de développement local, identique au placeholder de <c>.env.example</c> et donc au
        /// mot de passe réel du conteneur Postgres tel qu'initialisé par <c>docker-compose.yml</c> tant que
        /// personne n'a changé <c>.env</c>. Jamais utilisé si <c>MINIWOW_DB_PASSWORD</c> est définie.
        /// </summary>
        private const string DevDefaultPassword = "change-me-in-local-env";

        public static IServiceCollection AddPostgresCharacterPersistence(this IServiceCollection services, DatabaseOptions database)
        {
            string? password = Environment.GetEnvironmentVariable("MINIWOW_DB_PASSWORD");
            if (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("MINIWOW_DB_PASSWORD non définie : mot de passe de développement par défaut utilisé (loopback uniquement).");
                password = DevDefaultPassword;
            }

            string connectionString =
                $"Host={database.Host};Port={database.Port};Database={database.Database};" +
                $"Username={database.Username};Password={password}";

            services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            services.AddSingleton<MigrationRunner>();

            // Dernière inscription gagnante : remplace les implémentations en mémoire d'AddGameServer.
            services.AddSingleton<IAccountRepository, NpgsqlAccountRepository>();
            services.AddSingleton<ICharacterRepository, NpgsqlCharacterRepository>();

            return services;
        }
    }
}
