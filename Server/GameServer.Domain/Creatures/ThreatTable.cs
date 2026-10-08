using System;
using System.Collections.Generic;

namespace GameServer.Domain.Creatures
{
    /// <summary>
    /// Menace accumulée par joueur pour une créature. La cible est le joueur le plus menaçant encore éligible.
    /// </summary>
    public sealed class ThreatTable
    {
        private readonly Dictionary<uint, float> _threat = new Dictionary<uint, float>();

        public bool IsEmpty => _threat.Count == 0;

        /// <summary>Joueurs ayant généré de la menace, pour la répartition de l'expérience.</summary>
        public IEnumerable<uint> Sources => _threat.Keys;

        public void Add(uint playerId, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _threat.TryGetValue(playerId, out float current);
            _threat[playerId] = current + amount;
        }

        public float Get(uint playerId)
        {
            return _threat.TryGetValue(playerId, out float value) ? value : 0f;
        }

        /// <summary>Retourne l'identifiant le plus menaçant parmi ceux acceptés par <paramref name="isEligible"/>.</summary>
        public bool TryGetTop(Func<uint, bool> isEligible, out uint playerId)
        {
            playerId = 0;
            float best = float.MinValue;
            bool found = false;

            foreach (KeyValuePair<uint, float> entry in _threat)
            {
                if (entry.Value > best && isEligible(entry.Key))
                {
                    best = entry.Value;
                    playerId = entry.Key;
                    found = true;
                }
            }

            return found;
        }

        public void Clear()
        {
            _threat.Clear();
        }
    }
}
