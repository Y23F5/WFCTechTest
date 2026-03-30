using System;
using System.Collections.Generic;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Semantic;

namespace WFCTechTest.WFC.Compile
{
    /// <summary>
    /// @file CompileResult.cs
    /// @brief Wraps the compiled voxel output together with semantic source data and anchor metadata.
    /// </summary>
    [Serializable]
    public sealed class CompileResult
    {
        /// <summary>
        /// Initializes a compiled result wrapper.
        /// </summary>
        public CompileResult(VoxelOccupancyMap volume, SemanticGrid2D semanticGrid)
        {
            Volume = volume;
            SemanticGrid = semanticGrid;
        }

        /// <summary>
        /// Gets or sets the deterministic seed used to build this compiled result.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// Gets the compiled voxel map.
        /// </summary>
        public VoxelOccupancyMap Volume { get; }

        /// <summary>
        /// Gets the solved semantic grid used to build the voxel map.
        /// </summary>
        public SemanticGrid2D SemanticGrid { get; }

        /// <summary>
        /// Gets the compiled interest anchor positions.
        /// </summary>
        public List<GridCoord3D> InterestAnchors { get; } = new List<GridCoord3D>();

        /// <summary>
        /// Gets the generated object-level obstacle placements.
        /// </summary>
        public List<ObstaclePlacement> ObstaclePlacements { get; } = new List<ObstaclePlacement>();

        /// <summary>
        /// Gets or sets the number of multi-cell archetypes that degraded during placement.
        /// </summary>
        public int DegradedFootprints { get; set; }
    }
}
