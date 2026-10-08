using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace GameServer.Persistence.Migrations
{
    /// <summary>
    /// Applique les scripts SQL numérotés de <c>Migrations/*.sql</c> (embarqués) une seule fois chacun,
    /// suivis dans la table <c>schema_migrations</c>. Pas d'EF Core Migrations/DbUp : juste du SQL brut.
    /// Doit être exécuté avant que le serveur n'accepte des connexions.
    /// </summary>
    public sealed class MigrationRunner
    {
        private const string ResourcePrefix = "GameServer.Persistence.Migrations.";

        private readonly NpgsqlDataSource _dataSource;

        public MigrationRunner(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);

            await using (var createTable = new NpgsqlCommand(
                "CREATE TABLE IF NOT EXISTS schema_migrations (version INT PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL DEFAULT now())",
                connection))
            {
                await createTable.ExecuteNonQueryAsync(cancellationToken);
            }

            var applied = new HashSet<int>();
            await using (var select = new NpgsqlCommand("SELECT version FROM schema_migrations", connection))
            await using (NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    applied.Add(reader.GetInt32(0));
                }
            }

            foreach ((int version, string sql) in LoadEmbeddedMigrations().OrderBy(m => m.Version))
            {
                if (applied.Contains(version))
                {
                    continue;
                }

                await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

                await using (var exec = new NpgsqlCommand(sql, connection, transaction))
                {
                    await exec.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var record = new NpgsqlCommand("INSERT INTO schema_migrations (version) VALUES ($1)", connection, transaction)
                {
                    Parameters = { new NpgsqlParameter { Value = version } },
                })
                {
                    await record.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
        }

        private static IEnumerable<(int Version, string Sql)> LoadEmbeddedMigrations()
        {
            Assembly assembly = typeof(MigrationRunner).Assembly;

            foreach (string resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal) ||
                    !resourceName.EndsWith(".sql", StringComparison.Ordinal))
                {
                    continue;
                }

                string fileName = resourceName[ResourcePrefix.Length..^".sql".Length];
                int separator = fileName.IndexOf('_');
                if (separator <= 0 || !int.TryParse(fileName[..separator], out int version))
                {
                    throw new InvalidOperationException($"Nom de migration invalide : {resourceName}");
                }

                using Stream stream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Ressource introuvable : {resourceName}");
                using var textReader = new StreamReader(stream);
                yield return (version, textReader.ReadToEnd());
            }
        }
    }
}
