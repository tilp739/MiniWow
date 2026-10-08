using System.Security.Cryptography;
using System.Text;

namespace GameServer.Persistence.Accounts
{
    /// <summary>
    /// Hachage de mot de passe par PBKDF2 (intégré au BCL : aucune dépendance supplémentaire).
    /// Le sel et le nombre d'itérations sont stockés à côté du hachage, par compte.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;
        public const int CurrentIterations = 100_000;

        public static (byte[] Hash, byte[] Salt, int Iterations) Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] hash = Derive(password, salt, CurrentIterations);
            return (hash, salt, CurrentIterations);
        }

        public static bool Verify(string password, byte[] hash, byte[] salt, int iterations)
        {
            byte[] computed = Derive(password, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(computed, hash);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashSizeBytes);
        }
    }
}
