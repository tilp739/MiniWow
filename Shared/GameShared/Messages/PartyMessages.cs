using System;
using System.Collections.Generic;
using GameShared.Protocol;

namespace GameShared.Messages
{
    public enum PartyActionKind : byte
    {
        Invite = 1,
        Accept = 2,
        Decline = 3,
        Leave = 4,
        Kick = 5,
    }

    /// <summary>Action sur le groupe. <c>TargetEntityId</c> : joueur visé (Invite, Kick).</summary>
    public readonly struct PartyActionMessage : IWireMessage
    {
        public PartyActionMessage(PartyActionKind kind, uint targetEntityId)
        {
            Kind = kind;
            TargetEntityId = targetEntityId;
        }

        public PartyActionKind Kind { get; }

        public uint TargetEntityId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte((byte)Kind);
            writer.WriteUInt32(TargetEntityId);
        }

        public static PartyActionMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte kind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(PartyActionKind), kind))
            {
                throw new ProtocolException($"Action de groupe inconnue : {kind}.");
            }

            uint target = reader.ReadUInt32();
            reader.ExpectEnd();
            return new PartyActionMessage((PartyActionKind)kind, target);
        }
    }

    /// <summary>
    /// État du groupe vu par un joueur. <c>LeaderEntityId</c> = 0 si le joueur n'a pas de groupe.
    /// <c>InviterEntityId</c> = 0 si aucune invitation n'est en attente.
    /// </summary>
    public sealed class PartyMessage : IWireMessage
    {
        public const int MaxMembers = 5;
        public const int MaxNameByteLength = 32;

        public PartyMessage(
            uint leaderEntityId,
            IReadOnlyList<(uint EntityId, string Name)> members,
            uint inviterEntityId,
            string inviterName)
        {
            LeaderEntityId = leaderEntityId;
            Members = members;
            InviterEntityId = inviterEntityId;
            InviterName = inviterName;
        }

        public uint LeaderEntityId { get; }

        public IReadOnlyList<(uint EntityId, string Name)> Members { get; }

        public uint InviterEntityId { get; }

        public string InviterName { get; }

        public void Write(PacketWriter writer)
        {
            if (Members.Count > MaxMembers)
            {
                throw new ArgumentException("Groupe trop grand.");
            }

            writer.WriteUInt32(LeaderEntityId);
            writer.WriteByte((byte)Members.Count);
            foreach ((uint entityId, string name) in Members)
            {
                writer.WriteUInt32(entityId);
                writer.WriteString(name, MaxNameByteLength);
            }

            writer.WriteUInt32(InviterEntityId);
            writer.WriteString(InviterName, MaxNameByteLength);
        }

        public static PartyMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            uint leader = reader.ReadUInt32();
            int count = reader.ReadByte();
            if (count > MaxMembers)
            {
                throw new ProtocolException("Groupe trop grand.");
            }

            var members = new List<(uint EntityId, string Name)>(count);
            for (int i = 0; i < count; i++)
            {
                uint entityId = reader.ReadUInt32();
                string name = reader.ReadString(MaxNameByteLength);
                members.Add((entityId, name));
            }

            uint inviter = reader.ReadUInt32();
            string inviterName = reader.ReadString(MaxNameByteLength);
            reader.ExpectEnd();
            return new PartyMessage(leader, members, inviter, inviterName);
        }
    }

    public enum DungeonActionKind : byte
    {
        /// <summary>Entrer dans le donjon proposé par le PNJ ciblé. Le groupe entier entre s'il est à proximité.</summary>
        Enter = 1,
    }

    public readonly struct DungeonActionMessage : IWireMessage
    {
        /// <summary>
        /// <paramref name="adventureId"/> : 0 = porte simple (utilise le <c>DungeonZone</c> fixe du PNJ, cf.
        /// Crypt) ; non-zéro = aventure choisie au tableau des aventures de ce PNJ (cf. <see cref="AdventureSummary"/>).
        /// </summary>
        public DungeonActionMessage(DungeonActionKind kind, uint npcEntityId, ushort adventureId = 0)
        {
            Kind = kind;
            NpcEntityId = npcEntityId;
            AdventureId = adventureId;
        }

        public DungeonActionKind Kind { get; }

        public uint NpcEntityId { get; }

        public ushort AdventureId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte((byte)Kind);
            writer.WriteUInt32(NpcEntityId);
            writer.WriteUInt16(AdventureId);
        }

        public static DungeonActionMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte kind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(DungeonActionKind), kind))
            {
                throw new ProtocolException($"Action de donjon inconnue : {kind}.");
            }

            uint npc = reader.ReadUInt32();
            ushort adventureId = reader.ReadUInt16();
            reader.ExpectEnd();
            return new DungeonActionMessage((DungeonActionKind)kind, npc, adventureId);
        }
    }
}
