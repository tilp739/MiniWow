using System.Collections.Generic;
using GameServer.Domain.Adventures;
using GameServer.Domain.Classes;
using GameServer.Domain.Creatures;
using GameServer.Domain.Items;
using GameServer.Domain.Npcs;
using GameServer.Domain.Professions;
using GameServer.Domain.Quests;
using GameServer.Domain.Skills;

namespace GameServer.Content
{
    /// <summary>Données de monde chargées au démarrage. Immuables pendant l'exécution.</summary>
    public sealed class WorldContent
    {
        public WorldContent(
            string zoneId,
            float playerSpawnX,
            float playerSpawnZ,
            IReadOnlyDictionary<ushort, CreatureTemplate> creatures,
            IReadOnlyList<CreatureSpawn> creatureSpawns,
            IReadOnlyDictionary<CharacterClass, ClassDefinition> classes,
            IReadOnlyDictionary<ushort, SkillDefinition> skills,
            IReadOnlyDictionary<ushort, ItemDefinition> items,
            IReadOnlyDictionary<ushort, NpcDefinition> npcs,
            IReadOnlyDictionary<ushort, QuestDefinition> quests,
            IReadOnlyDictionary<byte, ProfessionDefinition> professions,
            IReadOnlyDictionary<ushort, RecipeDefinition> recipes,
            IReadOnlyDictionary<ushort, GatheringNodeDefinition> gatheringNodes,
            IReadOnlyList<GatheringNodeSpawn> gatheringNodeSpawns,
            IReadOnlyDictionary<ushort, AdventureDefinition> adventures)
        {
            ZoneId = zoneId;
            PlayerSpawnX = playerSpawnX;
            PlayerSpawnZ = playerSpawnZ;
            Creatures = creatures;
            CreatureSpawns = creatureSpawns;
            Classes = classes;
            Skills = skills;
            Items = items;
            Npcs = npcs;
            Quests = quests;
            Professions = professions;
            Recipes = recipes;
            GatheringNodes = gatheringNodes;
            GatheringNodeSpawns = gatheringNodeSpawns;
            Adventures = adventures;
        }

        /// <summary>Catalogue des aventures proposables (cf. Content/adventures.json).</summary>
        public IReadOnlyDictionary<ushort, AdventureDefinition> Adventures { get; }

        public IReadOnlyDictionary<ushort, ItemDefinition> Items { get; }

        public IReadOnlyDictionary<ushort, NpcDefinition> Npcs { get; }

        public IReadOnlyDictionary<ushort, QuestDefinition> Quests { get; }

        public IReadOnlyDictionary<byte, ProfessionDefinition> Professions { get; }

        public IReadOnlyDictionary<ushort, RecipeDefinition> Recipes { get; }

        public IReadOnlyDictionary<ushort, GatheringNodeDefinition> GatheringNodes { get; }

        public IReadOnlyList<GatheringNodeSpawn> GatheringNodeSpawns { get; }

        public string ZoneId { get; }

        public float PlayerSpawnX { get; }

        public float PlayerSpawnZ { get; }

        public IReadOnlyDictionary<ushort, CreatureTemplate> Creatures { get; }

        public IReadOnlyList<CreatureSpawn> CreatureSpawns { get; }

        public IReadOnlyDictionary<CharacterClass, ClassDefinition> Classes { get; }

        public IReadOnlyDictionary<ushort, SkillDefinition> Skills { get; }
    }

    /// <summary>Point d'apparition d'une créature dans la zone.</summary>
    public readonly struct CreatureSpawn
    {
        public CreatureSpawn(ushort templateId, float x, float z)
        {
            TemplateId = templateId;
            X = x;
            Z = z;
        }

        public ushort TemplateId { get; }

        public float X { get; }

        public float Z { get; }
    }

    /// <summary>Point d'apparition d'un nœud de récolte dans la zone.</summary>
    public readonly struct GatheringNodeSpawn
    {
        public GatheringNodeSpawn(ushort templateId, float x, float z)
        {
            TemplateId = templateId;
            X = x;
            Z = z;
        }

        public ushort TemplateId { get; }

        public float X { get; }

        public float Z { get; }
    }
}
