namespace GameServer.Configuration
{
    /// <summary>
    /// Connexion Postgres pour la persistance des comptes/personnages. Le mot de passe n'est jamais lu
    /// d'ici : il vient de la variable d'environnement <c>MINIWOW_DB_PASSWORD</c> (même que docker-compose.yml).
    /// </summary>
    public sealed class DatabaseOptions
    {
        public string Host { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 5432;

        public string Database { get; set; } = "miniwow_dev";

        public string Username { get; set; } = "miniwow";
    }
}
