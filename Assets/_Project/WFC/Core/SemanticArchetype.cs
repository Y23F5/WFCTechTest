namespace WFCTechTest.WFC.Core
{
    /// <summary>
    /// @file SemanticArchetype.cs
    /// @brief Enumerates semantic map archetypes used by the first-stage WFC solver.
    /// </summary>
    public enum SemanticArchetype
    {
        /// <summary>
        /// Open playable ground with no extra geometry above the floor plane.
        /// </summary>
        Open,

        /// <summary>
        /// Reserved open ground intended for future spawn or respawn logic.
        /// </summary>
        InterestAnchor,

        /// <summary>
        /// Straight outer boundary wall.
        /// </summary>
        BoundaryWall,

        /// <summary>
        /// Corner outer boundary wall.
        /// </summary>
        BoundaryCorner,

        /// <summary>
        /// Sparse low cover obstacle region.
        /// </summary>
        LowCoverSparse,

        /// <summary>
        /// Dense low cover obstacle region.
        /// </summary>
        LowCoverDense,

        /// <summary>
        /// Sparse high cover obstacle region.
        /// </summary>
        HighCoverSparse,

        /// <summary>
        /// Dense high cover obstacle region.
        /// </summary>
        HighCoverDense,

        /// <summary>
        /// Sparse tower obstacle region.
        /// </summary>
        TowerSparse,

        /// <summary>
        /// Dense tower obstacle region.
        /// </summary>
        TowerDense,

        /// <summary>
        /// Sparse blocker obstacle region.
        /// </summary>
        BlockerSparse,

        /// <summary>
        /// Dense blocker obstacle region.
        /// </summary>
        BlockerDense
    }

    /// <summary>
    /// Describes the primary obstacle semantic family used to group prefab registry entries.
    /// </summary>
    public enum ObstacleSemanticClass
    {
        None,
        LowCover,
        HighCover,
        Tower,
        Blocker
    }

    /// <summary>
    /// Describes the semantic density tag emitted by the heavy semantic WFC layer.
    /// </summary>
    public enum SemanticDensityBand
    {
        None,
        Sparse,
        Dense
    }

    /// <summary>
    /// Helper methods for querying heavy semantic archetypes.
    /// </summary>
    public static class SemanticArchetypeExtensions
    {
        /// <summary>
        /// Returns whether the archetype is one of the reserved boundary types.
        /// </summary>
        public static bool IsBoundary(this SemanticArchetype archetype)
        {
            return archetype == SemanticArchetype.BoundaryWall || archetype == SemanticArchetype.BoundaryCorner;
        }

        /// <summary>
        /// Returns whether the archetype behaves like open floor space.
        /// </summary>
        public static bool IsOpenLike(this SemanticArchetype archetype)
        {
            return archetype == SemanticArchetype.Open || archetype == SemanticArchetype.InterestAnchor;
        }

        /// <summary>
        /// Returns whether the archetype represents any obstacle semantic.
        /// </summary>
        public static bool IsObstacle(this SemanticArchetype archetype)
        {
            return archetype.GetObstacleSemanticClass() != ObstacleSemanticClass.None;
        }

        /// <summary>
        /// Resolves the primary obstacle semantic class for an archetype.
        /// </summary>
        public static ObstacleSemanticClass GetObstacleSemanticClass(this SemanticArchetype archetype)
        {
            return archetype switch
            {
                SemanticArchetype.LowCoverSparse or SemanticArchetype.LowCoverDense => ObstacleSemanticClass.LowCover,
                SemanticArchetype.HighCoverSparse or SemanticArchetype.HighCoverDense => ObstacleSemanticClass.HighCover,
                SemanticArchetype.TowerSparse or SemanticArchetype.TowerDense => ObstacleSemanticClass.Tower,
                SemanticArchetype.BlockerSparse or SemanticArchetype.BlockerDense => ObstacleSemanticClass.Blocker,
                _ => ObstacleSemanticClass.None
            };
        }

        /// <summary>
        /// Resolves the explicit density tag for an archetype.
        /// </summary>
        public static SemanticDensityBand GetDensityBand(this SemanticArchetype archetype)
        {
            return archetype switch
            {
                SemanticArchetype.LowCoverDense or SemanticArchetype.HighCoverDense or SemanticArchetype.TowerDense or SemanticArchetype.BlockerDense => SemanticDensityBand.Dense,
                SemanticArchetype.LowCoverSparse or SemanticArchetype.HighCoverSparse or SemanticArchetype.TowerSparse or SemanticArchetype.BlockerSparse => SemanticDensityBand.Sparse,
                _ => SemanticDensityBand.None
            };
        }
    }
}
