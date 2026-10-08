using System;
using System.Collections.Generic;

namespace GameServer.Domain.Quests
{
    /// <summary>
    /// Journal de quêtes d'un joueur : quêtes actives avec leur progression, et quêtes terminées.
    /// Une quête terminée ne peut pas être reprise.
    /// </summary>
    public sealed class QuestLog
    {
        private readonly Dictionary<ushort, int[]> _active = new Dictionary<ushort, int[]>();
        private readonly HashSet<ushort> _completed = new HashSet<ushort>();

        public IEnumerable<ushort> ActiveIds => _active.Keys;

        public IEnumerable<ushort> CompletedIds => _completed;

        public bool IsActive(ushort questId) => _active.ContainsKey(questId);

        public bool IsCompleted(ushort questId) => _completed.Contains(questId);

        public bool IsAvailable(ushort questId) => !IsActive(questId) && !IsCompleted(questId);

        /// <summary>Progression courante d'un objectif, ou 0 si la quête n'est pas active.</summary>
        public int Progress(ushort questId, int objectiveIndex)
        {
            return _active.TryGetValue(questId, out int[]? progress) ? progress[objectiveIndex] : 0;
        }

        public bool Accept(QuestDefinition quest)
        {
            if (!IsAvailable(quest.Id))
            {
                return false;
            }

            _active.Add(quest.Id, new int[quest.Objectives.Count]);
            return true;
        }

        /// <summary>Ajoute de la progression à un objectif, sans dépasser le nombre requis.</summary>
        public bool Advance(QuestDefinition quest, int objectiveIndex, int amount)
        {
            if (!_active.TryGetValue(quest.Id, out int[]? progress))
            {
                return false;
            }

            int required = quest.Objectives[objectiveIndex].Count;
            int next = Math.Min(required, progress[objectiveIndex] + amount);
            if (next == progress[objectiveIndex])
            {
                return false;
            }

            progress[objectiveIndex] = next;
            return true;
        }

        /// <summary>Fixe la progression d'un objectif à une valeur absolue (Collect, qui dépend du sac).</summary>
        public void SetProgress(ushort questId, int objectiveIndex, int value)
        {
            if (_active.TryGetValue(questId, out int[]? progress))
            {
                progress[objectiveIndex] = value;
            }
        }

        /// <summary>Vrai si chaque objectif a atteint son nombre requis.</summary>
        public bool IsSatisfied(QuestDefinition quest)
        {
            if (!_active.TryGetValue(quest.Id, out int[]? progress))
            {
                return false;
            }

            for (int i = 0; i < quest.Objectives.Count; i++)
            {
                if (progress[i] < quest.Objectives[i].Count)
                {
                    return false;
                }
            }

            return true;
        }

        public void Complete(ushort questId)
        {
            _active.Remove(questId);
            _completed.Add(questId);
        }

        /// <summary>Restaure une quête active avec sa progression. Réservé au chargement d'un personnage sauvegardé.</summary>
        public void RestoreActive(ushort questId, int[] progress)
        {
            _active[questId] = progress;
        }

        /// <summary>Restaure une quête terminée. Réservé au chargement d'un personnage sauvegardé.</summary>
        public void RestoreCompleted(ushort questId)
        {
            _completed.Add(questId);
        }
    }
}
