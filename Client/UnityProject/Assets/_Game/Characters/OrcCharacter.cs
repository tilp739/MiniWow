using GameShared.Messages;
using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Façade d'un personnage low-poly généré par code (Orc, Humain, ...) : regroupe le squelette,
    /// l'animateur, le rig de sockets et le gestionnaire d'équipement derrière une API simple
    /// (Equip/Unequip/Move). C'est le seul point d'entrée dont le reste du jeu (contrôleur du joueur, IA,
    /// réseau) a besoin. Le nom de la classe date du prototype où seul l'Orc existait ; elle sert maintenant
    /// toutes les races dont la tête a été implémentée dans <see cref="CharacterGenerator"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrcCharacter : MonoBehaviour
    {
        [SerializeField] private CharacterRace race = CharacterRace.Orc;
        [SerializeField] private BodyProportions proportions = BodyProportions.OrcDefault;

        [Tooltip("Vitesse de déplacement (m/s) utilisée par Move() et comme référence pour l'animation de marche.")]
        public float moveSpeed = 2.5f;

        public OrcAnimator Animator { get; private set; }
        public EquipmentManager Equipment { get; private set; }
        public SocketRig SocketRig { get; private set; }
        public CharacterSkeleton Skeleton { get; private set; }
        public BodyProportions Proportions => proportions;
        public CharacterRace Race => race;

        private void Awake()
        {
            // Permet de déposer le composant tel quel dans une scène et d'obtenir un Orc par défaut,
            // sans passer par OrcGenerator. Un appel explicite à Generate()/GenerateRandom() reconstruit
            // le corps avec les proportions voulues (en détruisant l'ancien corps).
            if (Skeleton == null && transform.childCount == 0)
            {
                Generate();
            }
        }

        /// <summary>(Re)génère entièrement le corps à partir des proportions courantes. Peut être appelé plusieurs fois.</summary>
        public void Generate()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyChild(transform.GetChild(i).gameObject);
            }

            Skeleton = CharacterGenerator.Generate(transform, proportions, race);

            SocketRig = GetComponent<SocketRig>() != null ? GetComponent<SocketRig>() : gameObject.AddComponent<SocketRig>();
            SocketRig.Initialize(Skeleton.SocketBindings);

            Equipment = GetComponent<EquipmentManager>() != null ? GetComponent<EquipmentManager>() : gameObject.AddComponent<EquipmentManager>();
            Equipment.Initialize(Skeleton, proportions, SocketRig);

            Animator = GetComponent<OrcAnimator>() != null ? GetComponent<OrcAnimator>() : gameObject.AddComponent<OrcAnimator>();
            Animator.walkSpeed = moveSpeed;
            Animator.Initialize(Skeleton);
        }

        /// <summary>Applique de nouvelles proportions et régénère immédiatement le corps.</summary>
        public void SetProportions(BodyProportions newProportions)
        {
            proportions = newProportions;
            Generate();
        }

        /// <summary>Génère un Orc aléatoire mais reproductible : la même seed donne toujours le même Orc.</summary>
        public void GenerateRandom(int seed)
        {
            race = CharacterRace.Orc;
            proportions = BodyProportions.RandomOrc(seed);
            Generate();
        }

        /// <summary>
        /// Change de race (et applique ses proportions par défaut), puis régénère. C'est ce qu'appellent
        /// <see cref="MiniWow.Client.Gameplay.LocalPlayerController"/> et
        /// <see cref="MiniWow.Client.Gameplay.RemoteEntities"/> une fois la race connue (depuis le serveur).
        /// </summary>
        public void SetRace(CharacterRace newRace)
        {
            race = newRace;
            proportions = race == CharacterRace.Human ? BodyProportions.HumanDefault : BodyProportions.OrcDefault;
            Generate();
        }

        public void Equip(EquipmentSlot slot, EquipmentData data) => Equipment.Equip(slot, data);

        public void Unequip(EquipmentSlot slot) => Equipment.Unequip(slot);

        /// <summary>
        /// Point d'entrée prévu pour un futur système de déplacement : avance le personnage dans
        /// <paramref name="direction"/> et pilote automatiquement l'animation (Idle à vitesse nulle,
        /// Walk sinon), sans que l'appelant ait à gérer lui-même l'état de l'animateur.
        /// </summary>
        public void Move(Vector3 direction)
        {
            float magnitude = direction.magnitude;
            if (magnitude > 0.0001f)
            {
                Vector3 flatDirection = direction.normalized;
                transform.position += flatDirection * moveSpeed * magnitude * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(flatDirection, Vector3.up);
            }

            Animator.SetMovementSpeed(magnitude * moveSpeed);
        }

        private static void DestroyChild(GameObject go)
        {
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }
    }
}
