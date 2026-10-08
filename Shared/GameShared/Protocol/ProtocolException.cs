using System;

namespace GameShared.Protocol
{
    /// <summary>
    /// Levée lorsqu'une trame ou un message est malformé.
    /// La connexion émettrice doit être fermée et l'incident journalisé.
    /// </summary>
    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }
}
