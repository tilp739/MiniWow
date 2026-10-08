using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Configuration;
using GameServer.Persistence.Accounts;
using GameServer.Persistence.Characters;
using GameServer.Simulation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameServer.Networking
{
    /// <summary>Accepte les connexions TCP et lance une <see cref="ClientSession"/> par client.</summary>
    public sealed class GameTcpListener : BackgroundService
    {
        private readonly GameServerOptions _options;
        private readonly IWorldDirectory _directory;
        private readonly IAccountRepository _accounts;
        private readonly ICharacterRepository _characters;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<GameTcpListener> _logger;
        private readonly TimeProvider _time = TimeProvider.System;

        private TcpListener? _listener;
        private int _activeConnections;
        private int _lastClientId;

        public GameTcpListener(
            GameServerOptions options,
            IWorldDirectory directory,
            IAccountRepository accounts,
            ICharacterRepository characters,
            ILoggerFactory loggerFactory,
            ILogger<GameTcpListener> logger)
        {
            _options = options;
            _directory = directory;
            _accounts = accounts;
            _characters = characters;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        /// <summary>Adresse réellement écoutée. Utile lorsque le port configuré est 0 (tests).</summary>
        public IPEndPoint? LocalEndpoint => _listener?.LocalEndpoint as IPEndPoint;

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // Le listener est ouvert ici, de façon synchrone, pour que LocalEndpoint soit disponible dès le démarrage.
            _listener = new TcpListener(IPAddress.Parse(_options.BindAddress), _options.Port);
            _listener.Start();
            _logger.LogInformation("GameServerListening {Address}", LocalEndpoint);

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient tcp;
                try
                {
                    tcp = await _listener!.AcceptTcpClientAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (Interlocked.Increment(ref _activeConnections) > _options.MaxConnections)
                {
                    Interlocked.Decrement(ref _activeConnections);
                    _logger.LogWarning("ConnectionRefusedMaxConnections {Max}", _options.MaxConnections);
                    tcp.Dispose();
                    continue;
                }

                // Chaque session gère ses propres erreurs ; rien ne doit remonter jusqu'ici.
                _ = RunSessionAsync(tcp, stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _listener?.Stop();
            return base.StopAsync(cancellationToken);
        }

        private async Task RunSessionAsync(TcpClient tcp, CancellationToken stoppingToken)
        {
            int clientId = Interlocked.Increment(ref _lastClientId);
            var session = new ClientSession(
                clientId,
                tcp,
                _directory,
                _options,
                _accounts,
                _characters,
                _time,
                _loggerFactory.CreateLogger<ClientSession>());

            try
            {
                await session.RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SessionCrashed {ClientId}", clientId);
            }
            finally
            {
                Interlocked.Decrement(ref _activeConnections);
            }
        }
    }
}
