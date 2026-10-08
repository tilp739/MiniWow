using System.Collections.Generic;
using GameShared.Messages;
using MiniWow.Client.Characters;
using UnityEngine;

namespace MiniWow.Client.Gameplay
{
    /// <summary>Informations sur une entité distante, telles que décidées par le serveur.</summary>
    public readonly struct EntityInfo
    {
        public EntityInfo(EntityKind kind, int templateId, int health, int maxHealth, Vector3 position)
        {
            Kind = kind;
            TemplateId = templateId;
            Health = health;
            MaxHealth = maxHealth;
            Position = position;
        }

        public EntityKind Kind { get; }

        public int TemplateId { get; }

        public int Health { get; }

        public int MaxHealth { get; }

        public Vector3 Position { get; }
    }

    /// <summary>
    /// Entités visibles autres que le joueur local (autres joueurs et créatures).
    /// Une entité apparaît quand elle entre dans un snapshot, disparaît quand elle en sort,
    /// et est interpolée vers la dernière position reçue.
    /// </summary>
    public sealed class RemoteEntities : MonoBehaviour
    {
        private const float GroundHeight = 1f; // capsule Unity : pivot au centre
        private const float Smoothing = 10f;

        private readonly Dictionary<uint, RemoteView> _views = new Dictionary<uint, RemoteView>();
        private readonly Dictionary<Collider, uint> _colliderOwners = new Dictionary<Collider, uint>();
        private readonly Dictionary<uint, EntityInfo> _lastKnown = new Dictionary<uint, EntityInfo>();
        private readonly HashSet<uint> _present = new HashSet<uint>();
        private readonly List<uint> _departed = new List<uint>();

        public int VisibleCount => _views.Count;

        /// <summary>Toutes les entités actuellement visibles (pour la minimap, par exemple).</summary>
        public IEnumerable<(uint EntityId, EntityInfo Info)> AllVisible()
        {
            foreach (KeyValuePair<uint, RemoteView> entry in _views)
            {
                yield return (entry.Key, entry.Value.Info);
            }
        }

        /// <summary>Infos courantes d'une entité visible.</summary>
        public bool TryGetInfo(uint entityId, out EntityInfo info)
        {
            if (_views.TryGetValue(entityId, out RemoteView view))
            {
                info = view.Info;
                return true;
            }

            info = default;
            return false;
        }

        /// <summary>
        /// Dernières infos connues, y compris pour une entité qui vient de disparaître
        /// (une créature morte est retirée du snapshot, mais son événement de mort arrive juste après).
        /// </summary>
        public bool TryGetLastKnown(uint entityId, out EntityInfo info)
        {
            return _lastKnown.TryGetValue(entityId, out info);
        }

        /// <summary>Animateur du modèle généré d'un joueur visible (Orc...), ou false si non trouvé/pas de modèle dédié.</summary>
        public bool TryGetOrcAnimator(uint entityId, out OrcAnimator animator)
        {
            if (_views.TryGetValue(entityId, out RemoteView view) && view.OrcVisual != null)
            {
                animator = view.OrcVisual.Animator;
                return animator != null;
            }

            animator = null;
            return false;
        }

        /// <summary>Créature visible la plus proche, dans un rayon donné. Null si aucune.</summary>
        public uint? FindNearestCreature(Vector3 from, float maxRange) => FindNearest(EntityKind.Creature, from, maxRange);

        /// <summary>Entité visible la plus proche d'un type donné, dans un rayon. Null si aucune.</summary>
        public uint? FindNearest(EntityKind kind, Vector3 from, float maxRange)
        {
            uint? nearest = null;
            float nearestDistance = maxRange;

            foreach (KeyValuePair<uint, RemoteView> entry in _views)
            {
                if (entry.Value.Info.Kind != kind)
                {
                    continue;
                }

                float distance = Vector3.Distance(from, entry.Value.Info.Position);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = entry.Key;
                }
            }

            return nearest;
        }

        /// <summary>Met à jour les entités visibles d'après le snapshot. Le joueur local est ignoré.</summary>
        public void ApplySnapshot(WorldSnapshotMessage snapshot)
        {
            _present.Clear();

            foreach (EntityState entity in snapshot.Entities)
            {
                if (entity.EntityId == snapshot.YourEntityId)
                {
                    continue;
                }

                _present.Add(entity.EntityId);

                var position = new Vector3(entity.X, GroundHeight, entity.Z);
                var info = new EntityInfo(entity.Kind, entity.TemplateId, entity.Health, entity.MaxHealth, position);
                _lastKnown[entity.EntityId] = info;

                bool created = false;
                if (!_views.TryGetValue(entity.EntityId, out RemoteView view))
                {
                    view = CreateView(entity);
                    _views.Add(entity.EntityId, view);
                    created = true;
                }

                view.SetTarget(info, entity.YawDegrees);
                if (created)
                {
                    view.Snap();
                }
            }

            _departed.Clear();
            foreach (uint id in _views.Keys)
            {
                if (!_present.Contains(id))
                {
                    _departed.Add(id);
                }
            }

            foreach (uint id in _departed)
            {
                RemoteView view = _views[id];
                _colliderOwners.Remove(view.Collider);
                Destroy(view.Root.gameObject);
                _views.Remove(id);
            }
        }

        private void Update()
        {
            float t = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
            foreach (RemoteView view in _views.Values)
            {
                view.Interpolate(t);
            }
        }

        /// <summary>Entité visible dont le collider a été touché par un rayon (clic de sélection).</summary>
        public bool TryGetEntityByCollider(Collider collider, out uint entityId) =>
            _colliderOwners.TryGetValue(collider, out entityId);

        /// <summary>Identifiants visibles dans un rayon, du plus proche au plus loin.</summary>
        public List<uint> VisibleIdsByDistance(Vector3 from, float maxRange)
        {
            var sorted = new List<(float Distance, uint Id)>();
            foreach (KeyValuePair<uint, RemoteView> entry in _views)
            {
                float distance = Vector3.Distance(from, entry.Value.Info.Position);
                if (distance <= maxRange)
                {
                    sorted.Add((distance, entry.Key));
                }
            }

            sorted.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            var ids = new List<uint>(sorted.Count);
            foreach ((float _, uint id) in sorted)
            {
                ids.Add(id);
            }

            return ids;
        }

        private RemoteView CreateView(EntityState entity)
        {
            // Joueur d'une race avec modèle dédié : généré par code plutôt que la capsule générique
            // (TemplateId porte la race pour un joueur, cf. EntityState). Race inconnue -> capsule générique.
            if (entity.Kind == EntityKind.Player && System.Enum.IsDefined(typeof(CharacterRace), (byte)entity.TemplateId))
            {
                return CreateGeneratedPlayerView(entity, (CharacterRace)entity.TemplateId);
            }

            // Créature quadrupède avec un modèle low-poly dédié (loup/sanglier) ; le boss garde la sphère
            // générique pour l'instant (forme unique, pas encore modélisée).
            if (entity.Kind == EntityKind.Creature && TryGetQuadrupedProfile(entity.TemplateId, out QuadrupedKind quadrupedKind, out float quadrupedScale, out Color quadrupedColor))
            {
                return CreateQuadrupedView(entity, quadrupedKind, quadrupedScale, quadrupedColor);
            }

            // Créature humanoïde (squelette, nécromancien) : réutilise le générateur de personnage (corps
            // partagé avec les joueurs), juste le mesh statique, sans animateur ni équipement.
            if (entity.Kind == EntityKind.Creature && TryGetHumanoidCreatureProfile(entity.TemplateId, out BodyProportions humanoidProportions, out CharacterRace humanoidRace))
            {
                return CreateHumanoidCreatureView(entity, humanoidProportions, humanoidRace);
            }

            PrimitiveType shape;
            Color color;
            switch (entity.Kind)
            {
                case EntityKind.Creature:
                    shape = PrimitiveType.Sphere;
                    color = new Color(0.75f, 0.25f, 0.2f);
                    break;

                case EntityKind.Npc:
                    shape = PrimitiveType.Cube;
                    color = new Color(0.9f, 0.75f, 0.3f);
                    break;

                case EntityKind.GatheringNode:
                    shape = PrimitiveType.Cylinder;
                    color = new Color(0.3f, 0.7f, 0.35f);
                    break;

                default:
                    shape = PrimitiveType.Capsule;
                    color = Color.HSVToRGB((entity.EntityId * 0.618034f) % 1f, 0.55f, 0.9f);
                    break;
            }

            GameObject body = GameObject.CreatePrimitive(shape);
            body.name = $"{entity.Kind}_{entity.EntityId}";
            body.transform.SetParent(transform, false);

            if (entity.Kind == EntityKind.Creature)
            {
                body.transform.localScale = Vector3.one * 1.2f;
            }

            // Le collider ne sert qu'à la sélection au clic : aucune physique, le serveur décide de tout le reste.
            Collider collider = body.GetComponent<Collider>();
            body.GetComponent<Renderer>().material.color = color;
            _colliderOwners[collider] = entity.EntityId;

            return new RemoteView(body.transform, entity.EntityId, collider);
        }

        /// <summary>
        /// Anchor positionné/tourné comme les autres vues (convention GroundHeight) ; le corps généré est un
        /// enfant décalé vers le bas puisqu'il est construit pieds à y=0, alors que GroundHeight suit le pivot
        /// central d'une capsule standard.
        /// </summary>
        private RemoteView CreateGeneratedPlayerView(EntityState entity, CharacterRace race)
        {
            var anchor = new GameObject($"Player_{entity.EntityId}_{race}");
            anchor.transform.SetParent(transform, false);

            var visual = new GameObject("CharacterVisual");
            visual.transform.SetParent(anchor.transform, false);
            visual.transform.localPosition = new Vector3(0f, -GroundHeight, 0f);
            OrcCharacter character = visual.AddComponent<OrcCharacter>(); // génère un corps par défaut dans Awake()...
            character.SetRace(race); // ... immédiatement remplacé par la bonne race.

            // Le collider ne sert qu'à la sélection au clic : aucune physique, le serveur décide de tout le reste.
            CapsuleCollider collider = anchor.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.5f;
            _colliderOwners[collider] = entity.EntityId;

            return new RemoteView(anchor.transform, entity.EntityId, collider, character);
        }

        /// <summary>
        /// Associe un identifiant de créature (cf. Content/creatures.json) à un archétype de quadrupède.
        /// </summary>
        private static bool TryGetQuadrupedProfile(ushort templateId, out QuadrupedKind kind, out float scale, out Color color)
        {
            switch (templateId)
            {
                case 1: // grey_wolf
                    kind = QuadrupedKind.Wolf;
                    scale = 1f;
                    color = new Color(0.55f, 0.55f, 0.58f);
                    return true;

                case 6: // alpha_wolf (élite)
                    kind = QuadrupedKind.Wolf;
                    scale = 1.3f;
                    color = new Color(0.32f, 0.2f, 0.18f);
                    return true;

                case 2: // wild_boar
                    kind = QuadrupedKind.Boar;
                    scale = 1f;
                    color = new Color(0.45f, 0.32f, 0.2f);
                    return true;

                case 7: // savage_boar_elite
                    kind = QuadrupedKind.Boar;
                    scale = 1.25f;
                    color = new Color(0.4f, 0.15f, 0.12f);
                    return true;

                default:
                    kind = default;
                    scale = 1f;
                    color = default;
                    return false;
            }
        }

        /// <summary>Même convention d'ancrage que <see cref="CreateGeneratedPlayerView"/> : modèle construit pieds à y=0.</summary>
        private RemoteView CreateQuadrupedView(EntityState entity, QuadrupedKind kind, float scale, Color color)
        {
            var anchor = new GameObject($"Creature_{entity.EntityId}_{kind}");
            anchor.transform.SetParent(transform, false);

            Mesh mesh = CreatureMeshBuilder.BuildQuadruped(kind, scale);
            Material material = CharacterMaterials.GetOrCreate($"Creature_{kind}", color);
            GameObject visual = LowPolyMeshBuilder.CreateMeshObject("Visual", mesh, material, anchor.transform);
            visual.transform.localPosition = new Vector3(0f, -GroundHeight, 0f);

            // Le collider ne sert qu'à la sélection au clic : aucune physique, le serveur décide de tout le reste.
            CapsuleCollider collider = anchor.AddComponent<CapsuleCollider>();
            collider.height = 1.1f * scale;
            collider.radius = 0.5f * scale;
            collider.direction = 2; // axe Z : plus adapté à un corps allongé vers l'avant qu'un axe vertical
            _colliderOwners[collider] = entity.EntityId;

            return new RemoteView(anchor.transform, entity.EntityId, collider);
        }

        /// <summary>Associe un identifiant de créature humanoïde à un gabarit de corps + une race (pour la tête).</summary>
        private static bool TryGetHumanoidCreatureProfile(ushort templateId, out BodyProportions proportions, out CharacterRace race)
        {
            switch (templateId)
            {
                case 4: // crypt_skeleton
                    proportions = BodyProportions.HumanDefault;
                    proportions.BodyWidth *= 0.78f;
                    proportions.ShoulderWidth *= 0.85f;
                    proportions.SkinColor = new Color(0.82f, 0.8f, 0.72f);
                    race = CharacterRace.Human;
                    return true;

                case 5: // renegade_necromancer
                    proportions = BodyProportions.HumanDefault;
                    proportions.Height *= 1.05f;
                    proportions.SkinColor = new Color(0.18f, 0.12f, 0.22f);
                    race = CharacterRace.Human;
                    return true;

                case 3: // ancient_warden (boss) : pas de mesh dédié "golem" pour l'instant, mais un Orc
                        // massif teinté pierre donne déjà une silhouette bien plus imposante qu'une sphère.
                    proportions = BodyProportions.OrcDefault;
                    proportions.Height *= 1.55f;
                    proportions.ShoulderWidth *= 1.3f;
                    proportions.BodyWidth *= 1.35f;
                    proportions.HeadSize *= 1.1f;
                    proportions.SkinColor = new Color(0.5f, 0.5f, 0.52f);
                    race = CharacterRace.Orc;
                    return true;

                default:
                    proportions = default;
                    race = default;
                    return false;
            }
        }

        /// <summary>
        /// Réutilise <see cref="CharacterGenerator"/> directement (sans OrcCharacter) : juste le mesh statique,
        /// aucun animateur ni équipement, puisque les créatures ne sont pas encore animées dans ce prototype.
        /// </summary>
        private RemoteView CreateHumanoidCreatureView(EntityState entity, BodyProportions proportions, CharacterRace race)
        {
            var anchor = new GameObject($"Creature_{entity.EntityId}_{race}");
            anchor.transform.SetParent(transform, false);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(anchor.transform, false);
            visual.transform.localPosition = new Vector3(0f, -GroundHeight, 0f);
            CharacterGenerator.Generate(visual.transform, proportions, race);

            // Le collider ne sert qu'à la sélection au clic : aucune physique, le serveur décide de tout le reste.
            CapsuleCollider collider = anchor.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.5f;
            _colliderOwners[collider] = entity.EntityId;

            return new RemoteView(anchor.transform, entity.EntityId, collider);
        }

        private sealed class RemoteView
        {
            private Vector3 _targetPosition;
            private Quaternion _targetRotation;

            public RemoteView(Transform root, uint entityId, Collider collider, OrcCharacter orcVisual = null)
            {
                Root = root;
                EntityId = entityId;
                Collider = collider;
                OrcVisual = orcVisual;
                _targetPosition = root.position;
                _targetRotation = root.rotation;
            }

            public Transform Root { get; }

            public uint EntityId { get; }

            public Collider Collider { get; }

            /// <summary>Non nul seulement pour un joueur Orc : alimenté en vitesse à chaque <see cref="Interpolate"/>.</summary>
            public OrcCharacter OrcVisual { get; }

            public EntityInfo Info { get; private set; }

            public void SetTarget(EntityInfo info, float yawDegrees)
            {
                Info = info;
                _targetPosition = info.Position;
                _targetRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            }

            /// <summary>Place directement la vue sur sa cible, sans interpolation (apparition).</summary>
            public void Snap()
            {
                Root.position = _targetPosition;
                Root.rotation = _targetRotation;
            }

            public void Interpolate(float t)
            {
                Vector3 before = Root.position;
                Root.position = Vector3.Lerp(Root.position, _targetPosition, t);
                Root.rotation = Quaternion.Slerp(Root.rotation, _targetRotation, t);

                if (OrcVisual != null)
                {
                    float speed = Vector3.Distance(before, Root.position) / Mathf.Max(Time.deltaTime, 0.0001f);
                    OrcVisual.Animator.SetMovementSpeed(speed);
                }
            }
        }
    }
}
