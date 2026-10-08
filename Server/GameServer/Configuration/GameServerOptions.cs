using System.Collections.Generic;
using GameServer.Domain.Combat;
using GameServer.Domain.Movement;

namespace GameServer.Configuration
{
    /// <summary>Configuration du serveur, lue dans la section <c>GameServer</c> (appsettings ou variables d'environnement).</summary>
    public sealed class GameServerOptions
    {
        public const string SectionName = "GameServer";

        /// <summary>Adresse d'écoute. Loopback par défaut : ne pas exposer le serveur sans décision explicite.</summary>
        public string BindAddress { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 7777;

        /// <summary>Ticks de simulation par seconde.</summary>
        public int TickRate { get; set; } = 20;

        public int MaxConnections { get; set; } = 200;

        /// <summary>Nombre maximal de joueurs dans le monde. Limité à 10 jusqu'aux benchmarks de M9.</summary>
        public int MaxPlayers { get; set; } = 10;

        /// <summary>Rayon, en unités, à l'intérieur duquel un joueur reçoit les autres.</summary>
        public float InterestRadius { get; set; } = 80f;

        /// <summary>Taille des cellules de la grille d'intérêt.</summary>
        public float InterestCellSize { get; set; } = 40f;

        /// <summary>Débit maximal de messages acceptés par connexion.</summary>
        public double MessagesPerSecond { get; set; } = 40;

        /// <summary>Rafale maximale de messages acceptés par connexion.</summary>
        public double MessageBurst { get; set; } = 60;

        /// <summary>Nombre d'anomalies tolérées avant fermeture de la connexion.</summary>
        public int MaxViolations { get; set; } = 200;

        /// <summary>Dossier des données (créatures, zones). Vide : le dossier <c>Content</c> à côté de l'exécutable.</summary>
        public string ContentDirectory { get; set; } = string.Empty;

        /// <summary>Zone chargée au démarrage : fichier <c>zones/{ZoneId}.json</c>.</summary>
        public string ZoneId { get; set; } = "greenhaven";

        /// <summary>Graine du générateur aléatoire de combat. Vide : aléatoire. Fixée en tests pour la reproductibilité.</summary>
        public int? RandomSeed { get; set; }

        /// <summary>Mots masqués dans le chat (insensible à la casse). Vide par défaut.</summary>
        public List<string> ChatBannedWords { get; set; } = new List<string>();

        public MovementSettings Movement { get; set; } = new MovementSettings();

        public CombatSettings Combat { get; set; } = new CombatSettings();

        public PlayerCombatSettings PlayerCombat { get; set; } = new PlayerCombatSettings();

        public DatabaseOptions Database { get; set; } = new DatabaseOptions();
    }
}
