using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Message sérialisable dans le payload d'une trame.</summary>
    public interface IWireMessage
    {
        void Write(PacketWriter writer);
    }
}
