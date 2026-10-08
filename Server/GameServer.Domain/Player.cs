using System.Collections.Generic;
using GameServer.Domain.Classes;
using GameServer.Domain.Combat;
using GameServer.Domain.Items;
using GameServer.Domain.Movement;
using GameServer.Domain.Professions;
using GameServer.Domain.Progression;
using GameServer.Domain.Quests;
using GameServer.Domain.Spatial;
using GameServer.Domain.Stats;

namespace GameServer.Domain
{
    /// <summary>Lancer en cours : le sort se résout quand <see cref="RemainingSeconds"/> atteint zéro.</summary>
    public sealed class PendingCast
    {
        public PendingCast(ushort skillId, uint targetId, float castSeconds)
        {
            SkillId = skillId;
            TargetId = targetId;
            RemainingSeconds = castSeconds;
        }

        public ushort SkillId { get; }

        public uint TargetId { get; }

        public float RemainingSeconds { get; set; }
    }

    /// <summary>
    /// Personnage présent dans le monde. <see cref="CharacterId"/> est <c>null</c> pour un personnage
    /// éphémère (non sauvegardé) ; non-null quand hydraté depuis un <c>CharacterSnapshot</c> persistant.
    /// </summary>
    public sealed class Player : IPlacedEntity
    {
        private readonly Dictionary<ushort, float> _cooldowns = new Dictionary<ushort, float>();

        public Player(
            uint entityId,
            string name,
            ClassDefinition classDefinition,
            MovementSettings movementSettings,
            float startX,
            float startZ,
            int level = 1,
            long experience = 0,
            long copper = 0,
            long? characterId = null,
            byte raceId = 0)
        {
            EntityId = entityId;
            Name = name;
            Class = classDefinition;
            CharacterId = characterId;
            RaceId = raceId;
            Motor = new PlayerMotor(movementSettings, startX, startZ);
            Level = level;
            Experience = experience;
            Copper = copper;
            Bonuses = CombatBonuses.None;
            Derived = StatCalculator.Derive(Class.StatsAtLevel(Level), Bonuses);
            Vitals = new Vitals(Derived.MaxHealth);
            Resource = new ResourcePool(classDefinition.Resource, Derived.MaxResource, classDefinition.ResourceRegenPerSecond, initial: classDefinition.Resource == ResourceKind.Mana ? Derived.MaxResource : 0);
        }

        public uint EntityId { get; }

        /// <summary>Identifiant du personnage en base, ou <c>null</c> s'il n'est pas persisté.</summary>
        public long? CharacterId { get; }

        public string Name { get; }

        public ClassDefinition Class { get; }

        /// <summary>Race jouable, purement cosmétique (cf. <c>GameShared.Messages.CharacterRace</c>).</summary>
        public byte RaceId { get; }

        public PlayerMotor Motor { get; }

        public Vitals Vitals { get; }

        public ResourcePool Resource { get; }

        public Inventory Inventory { get; } = new Inventory();

        /// <summary>Monnaie du joueur, en copper. Jamais négative.</summary>
        public long Copper { get; private set; }

        public void AddCopper(long amount)
        {
            if (amount > 0)
            {
                Copper += amount;
            }
        }

        public bool TrySpendCopper(long amount)
        {
            if (amount < 0 || Copper < amount)
            {
                return false;
            }

            Copper -= amount;
            return true;
        }

        public int Level { get; private set; }

        public long Experience { get; private set; }

        public CombatBonuses Bonuses { get; private set; }

        public DerivedStats Derived { get; private set; }

        public PendingCast? Cast { get; private set; }

        public float X => Motor.X;

        public float Z => Motor.Z;

        public bool IsAlive => Vitals.IsAlive;

        /// <summary>Temps restant avant la prochaine attaque de base, en secondes.</summary>
        public float AttackCooldownRemaining { get; private set; }

        /// <summary>Cible engagée en attaque automatique, ou null si aucune. Le tick de simulation swingue seul.</summary>
        public uint? AutoAttackTargetId { get; private set; }

        public void EngageAutoAttack(uint targetId)
        {
            AutoAttackTargetId = targetId;
        }

        public void DisengageAutoAttack()
        {
            AutoAttackTargetId = null;
        }

        /// <summary>Temps restant avant le respawn, en secondes. Positif uniquement après la mort.</summary>
        public float RespawnRemaining { get; private set; }

        public bool IsReadyToRespawn => !IsAlive && RespawnRemaining <= 0f;

        /// <summary>Fait avancer cooldowns, ressource, lancer et compte à rebours de respawn.</summary>
        public void Tick(float deltaSeconds)
        {
            if (AttackCooldownRemaining > 0f)
            {
                AttackCooldownRemaining = System.Math.Max(0f, AttackCooldownRemaining - deltaSeconds);
            }

            if (IsAlive)
            {
                Resource.Tick(deltaSeconds);
            }

            foreach (ushort skillId in new List<ushort>(_cooldowns.Keys))
            {
                float remaining = _cooldowns[skillId] - deltaSeconds;
                if (remaining <= 0f)
                {
                    _cooldowns.Remove(skillId);
                }
                else
                {
                    _cooldowns[skillId] = remaining;
                }
            }

            if (!IsAlive && RespawnRemaining > 0f)
            {
                RespawnRemaining = System.Math.Max(0f, RespawnRemaining - deltaSeconds);
            }
        }

        public bool IsOnCooldown(ushort skillId) => _cooldowns.ContainsKey(skillId);

        public void StartCooldown(ushort skillId, float seconds)
        {
            if (seconds > 0f)
            {
                _cooldowns[skillId] = seconds;
            }
        }

        public void StartAttackCooldown(float seconds)
        {
            AttackCooldownRemaining = seconds;
        }

        public void BeginCast(ushort skillId, uint targetId, float castSeconds)
        {
            Cast = new PendingCast(skillId, targetId, castSeconds);
        }

        public void CancelCast()
        {
            Cast = null;
        }

        /// <summary>Retire le temps écoulé du lancer ; retourne true quand il est terminé.</summary>
        public bool AdvanceCast(float deltaSeconds)
        {
            if (Cast == null)
            {
                return false;
            }

            Cast.RemainingSeconds -= deltaSeconds;
            return Cast.RemainingSeconds <= 0f;
        }

        /// <summary>Met à jour l'équipement : recalcule les dérivées et conserve la proportion de vie et de ressource.</summary>
        public void SetBonuses(CombatBonuses bonuses)
        {
            Bonuses = bonuses;
            RecomputeDerived();
        }

        /// <summary>Change de niveau, recalcule les statistiques et restaure la vie et la ressource.</summary>
        public void SetLevel(int level)
        {
            Level = level;
            RecomputeDerived();
            Vitals.Restore();
            Resource.Fill();
        }

        /// <summary>Quêtes actives et terminées de ce joueur.</summary>
        public QuestLog Quests { get; } = new QuestLog();

        /// <summary>Métiers connus de ce joueur et leur compétence.</summary>
        public ProfessionBook Professions { get; } = new ProfessionBook();

        /// <summary>Expérience accumulée dans le niveau courant.</summary>
        public long ExperienceToNextLevel => LevelTable.ExperienceToNext(Level);

        /// <summary>Ajoute de l'expérience et monte de niveau autant de fois que nécessaire. Retourne le nombre de niveaux gagnés.</summary>
        public int GrantExperience(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            Experience += amount;
            int gained = 0;

            while (Level < LevelTable.MaxLevel && Experience >= LevelTable.ExperienceToNext(Level))
            {
                Experience -= LevelTable.ExperienceToNext(Level);
                SetLevel(Level + 1);
                gained++;
            }

            if (Level >= LevelTable.MaxLevel)
            {
                Experience = 0;
            }

            return gained;
        }

        public void Die(float respawnSeconds)
        {
            Motor.Halt();
            AttackCooldownRemaining = 0f;
            Cast = null;
            AutoAttackTargetId = null;
            _cooldowns.Clear();
            RespawnRemaining = respawnSeconds;
        }

        public void Respawn(float x, float z)
        {
            Vitals.Restore();
            Resource.Fill();
            Motor.Teleport(x, z);
            RespawnRemaining = 0f;
        }

        private void RecomputeDerived()
        {
            Derived = StatCalculator.Derive(Class.StatsAtLevel(Level), Bonuses);
            Vitals.SetMaxHealth(Derived.MaxHealth);
            Resource.SetMax(Derived.MaxResource);
        }
    }
}
