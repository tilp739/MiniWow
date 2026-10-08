namespace GameShared.Protocol
{
    public static class ProtocolConstants
    {
        /// <summary>
        /// Version du protocole, échangée au handshake (Hello / HelloAck).
        /// À incrémenter uniquement lors d'un changement incompatible.
        /// </summary>
        public const ushort Version = 2;

        /// <summary>Taille du préfixe de longueur (u32 little-endian).</summary>
        public const int LengthPrefixSize = sizeof(uint);

        /// <summary>En-tête du corps : opcode (u16) + requestId (u32).</summary>
        public const int BodyHeaderSize = sizeof(ushort) + sizeof(uint);

        /// <summary>Taille maximale du corps d'une trame client → serveur.</summary>
        public const int MaxClientBodySize = 16 * 1024;

        /// <summary>Taille maximale du corps d'une trame serveur → client.</summary>
        public const int MaxServerBodySize = 64 * 1024;
    }
}
