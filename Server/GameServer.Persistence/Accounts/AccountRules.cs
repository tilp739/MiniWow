using System.Linq;

namespace GameServer.Persistence.Accounts
{
    /// <summary>Règles de validation partagées par toutes les implémentations de <see cref="IAccountRepository"/>.</summary>
    public static class AccountRules
    {
        public const int MinUsernameLength = 3;
        public const int MaxUsernameLength = 20;
        public const int MinPasswordLength = 6;
        public const int MaxPasswordLength = 72;

        public static bool IsValidUsername(string username) =>
            username.Length >= MinUsernameLength
            && username.Length <= MaxUsernameLength
            && username.All(char.IsLetterOrDigit);

        public static bool IsValidPassword(string password) =>
            password.Length >= MinPasswordLength && password.Length <= MaxPasswordLength;
    }
}
