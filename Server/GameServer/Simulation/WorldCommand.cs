using GameServer.Persistence.Model;
using GameShared.Messages;

namespace GameServer.Simulation
{
    public enum WorldCommandKind : byte
    {
        Enter,
        EnterWithCharacter,
        Move,
        Attack,
        CastSkill,
        InventoryAction,
        Loot,
        NpcAction,
        PartyAction,
        DungeonAction,
        Chat,
        ProfessionAction,
        DebugGiveTestGear,
        Disconnect,
    }

    /// <summary>
    /// Commande produite par les connexions réseau et consommée par la boucle de simulation.
    /// Seul le thread de simulation modifie l'état du monde. Les champs inutiles à un type donné valent zéro.
    /// </summary>
    public readonly struct WorldCommand
    {
        private WorldCommand(
            WorldCommandKind kind,
            IWorldClient client,
            uint sequence = 0,
            sbyte directionX = 0,
            sbyte directionZ = 0,
            uint targetEntityId = 0,
            byte classId = 0,
            ushort skillId = 0,
            string? name = null,
            InventoryActionMessage action = default,
            NpcActionMessage npcAction = default,
            PartyActionMessage partyAction = default,
            DungeonActionMessage dungeonAction = default,
            ChatSendMessage chat = default,
            CharacterSnapshot? snapshot = null,
            ProfessionActionMessage professionAction = default)
        {
            ProfessionAction = professionAction;
            Snapshot = snapshot;
            Chat = chat;
            NpcAction = npcAction;
            PartyAction = partyAction;
            DungeonAction = dungeonAction;
            Kind = kind;
            Client = client;
            Sequence = sequence;
            DirectionX = directionX;
            DirectionZ = directionZ;
            TargetEntityId = targetEntityId;
            ClassId = classId;
            SkillId = skillId;
            Name = name ?? string.Empty;
            Action = action;
        }

        public CharacterSnapshot? Snapshot { get; }

        public InventoryActionMessage Action { get; }

        public NpcActionMessage NpcAction { get; }

        public PartyActionMessage PartyAction { get; }

        public DungeonActionMessage DungeonAction { get; }

        public ChatSendMessage Chat { get; }

        public ProfessionActionMessage ProfessionAction { get; }

        public static WorldCommand ChatSend(IWorldClient client, ChatSendMessage message) =>
            new WorldCommand(WorldCommandKind.Chat, client, chat: message);

        public static WorldCommand Profession(IWorldClient client, ProfessionActionMessage action) =>
            new WorldCommand(WorldCommandKind.ProfessionAction, client, professionAction: action);

        public static WorldCommand Npc(IWorldClient client, NpcActionMessage action) =>
            new WorldCommand(WorldCommandKind.NpcAction, client, npcAction: action);

        public static WorldCommand Party(IWorldClient client, PartyActionMessage action) =>
            new WorldCommand(WorldCommandKind.PartyAction, client, partyAction: action);

        public static WorldCommand Dungeon(IWorldClient client, DungeonActionMessage action) =>
            new WorldCommand(WorldCommandKind.DungeonAction, client, dungeonAction: action);

        public WorldCommandKind Kind { get; }

        public IWorldClient Client { get; }

        public uint Sequence { get; }

        public sbyte DirectionX { get; }

        public sbyte DirectionZ { get; }

        public uint TargetEntityId { get; }

        public byte ClassId { get; }

        public ushort SkillId { get; }

        public string Name { get; }

        public static WorldCommand Enter(IWorldClient client, string name, byte classId) =>
            new WorldCommand(WorldCommandKind.Enter, client, classId: classId, name: name);

        /// <summary>Entrée dans le monde avec un personnage persistant déjà chargé depuis la base.</summary>
        public static WorldCommand EnterWithCharacter(IWorldClient client, CharacterSnapshot snapshot) =>
            new WorldCommand(WorldCommandKind.EnterWithCharacter, client, snapshot: snapshot);

        public static WorldCommand Move(IWorldClient client, uint sequence, sbyte directionX, sbyte directionZ) =>
            new WorldCommand(WorldCommandKind.Move, client, sequence, directionX, directionZ);

        public static WorldCommand Attack(IWorldClient client, uint targetEntityId) =>
            new WorldCommand(WorldCommandKind.Attack, client, targetEntityId: targetEntityId);

        public static WorldCommand Cast(IWorldClient client, ushort skillId, uint targetEntityId) =>
            new WorldCommand(WorldCommandKind.CastSkill, client, targetEntityId: targetEntityId, skillId: skillId);

        public static WorldCommand InventoryAction(IWorldClient client, InventoryActionMessage action) =>
            new WorldCommand(WorldCommandKind.InventoryAction, client, action: action);

        public static WorldCommand Loot(IWorldClient client, uint corpseEntityId) =>
            new WorldCommand(WorldCommandKind.Loot, client, targetEntityId: corpseEntityId);

        public static WorldCommand DebugGiveTestGear(IWorldClient client) =>
            new WorldCommand(WorldCommandKind.DebugGiveTestGear, client);

        public static WorldCommand Disconnect(IWorldClient client) =>
            new WorldCommand(WorldCommandKind.Disconnect, client);
    }
}
