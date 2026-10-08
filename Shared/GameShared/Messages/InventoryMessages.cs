using System;
using System.Collections.Generic;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>
    /// Contenu complet du sac et de l'équipement. Envoyé à chaque changement : simple et impossible à désynchroniser.
    /// Un identifiant d'objet nul (0) signifie emplacement vide.
    /// </summary>
    public sealed class InventoryMessage : IWireMessage
    {
        public const int BagSize = 24;

        /// <summary>Nombre d'emplacements d'équipement (cf. <c>GameServer.Domain.Items.EquipSlot</c>).</summary>
        public const int EquipmentSize = 19;

        public InventoryMessage(IReadOnlyList<(ushort ItemId, ushort Quantity)> bag, IReadOnlyList<ushort> equipment)
        {
            Bag = bag;
            Equipment = equipment;
        }

        public IReadOnlyList<(ushort ItemId, ushort Quantity)> Bag { get; }

        public IReadOnlyList<ushort> Equipment { get; }

        public void Write(PacketWriter writer)
        {
            if (Bag.Count != BagSize || Equipment.Count != EquipmentSize)
            {
                throw new ArgumentException("Taille d'inventaire inattendue.");
            }

            for (int i = 0; i < BagSize; i++)
            {
                writer.WriteUInt16(Bag[i].ItemId);
                writer.WriteUInt16(Bag[i].Quantity);
            }

            for (int i = 0; i < EquipmentSize; i++)
            {
                writer.WriteUInt16(Equipment[i]);
            }
        }

        public static InventoryMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            var bag = new List<(ushort ItemId, ushort Quantity)>(BagSize);
            for (int i = 0; i < BagSize; i++)
            {
                ushort itemId = reader.ReadUInt16();
                ushort quantity = reader.ReadUInt16();
                bag.Add((itemId, quantity));
            }

            var equipment = new List<ushort>(EquipmentSize);
            for (int i = 0; i < EquipmentSize; i++)
            {
                equipment.Add(reader.ReadUInt16());
            }

            reader.ExpectEnd();
            return new InventoryMessage(bag, equipment);
        }
    }

    public enum InventoryActionKind : byte
    {
        Move = 1,
        Split = 2,
        Destroy = 3,
        Equip = 4,
        Unequip = 5,
    }

    /// <summary>Action sur l'inventaire. <c>From</c> / <c>To</c> : emplacements du sac, ou d'équipement pour Unequip.</summary>
    public readonly struct InventoryActionMessage : IWireMessage
    {
        public InventoryActionMessage(InventoryActionKind kind, byte from, byte to, ushort amount)
        {
            Kind = kind;
            From = from;
            To = to;
            Amount = amount;
        }

        public InventoryActionKind Kind { get; }

        public byte From { get; }

        public byte To { get; }

        public ushort Amount { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte((byte)Kind);
            writer.WriteByte(From);
            writer.WriteByte(To);
            writer.WriteUInt16(Amount);
        }

        public static InventoryActionMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte kind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(InventoryActionKind), kind))
            {
                throw new ProtocolException($"Action d'inventaire inconnue : {kind}.");
            }

            byte from = reader.ReadByte();
            byte to = reader.ReadByte();
            ushort amount = reader.ReadUInt16();
            reader.ExpectEnd();
            return new InventoryActionMessage((InventoryActionKind)kind, from, to, amount);
        }
    }

    /// <summary>Demande de ramassage du butin d'un cadavre.</summary>
    public readonly struct LootRequestMessage : IWireMessage
    {
        public LootRequestMessage(uint corpseEntityId)
        {
            CorpseEntityId = corpseEntityId;
        }

        public uint CorpseEntityId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt32(CorpseEntityId);
        }

        public static LootRequestMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            uint corpse = reader.ReadUInt32();
            reader.ExpectEnd();
            return new LootRequestMessage(corpse);
        }
    }
}
