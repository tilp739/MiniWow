namespace GameServer.Domain.Spatial
{
    /// <summary>Entité positionnée dans le plan XZ, indexable par <see cref="InterestManager{T}"/>.</summary>
    public interface IPlacedEntity
    {
        uint EntityId { get; }

        float X { get; }

        float Z { get; }
    }
}
