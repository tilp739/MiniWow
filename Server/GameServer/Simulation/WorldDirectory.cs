using System;
using System.Collections.Generic;
using System.IO;
using GameServer.Configuration;
using GameServer.Content;
using GameServer.Domain.Chat;
using GameServer.Domain.Party;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>Accès aux mondes du serveur, identifiants globaux et joueurs en ligne.</summary>
    public interface IWorldDirectory
    {
        WorldSimulation Main { get; }

        IReadOnlyList<WorldSimulation> Worlds { get; }

        /// <summary>Groupes et invitations, partagés entre tous les mondes.</summary>
        PartyService Parties { get; }

        /// <summary>Identifiant d'entité unique sur tout le serveur (les mondes ne se chevauchent jamais).</summary>
        uint AllocateEntityId();

        /// <summary>Crée une instance fraîche d'une zone (donjon). Ticked par la boucle de jeu jusqu'à sa suppression.</summary>
        WorldSimulation CreateInstance(string zoneId);

        void RemoveInstance(WorldSimulation instance);

        /// <summary>Enregistre un joueur en ligne, pour pouvoir lui écrire même depuis un autre monde (groupes).</summary>
        void RegisterOnline(uint entityId, IWorldClient client, string name);

        void UnregisterOnline(uint entityId);

        bool TryGetOnline(uint entityId, out IWorldClient? client, out string name);

        /// <summary>Recherche un joueur en ligne par son nom (insensible à la casse), pour les chuchotements.</summary>
        bool TryFindOnlineByName(string name, out uint entityId, out IWorldClient? client);

        ChatFilter Chat { get; }

        /// <summary>File une action d'administration, exécutée sur le thread de simulation au prochain tick.</summary>
        void PostAdminAction(Action<IWorldDirectory> action);

        /// <summary>Exécute les actions d'administration en attente. Appelé par la boucle de jeu.</summary>
        void DrainAdminActions();
    }

    /// <summary>
    /// Registre des mondes. Tous les mondes sont ticked sur le même thread : un joueur peut donc être transféré
    /// d'un monde à l'autre directement, sans synchronisation supplémentaire.
    /// </summary>
    public sealed class WorldDirectory : IWorldDirectory
    {
        private readonly GameServerOptions _options;
        private readonly ILoggerFactory _loggers;
        private readonly ICharacterSaveQueue _saveQueue;
        private readonly string _contentDirectory;
        private readonly Dictionary<string, WorldContent> _contentCache = new Dictionary<string, WorldContent>();
        private readonly List<WorldSimulation> _worlds = new List<WorldSimulation>();
        private readonly Dictionary<uint, (IWorldClient Client, string Name)> _online = new Dictionary<uint, (IWorldClient Client, string Name)>();
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action<IWorldDirectory>> _adminActions = new System.Collections.Concurrent.ConcurrentQueue<Action<IWorldDirectory>>();
        private uint _nextEntityId = 1;

        public WorldDirectory(GameServerOptions options, ILoggerFactory loggers, ICharacterSaveQueue saveQueue)
        {
            _options = options;
            _loggers = loggers;
            _saveQueue = saveQueue;
            Chat = new ChatFilter(options.ChatBannedWords);
            _contentDirectory = string.IsNullOrWhiteSpace(options.ContentDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Content")
                : options.ContentDirectory;

            // Le monde principal est créé au démarrage : les données invalides arrêtent le serveur immédiatement.
            Main = Create(options.ZoneId, isInstance: false);
        }

        public WorldSimulation Main { get; }

        public IReadOnlyList<WorldSimulation> Worlds => _worlds;

        public PartyService Parties { get; } = new PartyService();

        public ChatFilter Chat { get; }

        public void PostAdminAction(Action<IWorldDirectory> action)
        {
            _adminActions.Enqueue(action);
        }

        public void DrainAdminActions()
        {
            while (_adminActions.TryDequeue(out Action<IWorldDirectory>? action))
            {
                action(this);
            }
        }

        public bool TryFindOnlineByName(string name, out uint entityId, out IWorldClient? client)
        {
            foreach (KeyValuePair<uint, (IWorldClient Client, string Name)> entry in _online)
            {
                if (string.Equals(entry.Value.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    entityId = entry.Key;
                    client = entry.Value.Client;
                    return true;
                }
            }

            entityId = 0;
            client = null;
            return false;
        }

        public uint AllocateEntityId() => _nextEntityId++;

        public WorldSimulation CreateInstance(string zoneId) => Create(zoneId, isInstance: true);

        public void RemoveInstance(WorldSimulation instance)
        {
            if (!ReferenceEquals(instance, Main))
            {
                _worlds.Remove(instance);
            }
        }

        public void RegisterOnline(uint entityId, IWorldClient client, string name)
        {
            _online[entityId] = (client, name);
        }

        public void UnregisterOnline(uint entityId)
        {
            _online.Remove(entityId);
        }

        public bool TryGetOnline(uint entityId, out IWorldClient? client, out string name)
        {
            if (_online.TryGetValue(entityId, out (IWorldClient Client, string Name) entry))
            {
                client = entry.Client;
                name = entry.Name;
                return true;
            }

            client = null;
            name = string.Empty;
            return false;
        }

        private WorldSimulation Create(string zoneId, bool isInstance)
        {
            WorldContent content = LoadContent(zoneId);
            var world = new WorldSimulation(_options, content, _loggers.CreateLogger<WorldSimulation>(), this, _saveQueue, isInstance);
            _worlds.Add(world);
            return world;
        }

        private WorldContent LoadContent(string zoneId)
        {
            if (!_contentCache.TryGetValue(zoneId, out WorldContent? content))
            {
                content = ContentLoader.Load(_contentDirectory, zoneId);
                _contentCache.Add(zoneId, content);
            }

            return content;
        }
    }
}
