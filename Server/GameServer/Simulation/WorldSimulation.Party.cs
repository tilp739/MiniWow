using System.Collections.Generic;
using GameServer.Domain.Party;
using GameShared.Messages;
using GameShared.Protocol;

namespace GameServer.Simulation
{
    /// <summary>Groupes : invitations, acceptation, départ, exclusion. Les règles sont dans <see cref="PartyService"/>.</summary>
    public sealed partial class WorldSimulation
    {
        private void HandlePartyAction(IWorldClient client, PartyActionMessage action)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            uint me = slot.Player.EntityId;
            PartyService parties = _directory.Parties;

            switch (action.Kind)
            {
                case PartyActionKind.Invite:
                    // Les invitations ne traversent pas les mondes : on invite un joueur de la même zone.
                    if (FindPlayer(action.TargetEntityId) == null || !parties.Invite(me, action.TargetEntityId))
                    {
                        Reject(client, ErrorCode.PartyError, "Invitation impossible.");
                        return;
                    }

                    SendPartyTo(action.TargetEntityId);
                    break;

                case PartyActionKind.Accept:
                    if (!parties.Accept(me))
                    {
                        Reject(client, ErrorCode.PartyError, "Aucune invitation valable.");
                        return;
                    }

                    NotifyPartyChange(parties.PartyOf(me)?.Members ?? new List<uint> { me });
                    break;

                case PartyActionKind.Decline:
                    parties.Decline(me);
                    SendPartyTo(me);
                    break;

                case PartyActionKind.Leave:
                    NotifyPartyChange(parties.Leave(me));
                    SendPartyTo(me);
                    break;

                case PartyActionKind.Kick:
                    if (!parties.Kick(me, action.TargetEntityId))
                    {
                        Reject(client, ErrorCode.PartyError, "Seul le chef peut exclure un membre.");
                        return;
                    }

                    NotifyPartyChange(parties.PartyOf(me)?.Members ?? new List<uint> { me });
                    SendPartyTo(action.TargetEntityId);
                    break;
            }
        }

        /// <summary>Prévient les joueurs d'un groupe qu'il a changé. Les membres peuvent être dans n'importe quel monde.</summary>
        private void NotifyPartyChange(IReadOnlyList<uint> members)
        {
            foreach (uint member in members)
            {
                SendPartyTo(member);
            }
        }

        /// <summary>Envoie l'état du groupe à un joueur : ses membres, son chef et son invitation éventuelle.</summary>
        private void SendPartyTo(uint entityId)
        {
            if (!_directory.TryGetOnline(entityId, out IWorldClient? client, out _) || client == null)
            {
                return;
            }

            PartyService parties = _directory.Parties;
            Domain.Party.Party? party = parties.PartyOf(entityId);

            var members = new List<(uint EntityId, string Name)>();
            if (party != null)
            {
                foreach (uint memberId in party.Members)
                {
                    _directory.TryGetOnline(memberId, out _, out string memberName);
                    members.Add((memberId, memberName));
                }
            }

            uint inviter = parties.InviterOf(entityId);
            string inviterName = string.Empty;
            if (inviter != 0)
            {
                _directory.TryGetOnline(inviter, out _, out inviterName);
            }

            var message = new PartyMessage(party?.LeaderEntityId ?? 0u, members, inviter, inviterName);
            client.TrySend(OutboundFrame.Rent(Opcode.Party, message, _writer));
        }
    }
}
