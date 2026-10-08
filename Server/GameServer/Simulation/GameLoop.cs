using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>
    /// Boucle de tick à cadence fixe. Fait avancer tous les mondes (principal et instances) sur ce seul thread,
    /// exécute les actions d'administration en attente, et publie les métriques de tick.
    /// </summary>
    public sealed class GameLoop : BackgroundService
    {
        private const double StatsIntervalSeconds = 5d;

        private readonly IWorldDirectory _directory;
        private readonly GameServerOptions _options;
        private readonly ILogger<GameLoop> _logger;

        public GameLoop(IWorldDirectory directory, GameServerOptions options, ILogger<GameLoop> logger)
        {
            _directory = directory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TimeSpan period = TimeSpan.FromSeconds(1d / _options.TickRate);
            float deltaSeconds = (float)period.TotalSeconds;

            using var timer = new PeriodicTimer(period);
            _logger.LogInformation("GameLoopStarted {TickRate}", _options.TickRate);

            double totalMs = 0;
            double maxMs = 0;
            int ticks = 0;
            long statsStart = Stopwatch.GetTimestamp();

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                long start = Stopwatch.GetTimestamp();

                _directory.DrainAdminActions();

                // Copie : une instance peut être supprimée pendant le tick.
                foreach (WorldSimulation world in _directory.Worlds.ToArray())
                {
                    world.Tick(deltaSeconds);
                }

                double tickMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                totalMs += tickMs;
                maxMs = Math.Max(maxMs, tickMs);
                ticks++;

                if (tickMs > period.TotalMilliseconds)
                {
                    _logger.LogWarning("TickOverrun {DurationMs} {BudgetMs}", tickMs, period.TotalMilliseconds);
                }

                if (Stopwatch.GetElapsedTime(statsStart).TotalSeconds >= StatsIntervalSeconds)
                {
                    _logger.LogInformation(
                        "TickStats {AverageMs} {MaxMs} {Ticks} {Players} {Worlds}",
                        Math.Round(totalMs / ticks, 3),
                        Math.Round(maxMs, 3),
                        ticks,
                        _directory.Worlds.Sum(w => w.CountPlayers()),
                        _directory.Worlds.Count);

                    totalMs = 0;
                    maxMs = 0;
                    ticks = 0;
                    statsStart = Stopwatch.GetTimestamp();
                }
            }
        }
    }
}
