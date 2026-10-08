using MiniWow.Client.Characters;
using UnityEngine;

namespace MiniWow.Client.World
{
    /// <summary>
    /// Décor low-poly généré par code (bâtiments, arbres, chemins, marqueurs de zone), même principe et
    /// mêmes primitives que les personnages/créatures (<see cref="LowPolyMeshBuilder"/>) : aucun asset
    /// externe, aucun Blender/FBX. Un objet = quelques GameObjects enfants simples ; pas besoin de les
    /// fusionner en un seul mesh vu le nombre modeste de props par scène.
    /// </summary>
    public static class WorldPropBuilder
    {
        /// <summary>Bâtiment simple : corps + toit à quatre pans. <paramref name="rotationY"/> en degrés.</summary>
        public static GameObject CreateBuilding(Transform parent, Vector3 position, Vector3 size, Color wallColor, Color roofColor, float rotationY = 0f)
        {
            var root = new GameObject("Building");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

            Material wallMat = CharacterMaterials.GetOrCreate($"Wall_{ColorUtility.ToHtmlStringRGB(wallColor)}", wallColor);
            Material roofMat = CharacterMaterials.GetOrCreate($"Roof_{ColorUtility.ToHtmlStringRGB(roofColor)}", roofColor);

            Mesh body = LowPolyMeshBuilder.Box(size);
            LowPolyMeshBuilder.CreateMeshObject("Walls", body, wallMat, root.transform);

            float roofHeight = size.y * 0.55f;
            Mesh roof = LowPolyMeshBuilder.TaperedBox(
                bottomSize: new Vector3(size.x * 1.08f, 0f, size.z * 1.08f),
                topSize: new Vector3(size.x * 0.1f, 0f, size.z * 0.1f),
                height: roofHeight);
            GameObject roofGo = LowPolyMeshBuilder.CreateMeshObject("Roof", roof, roofMat, root.transform);
            roofGo.transform.localPosition = new Vector3(0f, size.y, 0f);

            return root;
        }

        /// <summary>Arbre simple : tronc + feuillage conique.</summary>
        public static GameObject CreateTree(Transform parent, Vector3 position, float scale = 1f)
        {
            var root = new GameObject("Tree");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            Material trunkMat = CharacterMaterials.GetOrCreate("TreeTrunk", new Color(0.35f, 0.24f, 0.14f));
            Material leavesMat = CharacterMaterials.GetOrCreate("TreeLeaves", new Color(0.18f, 0.42f, 0.16f));

            Mesh trunk = LowPolyMeshBuilder.Cylinder(0.25f * scale, 0.2f * scale, 1.6f * scale, 6);
            LowPolyMeshBuilder.CreateMeshObject("Trunk", trunk, trunkMat, root.transform);

            Mesh leaves = LowPolyMeshBuilder.Cone(1.4f * scale, 2.6f * scale, 7);
            GameObject leavesGo = LowPolyMeshBuilder.CreateMeshObject("Leaves", leaves, leavesMat, root.transform);
            leavesGo.transform.localPosition = new Vector3(0f, 1.6f * scale, 0f);

            return root;
        }

        /// <summary>Rocher simple : une boîte irrégulière (échelle non uniforme + rotation aléatoire-ish).</summary>
        public static GameObject CreateRock(Transform parent, Vector3 position, float scale = 1f, float rotationY = 0f)
        {
            Material rockMat = CharacterMaterials.GetOrCreate("Rock", new Color(0.5f, 0.48f, 0.46f));
            Mesh mesh = LowPolyMeshBuilder.Box(new Vector3(1.2f, 0.8f, 1f) * scale);
            GameObject go = LowPolyMeshBuilder.CreateMeshObject("Rock", mesh, rockMat, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            return go;
        }

        /// <summary>Lampadaire : mât fin + lanterne. Purement décoratif (pas de vraie source de lumière).</summary>
        public static GameObject CreateLampPost(Transform parent, Vector3 position)
        {
            var root = new GameObject("LampPost");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            Material poleMat = CharacterMaterials.GetOrCreate("LampPole", new Color(0.2f, 0.2f, 0.2f));
            Material lightMat = CharacterMaterials.GetOrCreate("LampLight", new Color(1f, 0.85f, 0.5f));

            Mesh pole = LowPolyMeshBuilder.Cylinder(0.06f, 0.05f, 2.6f, 6);
            LowPolyMeshBuilder.CreateMeshObject("Pole", pole, poleMat, root.transform);

            Mesh lamp = LowPolyMeshBuilder.LowPolySphere(0.18f, 6, 3);
            GameObject lampGo = LowPolyMeshBuilder.CreateMeshObject("Lamp", lamp, lightMat, root.transform);
            lampGo.transform.localPosition = new Vector3(0f, 2.6f, 0f);

            return root;
        }

        /// <summary>Chemin plat entre deux points, pour lire visuellement les axes de circulation.</summary>
        public static GameObject CreatePath(Transform parent, Vector3 from, Vector3 to, float width = 2.2f)
        {
            Vector3 mid = (from + to) * 0.5f;
            float length = Vector3.Distance(from, to);
            Vector3 direction = (to - from).normalized;

            Material pathMat = CharacterMaterials.GetOrCreate("Path", new Color(0.62f, 0.52f, 0.36f));
            Mesh mesh = LowPolyMeshBuilder.Box(new Vector3(width, 0.05f, length));
            GameObject go = LowPolyMeshBuilder.CreateMeshObject("Path", mesh, pathMat, parent);
            go.transform.localPosition = mid + Vector3.up * 0.03f;
            go.transform.localRotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
            return go;
        }

        /// <summary>Anneau de pierre plat : marque une arène de boss ou une clairière importante.</summary>
        public static GameObject CreateArenaRing(Transform parent, Vector3 position, float radius, Color tint)
        {
            Material mat = CharacterMaterials.GetOrCreate($"Arena_{ColorUtility.ToHtmlStringRGB(tint)}", tint);
            Mesh mesh = LowPolyMeshBuilder.Cylinder(radius, radius, 0.08f, 12);
            GameObject go = LowPolyMeshBuilder.CreateMeshObject("ArenaFloor", mesh, mat, parent);
            go.transform.localPosition = position;
            return go;
        }

        /// <summary>Bannière plantée au sol : marque un campement (élite, mini-boss, ...).</summary>
        public static GameObject CreateBanner(Transform parent, Vector3 position, Color flagColor)
        {
            var root = new GameObject("Banner");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            Material poleMat = CharacterMaterials.GetOrCreate("BannerPole", new Color(0.3f, 0.22f, 0.12f));
            Material flagMat = CharacterMaterials.GetOrCreate($"Flag_{ColorUtility.ToHtmlStringRGB(flagColor)}", flagColor);

            Mesh pole = LowPolyMeshBuilder.Cylinder(0.05f, 0.05f, 2.4f, 5);
            LowPolyMeshBuilder.CreateMeshObject("Pole", pole, poleMat, root.transform);

            Mesh flag = LowPolyMeshBuilder.Box(new Vector3(0.05f, 0.6f, 0.9f));
            GameObject flagGo = LowPolyMeshBuilder.CreateMeshObject("Flag", flag, flagMat, root.transform);
            flagGo.transform.localPosition = new Vector3(0f, 1.9f, 0.45f);

            return root;
        }

        /// <summary>Table simple (plateau + pied), pour l'intérieur d'une taverne.</summary>
        public static GameObject CreateTable(Transform parent, Vector3 position)
        {
            var root = new GameObject("Table");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            Material woodMat = CharacterMaterials.GetOrCreate("TavernWood", new Color(0.4f, 0.26f, 0.15f));

            Mesh leg = LowPolyMeshBuilder.Cylinder(0.08f, 0.08f, 0.7f, 5);
            LowPolyMeshBuilder.CreateMeshObject("Leg", leg, woodMat, root.transform);

            Mesh top = LowPolyMeshBuilder.Box(new Vector3(1.3f, 0.08f, 1.3f));
            GameObject topGo = LowPolyMeshBuilder.CreateMeshObject("Top", top, woodMat, root.transform);
            topGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);

            return root;
        }

        /// <summary>Chaise simple (assise + dossier), pour l'intérieur d'une taverne.</summary>
        public static GameObject CreateChair(Transform parent, Vector3 position, float rotationY = 0f)
        {
            var root = new GameObject("Chair");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

            Material woodMat = CharacterMaterials.GetOrCreate("TavernWood", new Color(0.4f, 0.26f, 0.15f));

            Mesh seat = LowPolyMeshBuilder.Box(new Vector3(0.45f, 0.08f, 0.45f));
            GameObject seatGo = LowPolyMeshBuilder.CreateMeshObject("Seat", seat, woodMat, root.transform);
            seatGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            Mesh back = LowPolyMeshBuilder.Box(new Vector3(0.45f, 0.5f, 0.08f));
            GameObject backGo = LowPolyMeshBuilder.CreateMeshObject("Back", back, woodMat, root.transform);
            backGo.transform.localPosition = new Vector3(0f, 0.7f, -0.2f);

            return root;
        }

        /// <summary>Entrée de donjon : arche sombre simple (deux piliers + linteau).</summary>
        public static GameObject CreateCaveMouth(Transform parent, Vector3 position, float rotationY = 0f)
        {
            var root = new GameObject("DungeonEntrance");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

            Material stoneMat = CharacterMaterials.GetOrCreate("CaveStone", new Color(0.22f, 0.21f, 0.2f));

            Mesh pillar = LowPolyMeshBuilder.Box(new Vector3(0.8f, 3.2f, 0.8f));
            GameObject left = LowPolyMeshBuilder.CreateMeshObject("PillarLeft", pillar, stoneMat, root.transform);
            left.transform.localPosition = new Vector3(-1.6f, 0f, 0f);
            GameObject right = LowPolyMeshBuilder.CreateMeshObject("PillarRight", pillar, stoneMat, root.transform);
            right.transform.localPosition = new Vector3(1.6f, 0f, 0f);

            Mesh lintel = LowPolyMeshBuilder.Box(new Vector3(4f, 0.8f, 0.8f));
            GameObject lintelGo = LowPolyMeshBuilder.CreateMeshObject("Lintel", lintel, stoneMat, root.transform);
            lintelGo.transform.localPosition = new Vector3(0f, 3.2f, 0f);

            return root;
        }
    }
}
