using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Domain.Progression;
using GameServer.Domain.Quests;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    public sealed class ProgressionAndQuestLogTests
    {
        private static QuestDefinition Kill3() => new QuestDefinition
        {
            Id = 9,
            Key = "kill3",
            Objectives = new List<QuestObjectiveDefinition>
            {
                new QuestObjectiveDefinition { Kind = ObjectiveKind.Kill, TargetId = 1, Count = 3 },
            },
        };

        [Fact]
        public void GrantExperience_LevelsUp_WhenThresholdIsReached()
        {
            var player = TestData.NewPlayer(1, 0f, 0f);

            int gained = player.GrantExperience((int)LevelTable.ExperienceToNext(1));

            Assert.Equal(1, gained);
            Assert.Equal(2, player.Level);
            Assert.Equal(0, player.Experience);
        }

        [Fact]
        public void GrantExperience_LevelUp_RaisesHealthAndRecomputesStats()
        {
            var player = TestData.NewPlayer(1, 0f, 0f);
            int before = player.Vitals.MaxHealth;

            player.GrantExperience((int)LevelTable.ExperienceToNext(1));

            Assert.True(player.Vitals.MaxHealth > before);
        }

        [Fact]
        public void GrantExperience_StopsAtMaxLevel()
        {
            var player = TestData.NewPlayer(1, 0f, 0f);

            player.GrantExperience(int.MaxValue);

            Assert.Equal(LevelTable.MaxLevel, player.Level);
            Assert.Equal(0, player.Experience);
        }

        [Fact]
        public void QuestLog_AcceptsOnce_AndNeverAgainAfterCompletion()
        {
            var log = new QuestLog();
            QuestDefinition quest = Kill3();

            Assert.True(log.Accept(quest));
            Assert.False(log.Accept(quest));

            log.Advance(quest, 0, 3);
            Assert.True(log.IsSatisfied(quest));
            log.Complete(quest.Id);

            Assert.False(log.Accept(quest));
            Assert.True(log.IsCompleted(quest.Id));
        }

        [Fact]
        public void QuestLog_ClampsProgressToRequiredCount()
        {
            var log = new QuestLog();
            QuestDefinition quest = Kill3();
            log.Accept(quest);

            log.Advance(quest, 0, 10);

            Assert.Equal(3, log.Progress(quest.Id, 0));
        }

        [Fact]
        public void QuestLog_IsNotSatisfied_UntilEveryObjectiveIsDone()
        {
            var log = new QuestLog();
            QuestDefinition quest = Kill3();
            log.Accept(quest);
            log.Advance(quest, 0, 2);

            Assert.False(log.IsSatisfied(quest));
        }
    }

    /// <summary>PNJ et quêtes de bout en bout : accepter, remplir, rendre, récompenser, acheter.</summary>
    public sealed class QuestIntegrationTests
    {
        private const ushort MiraNpcId = 1;
        private const ushort TomNpcId = 2;
        private const ushort GreetingsQuestId = 3;

        private static readonly Dictionary<string, string?> CombatZone = new Dictionary<string, string?>
        {
            ["GameServer:ZoneId"] = "combat",
        };

        private static CancellationToken Timeout(int seconds = 20) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task GreetingsQuest_CanBeAcceptedCompletedAndRewarded()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindNpc(s, MiraNpcId) != null && FindNpc(s, TomNpcId) != null, ct);
            uint mira = FindNpc(view, MiraNpcId)!.Value.EntityId;
            uint tom = FindNpc(view, TomNpcId)!.Value.EntityId;

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.Talk, mira, 0, 0), ct);
            NpcMessage dialogue = await WaitForNpcAsync(client, ct);
            Assert.Contains(GreetingsQuestId, dialogue.OfferedQuests);

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.AcceptQuest, mira, GreetingsQuestId, 0), ct);
            QuestLogMessage accepted = await WaitForQuestLogAsync(client, log => log.Active.Any(q => q.QuestId == GreetingsQuestId), ct);
            Assert.Contains(accepted.Active, q => q.QuestId == GreetingsQuestId);

            // L'objectif « parler à Tom » se remplit en ouvrant son dialogue.
            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.Talk, tom, 0, 0), ct);
            await WaitForQuestLogAsync(client, log => log.Active.Any(q => q.QuestId == GreetingsQuestId && q.Objectives.All(o => o.Current >= o.Required)), ct);

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.TurnInQuest, tom, GreetingsQuestId, 0), ct);
            QuestLogMessage done = await WaitForQuestLogAsync(client, log => log.Completed.Contains(GreetingsQuestId), ct);
            Assert.DoesNotContain(done.Active, q => q.QuestId == GreetingsQuestId);

            StatsMessage stats = await WaitForStatsAsync(client, s => s.Copper >= 5, ct);
            Assert.Equal(5, stats.Copper);
            Assert.Equal(20u, stats.Experience);
        }

        [Fact]
        public async Task Buying_WithoutEnoughCopper_IsRefused()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindNpc(s, TomNpcId) != null, ct);
            uint tom = FindNpc(view, TomNpcId)!.Value.EntityId;

            // Potion de soin (objet 6) : 5 cuivre. Le joueur n'en a aucun.
            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.Buy, tom, 6, 1), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.NotEnoughCopper, error.Code);
        }

        [Fact]
        public async Task UnknownNpc_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindNpc(s, MiraNpcId) != null, ct);
            uint mira = FindNpc(view, MiraNpcId)!.Value.EntityId;

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.Talk, mira + 1000, 0, 0), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.NoSuchNpc, error.Code);
        }

        private static async Task<TestClient> JoinAsync(ServerHarness server, CancellationToken ct)
        {
            var client = new TestClient(server.Endpoint);
            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "quetes"), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            await client.SendAsync(Opcode.EnterWorld, new EnterWorldMessage(2), ct);
            return client;
        }

        private static async Task<NpcMessage> WaitForNpcAsync(TestClient client, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode == Opcode.Npc)
                {
                    return NpcMessage.Read(payload);
                }
            }
        }

        private static async Task<QuestLogMessage> WaitForQuestLogAsync(TestClient client, Func<QuestLogMessage, bool> predicate, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode != Opcode.Quests)
                {
                    continue;
                }

                QuestLogMessage log = QuestLogMessage.Read(payload);
                if (predicate(log))
                {
                    return log;
                }
            }
        }

        private static async Task<StatsMessage> WaitForStatsAsync(TestClient client, Func<StatsMessage, bool> predicate, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode != Opcode.Stats)
                {
                    continue;
                }

                StatsMessage stats = StatsMessage.Read(payload);
                if (predicate(stats))
                {
                    return stats;
                }
            }
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
