using System;
using System.Collections.Generic;

namespace GameServer.Domain.Spatial
{
    /// <summary>
    /// Index spatial en grille (plan XZ) : indique quelles entités sont dans le rayon de visibilité d'un spectateur.
    /// Reconstruit à chaque tick. Non thread-safe : utilisé uniquement par la boucle de simulation.
    /// </summary>
    public sealed class InterestManager<T> where T : class, IPlacedEntity
    {
        private readonly float _cellSize;
        private readonly float _radiusSquared;
        private readonly int _cellRadius;
        private readonly Dictionary<long, List<T>> _cells = new Dictionary<long, List<T>>();

        public InterestManager(float cellSize, float radius)
        {
            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "La taille de cellule doit être positive.");
            }

            if (radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Le rayon de visibilité doit être positif.");
            }

            _cellSize = cellSize;
            _radiusSquared = radius * radius;
            _cellRadius = (int)MathF.Ceiling(radius / cellSize);
        }

        /// <summary>Reconstruit la grille à partir des positions courantes.</summary>
        public void Rebuild(IReadOnlyList<T> entities)
        {
            foreach (List<T> cell in _cells.Values)
            {
                cell.Clear();
            }

            for (int i = 0; i < entities.Count; i++)
            {
                T entity = entities[i];
                long key = PackKey(CellIndex(entity.X), CellIndex(entity.Z));

                if (!_cells.TryGetValue(key, out List<T>? cell))
                {
                    cell = new List<T>();
                    _cells.Add(key, cell);
                }

                cell.Add(entity);
            }
        }

        /// <summary>Ajoute à <paramref name="results"/> toutes les entités dans le rayon du spectateur, lui compris.</summary>
        public void QueryVisible(IPlacedEntity viewer, List<T> results)
        {
            float viewerX = viewer.X;
            float viewerZ = viewer.Z;
            int centerX = CellIndex(viewerX);
            int centerZ = CellIndex(viewerZ);

            for (int cx = centerX - _cellRadius; cx <= centerX + _cellRadius; cx++)
            {
                for (int cz = centerZ - _cellRadius; cz <= centerZ + _cellRadius; cz++)
                {
                    if (!_cells.TryGetValue(PackKey(cx, cz), out List<T>? cell))
                    {
                        continue;
                    }

                    for (int i = 0; i < cell.Count; i++)
                    {
                        T candidate = cell[i];
                        if (DistanceSquared(viewerX, viewerZ, candidate) <= _radiusSquared)
                        {
                            results.Add(candidate);
                        }
                    }
                }
            }
        }

        private int CellIndex(float coordinate)
        {
            return (int)MathF.Floor(coordinate / _cellSize);
        }

        private static long PackKey(int cellX, int cellZ)
        {
            return ((long)cellX << 32) | (uint)cellZ;
        }

        private static float DistanceSquared(float originX, float originZ, T other)
        {
            float dx = other.X - originX;
            float dz = other.Z - originZ;
            return dx * dx + dz * dz;
        }
    }
}
