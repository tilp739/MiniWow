using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Domain.Adventures;
using GameServer.Domain.Classes;
using GameServer.Domain.Combat;
using GameServer.Domain.Creatures;
using GameServer.Domain.Items;
using GameServer.Domain.Npcs;
using GameServer.Domain.Professions;
using GameServer.Domain.Quests;
using GameServer.Domain.Skills;

namespace GameServer.Content
{
    /// <summary>
    /// Charge et valide les données de monde (JSON). Une donnée invalide empêche le démarrage :
    /// mieux vaut échouer tôt que jouer avec un monde incohérent.
    /// </summary>
    public static class ContentLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

        public static WorldContent Load(string directory, string zoneId)
        {
            Dictionary<ushort, CreatureTemplate> creatures = LoadCreatures(Path.Combine(directory, "creatures.json"));
            Dictionary<CharacterClass, ClassDefinition> classes = LoadClasses(Path.Combine(directory, "classes.json"));
            Dictionary<ushort, SkillDefinition> skills = LoadSkills(Path.Combine(directory, "skills.json"), classes);
            Dictionary<ushort, ItemDefinition> items = LoadItems(Path.Combine(directory, "items.json"));
            Dictionary<ushort, AdventureDefinition> adventures = LoadAdventures(Path.Combine(directory, "adventures.json"));
            Dictionary<byte, ProfessionDefinition> professions = LoadProfessions(Path.Combine(directory, "professions.json"));
            Dictionary<ushort, RecipeDefinition> recipes = LoadRecipes(Path.Combine(directory, "recipes.json"), professions, items);
            Dictionary<ushort, GatheringNodeDefinition> gatheringNodes = LoadGatheringNodes(Path.Combine(directory, "gathering_nodes.json"), professions, items);

            foreach (CreatureTemplate creature in creatures.Values)
            {
                foreach (LootEntry entry in creature.Loot)
                {
                    if (!items.ContainsKey(entry.ItemId))
                    {
                        throw new InvalidDataException($"creature '{creature.Key}' : objet de butin inconnu {entry.ItemId}.");
                    }
                }
            }

            string zonePath = Path.Combine(directory, "zones", zoneId + ".json");
            ZoneFile zone = Read<ZoneFile>(zonePath);

            if (string.IsNullOrWhiteSpace(zone.Id) || zone.Id != zoneId)
            {
                throw new InvalidDataException($"{zonePath} : l'identifiant de zone doit être '{zoneId}'.");
            }

            var spawns = new List<CreatureSpawn>();
            foreach (CreatureSpawnFile spawn in zone.Creatures ?? new List<CreatureSpawnFile>())
            {
                if (!creatures.ContainsKey(spawn.Template))
                {
                    throw new InvalidDataException($"{zonePath} : créature inconnue {spawn.Template}.");
                }

                spawns.Add(new CreatureSpawn(spawn.Template, spawn.X, spawn.Z));
            }

            var gatheringNodeSpawns = new List<GatheringNodeSpawn>();
            foreach (GatheringNodeSpawnFile spawn in zone.GatheringNodes ?? new List<GatheringNodeSpawnFile>())
            {
                if (!gatheringNodes.ContainsKey(spawn.Template))
                {
                    throw new InvalidDataException($"{zonePath} : nœud de récolte inconnu {spawn.Template}.");
                }

                gatheringNodeSpawns.Add(new GatheringNodeSpawn(spawn.Template, spawn.X, spawn.Z));
            }

            Dictionary<ushort, NpcDefinition> npcs = LoadNpcs(Path.Combine(directory, "npcs.json"), items, professions, adventures);
            Dictionary<ushort, QuestDefinition> quests = LoadQuests(Path.Combine(directory, "quests.json"), creatures, items, npcs);

            // Les PNJ présents dans la zone sont ceux listés par la zone.
            var zoneNpcs = new Dictionary<ushort, NpcDefinition>();
            foreach (ushort npcId in zone.Npcs ?? new List<ushort>())
            {
                if (!npcs.TryGetValue(npcId, out NpcDefinition? npc))
                {
                    throw new InvalidDataException($"{zonePath} : PNJ inconnu {npcId}.");
                }

                zoneNpcs.Add(npcId, npc);
            }

            return new WorldContent(
                zone.Id,
                zone.PlayerSpawn?.X ?? 0f,
                zone.PlayerSpawn?.Z ?? 0f,
                creatures,
                spawns,
                classes,
                skills,
                items,
                zoneNpcs,
                quests,
                professions,
                recipes,
                gatheringNodes,
                gatheringNodeSpawns,
                adventures);
        }

        private static Dictionary<ushort, NpcDefinition> LoadNpcs(
            string path,
            Dictionary<ushort, ItemDefinition> items,
            Dictionary<byte, ProfessionDefinition> professions,
            Dictionary<ushort, AdventureDefinition> adventures)
        {
            NpcFile file = Read<NpcFile>(path);
            var byId = new Dictionary<ushort, NpcDefinition>();

            foreach (NpcDefinition npc in file.Npcs ?? new List<NpcDefinition>())
            {
                if (string.IsNullOrWhiteSpace(npc.Key) || string.IsNullOrWhiteSpace(npc.Name))
                {
                    throw new InvalidDataException($"{path} : PNJ {npc.Id} sans clé ou sans nom.");
                }

                foreach (ushort adventureId in npc.OfferedAdventureIds)
                {
                    if (!adventures.ContainsKey(adventureId))
                    {
                        throw new InvalidDataException($"{path} : PNJ '{npc.Key}' propose une aventure inconnue ({adventureId}).");
                    }
                }

                foreach (ShopEntry entry in npc.Shop)
                {
                    if (!items.ContainsKey(entry.ItemId) || entry.Price < 0)
                    {
                        throw new InvalidDataException($"{path} : PNJ '{npc.Key}' vend un objet inconnu ou à prix négatif ({entry.ItemId}).");
                    }
                }

                foreach (byte professionId in npc.TeachesProfessions)
                {
                    if (!professions.ContainsKey(professionId))
                    {
                        throw new InvalidDataException($"{path} : PNJ '{npc.Key}' enseigne un métier inconnu ({professionId}).");
                    }
                }

                if (!byId.TryAdd(npc.Id, npc))
                {
                    throw new InvalidDataException($"{path} : identifiant de PNJ en double {npc.Id}.");
                }
            }

            return byId;
        }

        private static Dictionary<ushort, QuestDefinition> LoadQuests(
            string path,
            Dictionary<ushort, CreatureTemplate> creatures,
            Dictionary<ushort, ItemDefinition> items,
            Dictionary<ushort, NpcDefinition> npcs)
        {
            QuestFile file = Read<QuestFile>(path);
            var byId = new Dictionary<ushort, QuestDefinition>();

            foreach (QuestDefinition quest in file.Quests ?? new List<QuestDefinition>())
            {
                string where = $"{path} : quête '{quest.Key}'";

                if (!npcs.ContainsKey(quest.GiverNpcId) || !npcs.ContainsKey(quest.TurnInNpcId))
                {
                    throw new InvalidDataException($"{where} : PNJ de départ ou de rendu inconnu.");
                }

                if (quest.Objectives.Count == 0)
                {
                    throw new InvalidDataException($"{where} : aucun objectif.");
                }

                foreach (QuestObjectiveDefinition objective in quest.Objectives)
                {
                    if (objective.Count <= 0 || objective.Radius <= 0f)
                    {
                        throw new InvalidDataException($"{where} : objectif sans quantité ou rayon positif.");
                    }

                    bool valid = objective.Kind switch
                    {
                        ObjectiveKind.Kill => creatures.ContainsKey(objective.TargetId),
                        ObjectiveKind.Collect => items.ContainsKey(objective.TargetId),
                        ObjectiveKind.Talk => npcs.ContainsKey(objective.TargetId),
                        ObjectiveKind.GoTo => true,
                        _ => false,
                    };

                    if (!valid)
                    {
                        throw new InvalidDataException($"{where} : cible d'objectif inconnue ({objective.Kind} {objective.TargetId}).");
                    }
                }

                if (quest.Reward.ItemId != 0 && !items.ContainsKey(quest.Reward.ItemId))
                {
                    throw new InvalidDataException($"{where} : objet de récompense inconnu.");
                }

                if (!byId.TryAdd(quest.Id, quest))
                {
                    throw new InvalidDataException($"{path} : identifiant de quête en double {quest.Id}.");
                }
            }

            return byId;
        }

        private sealed class NpcFile
        {
            public List<NpcDefinition>? Npcs { get; set; }
        }

        private sealed class QuestFile
        {
            public List<QuestDefinition>? Quests { get; set; }
        }

        private static Dictionary<ushort, ItemDefinition> LoadItems(string path)
        {
            ItemFile file = Read<ItemFile>(path);
            var byId = new Dictionary<ushort, ItemDefinition>();

            foreach (ItemDefinition item in file.Items ?? new List<ItemDefinition>())
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.MaxStack < 1)
                {
                    throw new InvalidDataException($"{path} : objet {item.Id} sans clé ou pile invalide.");
                }

                if (!byId.TryAdd(item.Id, item))
                {
                    throw new InvalidDataException($"{path} : identifiant d'objet en double {item.Id}.");
                }
            }

            return byId;
        }

        private sealed class AdventureFile
        {
            public List<AdventureDefinition>? Adventures { get; set; }
        }

        private static Dictionary<ushort, AdventureDefinition> LoadAdventures(string path)
        {
            AdventureFile file = Read<AdventureFile>(path);
            var byId = new Dictionary<ushort, AdventureDefinition>();

            foreach (AdventureDefinition adventure in file.Adventures ?? new List<AdventureDefinition>())
            {
                if (string.IsNullOrWhiteSpace(adventure.Key) || string.IsNullOrWhiteSpace(adventure.Name) || string.IsNullOrWhiteSpace(adventure.ZoneId))
                {
                    throw new InvalidDataException($"{path} : aventure {adventure.Id} sans clé, nom ou zone.");
                }

                if (adventure.MinPlayers < 1 || adventure.MaxPlayers < adventure.MinPlayers)
                {
                    throw new InvalidDataException($"{path} : aventure '{adventure.Key}' a un nombre de joueurs incohérent.");
                }

                if (!byId.TryAdd(adventure.Id, adventure))
                {
                    throw new InvalidDataException($"{path} : identifiant d'aventure en double {adventure.Id}.");
                }
            }

            return byId;
        }

        private sealed class ItemFile
        {
            public List<ItemDefinition>? Items { get; set; }
        }

        private static Dictionary<byte, ProfessionDefinition> LoadProfessions(string path)
        {
            ProfessionFile file = Read<ProfessionFile>(path);
            var byId = new Dictionary<byte, ProfessionDefinition>();

            foreach (ProfessionDefinition profession in file.Professions ?? new List<ProfessionDefinition>())
            {
                if (string.IsNullOrWhiteSpace(profession.Key) || string.IsNullOrWhiteSpace(profession.Name) || profession.MaxSkill <= 0)
                {
                    throw new InvalidDataException($"{path} : métier {profession.Id} sans clé/nom ou compétence maximale invalide.");
                }

                if (!byId.TryAdd(profession.Id, profession))
                {
                    throw new InvalidDataException($"{path} : identifiant de métier en double {profession.Id}.");
                }
            }

            return byId;
        }

        private sealed class ProfessionFile
        {
            public List<ProfessionDefinition>? Professions { get; set; }
        }

        private static Dictionary<ushort, RecipeDefinition> LoadRecipes(
            string path,
            Dictionary<byte, ProfessionDefinition> professions,
            Dictionary<ushort, ItemDefinition> items)
        {
            RecipeFile file = Read<RecipeFile>(path);
            var byId = new Dictionary<ushort, RecipeDefinition>();

            foreach (RecipeDefinition recipe in file.Recipes ?? new List<RecipeDefinition>())
            {
                string where = $"{path} : recette '{recipe.Key}'";

                if (string.IsNullOrWhiteSpace(recipe.Key) || string.IsNullOrWhiteSpace(recipe.Name))
                {
                    throw new InvalidDataException($"{path} : recette {recipe.Id} sans clé ou sans nom.");
                }

                if (!professions.ContainsKey(recipe.ProfessionId))
                {
                    throw new InvalidDataException($"{where} : métier inconnu {recipe.ProfessionId}.");
                }

                if (!items.ContainsKey(recipe.ResultItemId) || recipe.ResultQuantity <= 0)
                {
                    throw new InvalidDataException($"{where} : résultat invalide.");
                }

                if (recipe.Reagents.Count == 0)
                {
                    throw new InvalidDataException($"{where} : aucun réactif.");
                }

                foreach (ReagentEntry reagent in recipe.Reagents)
                {
                    if (!items.ContainsKey(reagent.ItemId) || reagent.Quantity <= 0)
                    {
                        throw new InvalidDataException($"{where} : réactif inconnu ou quantité invalide ({reagent.ItemId}).");
                    }
                }

                if (!byId.TryAdd(recipe.Id, recipe))
                {
                    throw new InvalidDataException($"{path} : identifiant de recette en double {recipe.Id}.");
                }
            }

            return byId;
        }

        private sealed class RecipeFile
        {
            public List<RecipeDefinition>? Recipes { get; set; }
        }

        private static Dictionary<ushort, GatheringNodeDefinition> LoadGatheringNodes(
            string path,
            Dictionary<byte, ProfessionDefinition> professions,
            Dictionary<ushort, ItemDefinition> items)
        {
            GatheringNodeFile file = Read<GatheringNodeFile>(path);
            var byId = new Dictionary<ushort, GatheringNodeDefinition>();

            foreach (GatheringNodeDefinition node in file.GatheringNodes ?? new List<GatheringNodeDefinition>())
            {
                string where = $"{path} : nœud de récolte '{node.Key}'";

                if (string.IsNullOrWhiteSpace(node.Key) || string.IsNullOrWhiteSpace(node.Name))
                {
                    throw new InvalidDataException($"{path} : nœud de récolte {node.Id} sans clé ou sans nom.");
                }

                if (!professions.ContainsKey(node.ProfessionId))
                {
                    throw new InvalidDataException($"{where} : métier inconnu {node.ProfessionId}.");
                }

                if (!items.ContainsKey(node.ResultItemId) || node.ResultQuantityMin <= 0 || node.ResultQuantityMax < node.ResultQuantityMin)
                {
                    throw new InvalidDataException($"{where} : résultat invalide.");
                }

                if (node.RespawnSeconds <= 0f)
                {
                    throw new InvalidDataException($"{where} : le délai de réapparition doit être positif.");
                }

                if (!byId.TryAdd(node.Id, node))
                {
                    throw new InvalidDataException($"{path} : identifiant de nœud de récolte en double {node.Id}.");
                }
            }

            return byId;
        }

        private sealed class GatheringNodeFile
        {
            public List<GatheringNodeDefinition>? GatheringNodes { get; set; }
        }

        private static Dictionary<ushort, CreatureTemplate> LoadCreatures(string path)
        {
            CreatureFile file = Read<CreatureFile>(path);
            var byId = new Dictionary<ushort, CreatureTemplate>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CreatureTemplate template in file.Creatures ?? new List<CreatureTemplate>())
            {
                ValidateCreature(template, path);

                if (!byId.TryAdd(template.Id, template))
                {
                    throw new InvalidDataException($"{path} : identifiant de créature en double {template.Id}.");
                }

                if (!keys.Add(template.Key))
                {
                    throw new InvalidDataException($"{path} : clé de créature en double '{template.Key}'.");
                }
            }

            return byId;
        }

        private static Dictionary<CharacterClass, ClassDefinition> LoadClasses(string path)
        {
            ClassFile file = Read<ClassFile>(path);
            var byId = new Dictionary<CharacterClass, ClassDefinition>();

            foreach (ClassDefinition definition in file.Classes ?? new List<ClassDefinition>())
            {
                if (!Enum.IsDefined(typeof(CharacterClass), definition.Id))
                {
                    throw new InvalidDataException($"{path} : classe inconnue {definition.Id}.");
                }

                if (!byId.TryAdd(definition.Id, definition))
                {
                    throw new InvalidDataException($"{path} : classe en double {definition.Id}.");
                }
            }

            if (byId.Count == 0)
            {
                throw new InvalidDataException($"{path} : aucune classe définie.");
            }

            return byId;
        }

        private static Dictionary<ushort, SkillDefinition> LoadSkills(string path, Dictionary<CharacterClass, ClassDefinition> classes)
        {
            SkillFile file = Read<SkillFile>(path);
            var byId = new Dictionary<ushort, SkillDefinition>();

            foreach (SkillDefinition skill in file.Skills ?? new List<SkillDefinition>())
            {
                string where = $"{path} : compétence '{skill.Key}'";

                if (!classes.TryGetValue(skill.Class, out ClassDefinition? owner))
                {
                    throw new InvalidDataException($"{where} : classe inconnue {skill.Class}.");
                }

                if (skill.ResourceCost > 0 && skill.ResourceCostKind != owner!.Resource)
                {
                    throw new InvalidDataException($"{where} : la ressource doit être celle de la classe ({owner.Resource}).");
                }

                if (skill.Range < 0f || skill.CastSeconds < 0f || skill.CooldownSeconds < 0f || skill.ResourceCost < 0)
                {
                    throw new InvalidDataException($"{where} : portée, durées et coût ne peuvent pas être négatifs.");
                }

                if (!byId.TryAdd(skill.Id, skill))
                {
                    throw new InvalidDataException($"{path} : identifiant de compétence en double {skill.Id}.");
                }
            }

            return byId;
        }

        private static void ValidateCreature(CreatureTemplate t, string path)
        {
            string where = $"{path} : créature '{t.Key}'";

            if (string.IsNullOrWhiteSpace(t.Key) || string.IsNullOrWhiteSpace(t.Name))
            {
                throw new InvalidDataException($"{path} : clé et nom obligatoires (id {t.Id}).");
            }

            if (t.MaxHealth <= 0)
            {
                throw new InvalidDataException($"{where} : maxHealth doit être positif.");
            }

            if (t.DamageMin < 0 || t.DamageMin > t.DamageMax)
            {
                throw new InvalidDataException($"{where} : dégâts incohérents ({t.DamageMin}-{t.DamageMax}).");
            }

            if (t.MoveSpeed <= 0f || t.AttackRange <= 0f || t.AttackCooldownSeconds <= 0f)
            {
                throw new InvalidDataException($"{where} : vitesse, portée et cooldown doivent être positifs.");
            }

            if (t.AggroRange <= 0f || t.LeashRange < t.AggroRange)
            {
                throw new InvalidDataException($"{where} : la laisse doit être au moins égale à la portée d'aggro.");
            }

            if (t.RespawnSeconds < 0f || t.ExperienceReward < 0 || t.Armor < 0)
            {
                throw new InvalidDataException($"{where} : respawn, expérience et armure ne peuvent pas être négatifs.");
            }
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        private static T Read<T>(string path) where T : new()
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Fichier de données introuvable.", path);
            }

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ?? new T();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"{path} : JSON invalide ({ex.Message}).", ex);
            }
        }

        private sealed class CreatureFile
        {
            public List<CreatureTemplate>? Creatures { get; set; }
        }

        private sealed class ClassFile
        {
            public List<ClassDefinition>? Classes { get; set; }
        }

        private sealed class SkillFile
        {
            public List<SkillDefinition>? Skills { get; set; }
        }

        private sealed class ZoneFile
        {
            public string Id { get; set; } = string.Empty;

            public PointFile? PlayerSpawn { get; set; }

            public List<CreatureSpawnFile>? Creatures { get; set; }

            public List<ushort>? Npcs { get; set; }

            public List<GatheringNodeSpawnFile>? GatheringNodes { get; set; }
        }

        private sealed class PointFile
        {
            public float X { get; set; }

            public float Z { get; set; }
        }

        private sealed class CreatureSpawnFile
        {
            public ushort Template { get; set; }

            public float X { get; set; }

            public float Z { get; set; }
        }

        private sealed class GatheringNodeSpawnFile
        {
            public ushort Template { get; set; }

            public float X { get; set; }

            public float Z { get; set; }
        }
    }
}
