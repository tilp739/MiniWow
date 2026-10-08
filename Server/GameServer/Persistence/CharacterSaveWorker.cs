using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using GameServer.Persistence.Characters;
using GameServer.Persistence.Model;
using GameServer.Simulation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameServer.Persistence
{
    /// <summary>
    /// Seul endroit qui écrit réellement en base pour la sauvegarde de personnage. Le thread de simulation
    /// dépose des snapshots (copie pure, pas d'I/O) dans un canal ; ce worker les écrit de façon asynchrone,
    /// à l'image du <c>Channel&lt;OutboundFrame&gt;</c> déjà utilisé par <c>ClientSession</c> pour l'envoi réseau.
    /// </summary>
    public sealed class CharacterSaveWorker : BackgroundService, ICharacterSaveQueue
    {
        private const int QueueCapacity = 256;

        private readonly Channel<CharacterSnapshot> _channel =
            Channel.CreateBounded<CharacterSnapshot>(new BoundedChannelOptions(QueueCapacity) { SingleReader = true });

        private readonly ICharacterRepository _repository;
        private readonly ILogger<CharacterSaveWorker> _logger;

        public CharacterSaveWorker(ICharacterRepository repository, ILogger<CharacterSaveWorker> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public bool TryEnqueue(CharacterSnapshot snapshot) => _channel.Writer.TryWrite(snapshot);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (CharacterSnapshot snapshot in _channel.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await _repository.SaveCharacterAsync(snapshot, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "CharacterSaveFailed {CharacterId}", snapshot.CharacterId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Arrêt normal du serveur.
            }
        }
    }
}
