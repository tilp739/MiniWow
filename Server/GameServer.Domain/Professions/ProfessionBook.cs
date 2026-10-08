using System;
using System.Collections.Generic;

namespace GameServer.Domain.Professions
{
    /// <summary>Métiers connus d'un joueur et leur compétence. Une compétence de zéro signifie « non appris ».</summary>
    public sealed class ProfessionBook
    {
        private readonly Dictionary<byte, int> _skills = new Dictionary<byte, int>();

        public IEnumerable<byte> LearnedIds => _skills.Keys;

        public bool IsLearned(byte professionId) => _skills.ContainsKey(professionId);

        public int SkillOf(byte professionId) => _skills.TryGetValue(professionId, out int skill) ? skill : 0;

        /// <summary>Apprend un métier (compétence 1). Ne fait rien s'il est déjà appris.</summary>
        public bool Learn(byte professionId)
        {
            if (IsLearned(professionId))
            {
                return false;
            }

            _skills[professionId] = 1;
            return true;
        }

        /// <summary>Augmente la compétence d'un métier déjà appris, sans dépasser <paramref name="maxSkill"/>.</summary>
        public void GainSkill(byte professionId, int amount, int maxSkill)
        {
            if (!IsLearned(professionId) || amount <= 0)
            {
                return;
            }

            _skills[professionId] = Math.Min(maxSkill, _skills[professionId] + amount);
        }

        /// <summary>Restaure un métier et sa compétence. Réservé au chargement d'un personnage sauvegardé.</summary>
        public void RestoreLearned(byte professionId, int skill)
        {
            _skills[professionId] = skill;
        }
    }
}
