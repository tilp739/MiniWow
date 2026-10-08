using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace GameServer.Persistence.Accounts
{
    /// <summary>Implémentation réelle, contre Postgres. ADO.NET brut : pas d'ORM.</summary>
    public sealed class NpgsqlAccountRepository : IAccountRepository
    {
        private readonly NpgsqlDataSource _dataSource;

        public NpgsqlAccountRepository(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public async Task<CreateAccountResult> CreateAccountAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (!AccountRules.IsValidUsername(username))
            {
                return CreateAccountResult.Failed(CreateAccountOutcome.InvalidUsername);
            }

            if (!AccountRules.IsValidPassword(password))
            {
                return CreateAccountResult.Failed(CreateAccountOutcome.InvalidPassword);
            }

            (byte[] hash, byte[] salt, int iterations) = PasswordHasher.Hash(password);

            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO accounts (username, password_hash, password_salt, password_iterations)
                VALUES ($1, $2, $3, $4)
                ON CONFLICT (username) DO NOTHING
                RETURNING account_id
                """,
                connection)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = username },
                    new NpgsqlParameter { Value = hash },
                    new NpgsqlParameter { Value = salt },
                    new NpgsqlParameter { Value = iterations },
                },
            };

            object? result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is long accountId)
            {
                return new CreateAccountResult(CreateAccountOutcome.Created, accountId);
            }

            return CreateAccountResult.Failed(CreateAccountOutcome.UsernameTaken);
        }

        public async Task<AuthenticateResult> TryAuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                // $1::citext (pas juste "= $1") : sans le cast explicite, Npgsql envoie le paramètre en
                // "text" et Postgres résout l'opérateur côté text (sensible à la casse) plutôt que côté
                // citext, puisque citext a un cast implicite *vers* text. Vérifié en reproduisant le bug.
                "SELECT account_id, password_hash, password_salt, password_iterations FROM accounts WHERE username = $1::citext",
                connection)
            {
                Parameters = { new NpgsqlParameter { Value = username } },
            };

            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return AuthenticateResult.Failed(AuthenticateOutcome.UnknownUsername);
            }

            long accountId = reader.GetInt64(0);
            byte[] hash = (byte[])reader[1];
            byte[] salt = (byte[])reader[2];
            int iterations = reader.GetInt32(3);

            if (!PasswordHasher.Verify(password, hash, salt, iterations))
            {
                return AuthenticateResult.Failed(AuthenticateOutcome.WrongPassword);
            }

            return new AuthenticateResult(AuthenticateOutcome.Success, accountId);
        }
    }
}
