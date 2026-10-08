using System.Collections.Generic;

namespace GameServer.Domain.Party
{
    /// <summary>
    /// Groupe de joueurs. Invariants : au plus <see cref="MaxSize"/> membres, le chef est toujours membre,
    /// un groupe de moins de deux membres est dissous.
    /// </summary>
    public sealed class Party
    {
        public const int MaxSize = 5;

        private readonly List<uint> _members = new List<uint>();

        public Party(uint leaderEntityId)
        {
            LeaderEntityId = leaderEntityId;
            _members.Add(leaderEntityId);
        }

        public uint LeaderEntityId { get; private set; }

        public IReadOnlyList<uint> Members => _members;

        public bool Contains(uint entityId) => _members.Contains(entityId);

        public bool IsFull => _members.Count >= MaxSize;

        public bool TryAdd(uint entityId)
        {
            if (IsFull || Contains(entityId))
            {
                return false;
            }

            _members.Add(entityId);
            return true;
        }

        /// <summary>Retire un membre. Si c'était le chef, le plus ancien membre prend le relais.</summary>
        public bool Remove(uint entityId)
        {
            if (!_members.Remove(entityId))
            {
                return false;
            }

            if (entityId == LeaderEntityId && _members.Count > 0)
            {
                LeaderEntityId = _members[0];
            }

            return true;
        }

        /// <summary>Vrai si le groupe n'a plus assez de membres pour exister.</summary>
        public bool IsDisbanded => _members.Count < 2;
    }
}
