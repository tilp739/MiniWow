using GameShared.Messages;
using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Construit la hiérarchie complète d'un personnage low-poly sous un Transform racine : corps (Body),
    /// conteneurs d'équipement (Equipment) et points d'attache (Sockets). Fonctions de construction
    /// par partie (<see cref="CreateLowPolyHead"/>, <see cref="CreateLowPolyTorso"/>, ...) réutilisables
    /// indépendamment. Le corps (torse/bras/jambes/mains/pieds) est partagé entre races : seule la tête
    /// change de forme selon <see cref="CharacterRace"/> (<see cref="CreateLowPolyHead"/> pour l'Orc,
    /// <see cref="CreateLowPolyHumanHead"/> pour l'Humain). Une future race (Elfe, Nain, ...) suit le même
    /// principe : une nouvelle fonction de tête, rien d'autre à réécrire.
    /// </summary>
    public static class CharacterGenerator
    {
        private const int LimbSides = 7;
        private const int HornSides = 6;

        public static CharacterSkeleton Generate(Transform root, BodyProportions proportions, CharacterRace race = CharacterRace.Orc)
        {
            BodyLayout layout = BodyLayout.Compute(proportions);
            Material skin = CharacterMaterials.SkinMaterial(proportions.SkinColor);

            var skeleton = new CharacterSkeleton();

            Transform body = NewChild(root, "Body");
            skeleton.Body = body;

            skeleton.Torso = CreateLowPolyTorso(body, layout, skin);
            skeleton.Head = race == CharacterRace.Human
                ? CreateLowPolyHumanHead(body, layout, skin)
                : CreateLowPolyHead(body, layout, skin);

            skeleton.LeftArm = CreateLowPolyArm(body, "LeftArm", layout, skin, mirror: -1f);
            skeleton.RightArm = CreateLowPolyArm(body, "RightArm", layout, skin, mirror: 1f);
            // Enfants du bras (pas de Body) : la main est au poignet, elle doit suivre le balancement du bras.
            skeleton.LeftHand = CreateLowPolyHand(skeleton.LeftArm, "LeftHand", layout, skin);
            skeleton.RightHand = CreateLowPolyHand(skeleton.RightArm, "RightHand", layout, skin);

            skeleton.LeftLeg = CreateLowPolyLeg(body, "LeftLeg", layout, skin, mirror: -1f);
            skeleton.RightLeg = CreateLowPolyLeg(body, "RightLeg", layout, skin, mirror: 1f);
            // Enfants de la jambe (pas de Body) : le pied est à la cheville, il doit suivre le balancement de la jambe.
            skeleton.LeftFoot = CreateLowPolyFoot(skeleton.LeftLeg, "LeftFoot", layout, skin);
            skeleton.RightFoot = CreateLowPolyFoot(skeleton.RightLeg, "RightFoot", layout, skin);

            Transform equipment = NewChild(root, "Equipment");
            skeleton.EquipmentHead = NewChild(equipment, "Head");
            skeleton.EquipmentChest = NewChild(equipment, "Chest");
            skeleton.EquipmentHands = NewChild(equipment, "Hands");
            skeleton.EquipmentLegs = NewChild(equipment, "Legs");
            skeleton.EquipmentFeet = NewChild(equipment, "Feet");
            skeleton.EquipmentMainHand = NewChild(equipment, "MainHand");

            Transform sockets = NewChild(root, "Sockets");
            skeleton.HeadSocket = NewChild(sockets, "HeadSocket");
            skeleton.ChestSocket = NewChild(sockets, "ChestSocket");
            skeleton.LeftHandSocket = NewChild(sockets, "LeftHandSocket");
            skeleton.RightHandSocket = NewChild(sockets, "RightHandSocket");
            skeleton.LegsSocket = NewChild(sockets, "LegsSocket");
            skeleton.LeftFootSocket = NewChild(sockets, "LeftFootSocket");
            skeleton.RightFootSocket = NewChild(sockets, "RightFootSocket");

            // Les sockets épousent exactement le pivot de leur partie du corps (offset nul) : chaque
            // EquipmentData construit son mesh avec la même convention de pivot que la partie couverte.
            skeleton.SocketBindings.Add(new SocketBinding { BodyPart = skeleton.Head, Socket = skeleton.HeadSocket, LocalOffset = Vector3.zero, LocalRotationOffset = Quaternion.identity });
            skeleton.SocketBindings.Add(new SocketBinding { BodyPart = skeleton.Torso, Socket = skeleton.ChestSocket, LocalOffset = Vector3.zero, LocalRotationOffset = Quaternion.identity });
            skeleton.SocketBindings.Add(new SocketBinding { BodyPart = skeleton.LeftHand, Socket = skeleton.LeftHandSocket, LocalOffset = Vector3.zero, LocalRotationOffset = Quaternion.identity });
            skeleton.SocketBindings.Add(new SocketBinding { BodyPart = skeleton.RightHand, Socket = skeleton.RightHandSocket, LocalOffset = Vector3.zero, LocalRotationOffset = Quaternion.identity });
            // Le pantalon est rattaché au bassin (Torso) plutôt qu'à une jambe précise : une seule pièce
            // ne peut pas suivre deux jambes qui se balancent indépendamment. Voir la remarque dans
            // PantsEquipmentData. C'est la principale limitation connue de ce prototype.
            skeleton.SocketBindings.Add(new SocketBinding { BodyPart = skeleton.Torso, Socket = skeleton.LegsSocket, LocalOffset = Vector3.zero, LocalRotationOffset = Quaternion.identity });
            skeleton.SocketBindings.Add(new SocketBinding { BodyPart = skeleton.LeftFoot, Socket = skeleton.LeftFootSocket, LocalOffset = Vector3.zero, LocalRotationOffset = Quaternion.identity });
            skeleton.SocketBindings.Add(new SocketBinding { BodyPart = skeleton.RightFoot, Socket = skeleton.RightFootSocket, LocalOffset = Vector3.zero, LocalRotationOffset = Quaternion.identity });

            return skeleton;
        }

        public static Transform CreateLowPolyTorso(Transform parent, BodyLayout layout, Material material)
        {
            float hipWidth = layout.HipX * 2f * 0.85f;
            float shoulderWidth = layout.ShoulderX * 2f * 0.95f;

            Mesh mesh = LowPolyMeshBuilder.TaperedBox(
                bottomSize: new Vector3(hipWidth, 0f, layout.BodyDepth),
                topSize: new Vector3(shoulderWidth, 0f, layout.BodyDepth * 1.05f),
                height: layout.TorsoHeight);

            GameObject go = LowPolyMeshBuilder.CreateMeshObject("Torso", mesh, material, parent);
            go.transform.localPosition = new Vector3(0f, layout.HipY, 0f);
            return go.transform;
        }

        public static Transform CreateLowPolyHead(Transform parent, BodyLayout layout, Material material)
        {
            float h = layout.HeadHeight;

            // Mâchoire massive (large), base du visage.
            Mesh jaw = LowPolyMeshBuilder.Box(new Vector3(0.56f * h, 0.3f * h, 0.5f * h));

            // Crâne, plus étroit, posé sur la mâchoire.
            Mesh skull = LowPolyMeshBuilder.Box(new Vector3(0.46f * h, 0.5f * h, 0.46f * h));

            // Nez large.
            Mesh nose = LowPolyMeshBuilder.Box(new Vector3(0.18f * h, 0.12f * h, 0.14f * h));

            // Oreilles pointues, inclinées vers l'extérieur et le haut.
            Mesh ear = LowPolyMeshBuilder.Cone(0.1f * h, 0.26f * h, HornSides);

            // Défenses, de la mâchoire inférieure vers le haut.
            Mesh tusk = LowPolyMeshBuilder.Cone(0.045f * h, 0.22f * h, HornSides);

            Mesh combined = LowPolyMeshBuilder.Combine(
                (jaw, new Vector3(0f, 0.15f * h, 0.03f * h), Quaternion.identity, Vector3.one),
                (skull, new Vector3(0f, 0.53f * h, 0f), Quaternion.identity, Vector3.one),
                (nose, new Vector3(0f, 0.4f * h, 0.27f * h), Quaternion.identity, Vector3.one),
                (ear, new Vector3(-0.27f * h, 0.5f * h, 0f), Quaternion.Euler(10f, 0f, 55f), Vector3.one),
                (ear, new Vector3(0.27f * h, 0.5f * h, 0f), Quaternion.Euler(10f, 0f, -55f), Vector3.one),
                (tusk, new Vector3(-0.18f * h, 0.18f * h, 0.28f * h), Quaternion.Euler(-20f, 0f, 12f), Vector3.one),
                (tusk, new Vector3(0.18f * h, 0.18f * h, 0.28f * h), Quaternion.Euler(-20f, 0f, -12f), Vector3.one));

            GameObject go = LowPolyMeshBuilder.CreateMeshObject("Head", combined, material, parent);
            go.transform.localPosition = new Vector3(0f, layout.NeckY, 0f);
            return go.transform;
        }

        /// <summary>Tête humaine : crâne arrondi, mâchoire modeste, pas de défenses, oreilles discrètes.</summary>
        public static Transform CreateLowPolyHumanHead(Transform parent, BodyLayout layout, Material material)
        {
            float h = layout.HeadHeight;

            Mesh skull = LowPolyMeshBuilder.LowPolySphere(h * 0.32f, horizontalSegments: 8, verticalRings: 5);
            Mesh jaw = LowPolyMeshBuilder.Box(new Vector3(0.32f * h, 0.22f * h, 0.32f * h));
            Mesh nose = LowPolyMeshBuilder.Box(new Vector3(0.09f * h, 0.08f * h, 0.1f * h));
            Mesh ear = LowPolyMeshBuilder.Box(new Vector3(0.05f * h, 0.13f * h, 0.09f * h));

            Mesh combined = LowPolyMeshBuilder.Combine(
                (skull, new Vector3(0f, 0.68f * h, 0f), Quaternion.identity, Vector3.one),
                (jaw, new Vector3(0f, 0.42f * h, 0.02f * h), Quaternion.identity, Vector3.one),
                (nose, new Vector3(0f, 0.53f * h, 0.18f * h), Quaternion.identity, Vector3.one),
                (ear, new Vector3(-0.17f * h, 0.57f * h, 0f), Quaternion.identity, Vector3.one),
                (ear, new Vector3(0.17f * h, 0.57f * h, 0f), Quaternion.identity, Vector3.one));

            GameObject go = LowPolyMeshBuilder.CreateMeshObject("Head", combined, material, parent);
            go.transform.localPosition = new Vector3(0f, layout.NeckY, 0f);
            return go.transform;
        }

        public static Transform CreateLowPolyArm(Transform parent, string name, BodyLayout layout, Material material, float mirror)
        {
            float widthScale = Mathf.Sqrt(layout.BodyDepth / 0.28f); // suit grossièrement la carrure
            Mesh mesh = LowPolyMeshBuilder.Cylinder(
                radiusBottom: 0.075f * widthScale,
                radiusTop: 0.1f * widthScale,
                height: layout.ArmLength,
                sides: LimbSides,
                pivotAtTop: true);

            GameObject go = LowPolyMeshBuilder.CreateMeshObject(name, mesh, material, parent);
            go.transform.localPosition = new Vector3(layout.ShoulderX * mirror, layout.ShoulderY, 0f);
            return go.transform;
        }

        public static Transform CreateLowPolyLeg(Transform parent, string name, BodyLayout layout, Material material, float mirror)
        {
            float widthScale = Mathf.Sqrt(layout.BodyDepth / 0.28f);
            Mesh mesh = LowPolyMeshBuilder.Cylinder(
                radiusBottom: 0.1f * widthScale,
                radiusTop: 0.14f * widthScale,
                height: layout.LegLength,
                sides: LimbSides,
                pivotAtTop: true);

            GameObject go = LowPolyMeshBuilder.CreateMeshObject(name, mesh, material, parent);
            go.transform.localPosition = new Vector3(layout.HipX * mirror, layout.HipY, 0f);
            return go.transform;
        }

        /// <summary><paramref name="parent"/> doit être le bras correspondant (la main est à son poignet, l'extrémité basse).</summary>
        public static Transform CreateLowPolyHand(Transform parent, string name, BodyLayout layout, Material material)
        {
            float s = layout.HandSize;
            Mesh mesh = LowPolyMeshBuilder.Box(new Vector3(s * 1.1f, s * 0.85f, s * 1.3f), pivotAtTop: true);

            GameObject go = LowPolyMeshBuilder.CreateMeshObject(name, mesh, material, parent);
            go.transform.localPosition = new Vector3(0f, -layout.ArmLength, 0.02f);
            return go.transform;
        }

        /// <summary><paramref name="parent"/> doit être la jambe correspondante (le pied est à sa cheville, l'extrémité basse).</summary>
        public static Transform CreateLowPolyFoot(Transform parent, string name, BodyLayout layout, Material material)
        {
            float width = layout.HandSize * 1.05f;
            Mesh mesh = LowPolyMeshBuilder.Box(new Vector3(width, layout.FootHeight, layout.FootLength), pivotAtTop: true);

            GameObject go = LowPolyMeshBuilder.CreateMeshObject(name, mesh, material, parent);
            go.transform.localPosition = new Vector3(0f, -layout.LegLength, layout.FootLength * 0.2f);
            return go.transform;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
