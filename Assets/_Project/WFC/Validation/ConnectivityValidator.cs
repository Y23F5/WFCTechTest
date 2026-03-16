using System.Collections.Generic;
using UnityEngine;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Validation
{
    /// <summary>
    /// @file ConnectivityValidator.cs
    /// @brief Evaluates the connectivity of standable positions under walk and jump movement rules.
    /// </summary>
    public sealed class ConnectivityValidator
    {
        /// <summary>
        /// Computes the largest connected component ratio for the supplied voxel map.
        /// </summary>
        public float ComputeLargestComponentRatio(VoxelOccupancyMap map, float maxJumpHeight, float maxJumpDistance)
        {
            var standable = MovementRules.CollectStandablePositions(map);
            if (standable.Count == 0)
            {
                return 0f;
            }

            var standableSet = new HashSet<GridCoord3D>(standable);
            var lookup = BuildLookup(standable);
            var visited = new HashSet<GridCoord3D>();
            var largest = 0;

            foreach (var origin in standable)
            {
                if (!visited.Add(origin))
                {
                    continue;
                }

                var size = Flood(map, origin, standableSet, lookup, visited, maxJumpHeight, maxJumpDistance);
                if (size > largest)
                {
                    largest = size;
                }
            }

            return largest / (float)standable.Count;
        }

        private static int Flood(VoxelOccupancyMap map, GridCoord3D start, HashSet<GridCoord3D> standableSet, Dictionary<Vector2Int, List<GridCoord3D>> lookup, HashSet<GridCoord3D> visited, float maxJumpHeight, float maxJumpDistance)
        {
            var count = 0;
            var queue = new Queue<GridCoord3D>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                count++;

                foreach (var neighbor in MovementRules.EnumerateWalkNeighbors(map, current))
                {
                    if (standableSet.Contains(neighbor) && visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }

                foreach (var candidate in EnumerateJumpCandidates(current, lookup, maxJumpDistance))
                {
                    if (!visited.Contains(candidate) && MovementRules.CanJump(map, current, candidate, maxJumpHeight, maxJumpDistance))
                    {
                        visited.Add(candidate);
                        queue.Enqueue(candidate);
                    }
                }
            }

            return count;
        }

        private static Dictionary<Vector2Int, List<GridCoord3D>> BuildLookup(List<GridCoord3D> standable)
        {
            var lookup = new Dictionary<Vector2Int, List<GridCoord3D>>();
            foreach (var coord in standable)
            {
                var key = new Vector2Int(coord.X, coord.Z);
                if (!lookup.TryGetValue(key, out var list))
                {
                    list = new List<GridCoord3D>();
                    lookup[key] = list;
                }

                list.Add(coord);
            }

            return lookup;
        }

        private static IEnumerable<GridCoord3D> EnumerateJumpCandidates(GridCoord3D current, Dictionary<Vector2Int, List<GridCoord3D>> lookup, float maxJumpDistance)
        {
            var radius = Mathf.CeilToInt(maxJumpDistance);
            for (var x = current.X - radius; x <= current.X + radius; x++)
            {
                for (var z = current.Z - radius; z <= current.Z + radius; z++)
                {
                    if (lookup.TryGetValue(new Vector2Int(x, z), out var candidates))
                    {
                        foreach (var candidate in candidates)
                        {
                            yield return candidate;
                        }
                    }
                }
            }
        }
    }
}
