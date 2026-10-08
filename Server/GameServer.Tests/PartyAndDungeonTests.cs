using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Domain.Party;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    public sealed class PartyRulesTests
    {
        [Fact]
        public void Accept_CreatesPartyWithInviterAsLeader()
        {
            var parties = new PartyService();
            parties.Invite(1, 2);

            Assert.True(parties.Accept(2));

            Assert.Equal(1u, parties.PartyOf(2)!.LeaderEntityId);
            Assert.Equal(2, parties.PartyOf(1)!.Members.Count);
        }

        [Fact]
        public void Invite_IsRefused_WhenPartyIsFull()
        {
            var parties = new PartyService();
            for (uint invitee = 2; invitee <= Party.MaxSize; invitee++)
            {
                parties.Invite(1, invitee);
                parties.Accept(invitee);
            }

            Assert.False(parties.Invite(1, 99));
        }

        [Fact]
        public void Leave_PromotesAnotherMember_WhenLeaderLeaves()
        {
            var parties = new PartyService();
            parties.Invite(1, 2);
            parties.Accept(2);
            parties.Invite(1, 3);
            parties.Accept(3);

            parties.Leave(1);

            Assert.Equal(2u, parties.PartyOf(3)!.LeaderEntityId);
        }

        [Fact]
        public void Leave_DisbandsParty_WhenOnlyOneMemberRemains()
        {
            var parties = new PartyService();
            parties.Invite(1, 2);
            parties.Accept(2);

            parties.Leave(2);

            Assert.Null(parties.PartyOf(1));
        }

        [Fact]
        public void Kick_RequiresLeader()
        {
            var parties = new PartyService();
            parties.Invite(1, 2);
            parties.Accept(2);
            parties.Invite(1, 3);
            parties.Accept(3);

            Assert.False(parties.Kick(2, 3));
            Assert.True(parties.Kick(1, 3));
            Assert.Null(parties.PartyOf(3));
        }
    }

    /// <summary>Groupe et donjon de bout en bout, sur la zone "combat" qui contient le PNJ portail.</summary>
    public sealed class PartyAndDungeonIntegrationTests
    {
        private const ushort GatekeeperNpcId = 3;
        private const int TemplateWarden = 3;
        private const int TemplateWolf = 1;

        private static readonly Dictionary<string, string?> CombatZone = new Dictionary<string, string?>
        {
            ["GameServer:ZoneId"] = "combat",
        };

        private static CancellationToken Timeout(int seconds = 20) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task Invitation_IsAccepted_AndBothMembersSeeTheParty()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient leader = await JoinAsync(server, "chef", ct);
            await using TestClient guest = await JoinAsync(server, "invite", ct);
            WorldSnapshotMessage leaderView = await leader.WaitForSnapshotAsync(s => s.Entities.Count >= 2, ct);
            WorldSnapshotMessage guestView = await guest.WaitForSnapshotAsync(s => s.Entities.Count >= 2, ct);

            await leader.SendAsync(Opcode.PartyAction, new PartyActionMessage(PartyActionKind.Invite, guestView.YourEntityId), ct);
            PartyMessage invitation = await WaitForPartyAsync(guest, p => p.InviterEntityId == leaderView.YourEntityId, ct);
            Assert.Equal(leaderView.YourEntityId, invitation.InviterEntityId);

            await guest.SendAsync(Opcode.PartyAction, new PartyActionMessage(PartyActionKind.Accept, 0), ct);
            PartyMessage forLeader = await WaitForPartyAsync(leader, p => p.Members.Count == 2, ct);

            Assert.Equal(leaderView.YourEntityId, forLeader.LeaderEntityId);
        }

        [Fact]
        public async Task Entering_TheCrypt_MovesPlayerIntoInstanceWithBoss()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout(25);
            await using TestClient client = await JoinAsync(server, "explorateur", ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindNpc(s, GatekeeperNpcId) != null, ct);
            uint gate = FindNpc(view, GatekeeperNpcId)!.Value.EntityId;

            await client.SendAsync(Opcode.DungeonAction, new DungeonActionMessage(DungeonActionKind.Enter, gate), ct);

            // Dans la crypte : le boss est visible, et les loups de la zone de combat ont disparu.
            WorldSnapshotMessage inside = await client.WaitForSnapshotAsync(
                s => s.Entities.Any(e => e.Kind == EntityKind.Creature && e.TemplateId == TemplateWarden),
                ct);
            Assert.DoesNotContain(inside.Entities, e => e.Kind == EntityKind.Creature && e.TemplateId == TemplateWolf);
        }

        [Fact]
        public async Task Boss_SendsTelegraph_BeforeItsShockwave()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout(30);
            await using TestClient client = await JoinAsync(server, "cible", ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindNpc(s, GatekeeperNpcId) != null, ct);
            uint gate = FindNpc(view, GatekeeperNpcId)!.Value.EntityId;
            await client.SendAsync(Opcode.DungeonAction, new DungeonActionMessage(DungeonActionKind.Enter, gate), ct);
            WorldSnapshotMessage inside = await client.WaitForSnapshotAsync(
                s => s.Entities.Any(e => e.Kind == EntityKind.Creature && e.TemplateId == TemplateWarden),
                ct);
            uint boss = inside.Entities.First(e => e.Kind == EntityKind.Creature && e.TemplateId == TemplateWarden).EntityId;

            // Le boss ne s'anime qu'au contact : on le provoque d'un coup.
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(boss), ct);

            CombatEventMessage telegraph = await client.WaitForCombatEventAsync(
                e => e.Kind == CombatEventKind.Telegraph && e.SourceEntityId == boss, ct);
            Assert.True(telegraph.Amount > 0);
        }

        private static async Task<TestClient> JoinAsync(ServerHarness server, string name, CancellationToken ct)
        {
            var client = new TestClient(server.Endpoint);
            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, name), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            await client.SendAsync(Opcode.EnterWorld, new EnterWorldMessage(2), ct);
            return client;
        }

        private static async Task<PartyMessage> WaitForPartyAsync(TestClient client, Func<PartyMessage, bool> predicate, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode != Opcode.Party)
                {
                    continue;
                }

                PartyMessage party = PartyMessage.Read(payload);
                if (predicate(party))
                {
                    return party;
                }
            }
        }

        private static EntityState? FindNpc(WorldSnapshotMessage snapshot, ushort npcId)
        {
            foreach (EntityState entity in snapshot.Entities)
            {
                if (entity.Kind == EntityKind.Npc && entity.TemplateId == npcId)
                {
                    return entity;
                }
            }

            return null;
        }
    }
}
