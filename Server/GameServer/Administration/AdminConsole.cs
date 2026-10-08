using System;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Simulation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameServer.Administration
{
    /// <summary>
    /// Console d'administration, lue sur l'entrée standard du serveur : seul l'opérateur local peut la piloter.
    /// Aucune commande n'est accessible depuis le réseau. Les actions sont exécutées sur le thread de simulation.
    /// Commandes : help, info, give &lt;joueur&gt; &lt;clé-objet&gt; &lt;quantité&gt;.
    /// </summary>
    public sealed class AdminConsole : BackgroundService
    {
        private readonly IWorldDirectory _directory;
        private readonly ILogger<AdminConsole> _logger;

        public AdminConsole(IWorldDirectory directory, ILogger<AdminConsole> logger)
        {
            _directory = directory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AdminConsoleReady (tapez 'help')");

            while (!stoppingToken.IsCancellationRequested)
            {
                string? line = await Console.In.ReadLineAsync(stoppingToken);
                if (line == null)
                {
                    // Entrée fermée (service sans terminal) : la console se désactive, le serveur continue.
                    return;
                }

                Execute(line.Trim());
            }
        }

        private void Execute(string line)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            switch (parts[0].ToLowerInvariant())
            {
                case "help":
                    Console.WriteLine("Commandes : help | info | give <joueur> <clé-objet> <quantité>");
                    break;

                case "info":
                    _directory.PostAdminAction(d =>
                    {
                        int players = 0;
                        foreach (WorldSimulation world in d.Worlds)
                        {
                            players += world.CountPlayers();
                        }

                        _logger.LogInformation("AdminInfo {Worlds} {Players}", d.Worlds.Count, players);
                    });
                    break;

                case "give" when parts.Length == 4 && int.TryParse(parts[3], out int quantity):
                    string player = parts[1];
                    string itemKey = parts[2];
                    _directory.PostAdminAction(d =>
                    {
                        bool done = d.Main.AdminGiveItem(player, itemKey, quantity);
                        _logger.LogInformation("AdminGive {Player} {Item} {Quantity} {Done}", player, itemKey, quantity, done);
                    });
                    break;

                default:
                    Console.WriteLine("Commande inconnue. Tapez 'help'.");
                    break;
            }
        }
    }
}
