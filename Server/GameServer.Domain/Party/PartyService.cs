using System.Collections.Generic;

namespace GameServer.Domain.Party
{
    /// <summary>
    /// Groupes, invitations et appartenance, partagés par tous les mondes. Pas d'I/O : l'appelant envoie les mises à jour.
    /// Non thread-safe : utilisé uniquement depuis la boucle de jeu.
    /// </summary>
    public sealed class PartyService
    {
        private readonly Dictionary<uint, Party> _partyOf = new Dictionary<uint, Party>();
        private readonly Dictionary<uint, uint> _invitations = new Dictionary<uint, uint>();

        public Party? PartyOf(uint entityId) => _partyOf.TryGetValue(entityId, out Party? party) ? party : null;

        /// <summary>Invitation en attente d'un joueur : identifiant de l'invitant, ou 0 si aucune.</summary>
        public uint InviterOf(uint entityId) => _invitations.TryGetValue(entityId, out uint inviter) ? inviter : 0u;

        /// <summary>Crée une invitation. Refusé si le joueur est déjà dans un groupe ou si le groupe de l'invitant est plein.</summary>
        public bool Invite(uint inviter, uint invitee)
        {
            if (inviter == invitee || PartyOf(invitee) != null)
            {
                return false;
            }

            Party? party = PartyOf(inviter);
            if (party != null && party.IsFull)
            {
                return false;
            }

            _invitations[invitee] = inviter;
            return true;
        }

        /// <summary>Accepte l'invitation : crée le groupe si l'invitant n'en a pas encore.</summary>
        public bool Accept(uint invitee)
        {
            if (!_invitations.TryGetValue(invitee, out uint inviter))
            {
                return false;
            }

            _invitations.Remove(invitee);

            Party? party = PartyOf(inviter);
            if (party == null)
            {
                party = new Party(inviter);
                _partyOf[inviter] = party;
            }

            if (!party.TryAdd(invitee))
            {
                return false;
            }

            _partyOf[invitee] = party;
            return true;
        }

        public void Decline(uint invitee)
        {
            _invitations.Remove(invitee);
        }

        /// <summary>Quitte le groupe. Un groupe trop petit est dissous. Retourne les membres restants à prévenir.</summary>
        public IReadOnlyList<uint> Leave(uint entityId)
        {
            Party? party = PartyOf(entityId);
            if (party == null)
            {
                return new List<uint>();
            }

            _partyOf.Remove(entityId);
            party.Remove(entityId);

            var remaining = new List<uint>(party.Members);
            if (party.IsDisbanded)
            {
                foreach (uint member in remaining)
                {
                    _partyOf.Remove(member);
                }

                remaining.Clear();
            }

            return remaining;
        }

        /// <summary>Exclut un membre. Seul le chef peut exclure. Retourne false si l'action n'est pas permise.</summary>
        public bool Kick(uint leader, uint target)
        {
            Party? party = PartyOf(leader);
            if (party == null || party.LeaderEntityId != leader || leader == target || !party.Contains(target))
            {
                return false;
            }

            Leave(target);
            return true;
        }

        /// <summary>Retire un joueur déconnecté de son groupe et de ses invitations.</summary>
        public IReadOnlyList<uint> Forget(uint entityId)
        {
            _invitations.Remove(entityId);
            return Leave(entityId);
        }
    }
}
