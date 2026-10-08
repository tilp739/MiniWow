using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Persistence.Accounts
{
    /// <summary>
    /// Implémentation réelle et simple, en mémoire : pas un double de test au sens d'un framework de mock.
    /// Utilisée par défaut dans les tests (<c>ServerHarness</c>) pour garder la suite rapide et sans Postgres.
    /// Thread-safe, mais perd tout à l'arrêt du process — jamais utilisée en production.
    /// </summary>
    public sealed class InMemoryAccountRepository : IAccountRepository
    {
        private sealed class StoredAccount
        {
            public long AccountId;
            public string Username = string.Empty;
            public byte[] Hash = System.Array.Empty<byte>();
            public byte[] Salt = System.Array.Empty<byte>();
            public int Iterations;
        }

        private readonly ConcurrentDictionary<string, StoredAccount> _byUsername =
            new ConcurrentDictionary<string, StoredAccount>(System.StringComparer.OrdinalIgnoreCase);

        private long _nextAccountId = 1;

        public Task<CreateAccountResult> CreateAccountAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (!AccountRules.IsValidUsername(username))
            {
                return Task.FromResult(CreateAccountResult.Failed(CreateAccountOutcome.InvalidUsername));
            }

            if (!AccountRules.IsValidPassword(password))
            {
                return Task.FromResult(CreateAccountResult.Failed(CreateAccountOutcome.InvalidPassword));
            }

            (byte[] hash, byte[] salt, int iterations) = PasswordHasher.Hash(password);
            var account = new StoredAccount
            {
                AccountId = Interlocked.Increment(ref _nextAccountId) - 1,
                Username = username,
                Hash = hash,
                Salt = salt,
                Iterations = iterations,
            };

            if (!_byUsername.TryAdd(username, account))
            {
                return Task.FromResult(CreateAccountResult.Failed(CreateAccountOutcome.UsernameTaken));
            }

            return Task.FromResult(new CreateAccountResult(CreateAccountOutcome.Created, account.AccountId));
        }

        public Task<AuthenticateResult> TryAuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (!_byUsername.TryGetValue(username, out StoredAccount? account))
            {
                return Task.FromResult(AuthenticateResult.Failed(AuthenticateOutcome.UnknownUsername));
            }

            if (!PasswordHasher.Verify(password, account.Hash, account.Salt, account.Iterations))
            {
                return Task.FromResult(AuthenticateResult.Failed(AuthenticateOutcome.WrongPassword));
            }

            return Task.FromResult(new AuthenticateResult(AuthenticateOutcome.Success, account.AccountId));
        }
    }
}
