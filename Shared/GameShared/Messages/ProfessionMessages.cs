using System;
using System.Collections.Generic;
using GameShared.Protocol;

namespace GameShared.Messages
{
    public enum ProfessionActionKind : byte
    {
        /// <summary>Récolter un nœud. <c>TargetEntityId</c> porte l'entité du nœud.</summary>
        Gather = 1,
        /// <summary>Fabriquer une recette. <c>RecipeId</c> porte l'identifiant de la recette.</summary>
        Craft = 2,
    }

    /// <summary>Action de métier. <c>TargetEntityId</c> pour Gather, <c>RecipeId</c> pour Craft.</summary>
    public readonly struct ProfessionActionMessage : IWireMessage
    {
        public ProfessionActionMessage(ProfessionActionKind kind, uint targetEntityId, ushort recipeId)
        {
            Kind = kind;
            TargetEntityId = targetEntityId;
            RecipeId = recipeId;
        }

        public ProfessionActionKind Kind { get; }

        public uint TargetEntityId { get; }

        public ushort RecipeId { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte((byte)Kind);
            writer.WriteUInt32(TargetEntityId);
            writer.WriteUInt16(RecipeId);
        }

        public static ProfessionActionMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte kind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(ProfessionActionKind), kind))
            {
                throw new ProtocolException($"Action de métier inconnue : {kind}.");
            }

            uint target = reader.ReadUInt32();
            ushort recipeId = reader.ReadUInt16();
            reader.ExpectEnd();
            return new ProfessionActionMessage((ProfessionActionKind)kind, target, recipeId);
        }
    }

    /// <summary>Métiers connus du joueur, avec compétence courante et maximale de chacun.</summary>
    public sealed class ProfessionsMessage : IWireMessage
    {
        public const int MaxProfessions = 8;
        public const int MaxNameByteLength = 32;

        public ProfessionsMessage(IReadOnlyList<(byte ProfessionId, string Name, int Skill, int MaxSkill)> known)
        {
            Known = known;
        }

        public IReadOnlyList<(byte ProfessionId, string Name, int Skill, int MaxSkill)> Known { get; }

        public void Write(PacketWriter writer)
        {
            if (Known.Count > MaxProfessions)
            {
                throw new ArgumentException("Trop de métiers connus.");
            }

            writer.WriteByte((byte)Known.Count);
            foreach ((byte professionId, string name, int skill, int maxSkill) in Known)
            {
                writer.WriteByte(professionId);
                writer.WriteString(name, MaxNameByteLength);
                writer.WriteInt32(skill);
                writer.WriteInt32(maxSkill);
            }
        }

        public static ProfessionsMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            int count = reader.ReadByte();
            if (count > MaxProfessions)
            {
                throw new ProtocolException("Trop de métiers connus.");
            }

            var known = new List<(byte ProfessionId, string Name, int Skill, int MaxSkill)>(count);
            for (int i = 0; i < count; i++)
            {
                byte professionId = reader.ReadByte();
                string name = reader.ReadString(MaxNameByteLength);
                int skill = reader.ReadInt32();
                int maxSkill = reader.ReadInt32();
                known.Add((professionId, name, skill, maxSkill));
            }

            reader.ExpectEnd();
            return new ProfessionsMessage(known);
        }
    }
}
