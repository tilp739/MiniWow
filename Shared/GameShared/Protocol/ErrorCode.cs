namespace GameShared.Protocol
{
    /// <summary>Code d'erreur renvoyé au client dans un <c>Error</c>. Valeurs stables.</summary>
    public enum ErrorCode : ushort
    {
        ProtocolViolation = 1,
        UnsupportedVersion = 2,
        NotAuthenticated = 3,
        NotInWorld = 4,
        RateLimited = 5,
        WorldFull = 6,
        InvalidTarget = 7,
        OutOfRange = 8,
        OnCooldown = 9,
        Dead = 10,
        UnknownSkill = 11,
        WrongClass = 12,
        NotEnoughResource = 13,
        CastInterrupted = 14,
        InvalidArgument = 15,
        NotAllowed = 16,
        NoSuchItem = 17,
        InventoryFull = 18,
        NoSuchNpc = 19,
        TooFar = 20,
        QuestError = 21,
        PartyError = 22,
        ChatRejected = 23,
        NotEnoughCopper = 24,
    }
}
