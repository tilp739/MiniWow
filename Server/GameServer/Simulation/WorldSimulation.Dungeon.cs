using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.Domain;
using GameServer.Domain.Adventures;
using GameServer.Domain.Combat;
using GameServer.Domain.Creatures;
using GameServer.Domain.Npcs;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Logging;

namespace GameServer.Simulation
{
    /// <summary>
    /// Donjons instanciés et boss. L'entrée transfère le groupe dans une instance dédiée ; le boss a des phases,
    /// une onde de choc télégraphiée et des renforts. Sa mort termine le donjon, puis l'instance se ferme.
    /// </summary>
    public sealed partial class WorldSimulation
    {
        /// <summary>Distance maximale entre le chef et ses membres pour qu'ils entrent avec lui.</summary>
        private const float DungeonGroupRange = 30f;

        /// <summary>Délai avant fermeture d'une instance terminée : le temps de célébrer et de ramasser le butin.</summary>
        private const float InstanceCloseDelaySeconds = 30f;

        private const float EntrySpacing = 2f;

        private void HandleDungeonAction(IWorldClient client, DungeonActionMessage action)
        {
            if (!_players.TryGetValue(client.ClientId, out PlayerSlot? slot))
            {
                return;
            }

            Player leader = slot.Player;
            NpcInstance? gate = FindNpc(action.NpcEntityId);
            if (gate == null)
            {
                Reject(client, ErrorCode.NoSuchNpc, "PNJ inconnu.");
                return;
            }

            if (!TryResolveZone(gate.Definition, action.AdventureId, out string? zoneId, out string rejectReason))
            {
                Reject(client, ErrorCode.NotAllowed, rejectReason);
                return;
            }

            if (DistanceXZ(leader.X, leader.Z, gate.X, gate.Z) > NpcDefinition.InteractRange)
            {
                Reject(client, ErrorCode.TooFar, "Trop loin du portail.");
                return;
            }

            if (_isInstance)
            {
                Reject(client, ErrorCode.NotAllowed, "Vous êtes déjà dans un donjon.");
                return;
            }

            Domain.Party.Party? party = _directory.Parties.PartyOf(leader.EntityId);
            if (party != null && party.LeaderEntityId != leader.EntityId)
            {
                Reject(client, ErrorCode.NotAllowed, "Seul le chef du groupe peut entrer.");
                return;
            }

            WorldSimulation instance = _directory.CreateInstance(zoneId!);

            // Le chef entre, ainsi que les membres du groupe présents à proximité.
            var entrants = new List<(IWorldClient Client, Player Player)> { (client, leader) };
            if (party != null)
            {
                foreach (uint memberId in party.Members)
                {
                    if (memberId == leader.EntityId)
                    {
                        continue;
                    }

                    Player? member = FindPlayer(memberId);
                    if (member != null && DistanceXZ(leader.X, leader.Z, member.X, member.Z) <= DungeonGroupRange
                        && _directory.TryGetOnline(memberId, out IWorldClient? memberClient, out _) && memberClient != null)
                    {
                        entrants.Add((memberClient, member));
                    }
                }
            }

            for (int i = 0; i < entrants.Count; i++)
            {
                (IWorldClient entrantClient, Player entrant) = entrants[i];
                if (ReleasePlayer(entrantClient) == null)
                {
                    continue;
                }

                float x = instance.PlayerSpawnX + i * EntrySpacing;
                instance.AdoptPlayer(entrantClient, entrant, x, instance.PlayerSpawnZ);
            }

            _logger.LogInformation("DungeonEntered {Zone} {Count}", zoneId, entrants.Count);
        }

        /// <summary>
        /// Résout la zone à instancier pour une porte : une aventure choisie (<paramref name="adventureId"/>
        /// non nul, doit être proposée par ce PNJ) ou, par défaut, sa porte simple fixe (<c>DungeonZone</c>).
        /// </summary>
        private bool TryResolveZone(NpcDefinition npc, ushort adventureId, out string? zoneId, out string rejectReason)
        {
            if (adventureId != 0)
            {
                if (!npc.OfferedAdventureIds.Contains(adventureId))
                {
                    zoneId = null;
                    rejectReason = "Ce PNJ ne propose pas cette aventure.";
                    return false;
                }

                if (!_content.Adventures.TryGetValue(adventureId, out AdventureDefinition? adventure))
                {
                    zoneId = null;
                    rejectReason = "Aventure inconnue.";
                    return false;
                }

                zoneId = adventure.ZoneId;
                rejectReason = string.Empty;
                return true;
            }

            if (npc.DungeonZone == null)
            {
                zoneId = null;
                rejectReason = "Ce PNJ ne propose aucun donjon.";
                return false;
            }

            zoneId = npc.DungeonZone;
            rejectReason = string.Empty;
            return true;
        }

        /// <summary>Gère le boss de chaque instance : phases, onde de choc et renforts. Ne fait rien sans boss en combat.</summary>
        private void UpdateBosses(float deltaSeconds)
        {
            foreach (CreatureInstance boss in _creatures.ToArray())
            {
                if (!boss.Template.IsBoss || !boss.IsAlive || boss.State == CreatureState.Idle || boss.State == CreatureState.Return)
                {
                    continue;
                }

                CreatureTemplate template = boss.Template;
                float healthPercent = 100f * boss.Vitals.Current / boss.Vitals.MaxHealth;

                if (!boss.HasSummoned && template.SummonTemplateId != 0 && healthPercent <= template.SummonAtHealthPercent)
                {
                    boss.HasSummoned = true;
                    Summon(boss);
                }

                if (!boss.IsEnraged && healthPercent <= template.EnrageAtHealthPercent)
                {
                    boss.IsEnraged = true;
                    _logger.LogInformation("BossEnraged {BossId}", boss.EntityId);
                }

                if (boss.PendingShockwaveSeconds >= 0f)
                {
                    boss.PendingShockwaveSeconds -= deltaSeconds;
                    if (boss.PendingShockwaveSeconds < 0f)
                    {
                        boss.PendingShockwaveSeconds = -1f;
                        ImpactShockwave(boss);
                    }
                }
                else
                {
                    boss.ShockwaveCooldown -= deltaSeconds;
                    if (boss.ShockwaveCooldown <= 0f)
                    {
                        // Le télégraphe prévient les joueurs : ils ont le temps de sortir du rayon.
                        boss.PendingShockwaveSeconds = template.ShockwaveTelegraphSeconds;
                        boss.ShockwaveCooldown = template.ShockwaveIntervalSeconds;
                        int milliseconds = (int)(template.ShockwaveTelegraphSeconds * 1000f);
                        _events.Add(new CombatEventMessage(CombatEventKind.Telegraph, boss.EntityId, boss.EntityId, milliseconds));
                    }
                }
            }
        }

        private void ImpactShockwave(CreatureInstance boss)
        {
            CreatureTemplate template = boss.Template;
            foreach (PlayerSlot slot in _players.Values.ToArray())
            {
                Player player = slot.Player;
                if (!player.IsAlive || DistanceXZ(boss.X, boss.Z, player.X, player.Z) > template.ShockwaveRadius)
                {
                    continue;
                }

                float multiplier = boss.IsEnraged ? template.EnrageDamageMultiplier : 1f;
                int min = (int)(template.ShockwaveDamageMin * multiplier);
                int max = Math.Max(min, (int)(template.ShockwaveDamageMax * multiplier));
                DamageRoll roll = _resolver.RollDamage(min, max, player.Derived.Armor);
                DamagePlayer(player, boss.EntityId, roll.Amount, source: null);
            }
        }

        /// <summary>Invoque les renforts autour du boss. Ils attaquent la cible du boss et disparaissent à leur mort.</summary>
        private void Summon(CreatureInstance boss)
        {
            CreatureTemplate addTemplate = _content.Creatures[boss.Template.SummonTemplateId];
            for (int i = 0; i < boss.Template.SummonCount; i++)
            {
                float angle = i * MathF.PI * 2f / Math.Max(1, boss.Template.SummonCount);
                float x = boss.X + MathF.Cos(angle) * 3f;
                float z = boss.Z + MathF.Sin(angle) * 3f;
                var add = new CreatureInstance(_directory.AllocateEntityId(), addTemplate, x, z)
                {
                    Transient = true,
                };

                if (boss.TargetId is uint target)
                {
                    add.ReceiveHit(target, 1);
                }

                _creatures.Add(add);
            }

            _logger.LogInformation("BossSummoned {BossId} {Count}", boss.EntityId, boss.Template.SummonCount);
        }

        /// <summary>Victoire : le donjon est terminé, l'instance se fermera après un délai.</summary>
        private void CompleteDungeon(CreatureInstance boss)
        {
            _events.Add(new CombatEventMessage(CombatEventKind.Dungeon, boss.EntityId, boss.EntityId, 1));
            _logger.LogInformation("DungeonCompleted {BossId} {Instance}", boss.EntityId, _isInstance);

            if (_isInstance)
            {
                _closeCountdown = InstanceCloseDelaySeconds;
            }
        }

        /// <summary>Ferme les instances : après la victoire, ou immédiatement si plus personne n'y est.</summary>
        private void UpdateInstanceLifetime(float deltaSeconds)
        {
            if (!_isInstance)
            {
                return;
            }

            if (_players.Count == 0)
            {
                _directory.RemoveInstance(this);
                return;
            }

            if (_closeCountdown < 0f)
            {
                return;
            }

            _closeCountdown -= deltaSeconds;
            if (_closeCountdown > 0f)
            {
                return;
            }

            WorldSimulation main = _directory.Main;
            foreach (PlayerSlot slot in _players.Values.ToArray())
            {
                Player? player = ReleasePlayer(slot.Client);
                if (player != null)
                {
                    main.AdoptPlayer(slot.Client, player, main.PlayerSpawnX, main.PlayerSpawnZ);
                }
            }

            _directory.RemoveInstance(this);
            _logger.LogInformation("InstanceClosed");
        }

        /// <summary>Point d'apparition des joueurs dans ce monde.</summary>
        public float PlayerSpawnX => _content.PlayerSpawnX;

        public float PlayerSpawnZ => _content.PlayerSpawnZ;
    }
}
