using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Persistence.Accounts
{
    public enum CreateAccountOutcome
    {
        Created,
        UsernameTaken,
        InvalidUsername,
        InvalidPassword,
    }

    public readonly struct CreateAccountResult
    {
        public CreateAccountResult(CreateAccountOutcome outcome, long accountId)
        {
            Outcome = outcome;
            AccountId = accountId;
        }

        public CreateAccountOutcome Outcome { get; }

        public long AccountId { get; }

        public static CreateAccountResult Failed(CreateAccountOutcome outcome) => new CreateAccountResult(outcome, 0);
    }

    public enum AuthenticateOutcome
    {
        Success,
        UnknownUsername,
        WrongPassword,
    }

    public readonly struct AuthenticateResult
    {
        public AuthenticateResult(AuthenticateOutcome outcome, long accountId)
        {
            Outcome = outcome;
            AccountId = accountId;
        }

        public AuthenticateOutcome Outcome { get; }

        public long AccountId { get; }

        public static AuthenticateResult Failed(AuthenticateOutcome outcome) => new AuthenticateResult(outcome, 0);
    }

    /// <summary>Comptes joueurs : création et authentification. Aucune opération ne touche le thread de simulation.</summary>
    public interface IAccountRepository
    {
        Task<CreateAccountResult> CreateAccountAsync(string username, string password, CancellationToken cancellationToken = default);

        Task<AuthenticateResult> TryAuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    }
}
