using System.Collections.Generic;
using GameShared.Messages;
using MiniWow.Client.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWow.Client.Gameplay
{
    /// <summary>
    /// Sélection de cible et attaque, à la manière des MMORPG :
    /// clic gauche sélectionne sans agir, clic droit sélectionne ET déclenche l'action par défaut de la
    /// cible (attaquer/looter/parler/récolter) — sauf si la souris a bougé pendant l'appui, auquel cas c'est
    /// l'orbite caméra (<see cref="ThirdPersonCamera"/>) qui prime, sans action. Tab fait défiler les entités
    /// proches, Échap désélectionne et désengage, Espace attaque/engage la cible. Le serveur valide tout ;
    /// ce script ne décide d'aucun dégât.
    /// </summary>
    public sealed class CombatController : MonoBehaviour
    {
        private const float TabRange = 40f;

        /// <summary>Au-delà de ce déplacement en pixels pendant l'appui, le clic droit est une rotation de caméra, pas une interaction.</summary>
        private const float RightClickDragThresholdPixels = 6f;

        [SerializeField] private GameClient _client;
        [SerializeField] private RemoteEntities _entities;
        [SerializeField] private ThirdPersonCamera _camera;
        [SerializeField] private float _clickRange = 200f;

        private bool _rightButtonTracking;
        private bool _rightButtonDragged;
        private Vector2 _rightButtonPressPosition;

        public uint? TargetId { get; private set; }

        /// <summary>Sélectionne une entité (ou désélectionne avec null).</summary>
        public void SetTarget(uint? entityId)
        {
            TargetId = entityId;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null || !_client.IsInWorld)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && _camera != null && _camera.Camera != null)
            {
                PickUnderCursor(mouse);
            }

            HandleRightClick(mouse);

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                CycleTarget();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                TargetId = null;
                _client.SendDisengage();
            }

            // Une cible disparue (sortie du champ de vision, mort d'une créature) est désélectionnée.
            // Un cadavre reste ciblable tant qu'il est visible : c'est ce qui permet de le looter.
            if (TargetId.HasValue && !_entities.TryGetInfo(TargetId.Value, out _))
            {
                TargetId = null;
            }

            if (keyboard.spaceKey.wasPressedThisFrame && TargetId.HasValue)
            {
                _client.SendAttack(TargetId.Value);
            }
        }

        private void PickUnderCursor(Mouse mouse)
        {
            Ray ray = _camera.Camera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, _clickRange)
                && _entities.TryGetEntityByCollider(hit.collider, out uint entityId))
            {
                TargetId = entityId;
            }
        }

        /// <summary>
        /// Clic droit, à la manière des MMORPG classiques : un simple clic (sans déplacement de souris pendant
        /// l'appui) sélectionne la cible sous le curseur et déclenche son action par défaut. Un clic droit
        /// maintenu avec déplacement ne fait qu'orbiter la caméra (<see cref="ThirdPersonCamera"/>, qui lit le
        /// bouton indépendamment) : aucune action n'est alors déclenchée.
        /// </summary>
        private void HandleRightClick(Mouse mouse)
        {
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rightButtonTracking = true;
                _rightButtonDragged = false;
                _rightButtonPressPosition = mouse.position.ReadValue();
            }
            else if (_rightButtonTracking && mouse.rightButton.isPressed && !_rightButtonDragged)
            {
                float moved = Vector2.Distance(mouse.position.ReadValue(), _rightButtonPressPosition);
                if (moved > RightClickDragThresholdPixels)
                {
                    _rightButtonDragged = true;
                }
            }

            if (mouse.rightButton.wasReleasedThisFrame)
            {
                if (_rightButtonTracking && !_rightButtonDragged && _camera != null && _camera.Camera != null)
                {
                    InteractUnderCursor(mouse);
                }

                _rightButtonTracking = false;
            }
        }

        /// <summary>Sélectionne l'entité sous le curseur et déclenche son action par défaut selon son type.</summary>
        private void InteractUnderCursor(Mouse mouse)
        {
            Ray ray = _camera.Camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, _clickRange)
                || !_entities.TryGetEntityByCollider(hit.collider, out uint entityId)
                || !_entities.TryGetInfo(entityId, out EntityInfo info))
            {
                return;
            }

            TargetId = entityId;

            switch (info.Kind)
            {
                case EntityKind.Creature:
                    if (info.Health > 0)
                    {
                        _client.SendAttack(entityId);
                    }
                    else
                    {
                        _client.SendLoot(entityId);
                    }

                    break;

                case EntityKind.Npc:
                    _client.SendNpc(NpcActionKind.Talk, entityId);
                    break;

                case EntityKind.GatheringNode:
                    _client.SendGather(entityId);
                    break;
            }
        }

        /// <summary>Tab : passe à l'entité suivante, de la plus proche à la plus lointaine, puis revient au début.</summary>
        private void CycleTarget()
        {
            List<uint> candidates = _entities.VisibleIdsByDistance(transform.position, TabRange);
            if (candidates.Count == 0)
            {
                TargetId = null;
                return;
            }

            int current = TargetId.HasValue ? candidates.IndexOf(TargetId.Value) : -1;
            TargetId = candidates[(current + 1) % candidates.Count];
        }
    }
}
