using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    /// <summary>Combat de bout en bout sur la zone "combat" : attaques, riposte, mort et respawn.</summary>
    public sealed class CombatIntegrationTests
    {
        // Zone "combat" : loup de test à (2.5, 0), brute à (-2.5, 0), loup lointain à (50, 0).
        private const float NearWolfX = 2.5f;
        private const float BruteX = -2.5f;
        private const float FarWolfX = 50f;
        private const int TemplateWolf = 1;
        private const int TemplateBrute = 2;

        private static readonly Dictionary<string, string?> CombatZone = new Dictionary<string, string?>
        {
            ["GameServer:ZoneId"] = "combat",
            ["GameServer:PlayerCombat:RespawnSeconds"] = "1",
        };

        private static CancellationToken Timeout(int seconds = 20) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task Attack_KillsWolf_AfterEnoughHits()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindCreature(s, NearWolfX, TemplateWolf) != null, ct);
            uint wolfId = FindCreature(view, NearWolfX, TemplateWolf)!.Value.EntityId;

            // Le loup a 20 PV et chaque coup inflige au moins 8 : trois coups suffisent.
            for (int i = 0; i < 3; i++)
            {
                await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);
                await Task.Delay(1100, ct);
            }

            CombatEventMessage death = await client.WaitForCombatEventAsync(
                e => e.Kind == CombatEventKind.Death && e.TargetEntityId == wolfId, ct);
            Assert.Equal(wolfId, death.TargetEntityId);
        }

        [Fact]
        public async Task Attack_OnCooldown_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindCreature(s, NearWolfX, TemplateWolf) != null, ct);
            uint wolfId = FindCreature(view, NearWolfX, TemplateWolf)!.Value.EntityId;

            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.OnCooldown, error.Code);
        }

        [Fact]
        public async Task Attack_OutOfRange_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindCreature(s, FarWolfX, TemplateWolf) != null, ct);
            uint farWolfId = FindCreature(view, FarWolfX, TemplateWolf)!.Value.EntityId;

            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(farWolfId), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.OutOfRange, error.Code);
        }

        [Fact]
        public async Task Attack_UnknownTarget_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            await client.WaitForSnapshotAsync(s => s.Entities.Count > 0, ct);

            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(999999), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.InvalidTarget, error.Code);
        }

        [Fact]
        public async Task Attack_KeepsSwingingAutomatically_UntilTargetDies()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindCreature(s, NearWolfX, TemplateWolf) != null, ct);
            uint wolfId = FindCreature(view, NearWolfX, TemplateWolf)!.Value.EntityId;

            // Un seul engagement : l'auto-attack doit swinguer seul, à chaque tick, jusqu'à la mort du loup —
            // sans qu'aucune autre demande d'attaque ne soit envoyée.
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);

            CombatEventMessage death = await client.WaitForCombatEventAsync(
                e => e.Kind == CombatEventKind.Death && e.TargetEntityId == wolfId, ct);
            Assert.Equal(wolfId, death.TargetEntityId);
        }

        [Fact]
        public async Task Attack_ThenDisengage_StopsAutomaticSwings()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindCreature(s, NearWolfX, TemplateWolf) != null, ct);
            uint wolfId = FindCreature(view, NearWolfX, TemplateWolf)!.Value.EntityId;

            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);
            await client.WaitForCombatEventAsync(e => e.Kind == CombatEventKind.Damage && e.TargetEntityId == wolfId, ct);
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(0), ct);

            // Après désengagement, le loup ne doit plus jamais être touché : un seul coup au total.
            await Task.Delay(3000, ct);
            WorldSnapshotMessage later = await client.WaitForSnapshotAsync(s => s.Entities.Any(e => e.EntityId == wolfId), ct);
            EntityState wolf = later.Entities.First(e => e.EntityId == wolfId);
            Assert.True(wolf.Health > 0, "Le loup ne devrait pas être mort : désengagé après un seul coup.");
            Assert.True(wolf.Health < wolf.MaxHealth, "Le loup devrait avoir subi exactement un coup.");
        }

        [Fact]
        public async Task Creature_RetaliatesAgainstAttacker()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout(25);
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindCreature(s, NearWolfX, TemplateWolf) != null, ct);
            uint wolfId = FindCreature(view, NearWolfX, TemplateWolf)!.Value.EntityId;
            uint myId = view.YourEntityId;

            // Un seul coup, puis désengagement : l'auto-attack ne doit pas achever le loup avant qu'il riposte.
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(0), ct);

            CombatEventMessage retaliation = await client.WaitForCombatEventAsync(
                e => e.Kind == CombatEventKind.Damage && e.TargetEntityId == myId, ct);
            Assert.Equal(wolfId, retaliation.SourceEntityId);
        }

        [Fact]
        public async Task Player_Dies_ThenRespawnsAtSpawnWithFullHealth()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout(25);
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindCreature(s, BruteX, TemplateBrute) != null, ct);
            uint bruteId = FindCreature(view, BruteX, TemplateBrute)!.Value.EntityId;
            uint myId = view.YourEntityId;

            // Une seule attaque puis désengagement : la brute riposte pour 100 dégâts, ce qui tue le joueur
            // (100 PV). Sans désengagement, l'auto-attack achèverait la brute avant qu'elle n'atteigne le joueur.
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(bruteId), ct);
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(0), ct);

            CombatEventMessage death = await client.WaitForCombatEventAsync(
                e => e.Kind == CombatEventKind.Death && e.TargetEntityId == myId, ct);
            Assert.Equal(myId, death.TargetEntityId);

            CombatEventMessage respawn = await client.WaitForCombatEventAsync(
                e => e.Kind == CombatEventKind.Respawn && e.TargetEntityId == myId, ct);
            Assert.Equal(myId, respawn.TargetEntityId);

            WorldSnapshotMessage afterRespawn = await client.WaitForSnapshotAsync(
                s => s.Entities.Any(e => e.EntityId == myId && e.Health == e.MaxHealth && e.Health > 0),
                ct);
            EntityState self = afterRespawn.Entities.First(e => e.EntityId == myId);
            Assert.Equal(0f, self.X, 3);
        }

        [Fact]
        public async Task Creature_RespawnsAfterDeath()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout(30);
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindCreature(s, NearWolfX, TemplateWolf) != null, ct);
            uint wolfId = FindCreature(view, NearWolfX, TemplateWolf)!.Value.EntityId;

            for (int i = 0; i < 3; i++)
            {
                await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);
                await Task.Delay(1100, ct);
            }
            await client.WaitForCombatEventAsync(e => e.Kind == CombatEventKind.Death && e.TargetEntityId == wolfId, ct);

            // Le cadavre reste visible tant qu'il contient du butin : on le ramasse, puis il disparaît.
            await client.SendAsync(Opcode.LootRequest, new LootRequestMessage(wolfId), ct);
            await client.WaitForSnapshotAsync(s => !s.Entities.Any(e => e.EntityId == wolfId), ct);

            // Le loup réapparaît à pleine vie après son délai de respawn (1 s).
            WorldSnapshotMessage back = await client.WaitForSnapshotAsync(
                s => s.Entities.Any(e => e.EntityId == wolfId && e.Health == e.MaxHealth),
                ct);
            Assert.NotNull(back);
        }

        private static async Task<TestClient> JoinAsync(ServerHarness server, CancellationToken ct)
        {
            var client = new TestClient(server.Endpoint);
            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "combattant"), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            await client.SendRawAsync(GameShared.Protocol.FrameCodec.CreateFrame(Opcode.EnterWorld, 0, new byte[] { 2 }), ct);
            return client;
        }

        private static async Task<ErrorMessage> WaitForErrorAsync(TestClient client, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode == Opcode.Error)
                {
                    return ErrorMessage.Read(payload);
                }
            }
        }

        private static EntityState? FindCreature(WorldSnapshotMessage snapshot, float x, int templateId)
        {
            foreach (EntityState entity in snapshot.Entities)
            {
                if (entity.Kind == EntityKind.Creature && entity.TemplateId == templateId && Math.Abs(entity.X - x) < 0.01f)
                {
                    return entity;
                }
            }

            return null;
        }
    }
}
