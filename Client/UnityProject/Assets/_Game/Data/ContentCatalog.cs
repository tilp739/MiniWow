using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniWow.Client.Data
{
    /// <summary>
    /// Données de jeu lues depuis les copies JSON du serveur (Resources/Content). Une seule source : le serveur.
    /// Les DTO sont volontairement plats et publics : JsonUtility l'exige.
    /// </summary>
    public static class ContentCatalog
    {
        public static readonly Dictionary<int, ClassEntry> Classes = new Dictionary<int, ClassEntry>();
        public static readonly Dictionary<int, SkillEntry> Skills = new Dictionary<int, SkillEntry>();
        public static readonly Dictionary<int, ItemEntry> Items = new Dictionary<int, ItemEntry>();
        public static readonly Dictionary<int, NpcEntry> Npcs = new Dictionary<int, NpcEntry>();
        public static readonly Dictionary<int, QuestEntry> Quests = new Dictionary<int, QuestEntry>();
        public static readonly Dictionary<int, ProfessionEntry> Professions = new Dictionary<int, ProfessionEntry>();
        public static readonly Dictionary<int, RecipeEntry> Recipes = new Dictionary<int, RecipeEntry>();
        public static readonly Dictionary<int, GatheringNodeEntry> GatheringNodes = new Dictionary<int, GatheringNodeEntry>();

        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            foreach (ClassEntry entry in Read<ClassFile>("Content/classes").classes ?? Array.Empty<ClassEntry>())
            {
                Classes[ClassIdOf(entry.id)] = entry;
            }

            foreach (SkillEntry entry in Read<SkillFile>("Content/skills").skills ?? Array.Empty<SkillEntry>())
            {
                Skills[entry.id] = entry;
            }

            foreach (ItemEntry entry in Read<ItemFile>("Content/items").items ?? Array.Empty<ItemEntry>())
            {
                Items[entry.id] = entry;
            }

            foreach (NpcEntry entry in Read<NpcFile>("Content/npcs").npcs ?? Array.Empty<NpcEntry>())
            {
                Npcs[entry.id] = entry;
            }

            foreach (QuestEntry entry in Read<QuestFile>("Content/quests").quests ?? Array.Empty<QuestEntry>())
            {
                Quests[entry.id] = entry;
            }

            foreach (ProfessionEntry entry in Read<ProfessionFile>("Content/professions").professions ?? Array.Empty<ProfessionEntry>())
            {
                Professions[entry.id] = entry;
            }

            foreach (RecipeEntry entry in Read<RecipeFile>("Content/recipes").recipes ?? Array.Empty<RecipeEntry>())
            {
                Recipes[entry.id] = entry;
            }

            foreach (GatheringNodeEntry entry in Read<GatheringNodeFile>("Content/gathering_nodes").gatheringNodes ?? Array.Empty<GatheringNodeEntry>())
            {
                GatheringNodes[entry.id] = entry;
            }
        }

        /// <summary>Identifiant numérique d'une classe, selon l'enum du serveur (Warrior = 1, Mage = 2, Priest = 3).</summary>
        public static int ClassIdOf(string className)
        {
            switch (className)
            {
                case "Warrior": return 1;
                case "Mage": return 2;
                case "Priest": return 3;
                default: return 0;
            }
        }

        public static string ItemName(int itemId) => Items.TryGetValue(itemId, out ItemEntry item) ? item.name : $"Objet {itemId}";

        /// <summary>Nom de l'objet, encadré d'une balise de couleur IMGUI (&lt;color=#RRGGBB&gt;) selon sa rareté.</summary>
        public static string ItemNameColored(int itemId)
        {
            if (!Items.TryGetValue(itemId, out ItemEntry item))
            {
                return $"Objet {itemId}";
            }

            return $"<color=#{RarityColorHex(item.rarity)}>{item.name}</color>";
        }

        /// <summary>Couleur (sans #) associée à une rareté, même palette qu'un MMORPG classique.</summary>
        public static string RarityColorHex(string rarity) => rarity switch
        {
            "Poor" => "9d9d9d",
            "Uncommon" => "1eff00",
            "Rare" => "0070dd",
            "Epic" => "a335ee",
            "Legendary" => "ff8000",
            _ => "ffffff", // Common (ou rareté absente du JSON)
        };

        /// <summary>
        /// Index dans GameState.Equipment / InventoryMessage pour un nom de slot du JSON (ex. "Head" -> 0).
        /// Même ordre que GameServer.Domain.Items.EquipSlot (fixe, ne jamais réordonner). -1 si inconnu/vide.
        /// </summary>
        public static int EquipSlotIndex(string slotName) => slotName switch
        {
            "Head" => 0,
            "Chest" => 1,
            "Hands" => 2,
            "Legs" => 3,
            "Feet" => 4,
            "MainHand" => 5,
            "OffHand" => 6,
            "Finger1" => 7,
            "Finger2" => 8,
            "Neck" => 9,
            "Shoulder" => 10,
            "Back" => 11,
            "Wrist" => 12,
            "Waist" => 13,
            "Trinket1" => 14,
            "Trinket2" => 15,
            "Ranged" => 16,
            "Shirt" => 17,
            "Tabard" => 18,
            _ => -1,
        };

        /// <summary>Texte (multi-lignes, balises IMGUI &lt;color&gt;) d'une infobulle d'objet : nom, slot, stats, prix.</summary>
        public static string ItemTooltip(int itemId)
        {
            if (!Items.TryGetValue(itemId, out ItemEntry item))
            {
                return $"Objet {itemId}";
            }

            var lines = new System.Text.StringBuilder();
            lines.Append($"<color=#{RarityColorHex(item.rarity)}>{item.name}</color>");

            if (!string.IsNullOrEmpty(item.slot))
            {
                lines.Append($"\n<color=#888888>{item.slot}</color>");
            }

            if (item.armor > 0) lines.Append($"\n{item.armor} Armure");
            if (item.strength > 0) lines.Append($"\n+{item.strength} Force");
            if (item.agility > 0) lines.Append($"\n+{item.agility} Agilité");
            if (item.intelligence > 0) lines.Append($"\n+{item.intelligence} Intelligence");
            if (item.stamina > 0) lines.Append($"\n+{item.stamina} Endurance");
            if (item.spirit > 0) lines.Append($"\n+{item.spirit} Esprit");

            if (item.sellPrice > 0)
            {
                lines.Append($"\n<color=#888888>Vente : {item.sellPrice} cuivre</color>");
            }

            return lines.ToString();
        }

        public static string NpcName(int npcId) => Npcs.TryGetValue(npcId, out NpcEntry npc) ? npc.name : "PNJ";

        public static string QuestTitle(int questId) => Quests.TryGetValue(questId, out QuestEntry quest) ? quest.title : $"Quête {questId}";

        /// <summary>Un PNJ qui propose un donjon, pour l'affichage du portail.</summary>
        public static bool IsDungeonGate(int npcId) => Npcs.TryGetValue(npcId, out NpcEntry npc) && !string.IsNullOrEmpty(npc.dungeonZone);

        public static string ProfessionName(int professionId) =>
            Professions.TryGetValue(professionId, out ProfessionEntry profession) ? profession.name : $"Métier {professionId}";

        private static T Read<T>(string path) where T : new()
        {
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"Donnée introuvable : Resources/{path}. Lancez le build du serveur.");
                return new T();
            }

            return JsonUtility.FromJson<T>(asset.text) ?? new T();
        }

        // ----- DTO (champs publics, noms identiques au JSON du serveur) -----

        [Serializable]
        public sealed class ClassFile { public ClassEntry[] classes; }

        [Serializable]
        public sealed class ClassEntry
        {
            public string id;
            public string key;
            public string name;
            public string resource;
        }

        [Serializable]
        public sealed class SkillFile { public SkillEntry[] skills; }

        [Serializable]
        public sealed class SkillEntry
        {
            public int id;
            public string key;
            public string name;
            /// <summary>Champ « class » du JSON : nom de classe, à convertir avec <see cref="ClassIdOf"/>.</summary>
            public string @class;
            public string kind;
            public bool targetsEnemy;
            public float range;
            public float castSeconds;
            public float cooldownSeconds;
            public int resourceCost;
            public string resourceCostKind;
        }

        [Serializable]
        public sealed class ItemFile { public ItemEntry[] items; }

        [Serializable]
        public sealed class ItemEntry
        {
            public int id;
            public string key;
            public string name;
            public int maxStack;
            public string slot;
            public int sellPrice;
            /// <summary>Poor/Common/Uncommon/Rare/Epic/Legendary. Vide (JSON sans "rarity") = Common.</summary>
            public string rarity;
            public int armor;
            public int strength;
            public int agility;
            public int intelligence;
            public int stamina;
            public int spirit;
        }

        [Serializable]
        public sealed class NpcFile { public NpcEntry[] npcs; }

        [Serializable]
        public sealed class NpcEntry
        {
            public int id;
            public string key;
            public string name;
            public string text;
            public float x;
            public float z;
            public int[] offeredQuests;
            public ShopLine[] shop;
            public string dungeonZone;
        }

        [Serializable]
        public sealed class ShopLine
        {
            public int itemId;
            public int price;
        }

        [Serializable]
        public sealed class QuestFile { public QuestEntry[] quests; }

        [Serializable]
        public sealed class QuestEntry
        {
            public int id;
            public string key;
            public string title;
            public int giverNpcId;
            public int turnInNpcId;
            public ObjectiveLine[] objectives;
        }

        [Serializable]
        public sealed class ObjectiveLine
        {
            public string kind;
            public int targetId;
            public int count;
        }

        [Serializable]
        public sealed class ProfessionFile { public ProfessionEntry[] professions; }

        [Serializable]
        public sealed class ProfessionEntry
        {
            public int id;
            public string key;
            public string name;
            public int maxSkill;
        }

        [Serializable]
        public sealed class RecipeFile { public RecipeEntry[] recipes; }

        [Serializable]
        public sealed class RecipeEntry
        {
            public int id;
            public string key;
            public string name;
            public int professionId;
            public int requiredSkill;
            public int resultItemId;
            public int resultQuantity;
            public ReagentLine[] reagents;
        }

        [Serializable]
        public sealed class ReagentLine
        {
            public int itemId;
            public int quantity;
        }

        [Serializable]
        public sealed class GatheringNodeFile { public GatheringNodeEntry[] gatheringNodes; }

        [Serializable]
        public sealed class GatheringNodeEntry
        {
            public int id;
            public string key;
            public string name;
            public int professionId;
            public int requiredSkill;
            public int resultItemId;
            public int resultQuantityMin;
            public int resultQuantityMax;
            public float respawnSeconds;
        }
    }
}
