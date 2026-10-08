using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Domain.Classes;
using GameServer.Domain.Combat;
using GameServer.Domain.Stats;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    public sealed class StatsAndResourceTests
    {
        [Fact]
        public void StatCalculator_DerivesHealthFromStamina()
        {
            var stats = new PrimaryStats(strength: 0, agility: 0, intelligence: 0, stamina: 10, spirit: 0);

            DerivedStats derived = StatCalculator.Derive(stats, CombatBonuses.None);

            Assert.Equal(150, derived.MaxHealth); // 50 de base + 10 × 10
        }

        [Fact]
        public void StatCalculator_AddsEquipmentBonusesToPrimaries()
        {
            var primary = new PrimaryStats(10, 0, 0, 0, 0);
            var bonuses = new CombatBonuses(armor: 25, new PrimaryStats(5, 0, 0, 0, 0));

            DerivedStats derived = StatCalculator.Derive(primary, bonuses);

            Assert.Equal(30f, derived.AttackPower); // (10 + 5) × 2
            Assert.Equal(25, derived.Armor);
        }

        [Fact]
        public void ClassDefinition_GrowsStatsWithLevel()
        {
            ClassDefinition mage = TestData.Mage();

            PrimaryStats level3 = mage.StatsAtLevel(3);

            Assert.Equal(12 + 3 * 2, level3.Intelligence);
        }

        [Fact]
        public void ResourcePool_NeverExceedsBounds()
        {
            var pool = new ResourcePool(ResourceKind.Mana, max: 50, regenPerSecond: 10f, initial: 45);

            pool.Tick(10f);
            Assert.Equal(50, pool.Current);

            Assert.False(pool.TrySpend(51));
            Assert.True(pool.TrySpend(50));
            Assert.Equal(0, pool.Current);
        }

        [Fact]
        public void ResourcePool_RageDecaysOutOfCombat()
        {
            var rage = new ResourcePool(ResourceKind.Rage, max: 100, regenPerSecond: -2f, initial: 0);
            rage.Gain(30);

            rage.Tick(5f);

            Assert.Equal(20, rage.Current);
        }

        [Fact]
        public void Vitals_HealIsCappedAtMaximum()
        {
            var vitals = new Vitals(100);
            vitals.ApplyDamage(30);

            Assert.Equal(30, vitals.Heal(500));
            Assert.Equal(100, vitals.Current);
        }
    }

    /// <summary>Compétences de bout en bout sur la zone "combat" : coûts, cooldowns, classes, lancer et interruption.</summary>
    public sealed class SkillIntegrationTests
    {
        private const ushort EmberBolt = 3;
        private const ushort FrostLance = 4;
        private const ushort Mend = 5;
        private const float NearWolfX = 2.5f;
        private const int TemplateWolf = 1;

        private static readonly Dictionary<string, string?> CombatZone = new Dictionary<string, string?>
        {
            ["GameServer:ZoneId"] = "combat",
        };

        private static CancellationToken Timeout(int seconds = 20) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task Mage_CastsEmberBolt_WhichDamagesTarget_AfterCastTime()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, classId: 2, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindWolf(s) != null, ct);
            uint wolfId = FindWolf(view)!.Value.EntityId;

            await client.SendAsync(Opcode.CastSkill, new CastSkillMessage(EmberBolt, wolfId), ct);

            CombatEventMessage start = await client.WaitForCombatEventAsync(e => e.Kind == CombatEventKind.CastStart, ct);
            Assert.Equal(1500, start.Amount);
            CombatEventMessage damage = await client.WaitForCombatEventAsync(
                e => e.Kind == CombatEventKind.Damage && e.TargetEntityId == wolfId, ct);
            Assert.True(damage.Amount > 0);
        }

        [Fact]
        public async Task Mage_CannotCastPriestSkill()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, classId: 2, ct);
            await client.WaitForSnapshotAsync(s => s.Entities.Count > 0, ct);

            await client.SendAsync(Opcode.CastSkill, new CastSkillMessage(Mend, 0), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.WrongClass, error.Code);
        }

        [Fact]
        public async Task UnknownSkill_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, classId: 2, ct);
            await client.WaitForSnapshotAsync(s => s.Entities.Count > 0, ct);

            await client.SendAsync(Opcode.CastSkill, new CastSkillMessage(999, 0), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.UnknownSkill, error.Code);
        }

        [Fact]
        public async Task Skill_OnCooldown_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, classId: 2, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindWolf(s) != null, ct);
            uint wolfId = FindWolf(view)!.Value.EntityId;

            await client.SendAsync(Opcode.CastSkill, new CastSkillMessage(FrostLance, wolfId), ct);
            await client.SendAsync(Opcode.CastSkill, new CastSkillMessage(FrostLance, wolfId), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.OnCooldown, error.Code);
        }

        [Fact]
        public async Task Moving_WhileCasting_InterruptsTheCast()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, classId: 2, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindWolf(s) != null, ct);
            uint wolfId = FindWolf(view)!.Value.EntityId;

            await client.SendAsync(Opcode.CastSkill, new CastSkillMessage(EmberBolt, wolfId), ct);
            await client.WaitForCombatEventAsync(e => e.Kind == CombatEventKind.CastStart, ct);
            await client.SendAsync(Opcode.MoveInput, new MoveInputMessage(1, 127, 0), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.CastInterrupted, error.Code);
        }

        private static async Task<TestClient> JoinAsync(ServerHarness server, byte classId, CancellationToken ct)
        {
            var client = new TestClient(server.Endpoint);
            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "lanceur"), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            await client.SendAsync(Opcode.EnterWorld, new EnterWorldMessage(classId), ct);
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

        private static EntityState? FindWolf(WorldSnapshotMessage snapshot)
        {
            foreach (EntityState entity in snapshot.Entities)
            {
                if (entity.Kind == EntityKind.Creature && entity.TemplateId == TemplateWolf && Math.Abs(entity.X - NearWolfX) < 0.01f)
                {
                    return entity;
                }
            }

            return null;
        }
    }
}
