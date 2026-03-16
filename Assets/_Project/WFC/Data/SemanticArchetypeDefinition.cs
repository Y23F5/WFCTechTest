using System;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Data
{
    /// <summary>
    /// @file SemanticArchetypeDefinition.cs
    /// @brief Defines tunable weights and compile metadata for a semantic archetype.
    /// </summary>
    [Serializable]
    public sealed class SemanticArchetypeDefinition
    {
        /// <summary>
        /// Gets or sets the archetype identifier.
        /// </summary>
        public SemanticArchetype Archetype = SemanticArchetype.Open;

        /// <summary>
        /// Gets or sets the base solver weight used during observation.
        /// </summary>
        public float Weight = 1f;

        /// <summary>
        /// Gets or sets the percentage target used only for diagnostics and tuning.
        /// </summary>
        public float TargetRatio;

        /// <summary>
        /// Gets or sets the obstacle height in cubes above floor level.
        /// </summary>
        public int Height;

        /// <summary>
        /// Gets or sets the horizontal footprint width in cells.
        /// </summary>
        public int FootprintWidth = 1;

        /// <summary>
        /// Gets or sets the horizontal footprint depth in cells.
        /// </summary>
        public int FootprintDepth = 1;

        /// <summary>
        /// Gets or sets whether the archetype is reserved for boundary cells.
        /// </summary>
        public bool BoundaryOnly;

        /// <summary>
        /// Gets or sets whether the archetype reserves future spawn or respawn space.
        /// </summary>
        public bool IsInterestAnchor;
    }
}
