using System;
using System.Collections.Generic;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Compile
{
    /// <summary>
    /// @file ObstaclePlacement.cs
    /// @brief Describes one generated obstacle object placement before runtime prefab instantiation.
    /// </summary>
    [Serializable]
    public sealed class ObstaclePlacement
    {
        /// <summary>
        /// Gets or sets the exported obstacle type id selected from the prefab registry.
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// Gets or sets the display name selected from the prefab registry.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this placement allows random yaw.
        /// </summary>
        public bool AllowRandomYaw { get; set; }

        /// <summary>
        /// Gets or sets the anchor cell used for this placement.
        /// </summary>
        public GridCoord2D Anchor { get; set; }

        /// <summary>
        /// Gets or sets the occupied semantic cells covered by the placement footprint.
        /// </summary>
        public List<GridCoord2D> OccupiedCells { get; set; } = new List<GridCoord2D>();

        /// <summary>
        /// Gets or sets the placement height in stacked cells.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the logical footprint width in placement cells.
        /// </summary>
        public int FootprintWidth { get; set; }

        /// <summary>
        /// Gets or sets the logical footprint depth in placement cells.
        /// </summary>
        public int FootprintDepth { get; set; }

        /// <summary>
        /// Gets or sets the final y-axis rotation in degrees.
        /// </summary>
        public float RotationY { get; set; }

        /// <summary>
        /// Gets or sets the source semantic archetype.
        /// </summary>
        public SemanticArchetype Archetype { get; set; }

        /// <summary>
        /// Gets or sets the primary semantic class used for prefab registry matching and validation.
        /// </summary>
        public ObstacleSemanticClass SemanticClass { get; set; }

        /// <summary>
        /// Gets or sets the explicit density band emitted by the semantic WFC layer.
        /// </summary>
        public SemanticDensityBand DensityBand { get; set; }
    }
}
