using System;
using System.Collections.Generic;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Nature d'une entité du monde.</summary>
    public enum EntityKind : byte
    {
        /// <summary>Joueur : <c>TemplateId</c> porte sa race (<see cref="CharacterRace"/>), purement cosmétique.</summary>
        Player = 1,
        Creature = 2,
        /// <summary>PNJ : <c>TemplateId</c> porte l'identifiant du PNJ.</summary>
        Npc = 3,
        /// <summary>Nœud de récolte : <c>TemplateId</c> porte l'identifiant du nœud, <c>Health</c> 0/1 = épuisé/disponible.</summary>
        GatheringNode = 4,
    }

    /// <summary>
    /// État d'une entité à un tick donné. Le monde est plat : Y = 0.
    /// <see cref="TemplateId"/> identifie le modèle de créature/PNJ/nœud ; pour un joueur, porte sa race.
    /// </summary>
    public readonly struct EntityState
    {
        public EntityState(
            uint entityId,
            EntityKind kind,
            ushort templateId,
            float x,
            float z,
            float yawDegrees,
            ushort health,
            ushort maxHealth,
            ushort resource = 0,
            ushort maxResource = 0,
            ushort level = 1)
        {
            EntityId = entityId;
            Kind = kind;
            TemplateId = templateId;
            X = x;
            Z = z;
            YawDegrees = yawDegrees;
            Health = health;
            MaxHealth = maxHealth;
            Resource = resource;
            MaxResource = maxResource;
            Level = level;
        }

        /// <summary>Ressource de classe (mana ou colère). Zéro pour les créatures.</summary>
        public ushort Resource { get; }

        public ushort MaxResource { get; }

        public ushort Level { get; }

        public uint EntityId { get; }

        public EntityKind Kind { get; }

        public ushort TemplateId { get; }

        public float X { get; }

        public float Z { get; }

        public float YawDegrees { get; }

        public ushort Health { get; }

        public ushort MaxHealth { get; }
    }

    /// <summary>
    /// Snapshot du monde envoyé à chaque tick, limité aux entités visibles par le destinataire.
    /// <see cref="LastProcessedInput"/> indique la dernière entrée de son propre personnage que le serveur a appliquée.
    /// </summary>
    public sealed class WorldSnapshotMessage : IWireMessage
    {
        public const int MaxEntities = 256;

        public WorldSnapshotMessage(uint serverTick, uint yourEntityId, uint lastProcessedInput, IReadOnlyList<EntityState> entities)
        {
            ServerTick = serverTick;
            YourEntityId = yourEntityId;
            LastProcessedInput = lastProcessedInput;
            Entities = entities;
        }

        public uint ServerTick { get; }

        public uint YourEntityId { get; }

        public uint LastProcessedInput { get; }

        public IReadOnlyList<EntityState> Entities { get; }

        public void Write(PacketWriter writer)
        {
            if (Entities.Count > MaxEntities)
            {
                throw new ArgumentException($"Trop d'entités dans le snapshot : {Entities.Count} > {MaxEntities}.");
            }

            writer.WriteUInt32(ServerTick);
            writer.WriteUInt32(YourEntityId);
            writer.WriteUInt32(LastProcessedInput);
            writer.WriteUInt16((ushort)Entities.Count);

            for (int i = 0; i < Entities.Count; i++)
            {
                EntityState entity = Entities[i];
                writer.WriteUInt32(entity.EntityId);
                writer.WriteByte((byte)entity.Kind);
                writer.WriteUInt16(entity.TemplateId);
                writer.WriteSingle(entity.X);
                writer.WriteSingle(entity.Z);
                writer.WriteSingle(entity.YawDegrees);
                writer.WriteUInt16(entity.Health);
                writer.WriteUInt16(entity.MaxHealth);
                writer.WriteUInt16(entity.Resource);
                writer.WriteUInt16(entity.MaxResource);
                writer.WriteUInt16(entity.Level);
            }
        }

        public static WorldSnapshotMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            uint serverTick = reader.ReadUInt32();
            uint yourEntityId = reader.ReadUInt32();
            uint lastProcessedInput = reader.ReadUInt32();
            int count = reader.ReadUInt16();

            if (count > MaxEntities)
            {
                throw new ProtocolException($"Snapshot avec {count} entités (max {MaxEntities}).");
            }

            var entities = new List<EntityState>(count);
            for (int i = 0; i < count; i++)
            {
                uint entityId = reader.ReadUInt32();
                byte rawKind = reader.ReadByte();
                if (!Enum.IsDefined(typeof(EntityKind), rawKind))
                {
                    throw new ProtocolException($"Type d'entité inconnu : {rawKind}.");
                }

                ushort templateId = reader.ReadUInt16();
                float x = reader.ReadSingle();
                float z = reader.ReadSingle();
                float yaw = reader.ReadSingle();
                ushort health = reader.ReadUInt16();
                ushort maxHealth = reader.ReadUInt16();
                ushort resource = reader.ReadUInt16();
                ushort maxResource = reader.ReadUInt16();
                ushort level = reader.ReadUInt16();
                entities.Add(new EntityState(entityId, (EntityKind)rawKind, templateId, x, z, yaw, health, maxHealth, resource, maxResource, level));
            }

            reader.ExpectEnd();
            return new WorldSnapshotMessage(serverTick, yourEntityId, lastProcessedInput, entities);
        }
    }
}
