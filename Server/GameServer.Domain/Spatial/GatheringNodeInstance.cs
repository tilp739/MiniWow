using System;
using GameServer.Domain.Professions;

namespace GameServer.Domain.Spatial
{
    /// <summary>Nœud de récolte présent dans une zone : disponible, ou en attente de réapparition après récolte.</summary>
    public sealed class GatheringNodeInstance : IPlacedEntity
    {
        public GatheringNodeInstance(uint entityId, GatheringNodeDefinition definition, float x, float z)
        {
            EntityId = entityId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            X = x;
            Z = z;
        }

        public uint EntityId { get; }

        public GatheringNodeDefinition Definition { get; }

        public float X { get; }

        public float Z { get; }

        public bool IsAvailable => RespawnRemaining <= 0f;

        public float RespawnRemaining { get; private set; }

        public void Tick(float deltaSeconds)
        {
            if (RespawnRemaining > 0f)
            {
                RespawnRemaining = Math.Max(0f, RespawnRemaining - deltaSeconds);
            }
        }

        public void Deplete()
        {
            RespawnRemaining = Definition.RespawnSeconds;
        }
    }
}
