namespace GameShared.Protocol
{
    /// <summary>
    /// Identifiant unique de chaque message. Les valeurs sont stables : ne jamais les réutiliser.
    /// </summary>
    public enum Opcode : ushort
    {
        // Handshake
        Hello = 0x0001,          // client → serveur
        HelloAck = 0x0002,       // serveur → client
        Error = 0x0003,          // serveur → client

        // Monde
        EnterWorld = 0x0010,     // client → serveur, payload vide
        MoveInput = 0x0011,      // client → serveur
        WorldSnapshot = 0x0012,  // serveur → client
        AttackRequest = 0x0013,  // client → serveur
        CastSkill = 0x0014,      // client → serveur
        CombatEvent = 0x0020,    // serveur → client
        Chat = 0x0030,           // serveur → client : ligne de chat reçue
        ChatSend = 0x0031,       // client → serveur : message à envoyer
        Npc = 0x0040,            // serveur → client : dialogue et boutique
        NpcAction = 0x0041,      // client → serveur
        Quests = 0x0042,         // serveur → client : journal de quêtes
        Inventory = 0x0050,      // serveur → client : contenu complet
        InventoryAction = 0x0051,// client → serveur
        LootRequest = 0x0052,    // client → serveur
        Party = 0x0060,          // serveur → client : état du groupe
        PartyAction = 0x0061,    // client → serveur
        DungeonAction = 0x0062,  // client → serveur
        AdminInfo = 0x0070,      // serveur → client : réponse à une commande admin
        Stats = 0x0071,          // serveur → client : statistiques du joueur
        ZoneInfo = 0x0072,       // serveur → client : zone courante (changements d'instance compris)

        // Comptes et personnages (EnterWorld/0x0010 n'est plus émis, mais sa valeur n'est pas réutilisée)
        Register = 0x0080,            // client → serveur
        RegisterResult = 0x0081,       // serveur → client
        Login = 0x0082,                // client → serveur
        LoginResult = 0x0083,          // serveur → client
        CharacterList = 0x0084,        // serveur → client
        CreateCharacter = 0x0085,      // client → serveur
        CreateCharacterResult = 0x0086,// serveur → client
        EnterWorldByCharacter = 0x0087,// client → serveur

        // Métiers
        ProfessionAction = 0x0090,     // client → serveur : récolter ou fabriquer
        Professions = 0x0091,          // serveur → client : métiers connus et compétence

        // Debug / tests (jamais en production, mais pas de secret à protéger : utilisable tel quel)
        DebugGiveTestGear = 0x00A0,    // client → serveur, payload vide : un objet de test par emplacement d'équipement
    }
}
