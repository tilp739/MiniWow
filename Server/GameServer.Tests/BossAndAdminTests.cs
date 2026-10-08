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
    /// <summary>Victoire sur le boss : le donjon se termine. Et la console d'administration ajoute un objet.</summary>
    public sealed class BossAndAdminTests
    {
        private const ushort GatekeeperNpcId = 3;
        private const int TemplateWarden = 3;

        private static readonly Dictionary<string, string?> CombatZone = new Dictionary<string, string?>
        {
            ["GameServer:ZoneId"] = "combat",
        };

        private static CancellationToken Timeout(int seconds) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task KillingTheWarden_CompletesTheDungeon()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout(60);
            await using TestClient client = await JoinAsync(server, "vainqueur", ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindNpc(s, GatekeeperNpcId) != null, ct);
            uint gate = FindNpc(view, GatekeeperNpcId)!.Value.EntityId;

            await client.SendAsync(Opcode.DungeonAction, new DungeonActionMessage(DungeonActionKind.Enter, gate), ct);
            WorldSnapshotMessage inside = await client.WaitForSnapshotAsync(
                s => s.Entities.Any(e => e.Kind == EntityKind.Creature && e.TemplateId == TemplateWarden),
                ct);
            uint warden = inside.Entities.First(e => e.Kind == EntityKind.Creature && e.TemplateId == TemplateWarden).EntityId;

            // Le boss s'approche dès qu'il est en combat ; on attend qu'il soit à portée avant de frapper.
            await client.WaitForSnapshotAsync(
                s => s.Entities.Any(e => e.EntityId == warden && Math.Abs(e.X) < 2.5f),
                ct);

            // 30 PV ; chaque coup inflige au moins 8 : quatre coups suffisent. On en envoie cinq, espacés du cooldown.
            for (int i = 0; i < 5; i++)
            {
                await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(warden), ct);
                await Task.Delay(1100, ct);
            }

            CombatEventMessage dungeon = await client.WaitForCombatEventAsync(
                e => e.Kind == CombatEventKind.Dungeon && e.SourceEntityId == warden,
                ct);
            Assert.Equal(1, dungeon.Amount);
        }

        [Fact]
        public async Task AdminGive_AddsItemToNamedPlayer()
        {
            await using ServerHarness server = await ServerHarness.StartAsync();
            CancellationToken ct = Timeout(20);
            await using TestClient client = await JoinAsync(server, "admin-cible", ct);
            await client.WaitForSnapshotAsync(s => s.Entities.Count >= 1, ct);

            // Même chemin que la console d'administration : une action postée, exécutée sur le thread de simulation.
            server.Directory.PostAdminAction(d => d.Main.AdminGiveItem("admin-cible", "wolf_pelt", 3));

            InventoryMessage inventory = await WaitForInventoryAsync(
                client,
                msg => msg.Bag.Any(slot => slot.ItemId == 1 && slot.Quantity == 3),
                ct);
            Assert.Contains(inventory.Bag, slot => slot.ItemId == 1 && slot.Quantity == 3);
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

        private static async Task<InventoryMessage> WaitForInventoryAsync(TestClient client, Func<InventoryMessage, bool> predicate, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode != Opcode.Inventory)
                {
                    continue;
                }

                InventoryMessage inventory = InventoryMessage.Read(payload);
                if (predicate(inventory))
                {
                    return inventory;
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
