using GameServer.Networking;
using GameServer.Persistence;
using GameServer.Persistence.Accounts;
using GameServer.Persistence.Characters;
using GameServer.Simulation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GameServer.Configuration
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>Enregistre le serveur de jeu : options validées, listener TCP, simulation et boucle de tick.</summary>
        public static IServiceCollection AddGameServer(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<GameServerOptions>()
                .Bind(configuration.GetSection(GameServerOptions.SectionName))
                .Validate(o => o.TickRate is >= 5 and <= 60, "GameServer:TickRate doit être compris entre 5 et 60.")
                .Validate(o => o.Port is >= 0 and <= 65535, "GameServer:Port doit être compris entre 0 et 65535.")
                .Validate(o => o.MaxConnections > 0, "GameServer:MaxConnections doit être positif.")
                .Validate(o => o.MaxPlayers is >= 1 and <= 2000, "GameServer:MaxPlayers doit être compris entre 1 et 2000.")
                .Validate(o => o.InterestRadius > 0f, "GameServer:InterestRadius doit être positif.")
                .Validate(o => o.InterestCellSize > 0f, "GameServer:InterestCellSize doit être positif.")
                .Validate(o => o.Movement.WalkSpeed > 0f, "GameServer:Movement:WalkSpeed doit être positif.")
                .ValidateOnStart();

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<GameServerOptions>>().Value);

            // Comptes et personnages : implémentations en mémoire par défaut (suffisant pour les tests,
            // sans dépendance à Postgres). En production, Program.cs les remplace par les implémentations
            // Npgsql via AddPostgresCharacterPersistence, appelé après AddGameServer.
            services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
            services.AddSingleton<ICharacterRepository, InMemoryCharacterRepository>();
            services.AddSingleton<CharacterSaveWorker>();
            services.AddSingleton<ICharacterSaveQueue>(sp => sp.GetRequiredService<CharacterSaveWorker>());
            services.AddHostedService(sp => sp.GetRequiredService<CharacterSaveWorker>());

            // Le registre crée le monde principal : les données sont validées au démarrage.
            services.AddSingleton<WorldDirectory>();
            services.AddSingleton<IWorldDirectory>(sp => sp.GetRequiredService<WorldDirectory>());

            services.AddSingleton<GameTcpListener>();
            services.AddHostedService(sp => sp.GetRequiredService<GameTcpListener>());
            services.AddHostedService<GameLoop>();

            return services;
        }
    }
}
