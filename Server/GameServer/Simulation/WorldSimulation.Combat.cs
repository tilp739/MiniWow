using System;
using GameServer.Domain;
using GameServer.Domain.Combat;
using GameServer.Domain.Creatures;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>Combat de base (attaque automatique) et application des dégâts.</summary>
    public sealed partial class WorldSimulation
    {
        /// <summary>Rage gagnée par coup porté ou subi (classes à colère uniquement).</summary>
        private const int RageOnDealt = 10;
        private const int RageOnTaken = 5;

        /// <summary>
        /// Demande d'attaque : <paramref name="targetId"/> = 0 désengage (jamais un id d'entité valide).
        /// Sinon, valide et porte le premier coup immédiatement (comme avant), puis engage l'auto-attack :
        /// <see cref="TryAutoAttackSwing"/> continuera à swinguer seul à chaque tick, sans nouvelle demande.
        /// </summary>
        private void HandleAttack(IWorldClient client, uint targetId)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            Player attacker = slot.Player;

            if (targetId == 0)
            {
                attacker.DisengageAutoAttack();
                return;
            }

            PlayerCombatSettings combat = _options.PlayerCombat;

            if (!attacker.IsAlive)
            {
                Reject(client, ErrorCode.Dead, "Vous êtes mort.");
                return;
            }

            if (attacker.AttackCooldownRemaining > 0f)
            {
                Reject(client, ErrorCode.OnCooldown, "Attaque pas encore prête.");
                return;
            }

            CreatureInstance? creature = FindLivingCreature(targetId);
            Player? targetPlayer = creature == null ? FindPlayer(targetId) : null;
            if (creature == null && (targetPlayer == null || targetPlayer == attacker || !targetPlayer.IsAlive))
            {
                Reject(client, ErrorCode.InvalidTarget, "Cible invalide.");
                return;
            }

            float targetX = creature?.X ?? targetPlayer!.X;
            float targetZ = creature?.Z ?? targetPlayer!.Z;
            if (DistanceXZ(attacker.X, attacker.Z, targetX, targetZ) > combat.AttackRange)
            {
                Reject(client, ErrorCode.OutOfRange, "Cible hors de portée.");
                return;
            }

            attacker.EngageAutoAttack(targetId);
            SwingAt(attacker, creature, targetPlayer, combat);
        }

        /// <summary>Fait swinguer un joueur engagé : résout sa cible, vérifie portée/cooldown, puis frappe.</summary>
        private void TryAutoAttackSwing(Player attacker)
        {
            if (attacker.AutoAttackTargetId is not uint targetId)
            {
                return;
            }

            CreatureInstance? creature = FindLivingCreature(targetId);
            Player? targetPlayer = creature == null ? FindPlayer(targetId) : null;
            if (creature == null && (targetPlayer == null || targetPlayer == attacker || !targetPlayer.IsAlive))
            {
                // Cible disparue, morte ou invalide : l'engagement n'a plus de sens.
                attacker.DisengageAutoAttack();
                return;
            }

            if (attacker.AttackCooldownRemaining > 0f)
            {
                return;
            }

            PlayerCombatSettings combat = _options.PlayerCombat;
            float targetX = creature?.X ?? targetPlayer!.X;
            float targetZ = creature?.Z ?? targetPlayer!.Z;
            if (DistanceXZ(attacker.X, attacker.Z, targetX, targetZ) > combat.AttackRange)
            {
                // Hors de portée : reste engagé, retente au tick suivant (pas de désengagement implicite).
                return;
            }

            SwingAt(attacker, creature, targetPlayer, combat);
        }

        private void SwingAt(Player attacker, CreatureInstance? creature, Player? targetPlayer, PlayerCombatSettings combat)
        {
            attacker.StartAttackCooldown(combat.AttackCooldownSeconds);

            if (creature != null)
            {
                DamageRoll roll = _resolver.RollDamage(combat.DamageMin, combat.DamageMax, creature.Template.Armor);
                DamageCreature(creature, attacker.EntityId, roll.Amount, attacker);
            }
            else
            {
                DamageRoll roll = _resolver.RollDamage(combat.DamageMin, combat.DamageMax, targetPlayer!.Derived.Armor);
                DamagePlayer(targetPlayer!, attacker.EntityId, roll.Amount, attacker);
            }
        }

        private void ResolveCreatureAttack(CreatureInstance creature, uint targetPlayerId)
        {
            Player? target = FindPlayer(targetPlayerId);
            if (target == null || !target.IsAlive)
            {
                return;
            }

            // Un boss enragé frappe plus fort ; les créatures ordinaires ne sont jamais enragées.
            float multiplier = creature.IsEnraged ? creature.Template.EnrageDamageMultiplier : 1f;
            int min = (int)(creature.Template.DamageMin * multiplier);
            int max = Math.Max(min, (int)(creature.Template.DamageMax * multiplier));
            DamageRoll roll = _resolver.RollDamage(min, max, target.Derived.Armor);
            DamagePlayer(target, creature.EntityId, roll.Amount, source: null);
        }

        /// <summary>Applique des dégâts à une créature. Gère la menace, la mort, l'expérience et la ressource du lanceur.</summary>
        private void DamageCreature(CreatureInstance creature, uint sourceId, int amount, Player? source)
        {
            int applied = creature.Vitals.ApplyDamage(amount);
            _events.Add(new CombatEventMessage(CombatEventKind.Damage, sourceId, creature.EntityId, applied));

            if (source != null && source.Class.Resource == Domain.Combat.ResourceKind.Rage)
            {
                source.Resource.Gain(RageOnDealt);
            }

            if (creature.IsAlive)
            {
                creature.ReceiveHit(sourceId, applied);
                return;
            }

            // Le coup fatal compte dans la menace avant de relever les contributeurs : Die() vide la table.
            creature.ReceiveHit(sourceId, applied);

            _rewardRecipients.Clear();
            foreach (uint contributor in creature.Threat.Sources)
            {
                _rewardRecipients.Add(contributor);
            }

            creature.Die();
            _events.Add(new CombatEventMessage(CombatEventKind.Death, sourceId, creature.EntityId, 0));
            PrepareLoot(creature, _rewardRecipients);

            foreach (uint recipientId in _rewardRecipients)
            {
                Player? recipient = FindPlayer(recipientId);
                if (recipient == null)
                {
                    continue;
                }

                GrantExperience(recipient, creature.Template.ExperienceReward);
                CreditKill(recipient, creature.Template.Id);
            }

            if (creature.Template.IsBoss)
            {
                CompleteDungeon(creature);
            }

            _logger.LogInformation("CreatureKilled {CreatureId} {TemplateKey} {SourceId}", creature.EntityId, creature.Template.Key, sourceId);
        }

        /// <summary>Applique des dégâts à un joueur. La mort lance le compte à rebours de respawn.</summary>
        private void DamagePlayer(Player player, uint sourceId, int amount, Player? source)
        {
            int applied = player.Vitals.ApplyDamage(amount);
            _events.Add(new CombatEventMessage(CombatEventKind.Damage, sourceId, player.EntityId, applied));

            if (player.Class.Resource == Domain.Combat.ResourceKind.Rage)
            {
                player.Resource.Gain(RageOnTaken);
            }

            if (!player.IsAlive)
            {
                player.Die(_options.PlayerCombat.RespawnSeconds);
                _events.Add(new CombatEventMessage(CombatEventKind.Death, sourceId, player.EntityId, 0));
                _logger.LogInformation("PlayerDied {EntityId} {SourceId}", player.EntityId, sourceId);
            }
        }
    }
}
