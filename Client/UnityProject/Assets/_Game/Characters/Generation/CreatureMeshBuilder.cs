using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>Archétype de quadrupède low-poly supporté par <see cref="CreatureMeshBuilder"/>.</summary>
    public enum QuadrupedKind
    {
        Wolf,
        Boar,
    }

    /// <summary>
    /// Génère des modèles low-poly de monstres quadrupèdes par code (mêmes primitives que
    /// <see cref="LowPolyMeshBuilder"/>), pour remplacer les sphères/cubes génériques utilisés jusqu'ici pour
    /// les créatures. Un seul mesh statique combiné par créature : pas d'animation de membres pour l'instant
    /// (seule la position/rotation globale est interpolée, comme avant), ce qui suffit pour un rendu
    /// nettement plus lisible qu'une primitive, à un coût de complexité raisonnable pour ce prototype.
    /// </summary>
    public static class CreatureMeshBuilder
    {
        /// <summary>
        /// Construit un quadrupède, pieds à y=0 (sol), orienté vers +Z (avant). <paramref name="scale"/> permet
        /// de distinguer une variante "Élite" (plus grande) sans dupliquer la géométrie.
        /// </summary>
        public static Mesh BuildQuadruped(QuadrupedKind kind, float scale = 1f)
        {
            return kind switch
            {
                QuadrupedKind.Wolf => BuildWolf(scale),
                QuadrupedKind.Boar => BuildBoar(scale),
                _ => BuildWolf(scale),
            };
        }

        private static Mesh BuildWolf(float scale)
        {
            const float legHeight = 0.5f;
            const float bodyHeight = 0.42f;
            const float bodyLength = 1.15f;
            const float bodyWidth = 0.34f;
            float bodyBottomY = legHeight;

            Mesh body = LowPolyMeshBuilder.TaperedBox(
                bottomSize: new Vector3(bodyWidth, 0f, bodyLength),
                topSize: new Vector3(bodyWidth * 0.85f, 0f, bodyLength * 0.9f),
                height: bodyHeight);

            Mesh leg = LowPolyMeshBuilder.Cylinder(0.055f, 0.045f, legHeight, 6);

            Mesh headBox = LowPolyMeshBuilder.Box(new Vector3(0.26f, 0.24f, 0.32f));
            Mesh snout = LowPolyMeshBuilder.Box(new Vector3(0.14f, 0.13f, 0.24f));
            Mesh ear = LowPolyMeshBuilder.Cone(0.07f, 0.16f, 5);
            Mesh tail = LowPolyMeshBuilder.Cylinder(0.05f, 0.02f, 0.45f, 5);

            float legX = bodyWidth * 0.38f;
            float legFrontZ = bodyLength * 0.33f;
            float legBackZ = -bodyLength * 0.33f;
            float headZ = bodyLength * 0.5f + 0.1f;
            float headY = bodyBottomY + bodyHeight * 0.85f;

            Mesh combined = LowPolyMeshBuilder.Combine(
                (body, new Vector3(0f, bodyBottomY, 0f), Quaternion.identity, Vector3.one),
                (leg, new Vector3(-legX, 0f, legFrontZ), Quaternion.identity, Vector3.one),
                (leg, new Vector3(legX, 0f, legFrontZ), Quaternion.identity, Vector3.one),
                (leg, new Vector3(-legX, 0f, legBackZ), Quaternion.identity, Vector3.one),
                (leg, new Vector3(legX, 0f, legBackZ), Quaternion.identity, Vector3.one),
                (headBox, new Vector3(0f, headY, headZ), Quaternion.identity, Vector3.one),
                (snout, new Vector3(0f, headY - 0.03f, headZ + 0.26f), Quaternion.identity, Vector3.one),
                (ear, new Vector3(-0.09f, headY + 0.17f, headZ - 0.05f), Quaternion.Euler(-10f, 0f, 15f), Vector3.one),
                (ear, new Vector3(0.09f, headY + 0.17f, headZ - 0.05f), Quaternion.Euler(-10f, 0f, -15f), Vector3.one),
                (tail, new Vector3(0f, bodyBottomY + bodyHeight * 0.75f, -bodyLength * 0.55f), Quaternion.Euler(60f, 0f, 0f), Vector3.one));

            return ScaleMesh(combined, scale);
        }

        private static Mesh BuildBoar(float scale)
        {
            const float legHeight = 0.38f;
            const float bodyHeight = 0.48f;
            const float bodyLength = 0.95f;
            const float bodyWidth = 0.48f;
            float bodyBottomY = legHeight;

            Mesh body = LowPolyMeshBuilder.TaperedBox(
                bottomSize: new Vector3(bodyWidth, 0f, bodyLength),
                topSize: new Vector3(bodyWidth * 0.9f, 0f, bodyLength * 0.95f),
                height: bodyHeight);

            Mesh leg = LowPolyMeshBuilder.Cylinder(0.07f, 0.06f, legHeight, 6);

            Mesh headBox = LowPolyMeshBuilder.Box(new Vector3(0.3f, 0.22f, 0.26f));
            Mesh snout = LowPolyMeshBuilder.Box(new Vector3(0.18f, 0.14f, 0.16f));
            Mesh ear = LowPolyMeshBuilder.Cone(0.08f, 0.1f, 5);
            Mesh tusk = LowPolyMeshBuilder.Cone(0.025f, 0.12f, 5);
            Mesh tail = LowPolyMeshBuilder.Cone(0.03f, 0.1f, 5);

            float legX = bodyWidth * 0.34f;
            float legFrontZ = bodyLength * 0.32f;
            float legBackZ = -bodyLength * 0.32f;
            float headZ = bodyLength * 0.5f + 0.06f;
            float headY = bodyBottomY + bodyHeight * 0.55f;

            Mesh combined = LowPolyMeshBuilder.Combine(
                (body, new Vector3(0f, bodyBottomY, 0f), Quaternion.identity, Vector3.one),
                (leg, new Vector3(-legX, 0f, legFrontZ), Quaternion.identity, Vector3.one),
                (leg, new Vector3(legX, 0f, legFrontZ), Quaternion.identity, Vector3.one),
                (leg, new Vector3(-legX, 0f, legBackZ), Quaternion.identity, Vector3.one),
                (leg, new Vector3(legX, 0f, legBackZ), Quaternion.identity, Vector3.one),
                (headBox, new Vector3(0f, headY, headZ), Quaternion.identity, Vector3.one),
                (snout, new Vector3(0f, headY - 0.05f, headZ + 0.18f), Quaternion.identity, Vector3.one),
                (ear, new Vector3(-0.1f, headY + 0.13f, headZ - 0.08f), Quaternion.Euler(20f, 0f, 10f), Vector3.one),
                (ear, new Vector3(0.1f, headY + 0.13f, headZ - 0.08f), Quaternion.Euler(20f, 0f, -10f), Vector3.one),
                (tusk, new Vector3(-0.08f, headY - 0.1f, headZ + 0.22f), Quaternion.Euler(-100f, 0f, 20f), Vector3.one),
                (tusk, new Vector3(0.08f, headY - 0.1f, headZ + 0.22f), Quaternion.Euler(-100f, 0f, -20f), Vector3.one),
                (tail, new Vector3(0f, bodyBottomY + bodyHeight * 0.7f, -bodyLength * 0.52f), Quaternion.Euler(100f, 0f, 0f), Vector3.one));

            return ScaleMesh(combined, scale);
        }

        private static Mesh ScaleMesh(Mesh mesh, float scale)
        {
            if (Mathf.Approximately(scale, 1f))
            {
                return mesh;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] *= scale;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
