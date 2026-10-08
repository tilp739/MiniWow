using System;
using GameShared.Messages;

namespace MiniWow.Client.Gameplay
{
    /// <summary>Transforme un événement de combat en ligne de journal. Pure logique, sans effet de bord.</summary>
    public static class CombatTextFormatter
    {
        public static string Format(CombatEventMessage combatEvent, uint localEntityId, Func<uint, string> nameOf)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.Damage:
                    return $"{nameOf(combatEvent.SourceEntityId)} → {nameOf(combatEvent.TargetEntityId)} : {combatEvent.Amount} dégâts";

                case CombatEventKind.Death:
                    return $"{nameOf(combatEvent.TargetEntityId)} est vaincu.";

                case CombatEventKind.Respawn:
                    return combatEvent.TargetEntityId == localEntityId
                        ? "Vous êtes réapparu au point de départ."
                        : $"{nameOf(combatEvent.TargetEntityId)} réapparaît.";

                case CombatEventKind.Heal:
                    return $"{nameOf(combatEvent.SourceEntityId)} soigne {nameOf(combatEvent.TargetEntityId)} : +{combatEvent.Amount} PV";

                case CombatEventKind.CastStart:
                    return $"{nameOf(combatEvent.SourceEntityId)} lance une compétence ({combatEvent.Amount / 1000f:0.0} s)";

                case CombatEventKind.Telegraph:
                    return $"{nameOf(combatEvent.SourceEntityId)} prépare une onde de choc !";

                case CombatEventKind.Dungeon:
                    return "Le donjon est vaincu ! Fermeture de l'instance dans 30 s.";

                default:
                    return string.Empty;
            }
        }
    }
}
