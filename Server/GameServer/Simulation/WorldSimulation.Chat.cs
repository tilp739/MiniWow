using GameServer.Domain;
using GameShared.Messages;
using GameShared.Protocol;

namespace GameServer.Simulation
{
    /// <summary>Chat : local (zone), privé (joueur par son nom), groupe. Filtre et longueur validés ici.</summary>
    public sealed partial class WorldSimulation
    {
        /// <summary>Rayon de la conversation locale.</summary>
        private const float SayRange = 30f;

        private void HandleChat(IWorldClient client, ChatSendMessage request)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            Player sender = slot.Player;
            string? text = _directory.Chat.Apply(request.Text);
            if (text == null || text.Length == 0)
            {
                Reject(client, ErrorCode.ChatRejected, "Message refusé.");
                return;
            }

            var line = new ChatLineMessage(request.Channel, sender.Name, text);
            switch (request.Channel)
            {
                case ChatChannel.Say:
                    foreach (PlayerSlot listener in _players.Values)
                    {
                        if (DistanceXZ(sender.X, sender.Z, listener.Player.X, listener.Player.Z) <= SayRange)
                        {
                            SendChatLine(listener.Client, line);
                        }
                    }
                    break;

                case ChatChannel.Whisper:
                    if (!_directory.TryFindOnlineByName(request.Target, out _, out IWorldClient? target) || target == null)
                    {
                        Reject(client, ErrorCode.ChatRejected, "Joueur introuvable.");
                        return;
                    }

                    SendChatLine(target, line);
                    SendChatLine(client, line);
                    break;

                case ChatChannel.Party:
                    Domain.Party.Party? party = _directory.Parties.PartyOf(sender.EntityId);
                    if (party == null)
                    {
                        Reject(client, ErrorCode.PartyError, "Vous n'êtes pas dans un groupe.");
                        return;
                    }

                    foreach (uint memberId in party.Members)
                    {
                        if (_directory.TryGetOnline(memberId, out IWorldClient? member, out _) && member != null)
                        {
                            SendChatLine(member, line);
                        }
                    }
                    break;
            }
        }

        private void SendChatLine(IWorldClient client, ChatLineMessage line)
        {
            client.TrySend(OutboundFrame.Rent(Opcode.Chat, line, _writer));
        }
    }
}
