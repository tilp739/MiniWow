using System;
using System.Collections.Generic;
using GameServer.Domain;
using GameServer.Domain.Combat;
using GameServer.Domain.Creatures;
using GameServer.Domain.Movement;
using Xunit;

namespace GameServer.Tests
{
    public sealed class VitalsTests
    {
        [Fact]
        public void ApplyDamage_IsCappedAtRemainingHealth()
        {
            var vitals = new Vitals(10);

            int applied = vitals.ApplyDamage(25);

            Assert.Equal(10, applied);
            Assert.Equal(0, vitals.Current);
            Assert.False(vitals.IsAlive);
        }

        [Fact]
        public void ApplyDamage_IsIgnored_WhenAlreadyDead()
        {
            var vitals = new Vitals(5);
            vitals.ApplyDamage(5);

            Assert.Equal(0, vitals.ApplyDamage(3));
        }

        [Fact]
        public void Restore_ResetsToMaximum()
        {
            var vitals = new Vitals(10);
            vitals.ApplyDamage(7);

            vitals.Restore();

            Assert.Equal(10, vitals.Current);
        }
    }

    public sealed class CombatResolverTests
    {
        [Fact]
        public void RollDamage_StaysWithinRange_WithoutCritical()
        {
            var settings = new CombatSettings { CriticalChance = 0f };
            var resolver = new CombatResolver(settings, new Random(42));

            for (int i = 0; i < 200; i++)
            {
                int amount = resolver.RollDamage(3, 6, armor: 0).Amount;
                Assert.InRange(amount, 3, 6);
            }
        }

        [Fact]
        public void RollDamage_MultipliesDamage_OnCritical()
        {
            var settings = new CombatSettings { CriticalChance = 1f, CriticalMultiplier = 2f };
            var resolver = new CombatResolver(settings, new Random(1));

            DamageRoll roll = resolver.RollDamage(10, 10, armor: 0);

            Assert.True(roll.IsCritical);
            Assert.Equal(20, roll.Amount);
        }

        [Fact]
        public void RollDamage_IsReducedByArmor()
        {
            var settings = new CombatSettings { CriticalChance = 0f, ArmorConstant = 100f };
            var resolver = new CombatResolver(settings, new Random(1));

            // 100 d'armure avec une constante de 100 : réduction de 50 %.
            Assert.Equal(5, resolver.RollDamage(10, 10, armor: 100).Amount);
        }

        [Fact]
        public void RollDamage_IsAtLeastOne_EvenWithHugeArmor()
        {
            var settings = new CombatSettings { CriticalChance = 0f };
            var resolver = new CombatResolver(settings, new Random(1));

            Assert.Equal(1, resolver.RollDamage(1, 1, armor: 100000).Amount);
        }

        [Fact]
        public void RollDamage_Throws_WhenRangeIsInverted()
        {
            var resolver = new CombatResolver(new CombatSettings(), new Random(1));

            Assert.Throws<ArgumentException>(() => resolver.RollDamage(10, 5, armor: 0));
        }
    }

    public sealed class ThreatTableTests
    {
        [Fact]
        public void TryGetTop_ReturnsHighestEligibleThreat()
        {
            var table = new ThreatTable();
            table.Add(1, 10f);
            table.Add(2, 30f);
            table.Add(3, 20f);

            bool found = table.TryGetTop(id => id != 2, out uint top);

            Assert.True(found);
            Assert.Equal(3u, top);
        }

        [Fact]
        public void TryGetTop_ReturnsFalse_WhenNobodyIsEligible()
        {
            var table = new ThreatTable();
            table.Add(1, 10f);

            Assert.False(table.TryGetTop(_ => false, out _));
        }

        [Fact]
        public void Add_IgnoresNonPositiveAmounts()
        {
            var table = new ThreatTable();
            table.Add(1, 0f);
            table.Add(1, -5f);

            Assert.True(table.IsEmpty);
        }
    }

    public sealed class CreatureAiTests
    {
        private const float Dt = 0.1f;

        private static CreatureTemplate Wolf() => new CreatureTemplate
        {
            Id = 1,
            Key = "wolf",
            Name = "Loup",
            MaxHealth = 20,
            DamageMin = 1,
            DamageMax = 1,
            MoveSpeed = 10f,
            AggroRange = 5f,
            LeashRange = 10f,
            AttackRange = 1f,
            AttackCooldownSeconds = 1f,
            RespawnSeconds = 2f,
            ExperienceReward = 5,
        };

        private static Player PlayerAt(uint id, float x, float z) => TestData.NewPlayer(id, x, z);

        [Fact]
        public void Idle_StartsChasing_WhenPlayerIsInAggroRange()
        {
            var wolf = new CreatureInstance(10, Wolf(), 0f, 0f);
            var players = new List<Player> { PlayerAt(1, 3f, 0f) };

            CreatureAi.Update(wolf, players, Dt);

            Assert.Equal(CreatureState.Chase, wolf.State);
            Assert.Equal(1u, wolf.TargetId);
        }

        [Fact]
        public void Idle_StaysIdle_WhenPlayerIsOutOfAggroRange()
        {
            var wolf = new CreatureInstance(10, Wolf(), 0f, 0f);
            var players = new List<Player> { PlayerAt(1, 50f, 0f) };

            CreatureAi.Update(wolf, players, Dt);

            Assert.Equal(CreatureState.Idle, wolf.State);
        }

        [Fact]
        public void Chase_ReturnsAttackIntent_WhenInRangeAndCooldownReady_ThenWaitsForCooldown()
        {
            var wolf = new CreatureInstance(10, Wolf(), 0f, 0f);
            var players = new List<Player> { PlayerAt(1, 3f, 0f) };

            uint? firstAttack = null;
            int attackTicks = 0;
            for (int tick = 0; tick < 40 && firstAttack == null; tick++)
            {
                firstAttack = CreatureAi.Update(wolf, players, Dt);
                attackTicks++;
            }

            Assert.Equal(1u, firstAttack);
            Assert.Equal(CreatureState.Attack, wolf.State);

            // Pendant le cooldown (1 s = 10 ticks), aucune nouvelle attaque.
            for (int tick = 0; tick < 5; tick++)
            {
                Assert.Null(CreatureAi.Update(wolf, players, Dt));
            }
        }

        [Fact]
        public void Chase_ReturnsHome_WhenTargetLeavesLeash()
        {
            var wolf = new CreatureInstance(10, Wolf(), 0f, 0f);
            Player player = PlayerAt(1, 3f, 0f);
            var players = new List<Player> { player };
            CreatureAi.Update(wolf, players, Dt);
            Assert.Equal(CreatureState.Chase, wolf.State);

            player.Motor.Teleport(100f, 0f);
            CreatureAi.Update(wolf, players, Dt);

            Assert.Equal(CreatureState.Return, wolf.State);
            Assert.True(wolf.Threat.IsEmpty);
        }

        [Fact]
        public void Return_RestoresHealth_WhenHomeIsReached()
        {
            var wolf = new CreatureInstance(10, Wolf(), 0f, 0f);
            wolf.Vitals.ApplyDamage(5);
            wolf.ReceiveHit(1, 5);
            wolf.SetState(CreatureState.Return);
            wolf.MoveTowards(3f, 0f, 3f); // éloignée de son point d'origine

            var players = new List<Player>();
            for (int tick = 0; tick < 100 && wolf.State != CreatureState.Idle; tick++)
            {
                CreatureAi.Update(wolf, players, Dt);
            }

            Assert.Equal(CreatureState.Idle, wolf.State);
            Assert.Equal(wolf.Vitals.MaxHealth, wolf.Vitals.Current);
        }

        [Fact]
        public void ReceiveHit_FromIdle_ProvokesChase()
        {
            var wolf = new CreatureInstance(10, Wolf(), 0f, 0f);

            wolf.ReceiveHit(7, 4);

            Assert.Equal(CreatureState.Chase, wolf.State);
            Assert.Equal(7u, wolf.TargetId);
            Assert.True(wolf.Threat.Get(7) > 0f);
        }

        [Fact]
        public void Dead_RespawnsAtSpawnPoint_AfterRespawnDelay()
        {
            var wolf = new CreatureInstance(10, Wolf(), 4f, 4f);
            wolf.ReceiveHit(1, 20);
            wolf.Die();
            wolf.MoveTowards(9f, 9f, 100f); // le corps n'est pas à l'origine

            var players = new List<Player>();
            for (int tick = 0; tick < 30 && wolf.State == CreatureState.Dead; tick++)
            {
                CreatureAi.Update(wolf, players, 0.5f); // 2 s au total
            }

            Assert.Equal(CreatureState.Idle, wolf.State);
            Assert.True(wolf.IsAlive);
            Assert.Equal(4f, wolf.X, 3);
            Assert.Equal(4f, wolf.Z, 3);
        }

        [Fact]
        public void Die_ClearsThreatAndTarget()
        {
            var wolf = new CreatureInstance(10, Wolf(), 0f, 0f);
            wolf.ReceiveHit(1, 5);

            wolf.Die();

            Assert.Equal(CreatureState.Dead, wolf.State);
            Assert.True(wolf.Threat.IsEmpty);
            Assert.Null(wolf.TargetId);
        }
    }
}
