using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Persistence.Model;
using Npgsql;
using NpgsqlTypes;

namespace GameServer.Persistence.Characters
{
    /// <summary>Implémentation réelle, contre Postgres. ADO.NET brut : pas d'ORM.</summary>
    public sealed class NpgsqlCharacterRepository : ICharacterRepository
    {
        private readonly NpgsqlDataSource _dataSource;

        public NpgsqlCharacterRepository(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public async Task<IReadOnlyList<CharacterSummary>> ListCharactersAsync(long accountId, CancellationToken cancellationToken = default)
        {
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT character_id, name, class_id, level FROM characters WHERE account_id = $1 ORDER BY character_id",
                connection)
            {
                Parameters = { new NpgsqlParameter { Value = accountId } },
            };

            var results = new List<CharacterSummary>();
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new CharacterSummary(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    (byte)reader.GetInt16(2),
                    reader.GetInt32(3)));
            }

            return results;
        }

        public async Task<CreateCharacterResult> CreateCharacterAsync(
            long accountId,
            string name,
            byte classId,
            string startZone,
            float startX,
            float startZ,
            byte raceId = 0,
            CancellationToken cancellationToken = default)
        {
            if (!CharacterRules.IsValidName(name))
            {
                return CreateCharacterResult.Failed(CreateCharacterOutcome.InvalidName);
            }

            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using var countCommand = new NpgsqlCommand(
                "SELECT count(*) FROM characters WHERE account_id = $1", connection, transaction)
            {
                Parameters = { new NpgsqlParameter { Value = accountId } },
            };
            long existing = (long)(await countCommand.ExecuteScalarAsync(cancellationToken))!;
            if (existing >= CharacterRules.MaxCharactersPerAccount)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CreateCharacterResult.Failed(CreateCharacterOutcome.LimitReached);
            }

            await using var insertCommand = new NpgsqlCommand(
                """
                INSERT INTO characters (account_id, name, class_id, zone, pos_x, pos_z, inventory, quest_log, race_id)
                VALUES ($1, $2, $3, $4, $5, $6, '{}', '{}', $7)
                ON CONFLICT (name) DO NOTHING
                RETURNING character_id
                """,
                connection,
                transaction)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = accountId },
                    new NpgsqlParameter { Value = name },
                    new NpgsqlParameter { Value = (short)classId },
                    new NpgsqlParameter { Value = startZone },
                    new NpgsqlParameter { Value = startX },
                    new NpgsqlParameter { Value = startZ },
                    new NpgsqlParameter { Value = (short)raceId },
                },
            };

            object? inserted = await insertCommand.ExecuteScalarAsync(cancellationToken);
            if (inserted is not long characterId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CreateCharacterResult.Failed(CreateCharacterOutcome.NameTaken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new CreateCharacterResult(CreateCharacterOutcome.Created, new CharacterSummary(characterId, name, classId, 1));
        }

        public async Task<CharacterSnapshot?> LoadCharacterAsync(long characterId, CancellationToken cancellationToken = default)
        {
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                SELECT character_id, account_id, name, class_id, level, experience, copper, zone, pos_x, pos_z, inventory, quest_log, professions, race_id
                FROM characters WHERE character_id = $1
                """,
                connection)
            {
                Parameters = { new NpgsqlParameter { Value = characterId } },
            };

            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new CharacterSnapshot(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                (byte)reader.GetInt16(3),
                reader.GetInt32(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                reader.GetString(7),
                reader.GetFloat(8),
                reader.GetFloat(9),
                CharacterStateJson.DeserializeInventory(reader.GetString(10)),
                CharacterStateJson.DeserializeQuests(reader.GetString(11)),
                CharacterStateJson.DeserializeProfessions(reader.GetString(12)),
                (byte)reader.GetInt16(13));
        }

        public async Task SaveCharacterAsync(CharacterSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                UPDATE characters SET
                    level = $2, experience = $3, copper = $4, zone = $5, pos_x = $6, pos_z = $7,
                    inventory = $8, quest_log = $9, professions = $10, last_played_at = now()
                WHERE character_id = $1
                """,
                connection)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = snapshot.CharacterId },
                    new NpgsqlParameter { Value = snapshot.Level },
                    new NpgsqlParameter { Value = snapshot.Experience },
                    new NpgsqlParameter { Value = snapshot.Copper },
                    new NpgsqlParameter { Value = snapshot.Zone },
                    new NpgsqlParameter { Value = snapshot.X },
                    new NpgsqlParameter { Value = snapshot.Z },
                    new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = CharacterStateJson.Serialize(snapshot.Inventory) },
                    new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = CharacterStateJson.Serialize(snapshot.Quests) },
                    new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = CharacterStateJson.Serialize(snapshot.Professions) },
                },
            };

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
