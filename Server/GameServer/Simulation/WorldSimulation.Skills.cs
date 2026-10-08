using System;
using GameServer.Domain;
using GameServer.Domain.Combat;
using GameServer.Domain.Creatures;
using GameServer.Domain.Skills;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>Compétences : validation, cible, coût, cooldown, lancer et effet. Toutes les valeurs viennent de Content/.</summary>
    public sealed partial class WorldSimulation
    {
        /// <summary>Cible résolue par le serveur. Une seule des deux références est renseignée.</summary>
        private readonly struct SkillTarget
        {
            public SkillTarget(uint id, CreatureInstance? creature, Player? player)
            {
                Id = id;
                Creature = creature;
                Player = player;
            }

            public uint Id { get; }

            public CreatureInstance? Creature { get; }

            public Player? Player { get; }

            public float X => Creature?.X ?? Player!.X;

            public float Z => Creature?.Z ?? Player!.Z;
        }

        private void HandleCast(IWorldClient client, ushort skillId, uint targetId)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            Player caster = slot.Player;

            if (!caster.IsAlive)
            {
                Reject(client, ErrorCode.Dead, "Vous êtes mort.");
                return;
            }

            if (caster.Cast != null)
            {
                Reject(client, ErrorCode.NotAllowed, "Un lancer est déjà en cours.");
                return;
            }

            if (!_content.Skills.TryGetValue(skillId, out SkillDefinition? skill))
            {
                Reject(client, ErrorCode.UnknownSkill, "Compétence inconnue.");
                return;
            }

            if (skill.Class != caster.Class.Id)
            {
                Reject(client, ErrorCode.WrongClass, "Cette compétence n'appartient pas à votre classe.");
                return;
            }

            if (caster.IsOnCooldown(skillId))
            {
                Reject(client, ErrorCode.OnCooldown, "Compétence en recharge.");
                return;
            }

            if (skill.ResourceCost > 0 && !caster.Resource.CanSpend(skill.ResourceCost))
            {
                Reject(client, ErrorCode.NotEnoughResource, "Ressource insuffisante.");
                return;
            }

            if (!TryResolveSkillTarget(caster, skill, targetId, out SkillTarget target, out ErrorCode error, out string text))
            {
                Reject(client, error, text);
                return;
            }

            caster.Resource.TrySpend(skill.ResourceCost);
            caster.StartCooldown(skillId, skill.CooldownSeconds);

            if (skill.CastSeconds > 0f)
            {
                caster.BeginCast(skillId, target.Id, skill.CastSeconds);
                _events.Add(new CombatEventMessage(CombatEventKind.CastStart, caster.EntityId, target.Id, (int)(skill.CastSeconds * 1000f)));
                return;
            }

            ApplySkill(caster, skill, target);
        }

        /// <summary>Le lancer est terminé : on revalide la cible (elle a pu mourir ou s'éloigner), puis on applique l'effet.</summary>
        private void ResolveCast(Player caster)
        {
            PendingCast cast = caster.Cast!;
            caster.CancelCast();

            SkillDefinition skill = _content.Skills[cast.SkillId];

            if (!TryResolveSkillTarget(caster, skill, cast.TargetId, out SkillTarget target, out ErrorCode error, out string text))
            {
                // Le coût est remboursé : le lancer n'a pas abouti.
                caster.Resource.Gain(skill.ResourceCost);
                _logger.LogInformation("CastFailed {EntityId} {Skill} {Error}", caster.EntityId, skill.Key, error);
                return;
            }

            ApplySkill(caster, skill, target);
        }

        private bool TryResolveSkillTarget(
            Player caster,
            SkillDefinition skill,
            uint targetId,
            out SkillTarget target,
            out ErrorCode error,
            out string text)
        {
            target = default;
            error = ErrorCode.InvalidTarget;
            text = "Cible invalide.";

            // Sans portée, ou sans cible explicite pour un sort non hostile : le lanceur est la cible.
            if (skill.Range == 0f || (targetId == 0 && !skill.TargetsEnemy))
            {
                target = new SkillTarget(caster.EntityId, null, caster);
                return true;
            }

            if (skill.TargetsEnemy)
            {
                CreatureInstance? creature = FindLivingCreature(targetId);
                Player? enemy = creature == null ? FindPlayer(targetId) : null;

                if (creature == null && (enemy == null || enemy == caster || !enemy.IsAlive))
                {
                    return false;
                }

                if (skill.Kind == SkillKind.Taunt && creature == null)
                {
                    return false;
                }

                target = new SkillTarget(targetId, creature, enemy);
            }
            else
            {
                Player? ally = targetId == caster.EntityId ? caster : FindPlayer(targetId);
                if (ally == null || !ally.IsAlive)
                {
                    return false;
                }

                target = new SkillTarget(targetId, null, ally);
            }

            if (DistanceXZ(caster.X, caster.Z, target.X, target.Z) > skill.Range)
            {
                error = ErrorCode.TooFar;
                text = "Cible trop éloignée.";
                return false;
            }

            return true;
        }

        private void ApplySkill(Player caster, SkillDefinition skill, SkillTarget target)
        {
            // Puissance : base + coefficient × (puissance d'attaque pour le physique, puissance des sorts sinon).
            float scaling = skill.School == SpellSchool.Physical ? caster.Derived.AttackPower : caster.Derived.SpellPower;
            float power = skill.BasePower + skill.Coefficient * scaling;

            switch (skill.Kind)
            {
                case SkillKind.Damage:
                    int min = Math.Max(1, (int)MathF.Round(power * 0.9f));
                    int max = Math.Max(min, (int)MathF.Round(power * 1.1f));

                    if (target.Creature != null)
                    {
                        DamageRoll creatureRoll = _resolver.RollDamage(min, max, target.Creature.Template.Armor);
                        DamageCreature(target.Creature, caster.EntityId, creatureRoll.Amount, caster);
                    }
                    else
                    {
                        DamageRoll playerRoll = _resolver.RollDamage(min, max, target.Player!.Derived.Armor);
                        DamagePlayer(target.Player!, caster.EntityId, playerRoll.Amount, caster);
                    }
                    break;

                case SkillKind.Heal:
                    int healed = target.Player!.Vitals.Heal((int)MathF.Round(power));
                    if (healed > 0)
                    {
                        _events.Add(new CombatEventMessage(CombatEventKind.Heal, caster.EntityId, target.Player.EntityId, healed));
                    }
                    break;

                case SkillKind.Taunt:
                    // Une menace très élevée force la créature à se concentrer sur le lanceur.
                    target.Creature!.ReceiveHit(caster.EntityId, TauntThreat);
                    break;
            }

            _logger.LogInformation("SkillUsed {EntityId} {Skill} {TargetId}", caster.EntityId, skill.Key, target.Id);
        }

        private const int TauntThreat = 1000;
    }
}
