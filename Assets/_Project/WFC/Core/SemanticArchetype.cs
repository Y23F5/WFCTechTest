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
        /// One-cell low cover block.
        /// </summary>
        LowCover1x1,

        /// <summary>
        /// Two-cell low cover strip.
        /// </summary>
        LowCover1x2,

        /// <summary>
        /// One-cell medium cover stack.
        /// </summary>
        HighCover1x1,

        /// <summary>
        /// Two-cell medium cover strip.
        /// </summary>
        HighCover1x2,

        /// <summary>
        /// One-cell tall tower used as a sparse vertical accent.
        /// </summary>
        Tower1x1,

        /// <summary>
        /// Two-by-two medium cover block.
        /// </summary>
        Block2x2
    }
}
