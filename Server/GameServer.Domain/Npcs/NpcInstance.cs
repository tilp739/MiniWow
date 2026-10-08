using System;
using GameServer.Domain.Spatial;

namespace GameServer.Domain.Npcs
{
    /// <summary>PNJ présent dans une zone. Immobile : seule sa définition change d'une zone à l'autre.</summary>
    public sealed class NpcInstance : IPlacedEntity
    {
        public NpcInstance(uint entityId, NpcDefinition definition)
        {
            EntityId = entityId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public uint EntityId { get; }

        public NpcDefinition Definition { get; }

        public float X => Definition.X;

        public float Z => Definition.Z;
    }
}
