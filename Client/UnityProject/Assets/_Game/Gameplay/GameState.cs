using System.Collections.Generic;
using GameShared.Messages;

namespace MiniWow.Client.Gameplay
{
    /// <summary>
    /// État du joueur tel que reçu du serveur. Ce script ne calcule rien qui fasse foi : il stocke et affiche.
    /// </summary>
    public sealed class GameState
    {
        public const int MaxChatLines = 60;

        /// <summary>Zone courante (Hub, Aventure, donjon, ...), vide tant qu'aucun <c>ZoneInfo</c> n'est reçu.</summary>
        public string ZoneId { get; set; } = string.Empty;

        public ushort Level { get; set; } = 1;
        public uint Experience { get; set; }
        public uint ExperienceToNext { get; set; }
        public long Copper { get; set; }

        /// <summary>Personnages du compte connecté (écran de sélection), reçus via <see cref="ApplyCharacterList"/>.</summary>
        public readonly List<(long CharacterId, string Name, byte ClassId, ushort Level)> Characters =
            new List<(long CharacterId, string Name, byte ClassId, ushort Level)>();

        /// <summary>Dernier message d'échec de connexion/création, affiché par l'écran correspondant. Vide sinon.</summary>
        public string LoginError { get; set; } = string.Empty;
        public string RegisterError { get; set; } = string.Empty;
        public string CreateCharacterError { get; set; } = string.Empty;

        public void ApplyCharacterList(CharacterListMessage message)
        {
            Characters.Clear();
            Characters.AddRange(message.Characters);
        }

        public readonly ushort[] BagItems = new ushort[InventoryMessage.BagSize];
        public readonly ushort[] BagQuantities = new ushort[InventoryMessage.BagSize];
        public readonly ushort[] Equipment = new ushort[InventoryMessage.EquipmentSize];

        public readonly List<QuestView> ActiveQuests = new List<QuestView>();
        public readonly HashSet<ushort> CompletedQuests = new HashSet<ushort>();

        /// <summary>Métiers connus du joueur, avec compétence courante et maximale.</summary>
        public readonly List<(byte ProfessionId, string Name, int Skill, int MaxSkill)> Professions =
            new List<(byte ProfessionId, string Name, int Skill, int MaxSkill)>();

        public void ApplyProfessions(ProfessionsMessage message)
        {
            Professions.Clear();
            Professions.AddRange(message.Known);
        }

        public uint PartyLeader { get; set; }
        public readonly List<(uint EntityId, string Name)> PartyMembers = new List<(uint EntityId, string Name)>();
        public uint InviterId { get; set; }
        public string InviterName { get; set; } = string.Empty;

        /// <summary>Dialogue du PNJ ouvert, ou null.</summary>
        public NpcMessage Dialogue { get; set; }

        public readonly List<string> ChatLines = new List<string>();

        /// <summary>Lancer en cours, pour la barre de lancement.</summary>
        public ushort CastingSkillId { get; set; }
        public float CastRemainingSeconds { get; set; }
        public float CastTotalSeconds { get; set; }

        /// <summary>Annonce de l'onde de choc d'un boss, avant impact.</summary>
        public float TelegraphRemainingSeconds { get; set; }

        public void AddChat(string line)
        {
            ChatLines.Add(line);
            while (ChatLines.Count > MaxChatLines)
            {
                ChatLines.RemoveAt(0);
            }
        }

        public void ApplyInventory(InventoryMessage message)
        {
            for (int i = 0; i < InventoryMessage.BagSize; i++)
            {
                BagItems[i] = message.Bag[i].ItemId;
                BagQuantities[i] = message.Bag[i].Quantity;
            }

            for (int i = 0; i < InventoryMessage.EquipmentSize; i++)
            {
                Equipment[i] = message.Equipment[i];
            }
        }

        public int CountInBag(int itemId)
        {
            int total = 0;
            for (int i = 0; i < BagItems.Length; i++)
            {
                if (BagItems[i] == itemId)
                {
                    total += BagQuantities[i];
                }
            }

            return total;
        }

        public void ApplyQuestLog(QuestLogMessage log)
        {
            ActiveQuests.Clear();
            foreach ((ushort questId, string title, IReadOnlyList<QuestObjectiveView> objectives) in log.Active)
            {
                ActiveQuests.Add(new QuestView(questId, title, objectives));
            }

            CompletedQuests.Clear();
            foreach (ushort id in log.Completed)
            {
                CompletedQuests.Add(id);
            }
        }

        public void ApplyParty(PartyMessage party)
        {
            PartyLeader = party.LeaderEntityId;
            PartyMembers.Clear();
            PartyMembers.AddRange(party.Members);
            InviterId = party.InviterEntityId;
            InviterName = party.InviterName;
        }

        public int Strength { get; private set; }
        public int Agility { get; private set; }
        public int Intelligence { get; private set; }
        public int Stamina { get; private set; }
        public int Spirit { get; private set; }
        public float AttackPower { get; private set; }
        public float SpellPower { get; private set; }
        public float CriticalChance { get; private set; }
        public int Armor { get; private set; }

        public void ApplyStats(StatsMessage stats)
        {
            Level = stats.Level;
            Experience = stats.Experience;
            ExperienceToNext = stats.ExperienceToNext;
            Copper = stats.Copper;
            Strength = stats.Strength;
            Agility = stats.Agility;
            Intelligence = stats.Intelligence;
            Stamina = stats.Stamina;
            Spirit = stats.Spirit;
            AttackPower = stats.AttackPower;
            SpellPower = stats.SpellPower;
            CriticalChance = stats.CriticalChance;
            Armor = stats.Armor;
        }

        public readonly struct QuestView
        {
            public QuestView(ushort id, string title, IReadOnlyList<QuestObjectiveView> objectives)
            {
                Id = id;
                Title = title;
                Objectives = objectives;
            }

            public ushort Id { get; }

            public string Title { get; }

            public IReadOnlyList<QuestObjectiveView> Objectives { get; }
        }
    }
}
