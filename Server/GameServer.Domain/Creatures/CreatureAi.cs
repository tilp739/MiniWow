using System;
using System.Collections.Generic;
using GameServer.Domain.Spatial;

namespace GameServer.Domain.Creatures
{
    /// <summary>
    /// IA des créatures : Idle → Chase → Attack → Return → Idle, et Dead pour le respawn.
    /// Ne déclenche pas les dégâts : elle renvoie l'intention d'attaquer, la simulation applique le combat.
    /// Patrouille et alerte de groupe sont prévues pour un milestone ultérieur.
    /// </summary>
    public static class CreatureAi
    {
        /// <summary>Distance sous laquelle une créature considère être rentrée chez elle.</summary>
        private const float HomeArrivalDistance = 0.5f;

        /// <summary>
        /// Fait avancer la créature d'un tick. Retourne l'identifiant du joueur à frapper ce tick, ou null.
        /// </summary>
        public static uint? Update(CreatureInstance creature, IReadOnlyList<Player> players, float deltaSeconds)
        {
            creature.TickCooldown(deltaSeconds);
            CreatureTemplate template = creature.Template;

            switch (creature.State)
            {
                case CreatureState.Dead:
                    creature.TickRespawn(deltaSeconds);
                    if (creature.IsReadyToRespawn)
                    {
                        creature.Respawn();
                    }
                    return null;

                case CreatureState.Idle:
                    Player? aggro = FindNearestLiving(players, creature.X, creature.Z, template.AggroRange);
                    if (aggro != null)
                    {
                        creature.Threat.Add(aggro.EntityId, 1f);
                        creature.TargetId = aggro.EntityId;
                        creature.SetState(CreatureState.Chase);
                    }
                    return null;

                case CreatureState.Chase:
                case CreatureState.Attack:
                    return UpdateCombat(creature, players, deltaSeconds);

                case CreatureState.Return:
                    UpdateReturn(creature, deltaSeconds);
                    return null;

                default:
                    throw new InvalidOperationException($"État de créature inconnu : {creature.State}.");
            }
        }

        private static uint? UpdateCombat(CreatureInstance creature, IReadOnlyList<Player> players, float deltaSeconds)
        {
            CreatureTemplate template = creature.Template;
            Player? target = ResolveTarget(creature, players);

            if (target == null)
            {
                creature.Threat.Clear();
                creature.TargetId = null;
                creature.SetState(CreatureState.Return);
                return null;
            }

            float distance = DistanceXZ(creature.X, creature.Z, target.X, target.Z);
            if (distance <= template.AttackRange)
            {
                creature.SetState(CreatureState.Attack);
                if (creature.AttackCooldownRemaining <= 0f)
                {
                    creature.StartAttackCooldown();
                    return target.EntityId;
                }

                return null;
            }

            creature.SetState(CreatureState.Chase);
            creature.MoveTowards(target.X, target.Z, template.MoveSpeed * deltaSeconds);
            return null;
        }

        private static void UpdateReturn(CreatureInstance creature, float deltaSeconds)
        {
            float distance = DistanceXZ(creature.X, creature.Z, creature.SpawnX, creature.SpawnZ);
            if (distance <= HomeArrivalDistance)
            {
                // Retour à la maison : la créature récupère sa vie et redevient calme.
                creature.Vitals.Restore();
                creature.SetState(CreatureState.Idle);
                return;
            }

            creature.MoveTowards(creature.SpawnX, creature.SpawnZ, creature.Template.MoveSpeed * deltaSeconds);
        }

        /// <summary>
        /// Garde la cible courante si elle est vivante et dans la laisse ; sinon prend le joueur le plus menaçant
        /// encore éligible. Retourne null si aucun joueur n'est éligible.
        /// </summary>
        private static Player? ResolveTarget(CreatureInstance creature, IReadOnlyList<Player> players)
        {
            float leash = creature.Template.LeashRange;

            if (creature.TargetId is uint currentId)
            {
                Player? current = FindLiving(players, currentId);
                if (current != null && DistanceXZ(creature.SpawnX, creature.SpawnZ, current.X, current.Z) <= leash)
                {
                    return current;
                }
            }

            bool Eligible(uint id)
            {
                Player? candidate = FindLiving(players, id);
                return candidate != null && DistanceXZ(creature.SpawnX, creature.SpawnZ, candidate.X, candidate.Z) <= leash;
            }

            if (creature.Threat.TryGetTop(Eligible, out uint topId))
            {
                creature.TargetId = topId;
                return FindLiving(players, topId);
            }

            return null;
        }

        private static Player? FindLiving(IReadOnlyList<Player> players, uint entityId)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].EntityId == entityId && players[i].IsAlive)
                {
                    return players[i];
                }
            }

            return null;
        }

        private static Player? FindNearestLiving(IReadOnlyList<Player> players, float x, float z, float range)
        {
            Player? nearest = null;
            float nearestDistance = range;

            for (int i = 0; i < players.Count; i++)
            {
                Player candidate = players[i];
                if (!candidate.IsAlive)
                {
                    continue;
                }

                float distance = DistanceXZ(x, z, candidate.X, candidate.Z);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private static float DistanceXZ(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax;
            float dz = bz - az;
            return MathF.Sqrt(dx * dx + dz * dz);
        }
    }
}
