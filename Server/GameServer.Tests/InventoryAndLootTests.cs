using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Domain.Items;
using GameShared.Messages;
using GameShared.Protocol;
using Xunit;

namespace GameServer.Tests
{
    /// <summary>Règles d'inventaire : atomicité, empilement, équipement. Sans réseau.</summary>
    public sealed class InventoryTests
    {
        private static readonly ItemDefinition Pelt = new ItemDefinition { Id = 1, Key = "pelt", Name = "Fourrure", MaxStack = 20 };
        private static readonly ItemDefinition Sword = new ItemDefinition { Id = 3, Key = "sword", Name = "Épée", MaxStack = 1, Slot = EquipSlot.MainHand, Strength = 2 };
        private static readonly ItemDefinition Cap = new ItemDefinition { Id = 4, Key = "cap", Name = "Casque", MaxStack = 1, Slot = EquipSlot.Head, Armor = 5 };

        [Fact]
        public void TryAdd_StacksThenFillsFreeSlots()
        {
            var inventory = new Inventory();

            Assert.True(inventory.TryAdd(Pelt, 25));

            Assert.Equal(20, inventory.GetBag(0)!.Quantity);
            Assert.Equal(5, inventory.GetBag(1)!.Quantity);
            Assert.Equal(25, inventory.CountInBag(Pelt.Id));
        }

        [Fact]
        public void TryAdd_IsAtomic_WhenBagCannotHoldEverything()
        {
            var inventory = new Inventory();
            for (int i = 0; i < Inventory.BagSize; i++)
            {
                inventory.TryAdd(Sword, 1);
            }

            Assert.False(inventory.TryAdd(Pelt, 1));
            Assert.Equal(0, inventory.CountInBag(Pelt.Id));
        }

        [Fact]
        public void TryRemove_IsAtomic_WhenNotEnoughItems()
        {
            var inventory = new Inventory();
            inventory.TryAdd(Pelt, 3);

            Assert.False(inventory.TryRemove(Pelt.Id, 4));
            Assert.Equal(3, inventory.CountInBag(Pelt.Id));
        }

        [Fact]
        public void TryMove_MergesSameStackableItem()
        {
            var inventory = new Inventory();
            inventory.TryAdd(Pelt, 10);
            inventory.TrySplit(0, 1, 4); // 6 dans le slot 0, 4 dans le slot 1

            Assert.True(inventory.TryMove(1, 0));

            Assert.Equal(10, inventory.GetBag(0)!.Quantity);
            Assert.Null(inventory.GetBag(1));
        }

        [Fact]
        public void TrySplit_CreatesNewStackInEmptySlot()
        {
            var inventory = new Inventory();
            inventory.TryAdd(Pelt, 10);

            Assert.True(inventory.TrySplit(0, 5, 4));

            Assert.Equal(6, inventory.GetBag(0)!.Quantity);
            Assert.Equal(4, inventory.GetBag(5)!.Quantity);
            Assert.Equal(10, inventory.CountInBag(Pelt.Id));
        }

        [Fact]
        public void TryEquip_MovesItemAndAddsBonuses()
        {
            var inventory = new Inventory();
            inventory.TryAdd(Sword, 1);

            Assert.True(inventory.TryEquip(0));

            Assert.Null(inventory.GetBag(0));
            Assert.Same(Sword, inventory.GetEquipped(EquipSlot.MainHand)!.Item);
            Assert.Equal(2, inventory.ComputeBonuses().Stats.Strength);
        }

        [Fact]
        public void TryEquip_ReturnsPreviousItemToBag()
        {
            var inventory = new Inventory();
            inventory.TryAdd(Sword, 1);
            inventory.TryAdd(Sword, 1);
            inventory.TryEquip(0);

            Assert.True(inventory.TryEquip(1));

            Assert.Same(Sword, inventory.GetEquipped(EquipSlot.MainHand)!.Item);
            // L'ancien objet revient dans le slot que le nouveau vient de libérer.
            Assert.NotNull(inventory.GetBag(1));
            Assert.Equal(1, inventory.CountInBag(Sword.Id));
        }

        [Fact]
        public void TryEquip_Fails_WhenItemIsNotEquippable()
        {
            var inventory = new Inventory();
            inventory.TryAdd(Pelt, 1);

            Assert.False(inventory.TryEquip(0));
        }

        [Fact]
        public void TryUnequip_Fails_WhenBagIsFull()
        {
            var inventory = new Inventory();
            inventory.TryAdd(Cap, 1);
            inventory.TryEquip(0);
            for (int i = 0; i < Inventory.BagSize; i++)
            {
                inventory.TryAdd(Sword, 1);
            }

            Assert.False(inventory.TryUnequip(EquipSlot.Head));
            Assert.NotNull(inventory.GetEquipped(EquipSlot.Head));
        }

        [Fact]
        public void TryDestroy_RemovesStack()
        {
            var inventory = new Inventory();
            inventory.TryAdd(Pelt, 2);

            Assert.True(inventory.TryDestroy(0));
            Assert.Equal(0, inventory.CountInBag(Pelt.Id));
        }
    }

    /// <summary>Butin de bout en bout : tuer, ramasser, voir l'inventaire.</summary>
    public sealed class LootIntegrationTests
    {
        private const float NearWolfX = 2.5f;
        private const int TemplateWolf = 1;
        private const ushort WolfPelt = 1;

        private static readonly Dictionary<string, string?> CombatZone = new Dictionary<string, string?>
        {
            ["GameServer:ZoneId"] = "combat",
        };

        private static CancellationToken Timeout(int seconds = 25) =>
            new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

        [Fact]
        public async Task KillingWolf_ThenLooting_PutsPeltInBag()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindWolf(s) != null, ct);
            uint wolfId = FindWolf(view)!.Value.EntityId;

            // Un seul engagement : l'auto-attack swingue seule jusqu'à la mort du loup.
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);
            await client.WaitForCombatEventAsync(e => e.Kind == CombatEventKind.Death && e.TargetEntityId == wolfId, ct);

            // Le cadavre reste visible tant qu'il contient du butin.
            await client.WaitForSnapshotAsync(s => s.Entities.Any(e => e.EntityId == wolfId), ct);
            await client.SendAsync(Opcode.LootRequest, new LootRequestMessage(wolfId), ct);

            InventoryMessage inventory = await WaitForInventoryOrErrorAsync(
                client,
                msg => msg.Bag.Any(slot => slot.ItemId == WolfPelt),
                ct);
            Assert.Contains(inventory.Bag, slot => slot.ItemId == WolfPelt && slot.Quantity >= 1);
        }

        [Fact]
        public async Task LootingTwice_IsRejected_BecauseCorpseIsEmpty()
        {
            await using ServerHarness server = await ServerHarness.StartAsync(CombatZone);
            CancellationToken ct = Timeout();
            await using TestClient client = await JoinAsync(server, ct);
            WorldSnapshotMessage view = await client.WaitForSnapshotAsync(s => FindWolf(s) != null, ct);
            uint wolfId = FindWolf(view)!.Value.EntityId;

            // Un seul engagement : l'auto-attack swingue seule jusqu'à la mort du loup.
            await client.SendAsync(Opcode.AttackRequest, new AttackRequestMessage(wolfId), ct);
            await client.WaitForCombatEventAsync(e => e.Kind == CombatEventKind.Death && e.TargetEntityId == wolfId, ct);

            await client.SendAsync(Opcode.LootRequest, new LootRequestMessage(wolfId), ct);
            await WaitForInventoryOrErrorAsync(client, msg => msg.Bag.Any(slot => slot.ItemId == WolfPelt), ct);
            await client.SendAsync(Opcode.LootRequest, new LootRequestMessage(wolfId), ct);

            ErrorMessage error = await WaitForErrorAsync(client, ct);
            Assert.Equal(ErrorCode.NoSuchItem, error.Code);
        }

        private static async Task<TestClient> JoinAsync(ServerHarness server, CancellationToken ct)
        {
            var client = new TestClient(server.Endpoint);
            await client.SendAsync(Opcode.Hello, new HelloMessage(ProtocolConstants.Version, "pilleur"), ct);
            (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
            Assert.Equal(Opcode.HelloAck, opcode);
            Assert.True(HelloAckMessage.Read(payload).Accepted);

            // Mage : 100 PV. Dégâts de base : 8 à 12, indépendants de la classe.
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

        /// <summary>Lit les trames jusqu'à l'inventaire attendu, en signalant toute erreur serveur au passage.</summary>
        private static async Task<InventoryMessage> WaitForInventoryOrErrorAsync(TestClient client, Func<InventoryMessage, bool> predicate, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await client.ReceiveAsync(ct);
                if (opcode == Opcode.Error)
                {
                    ErrorMessage error = ErrorMessage.Read(payload);
                    throw new Xunit.Sdk.XunitException($"Erreur serveur {error.Code} : {error.Text}");
                }

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
