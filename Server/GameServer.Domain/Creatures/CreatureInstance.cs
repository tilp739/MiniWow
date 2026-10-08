using System;
using System.Collections.Generic;
using GameServer.Domain.Combat;
using GameServer.Domain.Spatial;

namespace GameServer.Domain.Creatures
{
    public enum CreatureState : byte
    {
        Idle,
        Chase,
        Attack,
        Return,
        Dead,
    }

    /// <summary>État courant d'une créature. Le modèle reste dans <see cref="CreatureTemplate"/>.</summary>
    public sealed class CreatureInstance : IPlacedEntity
    {
        public CreatureInstance(uint entityId, CreatureTemplate template, float spawnX, float spawnZ)
        {
            EntityId = entityId;
            Template = template ?? throw new ArgumentNullException(nameof(template));
            SpawnX = spawnX;
            SpawnZ = spawnZ;
            X = spawnX;
            Z = spawnZ;
            Vitals = new Vitals(template.MaxHealth);
        }

        public uint EntityId { get; }

        public CreatureTemplate Template { get; }

        public float SpawnX { get; }

        public float SpawnZ { get; }

        public float X { get; private set; }

        public float Z { get; private set; }

        public float YawDegrees { get; private set; }

        public Vitals Vitals { get; }

        public CreatureState State { get; private set; } = CreatureState.Idle;

        public ThreatTable Threat { get; } = new ThreatTable();

        /// <summary>Joueur ciblé, ou null hors combat.</summary>
        public uint? TargetId { get; set; }

        public float AttackCooldownRemaining { get; private set; }

        public float RespawnRemaining { get; private set; }

        public bool IsAlive => Vitals.IsAlive;

        public void SetState(CreatureState state)
        {
            State = state;
        }

        public void StartAttackCooldown()
        {
            AttackCooldownRemaining = Template.AttackCooldownSeconds;
        }

        public void TickCooldown(float deltaSeconds)
        {
            if (AttackCooldownRemaining > 0f)
            {
                AttackCooldownRemaining = Math.Max(0f, AttackCooldownRemaining - deltaSeconds);
            }
        }

        /// <summary>Se déplace vers un point d'au plus <paramref name="maxStep"/> unités, et oriente le modèle.</summary>
        public void MoveTowards(float targetX, float targetZ, float maxStep)
        {
            float dx = targetX - X;
            float dz = targetZ - Z;
            float distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance <= 0f)
            {
                return;
            }

            float step = Math.Min(maxStep, distance);
            X += dx / distance * step;
            Z += dz / distance * step;
            YawDegrees = MathF.Atan2(dx, dz) * (180f / MathF.PI);
        }

        /// <summary>
        /// Enregistre des dégâts reçus : la menace est proportionnelle aux dégâts, et une créature calme passe en poursuite.
        /// </summary>
        public void ReceiveHit(uint sourceId, int appliedDamage)
        {
            Threat.Add(sourceId, appliedDamage);
            if (State == CreatureState.Idle)
            {
                TargetId = sourceId;
                State = CreatureState.Chase;
            }
        }

        /// <summary>Renfort invoqué : disparaît à sa mort au lieu de réapparaître.</summary>
        public bool Transient { get; set; }

        /// <summary>Temps restant avant la prochaine onde de choc (boss).</summary>
        public float ShockwaveCooldown { get; set; }

        /// <summary>Temps restant avant l'impact de l'onde de choc en cours, ou négatif si aucune.</summary>
        public float PendingShockwaveSeconds { get; set; } = -1f;

        public bool HasSummoned { get; set; }

        public bool IsEnraged { get; set; }

        /// <summary>Butin en attente sur le cadavre, et joueurs autorisés à le ramasser (contributeurs).</summary>
        public List<(ushort ItemId, int Quantity)> PendingLoot { get; } = new List<(ushort ItemId, int Quantity)>();

        public int PendingCopper { get; private set; }

        public HashSet<uint> LootOwners { get; } = new HashSet<uint>();

        /// <summary>Un cadavre reste visible tant qu'il contient du butin.</summary>
        public bool IsLootable => State == CreatureState.Dead && (PendingLoot.Count > 0 || PendingCopper > 0);

        public void SetPendingCopper(int copper)
        {
            PendingCopper = copper;
        }

        /// <summary>Vide le butin : appelé quand un contributeur le ramasse.</summary>
        public void ClearLoot()
        {
            PendingLoot.Clear();
            PendingCopper = 0;
        }

        /// <summary>Marque la créature comme morte et lance le compte à rebours de réapparition.</summary>
        public void Die()
        {
            State = CreatureState.Dead;
            RespawnRemaining = Template.RespawnSeconds;
            TargetId = null;
            Threat.Clear();
        }

        public void TickRespawn(float deltaSeconds)
        {
            if (State == CreatureState.Dead && RespawnRemaining > 0f)
            {
                RespawnRemaining = Math.Max(0f, RespawnRemaining - deltaSeconds);
            }
        }

        public bool IsReadyToRespawn => State == CreatureState.Dead && RespawnRemaining <= 0f;

        /// <summary>Réapparition au point d'origine, pleine vie, sans menace.</summary>
        public void Respawn()
        {
            Vitals.Restore();
            X = SpawnX;
            Z = SpawnZ;
            State = CreatureState.Idle;
            TargetId = null;
            AttackCooldownRemaining = 0f;
            Threat.Clear();
            ClearLoot();
            LootOwners.Clear();
        }
    }
}
