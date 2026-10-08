namespace GameShared.Protocol
{
    /// <summary>En-tête décodé d'une trame.</summary>
    public readonly struct FrameHeader
    {
        public FrameHeader(Opcode opcode, uint requestId)
        {
            Opcode = opcode;
            RequestId = requestId;
        }

        public Opcode Opcode { get; }

        /// <summary>0 si aucune réponse n'est attendue.</summary>
        public uint RequestId { get; }
    }
}
