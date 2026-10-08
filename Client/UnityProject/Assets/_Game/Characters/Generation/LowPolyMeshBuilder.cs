using System.Collections.Generic;
using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Fonctions réutilisables pour générer des meshes low-poly par code (Vector3[] / int[] / Mesh),
    /// sans aucun fichier FBX/OBJ ni logiciel de modélisation. Toutes les formes sont "flat shaded" :
    /// chaque face a ses propres sommets/normales, ce qui donne le rendu à facettes typique du low-poly.
    ///
    /// Convention de pivot : sauf indication contraire, une forme occupe l'espace local
    /// y ∈ [0, height] ("pivot en bas", comme un socle qui s'élève). Avec <c>pivotAtTop = true</c>,
    /// elle occupe y ∈ [-height, 0] ("pivot en haut", comme un membre suspendu à une épaule/hanche).
    /// Ce choix permet de faire tourner un membre autour de son articulation simplement en tournant
    /// le Transform qui porte le mesh, sans squelette ni bones.
    /// </summary>
    public static class LowPolyMeshBuilder
    {
        /// <summary>Pavé (éventuellement tronconique) simple, utile pour mains/pieds/éléments d'équipement.</summary>
        public static Mesh Box(Vector3 size, bool pivotAtTop = false) => TaperedBox(size, size, size.y, pivotAtTop);

        /// <summary>
        /// Pavé dont la section change entre le bas (<paramref name="bottomSize"/>) et le haut
        /// (<paramref name="topSize"/>). Sert aussi bien à un torse (hanches étroites, épaules larges)
        /// qu'à un bras ou une jambe (membre plus large près de l'articulation).
        /// </summary>
        public static Mesh TaperedBox(Vector3 bottomSize, Vector3 topSize, float height, bool pivotAtTop = false)
        {
            float y0 = pivotAtTop ? -height : 0f;
            float y1 = pivotAtTop ? 0f : height;

            Vector3 hb = bottomSize * 0.5f;
            Vector3 ht = topSize * 0.5f;

            Vector3 b0 = new Vector3(-hb.x, y0, -hb.z);
            Vector3 b1 = new Vector3(hb.x, y0, -hb.z);
            Vector3 b2 = new Vector3(hb.x, y0, hb.z);
            Vector3 b3 = new Vector3(-hb.x, y0, hb.z);
            Vector3 t0 = new Vector3(-ht.x, y1, -ht.z);
            Vector3 t1 = new Vector3(ht.x, y1, -ht.z);
            Vector3 t2 = new Vector3(ht.x, y1, ht.z);
            Vector3 t3 = new Vector3(-ht.x, y1, ht.z);

            var builder = new MeshBuilder();
            builder.AddQuad(t0, t1, t2, t3, Vector3.up);
            builder.AddQuad(b1, b0, b3, b2, Vector3.down);
            builder.AddQuad(b0, t0, t3, b3, Vector3.left);
            builder.AddQuad(b2, t2, t1, b1, Vector3.right);
            builder.AddQuad(b3, t3, t2, b2, Vector3.forward);
            builder.AddQuad(b1, t1, t0, b0, Vector3.back);
            return builder.ToMesh();
        }

        /// <summary>
        /// Cylindre low-poly (prisme à <paramref name="sides"/> faces), éventuellement tronconique
        /// (rayons différents en bas/haut). Avec <paramref name="radiusTop"/> = 0, donne un cône
        /// (utile pour oreilles, défenses, cornes).
        /// </summary>
        public static Mesh Cylinder(float radiusBottom, float radiusTop, float height, int sides, bool pivotAtTop = false)
        {
            sides = Mathf.Max(3, sides);
            float y0 = pivotAtTop ? -height : 0f;
            float y1 = pivotAtTop ? 0f : height;

            var bottomRing = new Vector3[sides];
            var topRing = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float cx = Mathf.Cos(angle);
                float cz = Mathf.Sin(angle);
                bottomRing[i] = new Vector3(cx * radiusBottom, y0, cz * radiusBottom);
                topRing[i] = new Vector3(cx * radiusTop, y1, cz * radiusTop);
            }

            var builder = new MeshBuilder();
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Vector3 radial = radiusBottom > 0.0001f
                    ? new Vector3(bottomRing[i].x, 0f, bottomRing[i].z)
                    : new Vector3(topRing[i].x, 0f, topRing[i].z);
                Vector3 outward = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector3.up;

                builder.AddQuad(bottomRing[i], bottomRing[next], topRing[next], topRing[i], outward);
            }

            if (radiusBottom > 0.0001f)
            {
                Vector3 bottomCenter = new Vector3(0f, y0, 0f);
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    builder.AddTriangle(bottomCenter, bottomRing[next], bottomRing[i], Vector3.down);
                }
            }

            if (radiusTop > 0.0001f)
            {
                Vector3 topCenter = new Vector3(0f, y1, 0f);
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    builder.AddTriangle(topCenter, topRing[i], topRing[next], Vector3.up);
                }
            }

            return builder.ToMesh();
        }

        /// <summary>Cône low-poly : cas particulier de <see cref="Cylinder"/> avec un rayon de pointe nul.</summary>
        public static Mesh Cone(float radius, float height, int sides, bool pivotAtTop = false) =>
            Cylinder(radius, 0f, height, sides, pivotAtTop);

        /// <summary>
        /// Sphère low-poly (peu de segments/anneaux), centrée sur l'origine locale. Utile pour une tête
        /// arrondie ou un dôme de casque.
        /// </summary>
        public static Mesh LowPolySphere(float radius, int horizontalSegments = 8, int verticalRings = 4)
        {
            horizontalSegments = Mathf.Max(3, horizontalSegments);
            verticalRings = Mathf.Max(2, verticalRings);

            var builder = new MeshBuilder();

            Vector3 PointOnSphere(float phi, float theta) => new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta) * radius,
                Mathf.Cos(phi) * radius,
                Mathf.Sin(phi) * Mathf.Sin(theta) * radius);

            for (int ring = 0; ring < verticalRings; ring++)
            {
                float phi0 = (float)ring / verticalRings * Mathf.PI;
                float phi1 = (float)(ring + 1) / verticalRings * Mathf.PI;

                for (int seg = 0; seg < horizontalSegments; seg++)
                {
                    float theta0 = (float)seg / horizontalSegments * Mathf.PI * 2f;
                    float theta1 = (float)(seg + 1) / horizontalSegments * Mathf.PI * 2f;

                    Vector3 a = PointOnSphere(phi0, theta0);
                    Vector3 b = PointOnSphere(phi1, theta0);
                    Vector3 c = PointOnSphere(phi1, theta1);
                    Vector3 d = PointOnSphere(phi0, theta1);

                    Vector3 outward = (a + b + c + d).normalized;

                    if (ring == 0)
                    {
                        // Pôle haut : a et d sont confondus, la face est un triangle.
                        builder.AddTriangle(a, b, c, outward);
                    }
                    else if (ring == verticalRings - 1)
                    {
                        // Pôle bas : b et c sont confondus.
                        builder.AddTriangle(a, b, d, outward);
                    }
                    else
                    {
                        builder.AddQuad(a, b, c, d, outward);
                    }
                }
            }

            return builder.ToMesh();
        }

        /// <summary>Fusionne plusieurs meshes (avec leur propre transformation locale) en un seul mesh.</summary>
        public static Mesh Combine(params (Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)[] parts)
        {
            var combineInstances = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                combineInstances[i] = new CombineInstance
                {
                    mesh = parts[i].mesh,
                    transform = Matrix4x4.TRS(parts[i].position, parts[i].rotation, parts[i].scale)
                };
            }

            var result = new Mesh { name = "LowPolyCombined" };
            result.CombineMeshes(combineInstances, mergeSubMeshes: true, useMatrices: true);
            result.RecalculateBounds();
            return result;
        }

        /// <summary>Raccourci : fusionne des parties avec rotation/échelle par défaut (identité / 1).</summary>
        public static Mesh Combine(params (Mesh mesh, Vector3 position)[] parts)
        {
            var full = new (Mesh, Vector3, Quaternion, Vector3)[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                full[i] = (parts[i].Item1, parts[i].Item2, Quaternion.identity, Vector3.one);
            }

            return Combine(full);
        }

        /// <summary>Crée un GameObject avec MeshFilter + MeshRenderer portant le mesh et le matériau donnés.</summary>
        public static GameObject CreateMeshObject(
            string name, Mesh mesh, Material material, Transform parent,
            Vector3? localPosition = null, Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition ?? Vector3.zero;
            go.transform.localRotation = localRotation ?? Quaternion.identity;

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            return go;
        }

        /// <summary>Accumulateur de sommets/normales/triangles avec correction automatique du sens des faces.</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            /// <summary>
            /// Ajoute un triangle. <paramref name="outwardHint"/> indique approximativement la direction
            /// "vers l'extérieur" de la face : si l'ordre des sommets donne une normale opposée, les sommets
            /// sont réordonnés automatiquement. Ainsi l'ordre exact dans lequel on liste les coins d'une face
            /// n'a pas besoin d'être mémorisé manuellement pour chacune des six faces d'un pavé.
            /// </summary>
            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outwardHint)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, outwardHint) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }

                normal.Normalize();

                int baseIndex = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);
                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 2);
            }

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outwardHint)
            {
                AddTriangle(a, b, c, outwardHint);
                AddTriangle(a, c, d, outwardHint);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh { name = "LowPolyPart" };
                if (_vertices.Count > 65000)
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
