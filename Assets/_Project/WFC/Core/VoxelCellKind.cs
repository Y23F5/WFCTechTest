namespace WFCTechTest.WFC.Core
{
    /// <summary>
    /// @file VoxelCellKind.cs
    /// @brief Enumerates solid voxel categories for compiled cube output and debug rendering.
    /// </summary>
    public enum VoxelCellKind
    {
        /// <summary>
        /// Empty space.
        /// </summary>
        Air,

        /// <summary>
        /// Ground floor cube.
        /// </summary>
        Floor,

        /// <summary>
        /// Boundary wall cube.
        /// </summary>
        Wall,

        /// <summary>
        /// Low cover cube.
        /// </summary>
        LowCover,

        /// <summary>
        /// Medium cover cube.
        /// </summary>
        HighCover,

        /// <summary>
        /// Tall tower cube.
        /// </summary>
        Tower,

        /// <summary>
        /// Spawn anchor marker cube category for diagnostics only.
        /// </summary>
        InterestAnchor
    }
}
