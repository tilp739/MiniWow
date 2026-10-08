using GameShared.Messages;
using MiniWow.Client.Characters;
using MiniWow.Client.Network;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace MiniWow.Client.Gameplay
{
    /// <summary>
    /// Joueur local : envoie la direction saisie au serveur à 20 Hz et affiche la position que le serveur décide.
    /// Aucune position n'est calculée ici qui fasse foi : le client n'a pas d'autorité sur le mouvement.
    /// </summary>
    public sealed class LocalPlayerController : MonoBehaviour
    {
        private const float SendIntervalSeconds = 0.05f; // 20 Hz, aligné sur le tick serveur
        private const float DirectionScale = 127f;
        private const float PositionSmoothing = 15f;
        private const float RotationSmoothing = 15f;

        [SerializeField] private GameClient _client;
        [SerializeField] private ThirdPersonCamera _camera;
        [SerializeField] private float _groundHeight = 1f; // capsule Unity : pivot au centre, hauteur 2

        private uint _nextSequence = 1;
        private float _sendTimer;
        private bool _hasServerState;
        private Vector3 _serverPosition;
        private float _serverYaw;
        private OrcCharacter _orcVisual;

        /// <summary>Dernière entrée traitée par le serveur. Préparé pour la réconciliation en M2.</summary>
        public uint LastProcessedInput { get; private set; }

        /// <summary>Points de vie, tels que décidés par le serveur.</summary>
        public int Health { get; private set; }

        public int MaxHealth { get; private set; }

        /// <summary>Ressource de classe (mana ou colère), telle que décidée par le serveur.</summary>
        public int Resource { get; private set; }

        public int MaxResource { get; private set; }

        public int Level { get; private set; }

        /// <summary>Vrai dès qu'un snapshot a été reçu pour ce joueur.</summary>
        public bool HasState => _hasServerState;

        /// <summary>Modèle généré (Orc...) du joueur local, ou null si sa race n'a pas encore de modèle dédié.</summary>
        public OrcCharacter OrcCharacter => _orcVisual;

        /// <summary>Animateur du modèle généré (Orc...), ou null si la race n'a pas encore de modèle dédié.</summary>
        public OrcAnimator OrcAnimator => _orcVisual != null ? _orcVisual.Animator : null;

        /// <summary>Petit saut cosmétique (aucun effet serveur : le monde est plat, Y=0). Sans effet si pas de modèle généré.</summary>
        public void PlayJump() => OrcAnimator?.PlayJump();

        /// <summary>Applique l'état autoritaire du serveur pour ce joueur.</summary>
        public void ApplySnapshot(WorldSnapshotMessage snapshot)
        {
            LastProcessedInput = snapshot.LastProcessedInput;

            foreach (EntityState entity in snapshot.Entities)
            {
                if (entity.EntityId != snapshot.YourEntityId)
                {
                    continue;
                }

                _serverPosition = new Vector3(entity.X, _groundHeight, entity.Z);
                _serverYaw = entity.YawDegrees;
                Health = entity.Health;
                MaxHealth = entity.MaxHealth;
                Resource = entity.Resource;
                MaxResource = entity.MaxResource;
                Level = entity.Level;

                if (!_hasServerState)
                {
                    // Race connue une seule fois (elle ne change jamais en jeu) : TemplateId la porte pour un joueur.
                    ApplyRaceVisual((byte)entity.TemplateId);
                }

                _hasServerState = true;
                return;
            }
        }

        /// <summary>
        /// Remplace la capsule générique par le modèle low-poly correspondant à la race choisie à la création
        /// du personnage. Les races sans modèle dédié pour l'instant gardent la capsule (pas de régression).
        /// </summary>
        private void ApplyRaceVisual(byte raceId)
        {
            if (!System.Enum.IsDefined(typeof(CharacterRace), raceId))
            {
                return; // Race inconnue (future race pas encore implémentée) : capsule générique conservée.
            }

            if (TryGetComponent(out MeshRenderer capsuleRenderer))
            {
                capsuleRenderer.enabled = false;
            }

            var visualObject = new GameObject("CharacterVisual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = new Vector3(0f, -_groundHeight, 0f);
            _orcVisual = visualObject.AddComponent<OrcCharacter>(); // génère un corps par défaut dans Awake()...
            _orcVisual.SetRace((CharacterRace)raceId); // ... immédiatement remplacé par la bonne race.
        }

        private void Update()
        {
            _sendTimer -= Time.deltaTime;
            if (_sendTimer <= 0f)
            {
                _sendTimer = SendIntervalSeconds;
                SendCurrentDirection();
            }

            if (_hasServerState)
            {
                Vector3 beforeMove = transform.position;

                float t = 1f - Mathf.Exp(-PositionSmoothing * Time.deltaTime);
                float r = 1f - Mathf.Exp(-RotationSmoothing * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, _serverPosition, t);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _serverYaw, 0f), r);

                if (_orcVisual != null)
                {
                    // Vitesse estimée depuis le mouvement visuel lissé : pas d'autorité, juste de quoi animer.
                    float speed = Vector3.Distance(beforeMove, transform.position) / Mathf.Max(Time.deltaTime, 0.0001f);
                    _orcVisual.Animator.SetMovementSpeed(speed);
                }
            }
        }

        private void SendCurrentDirection()
        {
            (sbyte x, sbyte z) = ReadWorldDirection();
            _client.SendMoveInput(_nextSequence, x, z);
            _nextSequence++;
        }

        /// <summary>
        /// Convertit la saisie (avant/arrière, gauche/droite) en direction monde, relative à la caméra :
        /// « avant » est la direction où regarde la vue, comme dans les MMORPG classiques.
        /// </summary>
        private (sbyte X, sbyte Z) ReadWorldDirection()
        {
            (sbyte localX, sbyte localZ) = ReadDirection();
            if (_camera == null)
            {
                return (localX, localZ);
            }

            Vector3 world = _camera.FlatRight * localX + _camera.FlatForward * localZ;
            if (world.sqrMagnitude > 1f)
            {
                world.Normalize();
            }

            return ((sbyte)Mathf.RoundToInt(world.x * DirectionScale), (sbyte)Mathf.RoundToInt(world.z * DirectionScale));
        }

        private static (sbyte X, sbyte Z) ReadDirection()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return (0, 0);
            }

            float x = (IsHeld(keyboard.dKey, keyboard.rightArrowKey) ? 1f : 0f) - (IsHeld(keyboard.aKey, keyboard.leftArrowKey) ? 1f : 0f);
            float z = (IsHeld(keyboard.wKey, keyboard.upArrowKey) ? 1f : 0f) - (IsHeld(keyboard.sKey, keyboard.downArrowKey) ? 1f : 0f);

            var direction = new Vector2(x, z);
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return ((sbyte)Mathf.RoundToInt(direction.x * DirectionScale), (sbyte)Mathf.RoundToInt(direction.y * DirectionScale));
        }

        private static bool IsHeld(KeyControl primary, KeyControl secondary)
        {
            return primary.isPressed || secondary.isPressed;
        }
    }
}
