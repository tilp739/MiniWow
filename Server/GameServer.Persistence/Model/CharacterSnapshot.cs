using System.Collections.Generic;

namespace GameServer.Persistence.Model
{
    /// <summary>Pile d'objets dans un emplacement de sac, telle que sauvegardée.</summary>
    public sealed class BagSlotSnapshot
    {
        public BagSlotSnapshot(int slot, ushort itemId, int quantity)
        {
            Slot = slot;
            ItemId = itemId;
            Quantity = quantity;
        }

        public int Slot { get; }

        public ushort ItemId { get; }

        public int Quantity { get; }
    }

    /// <summary>Objet équipé dans un emplacement, tel que sauvegardé.</summary>
    public sealed class EquippedItemSnapshot
    {
        public EquippedItemSnapshot(byte equipSlot, ushort itemId, int quantity)
        {
            EquipSlot = equipSlot;
            ItemId = itemId;
            Quantity = quantity;
        }

        /// <summary>Valeur numérique de <c>GameServer.Domain.Items.EquipSlot</c>.</summary>
        public byte EquipSlot { get; }

        public ushort ItemId { get; }

        public int Quantity { get; }
    }

    /// <summary>Sac et équipement, sous une forme neutre (sans dépendre d'un catalogue d'objets).</summary>
    public sealed class InventorySnapshot
    {
        public InventorySnapshot(IReadOnlyList<BagSlotSnapshot> bag, IReadOnlyList<EquippedItemSnapshot> equipment)
        {
            Bag = bag;
            Equipment = equipment;
        }

        public static InventorySnapshot Empty { get; } =
            new InventorySnapshot(System.Array.Empty<BagSlotSnapshot>(), System.Array.Empty<EquippedItemSnapshot>());

        public IReadOnlyList<BagSlotSnapshot> Bag { get; }

        public IReadOnlyList<EquippedItemSnapshot> Equipment { get; }
    }

    /// <summary>Progression d'une quête active, telle que sauvegardée.</summary>
    public sealed class ActiveQuestSnapshot
    {
        public ActiveQuestSnapshot(ushort questId, IReadOnlyList<int> progress)
        {
            QuestId = questId;
            Progress = progress;
        }

        public ushort QuestId { get; }

        public IReadOnlyList<int> Progress { get; }
    }

    /// <summary>Journal de quêtes, sous une forme neutre (sans dépendre du catalogue de quêtes).</summary>
    public sealed class QuestLogSnapshot
    {
        public QuestLogSnapshot(IReadOnlyList<ActiveQuestSnapshot> active, IReadOnlyList<ushort> completed)
        {
            Active = active;
            Completed = completed;
        }

        public static QuestLogSnapshot Empty { get; } =
            new QuestLogSnapshot(System.Array.Empty<ActiveQuestSnapshot>(), System.Array.Empty<ushort>());

        public IReadOnlyList<ActiveQuestSnapshot> Active { get; }

        public IReadOnlyList<ushort> Completed { get; }
    }

    /// <summary>Métier connu et sa compétence, tel que sauvegardé.</summary>
    public sealed class ProfessionSnapshot
    {
        public ProfessionSnapshot(byte professionId, int skill)
        {
            ProfessionId = professionId;
            Skill = skill;
        }

        public byte ProfessionId { get; }

        public int Skill { get; }
    }

    /// <summary>
    /// État complet d'un personnage, tel qu'échangé avec la base. Ne contient que des types primitifs :
    /// aucune dépendance au catalogue de contenu (objets/quêtes), qui n'existe que côté simulation.
    /// </summary>
    public sealed class CharacterSnapshot
    {
        public CharacterSnapshot(
            long characterId,
            long accountId,
            string name,
            byte classId,
            int level,
            long experience,
            long copper,
            string zone,
            float x,
            float z,
            InventorySnapshot inventory,
            QuestLogSnapshot quests,
            IReadOnlyList<ProfessionSnapshot> professions,
            byte raceId = 0)
        {
            CharacterId = characterId;
            AccountId = accountId;
            Name = name;
            ClassId = classId;
            Level = level;
            Experience = experience;
            Copper = copper;
            Zone = zone;
            X = x;
            Z = z;
            Inventory = inventory;
            Quests = quests;
            Professions = professions;
            RaceId = raceId;
        }

        public long CharacterId { get; }

        public long AccountId { get; }

        public string Name { get; }

        public byte ClassId { get; }

        /// <summary>Race jouable (purement cosmétique, cf. <c>GameShared.Messages.CharacterRace</c>).</summary>
        public byte RaceId { get; }

        public int Level { get; }

        public long Experience { get; }

        public long Copper { get; }

        public string Zone { get; }

        public float X { get; }

        public float Z { get; }

        public InventorySnapshot Inventory { get; }

        public QuestLogSnapshot Quests { get; }

        public IReadOnlyList<ProfessionSnapshot> Professions { get; }
    }
}
