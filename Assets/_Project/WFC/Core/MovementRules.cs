using System;
using System.Collections.Generic;
using UnityEngine;

namespace WFCTechTest.WFC.Core
{
    /// <summary>
    /// @file MovementRules.cs
    /// @brief Implements standability, walk, and jump predicates for compiled voxel results.
    /// </summary>
    public static class MovementRules
    {
        private static readonly GridCoord2D[] WalkOffsets =
        {
            new GridCoord2D(-1, -1),
            new GridCoord2D(-1, 0),
            new GridCoord2D(-1, 1),
            new GridCoord2D(0, -1),
            new GridCoord2D(0, 1),
            new GridCoord2D(1, -1),
            new GridCoord2D(1, 0),
            new GridCoord2D(1, 1)
        };

        /// <summary>
        /// Enumerates candidate standing positions for a compiled voxel map.
        /// </summary>
        public static List<GridCoord3D> CollectStandablePositions(VoxelOccupancyMap map)
        {
            var positions = new List<GridCoord3D>();

            for (var x = 0; x < map.Width; x++)
            {
                for (var y = 1; y < map.Height - 2; y++)
                {
                    for (var z = 0; z < map.Depth; z++)
                    {
                        if (IsStandable(map, x, y, z))
                        {
                            positions.Add(new GridCoord3D(x, y, z));
                        }
                    }
                }
            }

            return positions;
        }

        /// <summary>
        /// Returns whether the supplied coordinate is a valid standing position.
        /// </summary>
        public static bool IsStandable(VoxelOccupancyMap map, int x, int y, int z)
        {
            if (!map.IsInBounds(x, y, z) || !map.IsInBounds(x, y + 2, z))
            {
                return false;
            }

            return !map.IsSolid(x, y, z)
                   && map.IsSolid(x, y - 1, z)
                   && !map.IsSolid(x, y + 1, z)
                   && !map.IsSolid(x, y + 2, z);
        }

        /// <summary>
        /// Enumerates walk neighbors from a standable origin.
        /// </summary>
        public static IEnumerable<GridCoord3D> EnumerateWalkNeighbors(VoxelOccupancyMap map, GridCoord3D origin)
        {
            foreach (var offset in WalkOffsets)
            {
                var target = new GridCoord3D(origin.X + offset.X, origin.Y, origin.Z + offset.Z);
                if (!map.IsInBounds(target.X, target.Y, target.Z))
                {
                    continue;
                }

                for (var y = origin.Y - 1; y <= origin.Y + 1; y++)
                {
                    if (CanWalk(map, origin, new GridCoord3D(target.X, y, target.Z)))
                    {
                        yield return new GridCoord3D(target.X, y, target.Z);
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether a target standing position is reachable by a normal walk move.
        /// </summary>
        public static bool CanWalk(VoxelOccupancyMap map, GridCoord3D origin, GridCoord3D target)
        {
            if (!IsStandable(map, origin.X, origin.Y, origin.Z) || !IsStandable(map, target.X, target.Y, target.Z))
            {
                return false;
            }

            var dx = Math.Abs(target.X - origin.X);
            var dz = Math.Abs(target.Z - origin.Z);
            var dy = Math.Abs(target.Y - origin.Y);
            return dx <= 1 && dz <= 1 && (dx + dz) > 0 && dy <= 1;
        }

        /// <summary>
        /// Returns whether a target standing position is reachable by a jump move.
        /// </summary>
        public static bool CanJump(VoxelOccupancyMap map, GridCoord3D origin, GridCoord3D target, float maxJumpHeight, float maxJumpDistance)
        {
            if (!IsStandable(map, origin.X, origin.Y, origin.Z) || !IsStandable(map, target.X, target.Y, target.Z))
            {
                return false;
            }

            if (origin.Equals(target))
            {
                return false;
            }

            var rise = target.Y - origin.Y;
            if (rise > maxJumpHeight)
            {
                return false;
            }

            var horizontal = Vector2.Distance(new Vector2(origin.X, origin.Z), new Vector2(target.X, target.Z));
            if (horizontal > maxJumpDistance || horizontal <= 1.01f)
            {
                return false;
            }

            return HasClearTrajectory(map, origin, target);
        }

        /// <summary>
        /// Returns the number of standable positions located at the ground plane.
        /// </summary>
        public static int CountGroundStandableCells(VoxelOccupancyMap map)
        {
            var count = 0;
            for (var x = 0; x < map.Width; x++)
            {
                for (var z = 0; z < map.Depth; z++)
                {
                    if (IsStandable(map, x, 1, z))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Returns the total number of standable positions in the full voxel stack.
        /// </summary>
        public static int CountAllStandableCells(VoxelOccupancyMap map)
        {
            return CollectStandablePositions(map).Count;
        }

        private static bool HasClearTrajectory(VoxelOccupancyMap map, GridCoord3D origin, GridCoord3D target)
        {
            var start = new Vector3(origin.X + 0.5f, origin.Y + 0.6f, origin.Z + 0.5f);
            var end = new Vector3(target.X + 0.5f, target.Y + 0.6f, target.Z + 0.5f);
            var steps = Mathf.Max(6, Mathf.CeilToInt(Vector3.Distance(start, end) * 4f));

            /* Samples a conservative centerline through the jump volume to avoid false positive through-wall jumps. */
            for (var i = 1; i < steps; i++)
            {
                var t = i / (float)steps;
                var sample = Vector3.Lerp(start, end, t);
                var sampleX = Mathf.FloorToInt(sample.x);
                var sampleY = Mathf.FloorToInt(sample.y);
                var sampleZ = Mathf.FloorToInt(sample.z);
                if (map.IsSolid(sampleX, sampleY, sampleZ))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
