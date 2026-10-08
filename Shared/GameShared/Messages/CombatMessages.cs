using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Demande d'attaque d'une cible. Le serveur valide portée, cooldown et état de la cible.</summary>
    public readonly struct AttackRequestMessage : IWireMessage
    {
        public AttackRequestMessage(uint targetEntityId)
        {
            TargetEntityId = targetEntityId;
        }

        public uint TargetEntityId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt32(TargetEntityId);
        }

        public static AttackRequestMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            uint target = reader.ReadUInt32();
            reader.ExpectEnd();
            return new AttackRequestMessage(target);
        }
    }

    public enum CombatEventKind : byte
    {
        Damage = 1,
        Death = 2,
        Respawn = 3,
        Heal = 4,
        /// <summary>Début d'un lancer. <c>Amount</c> = durée du lancer en millisecondes, <c>SourceEntityId</c> = lanceur.</summary>
        CastStart = 5,
        /// <summary>Télégraphe d'une attaque de boss : <c>Amount</c> = délai avant impact en millisecondes.</summary>
        Telegraph = 6,
        /// <summary>Événement de donjon : <c>Amount</c> = identifiant de l'événement (voir le client).</summary>
        Dungeon = 7,
    }

    /// <summary>Entrée dans le monde : classe choisie (1 octet).</summary>
    public readonly struct EnterWorldMessage : IWireMessage
    {
        public EnterWorldMessage(byte classId)
        {
            ClassId = classId;
        }

        public byte ClassId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte(ClassId);
        }

        public static EnterWorldMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte classId = reader.ReadByte();
            reader.ExpectEnd();
            return new EnterWorldMessage(classId);
        }
    }

    /// <summary>Demande de lancer une compétence sur une cible (0 = soi-même).</summary>
    public readonly struct CastSkillMessage : IWireMessage
    {
        public CastSkillMessage(ushort skillId, uint targetEntityId)
        {
            SkillId = skillId;
            TargetEntityId = targetEntityId;
        }

        public ushort SkillId { get; }

        public uint TargetEntityId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt16(SkillId);
            writer.WriteUInt32(TargetEntityId);
        }

        public static CastSkillMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            ushort skill = reader.ReadUInt16();
            uint target = reader.ReadUInt32();
            reader.ExpectEnd();
            return new CastSkillMessage(skill, target);
        }
    }

    /// <summary>Événement de combat diffusé aux clients qui voient la source ou la cible.</summary>
    public readonly struct CombatEventMessage : IWireMessage
    {
        public CombatEventMessage(CombatEventKind kind, uint sourceEntityId, uint targetEntityId, int amount)
        {
            Kind = kind;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            Amount = amount;
        }

        public CombatEventKind Kind { get; }

        public uint SourceEntityId { get; }

        public uint TargetEntityId { get; }

        /// <summary>Dégâts infligés (Damage) ou 0.</summary>
        public int Amount { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte((byte)Kind);
            writer.WriteUInt32(SourceEntityId);
            writer.WriteUInt32(TargetEntityId);
            writer.WriteInt32(Amount);
        }

        public static CombatEventMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte rawKind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(CombatEventKind), rawKind))
            {
                throw new ProtocolException($"Type d'événement de combat inconnu : {rawKind}.");
            }

            uint source = reader.ReadUInt32();
            uint target = reader.ReadUInt32();
            int amount = reader.ReadInt32();
            reader.ExpectEnd();

            return new CombatEventMessage((CombatEventKind)rawKind, source, target, amount);
        }
    }
}
