using System;
using System.Collections.Generic;
using GameShared.Protocol;

namespace GameShared.Messages
{
    /// <summary>Dialogue d'un PNJ : texte, quêtes proposées ou à rendre, et boutique.</summary>
    public sealed class NpcMessage : IWireMessage
    {
        public const int MaxQuests = 8;
        public const int MaxShopItems = 16;
        public const int MaxTextByteLength = 256;
        public const int MaxNameByteLength = 32;

        public const int MaxProfessions = 8;
        public const int MaxAdventures = 8;
        public const int MaxAdventureNameByteLength = 32;

        public NpcMessage(
            uint npcEntityId,
            string name,
            string text,
            IReadOnlyList<ushort> offeredQuests,
            IReadOnlyList<ushort> turnInQuests,
            IReadOnlyList<(ushort ItemId, int Price)> shop,
            IReadOnlyList<byte> teachesProfessions,
            IReadOnlyList<AdventureSummary> adventures)
        {
            NpcEntityId = npcEntityId;
            Name = name;
            Text = text;
            OfferedQuests = offeredQuests;
            TurnInQuests = turnInQuests;
            Shop = shop;
            TeachesProfessions = teachesProfessions;
            Adventures = adventures;
        }

        public uint NpcEntityId { get; }

        public string Name { get; }

        public string Text { get; }

        public IReadOnlyList<ushort> OfferedQuests { get; }

        public IReadOnlyList<ushort> TurnInQuests { get; }

        public IReadOnlyList<(ushort ItemId, int Price)> Shop { get; }

        public IReadOnlyList<byte> TeachesProfessions { get; }

        /// <summary>Tableau des aventures proposées par ce PNJ (vide pour un PNJ ordinaire).</summary>
        public IReadOnlyList<AdventureSummary> Adventures { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt32(NpcEntityId);
            writer.WriteString(Name, MaxNameByteLength);
            writer.WriteString(Text, MaxTextByteLength);

            WriteIds(writer, OfferedQuests, MaxQuests);
            WriteIds(writer, TurnInQuests, MaxQuests);

            if (Shop.Count > MaxShopItems)
            {
                throw new ArgumentException("Boutique trop grande.");
            }

            writer.WriteByte((byte)Shop.Count);
            foreach ((ushort itemId, int price) in Shop)
            {
                writer.WriteUInt16(itemId);
                writer.WriteInt32(price);
            }

            if (TeachesProfessions.Count > MaxProfessions)
            {
                throw new ArgumentException("Trop de métiers enseignés.");
            }

            writer.WriteByte((byte)TeachesProfessions.Count);
            foreach (byte professionId in TeachesProfessions)
            {
                writer.WriteByte(professionId);
            }

            if (Adventures.Count > MaxAdventures)
            {
                throw new ArgumentException("Trop d'aventures proposées.");
            }

            writer.WriteByte((byte)Adventures.Count);
            foreach (AdventureSummary adventure in Adventures)
            {
                writer.WriteUInt16(adventure.Id);
                writer.WriteString(adventure.Name, MaxAdventureNameByteLength);
                writer.WriteByte(adventure.RecommendedLevel);
                writer.WriteByte(adventure.MinPlayers);
                writer.WriteByte(adventure.MaxPlayers);
                writer.WriteByte(adventure.Difficulty);
                writer.WriteByte(adventure.BossCount);
                writer.WriteByte(adventure.DungeonCount);
            }
        }

        public static NpcMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            uint npc = reader.ReadUInt32();
            string name = reader.ReadString(MaxNameByteLength);
            string text = reader.ReadString(MaxTextByteLength);
            List<ushort> offered = ReadIds(ref reader, MaxQuests);
            List<ushort> turnIn = ReadIds(ref reader, MaxQuests);

            int shopCount = reader.ReadByte();
            if (shopCount > MaxShopItems)
            {
                throw new ProtocolException("Boutique trop grande.");
            }

            var shop = new List<(ushort ItemId, int Price)>(shopCount);
            for (int i = 0; i < shopCount; i++)
            {
                ushort itemId = reader.ReadUInt16();
                int price = reader.ReadInt32();
                shop.Add((itemId, price));
            }

            int professionCount = reader.ReadByte();
            if (professionCount > MaxProfessions)
            {
                throw new ProtocolException("Trop de métiers enseignés.");
            }

            var professions = new List<byte>(professionCount);
            for (int i = 0; i < professionCount; i++)
            {
                professions.Add(reader.ReadByte());
            }

            int adventureCount = reader.ReadByte();
            if (adventureCount > MaxAdventures)
            {
                throw new ProtocolException("Trop d'aventures proposées.");
            }

            var adventures = new List<AdventureSummary>(adventureCount);
            for (int i = 0; i < adventureCount; i++)
            {
                ushort adventureId = reader.ReadUInt16();
                string adventureName = reader.ReadString(MaxAdventureNameByteLength);
                byte recommendedLevel = reader.ReadByte();
                byte minPlayers = reader.ReadByte();
                byte maxPlayers = reader.ReadByte();
                byte difficulty = reader.ReadByte();
                byte bossCount = reader.ReadByte();
                byte dungeonCount = reader.ReadByte();
                adventures.Add(new AdventureSummary(adventureId, adventureName, recommendedLevel, minPlayers, maxPlayers, difficulty, bossCount, dungeonCount));
            }

            reader.ExpectEnd();
            return new NpcMessage(npc, name, text, offered, turnIn, shop, professions, adventures);
        }

        private static void WriteIds(PacketWriter writer, IReadOnlyList<ushort> ids, int max)
        {
            if (ids.Count > max)
            {
                throw new ArgumentException("Liste de quêtes trop grande.");
            }

            writer.WriteByte((byte)ids.Count);
            foreach (ushort id in ids)
            {
                writer.WriteUInt16(id);
            }
        }

        // Passé par ref : PacketReader est un ref struct, une copie perdrait la position de lecture.
        private static List<ushort> ReadIds(ref PacketReader reader, int max)
        {
            int count = reader.ReadByte();
            if (count > max)
            {
                throw new ProtocolException("Liste de quêtes trop grande.");
            }

            var ids = new List<ushort>(count);
            for (int i = 0; i < count; i++)
            {
                ids.Add(reader.ReadUInt16());
            }

            return ids;
        }
    }

    public enum NpcActionKind : byte
    {
        Talk = 1,
        AcceptQuest = 2,
        TurnInQuest = 3,
        Buy = 4,
        Sell = 5,
        /// <summary>Apprendre un métier. <c>Argument</c> : identifiant du métier.</summary>
        LearnProfession = 6,
    }

    /// <summary>
    /// Action sur un PNJ. <c>Argument</c> : identifiant de quête (Accept/TurnIn), d'objet (Buy/Sell).
    /// <c>Quantity</c> : quantité pour Buy et Sell.
    /// </summary>
    public readonly struct NpcActionMessage : IWireMessage
    {
        public NpcActionMessage(NpcActionKind kind, uint npcEntityId, ushort argument, ushort quantity)
        {
            Kind = kind;
            NpcEntityId = npcEntityId;
            Argument = argument;
            Quantity = quantity;
        }

        public NpcActionKind Kind { get; }

        public uint NpcEntityId { get; }

        public ushort Argument { get; }

        public ushort Quantity { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteByte((byte)Kind);
            writer.WriteUInt32(NpcEntityId);
            writer.WriteUInt16(Argument);
            writer.WriteUInt16(Quantity);
        }

        public static NpcActionMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            byte kind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(NpcActionKind), kind))
            {
                throw new ProtocolException($"Action de PNJ inconnue : {kind}.");
            }

            uint npc = reader.ReadUInt32();
            ushort argument = reader.ReadUInt16();
            ushort quantity = reader.ReadUInt16();
            reader.ExpectEnd();
            return new NpcActionMessage((NpcActionKind)kind, npc, argument, quantity);
        }
    }

    /// <summary>Un objectif de quête tel que vu par le joueur.</summary>
    public readonly struct QuestObjectiveView
    {
        public QuestObjectiveView(byte type, ushort targetId, ushort current, ushort required)
        {
            Type = type;
            TargetId = targetId;
            Current = current;
            Required = required;
        }

        /// <summary>1 Kill, 2 Collect, 3 Talk, 4 GoTo.</summary>
        public byte Type { get; }

        public ushort TargetId { get; }

        public ushort Current { get; }

        public ushort Required { get; }
    }

    /// <summary>Quête active avec sa progression, et liste des quêtes déjà terminées.</summary>
    public sealed class QuestLogMessage : IWireMessage
    {
        public const int MaxActive = 16;
        public const int MaxObjectives = 8;
        public const int MaxTitleByteLength = 64;

        public QuestLogMessage(IReadOnlyList<(ushort QuestId, string Title, IReadOnlyList<QuestObjectiveView> Objectives)> active, IReadOnlyList<ushort> completed)
        {
            Active = active;
            Completed = completed;
        }

        public IReadOnlyList<(ushort QuestId, string Title, IReadOnlyList<QuestObjectiveView> Objectives)> Active { get; }

        public IReadOnlyList<ushort> Completed { get; }

        public void Write(PacketWriter writer)
        {
            if (Active.Count > MaxActive)
            {
                throw new ArgumentException("Trop de quêtes actives.");
            }

            writer.WriteByte((byte)Active.Count);
            foreach ((ushort questId, string title, IReadOnlyList<QuestObjectiveView> objectives) in Active)
            {
                writer.WriteUInt16(questId);
                writer.WriteString(title, MaxTitleByteLength);
                writer.WriteByte((byte)objectives.Count);
                foreach (QuestObjectiveView objective in objectives)
                {
                    writer.WriteByte(objective.Type);
                    writer.WriteUInt16(objective.TargetId);
                    writer.WriteUInt16(objective.Current);
                    writer.WriteUInt16(objective.Required);
                }
            }

            writer.WriteUInt16((ushort)Completed.Count);
            foreach (ushort id in Completed)
            {
                writer.WriteUInt16(id);
            }
        }

        public static QuestLogMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            int activeCount = reader.ReadByte();
            if (activeCount > MaxActive)
            {
                throw new ProtocolException("Trop de quêtes actives.");
            }

            var active = new List<(ushort QuestId, string Title, IReadOnlyList<QuestObjectiveView> Objectives)>(activeCount);
            for (int i = 0; i < activeCount; i++)
            {
                ushort questId = reader.ReadUInt16();
                string title = reader.ReadString(MaxTitleByteLength);
                int objectiveCount = reader.ReadByte();
                if (objectiveCount > MaxObjectives)
                {
                    throw new ProtocolException("Trop d'objectifs.");
                }

                var objectives = new List<QuestObjectiveView>(objectiveCount);
                for (int j = 0; j < objectiveCount; j++)
                {
                    byte type = reader.ReadByte();
                    ushort target = reader.ReadUInt16();
                    ushort current = reader.ReadUInt16();
                    ushort required = reader.ReadUInt16();
                    objectives.Add(new QuestObjectiveView(type, target, current, required));
                }

                active.Add((questId, title, objectives));
            }

            int completedCount = reader.ReadUInt16();
            var completed = new List<ushort>(completedCount);
            for (int i = 0; i < completedCount; i++)
            {
                completed.Add(reader.ReadUInt16());
            }

            reader.ExpectEnd();
            return new QuestLogMessage(active, completed);
        }
    }

    /// <summary>
    /// Statistiques du joueur : niveau, expérience dans le niveau, seuil, monnaie, caractéristiques primaires
    /// (classe + équipement) et dérivées. De quoi afficher un panneau de personnage complet côté client.
    /// </summary>
    public readonly struct StatsMessage : IWireMessage
    {
        public StatsMessage(
            ushort level, uint experience, uint experienceToNext, long copper,
            int strength, int agility, int intelligence, int stamina, int spirit,
            float attackPower, float spellPower, float criticalChance, int armor)
        {
            Level = level;
            Experience = experience;
            ExperienceToNext = experienceToNext;
            Copper = copper;
            Strength = strength;
            Agility = agility;
            Intelligence = intelligence;
            Stamina = stamina;
            Spirit = spirit;
            AttackPower = attackPower;
            SpellPower = spellPower;
            CriticalChance = criticalChance;
            Armor = armor;
        }

        public ushort Level { get; }

        public uint Experience { get; }

        public uint ExperienceToNext { get; }

        public long Copper { get; }

        /// <summary>Caractéristiques primaires totales (classe au niveau courant + bonus d'équipement).</summary>
        public int Strength { get; }

        public int Agility { get; }

        public int Intelligence { get; }

        public int Stamina { get; }

        public int Spirit { get; }

        public float AttackPower { get; }

        public float SpellPower { get; }

        /// <summary>Chance de coup critique, en fraction (0.05 = 5%).</summary>
        public float CriticalChance { get; }

        public int Armor { get; }

        public void Write(PacketWriter writer)
        {
            writer.WriteUInt16(Level);
            writer.WriteUInt32(Experience);
            writer.WriteUInt32(ExperienceToNext);
            writer.WriteInt64(Copper);
            writer.WriteInt32(Strength);
            writer.WriteInt32(Agility);
            writer.WriteInt32(Intelligence);
            writer.WriteInt32(Stamina);
            writer.WriteInt32(Spirit);
            writer.WriteSingle(AttackPower);
            writer.WriteSingle(SpellPower);
            writer.WriteSingle(CriticalChance);
            writer.WriteInt32(Armor);
        }

        public static StatsMessage Read(ReadOnlySpan<byte> payload)
        {
            var reader = new PacketReader(payload);
            ushort level = reader.ReadUInt16();
            uint experience = reader.ReadUInt32();
            uint toNext = reader.ReadUInt32();
            long copper = reader.ReadInt64();
            int strength = reader.ReadInt32();
            int agility = reader.ReadInt32();
            int intelligence = reader.ReadInt32();
            int stamina = reader.ReadInt32();
            int spirit = reader.ReadInt32();
            float attackPower = reader.ReadSingle();
            float spellPower = reader.ReadSingle();
            float criticalChance = reader.ReadSingle();
            int armor = reader.ReadInt32();
            reader.ExpectEnd();
            return new StatsMessage(
                level, experience, toNext, copper,
                strength, agility, intelligence, stamina, spirit,
                attackPower, spellPower, criticalChance, armor);
        }
    }
}
