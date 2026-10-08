using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    /// <summary>Métiers de bout en bout : apprentissage, récolte, fabrication, et leurs rejets.</summary>
    public sealed class ProfessionsTests
    {
        private const ushort MiraNpcId = 1;
        private const ushort TomNpcId = 2;
        private const byte Mining = 1;
        private const byte Herbalism = 2;
        private const byte Blacksmithing = 3;
        private const byte Alchemy = 4;
        private const ushort CopperOreItemId = 7;
        private const ushort SilverleafItemId = 8;
        private const ushort CopperDaggerItemId = 9;
        private const ushort HealingPotionItemId = 6;
        private const ushort CopperDaggerRecipeId = 1;
        private const ushort HealingPotionRecipeId = 2;

        private static readonly Dictionary<string, string?> ProfessionsZone = new Dictionary<string, string?>
        {
            ["GameServer:ZoneId"] = "professions",
        };

        private static CancellationToken Timeout(int seconds = 10) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task LearnProfession_ThenGather_AddsItemAndGainsSkill()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(ProfessionsZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);

            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(
                s => FindEntity(s, EntityKind.Npc, TomNpcId) != null && FindEntity(s, EntityKind.GatheringNode, 1) != null,
                ct);
            uint tom = FindEntity(view, EntityKind.Npc, TomNpcId)!.Value.EntityId;
            uint node = FindEntity(view, EntityKind.GatheringNode, 1)!.Value.EntityId;

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, tom, Mining, 0), ct);
            ProfessionsMessage afterLearn = await WaitForProfessionsAsync(client, p => p.Known.Count > 0, ct);
            Assert.Equal(1, afterLearn.Known[0].Skill);

            await client.SendAsync(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Gather, node, 0), ct);
            InventoryMessage inventory = await WaitForInventoryAsync(client, inv => CountInBag(inv, CopperOreItemId) > 0, ct);
            ProfessionsMessage afterGather = await WaitForProfessionsAsync(client, p => p.Known[0].Skill == 2, ct);

            Assert.True(CountInBag(inventory, CopperOreItemId) is 1 or 2);
            Assert.Equal(2, afterGather.Known[0].Skill);
        }

        [Fact]
        public async Task Gather_WithoutLearningProfession_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(ProfessionsZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);

            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindEntity(s, EntityKind.GatheringNode, 1) != null, ct);
            uint node = FindEntity(view, EntityKind.GatheringNode, 1)!.Value.EntityId;

            await client.SendAsync(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Gather, node, 0), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.NotAllowed, error.Code);
        }

        [Fact]
        public async Task Gather_DepletedNode_IsRejectedUntilRespawn()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(ProfessionsZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);

            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(
                s => FindEntity(s, EntityKind.Npc, TomNpcId) != null && FindEntity(s, EntityKind.GatheringNode, 1) != null,
                ct);
            uint tom = FindEntity(view, EntityKind.Npc, TomNpcId)!.Value.EntityId;
            uint node = FindEntity(view, EntityKind.GatheringNode, 1)!.Value.EntityId;

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, tom, Mining, 0), ct);
            await WaitForProfessionsAsync(client, p => p.Known.Count > 0, ct);

            await client.SendAsync(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Gather, node, 0), ct);
            await WaitForInventoryAsync(client, inv => CountInBag(inv, CopperOreItemId) > 0, ct);

            // Épuisé : une deuxième récolte immédiate doit être refusée (réapparition dans 30s).
            await client.SendAsync(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Gather, node, 0), ct);
            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.NoSuchItem, error.Code);

            // Le snapshot reflète aussi l'indisponibilité (Health 0/1, cf. EntityState pour un nœud).
            await client.WaitForSnapshotAsync(s => FindEntity(s, EntityKind.GatheringNode, 1)!.Value.Health == 0, ct);
        }

        [Fact]
        public async Task Craft_WithEnoughReagents_ProducesItemAndConsumesThem()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(ProfessionsZone);
            CancellationToken ct = Timeout(20);
            await using TestClient client = await JoinAsync(server, ct);

            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindEntity(s, EntityKind.Npc, TomNpcId) != null, ct);
            uint tom = FindEntity(view, EntityKind.Npc, TomNpcId)!.Value.EntityId;

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, tom, Mining, 0), ct);
            await WaitForProfessionsAsync(client, p => p.Known.Count > 0, ct);
            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, tom, Blacksmithing, 0), ct);
            await WaitForProfessionsAsync(client, p => p.Known.Count == 2, ct);

            // Trois veines de cuivre (1-2 chacune) : au moins 3 minerais au total, garanti.
            List<uint> veins = await GatherAllOfKindAsync(client, view, 1, ct);
            Assert.Equal(3, veins.Count);
            InventoryMessage? stocked = null;
            foreach (uint vein in veins)
            {
                await client.SendAsync(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Gather, vein, 0), ct);
                stocked = await WaitForInventoryAsync(client, _ => true, ct);
            }

            int before = CountInBag(stocked!, CopperOreItemId);
            Assert.True(before >= 3, $"Au moins 3 minerais attendus, {before} obtenus.");

            await client.SendAsync(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Craft, 0, CopperDaggerRecipeId), ct);
            InventoryMessage crafted = await WaitForInventoryAsync(client, inv => CountInBag(inv, CopperDaggerItemId) > 0, ct);

            Assert.Equal(1, CountInBag(crafted, CopperDaggerItemId));
            Assert.Equal(before - 3, CountInBag(crafted, CopperOreItemId));
        }

        [Fact]
        public async Task Craft_WithoutReagents_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(ProfessionsZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);

            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindEntity(s, EntityKind.Npc, TomNpcId) != null, ct);
            uint tom = FindEntity(view, EntityKind.Npc, TomNpcId)!.Value.EntityId;

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, tom, Blacksmithing, 0), ct);
            await WaitForProfessionsAsync(client, p => p.Known.Count > 0, ct);

            await client.SendAsync(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Craft, 0, CopperDaggerRecipeId), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.NoSuchItem, error.Code);
        }

        [Fact]
        public async Task Craft_WithoutLearningProfession_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(ProfessionsZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);

            await client.SendAsync(Opcode.ProfessionAction, new ProfessionActionMessage(ProfessionActionKind.Craft, 0, HealingPotionRecipeId), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.NotAllowed, error.Code);
        }

        [Fact]
        public async Task LearnProfession_Twice_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(ProfessionsZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);

            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindEntity(s, EntityKind.Npc, TomNpcId) != null, ct);
            uint tom = FindEntity(view, EntityKind.Npc, TomNpcId)!.Value.EntityId;

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, tom, Herbalism, 0), ct);
            await WaitForProfessionsAsync(client, p => p.Known.Count > 0, ct);

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, tom, Herbalism, 0), ct);
            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.NotAllowed, error.Code);
        }

        [Fact]
        public async Task LearnProfession_FromNpcThatDoesNotTeachIt_IsRejected()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(ProfessionsZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);

            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindEntity(s, EntityKind.Npc, MiraNpcId) != null, ct);
            uint mira = FindEntity(view, EntityKind.Npc, MiraNpcId)!.Value.EntityId;

            await client.SendAsync(Opcode.NpcAction, new NpcActionMessage(NpcActionKind.LearnProfession, mira, Alchemy, 0), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.InvalidArgument, error.Code);
        }

        /// <summary>Entités distinctes d'un même modèle (plusieurs nœuds de récolte du même gisement).</summary>
        private static async Task<List<uint>> GatherAllOfKindAsync(TestClient client, WorldSnapshotMessage view, ushort templateId, CancellationToken ct)
        {
            var ids = new List<uint>();
            foreach (EntityState entity in view.Entities)
            {
                if (entity.Kind == EntityKind.GatheringNode && entity.TemplateId == templateId)
                {
                    ids.Add(entity.EntityId);
                }
            }

            await Task.CompletedTask;
            return ids;
        }

        private static async Task<TestClient> JoinAsync(ServerHarness server, CancellationToken ct)
        {
            var client = new TestClient(server.Endpoint);
            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "metiers"), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            await client.SendAsync(Opcode.EnterWorld, new EnterWorldMessage(1), ct);
            return client;
        }

        private static int CountInBag(InventoryMessage inventory, ushort itemId)
        {
            int total = 0;
            foreach ((ushort bagItemId, ushort quantity) in inventory.Bag)
            {
                if (bagItemId == itemId)
                {
                    total += quantity;
                }
            }

            return total;
        }

        private static EntityState? FindEntity(WorldSnapshotMessage snapshot, EntityKind kind, ushort templateId)
        {
            foreach (EntityState entity in snapshot.Entities)
            {
                if (entity.Kind == kind && entity.TemplateId == templateId)
                {
                    return entity;
                }
            }

            return null;
        }

        private static async Task<ProfessionsMessage> WaitForProfessionsAsync(TestClient client, Func<ProfessionsMessage, bool> predicate, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode != Opcode.Professions)
                {
                    continue;
                }

                ProfessionsMessage message = ProfessionsMessage.Read(payload);
                if (predicate(message))
                {
                    return message;
                }
            }
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

                InventoryMessage message = InventoryMessage.Read(payload);
                if (predicate(message))
                {
                    return message;
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
    }
}
