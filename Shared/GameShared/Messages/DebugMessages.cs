using System;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>
    /// Demande de test : donne un objet par emplacement d'équipement (cf. <c>WorldSimulation.GiveTestGear</c>).
    /// Payload vide. Jamais de données sensibles ici : pas de risque à l'utiliser tel quel.
    /// </summary>
    public readonly struct DebugGiveTestGearMessage : IWireMessage
    {
        public void Write(PacketWriter writer)
        {
            // Payload vide.
        }

        public static DebugGiveTestGearMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            reader.ExpectEnd();
            return new DebugGiveTestGearMessage();
        }
    }
}
