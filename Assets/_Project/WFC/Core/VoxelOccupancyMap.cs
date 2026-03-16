using System;
using System.Collections.Generic;

namespace WFCTechTest.WFC.Core
{
    /// <summary>
    /// @file VoxelOccupancyMap.cs
    /// @brief Stores the compiled 3D cube grid and offers bounds-safe read and write helpers.
    /// </summary>
    [Serializable]
    public sealed class VoxelOccupancyMap
    {
        private readonly VoxelCellKind[] _cells;

        /// <summary>
        /// Initializes a voxel grid with the supplied dimensions.
        /// </summary>
        public VoxelOccupancyMap(int width, int height, int depth)
        {
            if (width <= 0 || height <= 0 || depth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "All dimensions must be positive.");
            }

            Width = width;
            Height = height;
            Depth = depth;
            _cells = new VoxelCellKind[width * height * depth];
        }

        /// <summary>
        /// Gets the voxel grid width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the voxel grid height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the voxel grid depth.
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Enumerates all solid voxels currently present in the map.
        /// </summary>
        public IEnumerable<GridCoord3D> EnumerateSolidCells()
        {
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    for (var z = 0; z < Depth; z++)
                    {
                        if (GetCell(x, y, z) != VoxelCellKind.Air)
                        {
                            yield return new GridCoord3D(x, y, z);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether the supplied voxel coordinate is inside the map.
        /// </summary>
        public bool IsInBounds(int x, int y, int z)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth;
        }

        /// <summary>
        /// Reads a voxel cell value.
        /// </summary>
        public VoxelCellKind GetCell(int x, int y, int z)
        {
            return _cells[GetIndex(x, y, z)];
        }

        /// <summary>
        /// Returns whether a coordinate contains a solid cube.
        /// </summary>
        public bool IsSolid(int x, int y, int z)
        {
            return IsInBounds(x, y, z) && GetCell(x, y, z) != VoxelCellKind.Air;
        }

        /// <summary>
        /// Writes a voxel cell value.
        /// </summary>
        public void SetCell(int x, int y, int z, VoxelCellKind kind)
        {
            _cells[GetIndex(x, y, z)] = kind;
        }

        /// <summary>
        /// Produces a deep copy of the voxel grid.
        /// </summary>
        public VoxelOccupancyMap Clone()
        {
            var clone = new VoxelOccupancyMap(Width, Height, Depth);
            Array.Copy(_cells, clone._cells, _cells.Length);
            return clone;
        }

        private int GetIndex(int x, int y, int z)
        {
            if (!IsInBounds(x, y, z))
            {
                throw new ArgumentOutOfRangeException($"Coordinate ({x}, {y}, {z}) is outside the voxel map.");
            }

            return ((y * Depth) + z) * Width + x;
        }
    }
}
