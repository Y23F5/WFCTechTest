using System;
using System.Collections.Generic;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Semantic
{
    /// <summary>
    /// @file SemanticGrid2D.cs
    /// @brief Stores solved semantic archetypes for the 2D planning layer.
    /// </summary>
    [Serializable]
    public sealed class SemanticGrid2D
    {
        private readonly SemanticArchetype[] _cells;

        /// <summary>
        /// Initializes a semantic grid with the supplied dimensions.
        /// </summary>
        public SemanticGrid2D(int width, int depth)
        {
            Width = width;
            Depth = depth;
            _cells = new SemanticArchetype[width * depth];
        }

        /// <summary>
        /// Gets the semantic width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the semantic depth.
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Reads a semantic archetype at the supplied coordinate.
        /// </summary>
        public SemanticArchetype Get(int x, int z)
        {
            return _cells[GetIndex(x, z)];
        }

        /// <summary>
        /// Writes a semantic archetype at the supplied coordinate.
        /// </summary>
        public void Set(int x, int z, SemanticArchetype archetype)
        {
            _cells[GetIndex(x, z)] = archetype;
        }

        /// <summary>
        /// Enumerates every semantic coordinate in the grid.
        /// </summary>
        public IEnumerable<GridCoord2D> EnumerateCoords()
        {
            for (var x = 0; x < Width; x++)
            {
                for (var z = 0; z < Depth; z++)
                {
                    yield return new GridCoord2D(x, z);
                }
            }
        }

        private int GetIndex(int x, int z)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Depth)
            {
                throw new ArgumentOutOfRangeException($"Coordinate ({x}, {z}) is outside the semantic grid.");
            }

            return (z * Width) + x;
        }
    }
}
