namespace WFCTechTest.WFC.Diagnostics
{
    /// <summary>
    /// @file GenerationFailureReason.cs
    /// @brief Enumerates structured failure reasons emitted by the generation pipeline.
    /// </summary>
    public enum GenerationFailureReason
    {
        /// <summary>
        /// No failure occurred.
        /// </summary>
        None,

        /// <summary>
        /// The semantic solver hit an empty domain contradiction.
        /// </summary>
        SemanticContradiction,

        /// <summary>
        /// The compile step had to degrade too many multi-cell footprints.
        /// </summary>
        FootprintConflict,

        /// <summary>
        /// Boundary cells failed hard wall validation.
        /// </summary>
        BoundaryViolation,

        /// <summary>
        /// Walkable ground coverage fell outside the approved range.
        /// </summary>
        CoverageOutOfRange,

        /// <summary>
        /// The final standable graph was not connected enough.
        /// </summary>
        ConnectivityFailure,

        /// <summary>
        /// An interest anchor landed in a bad or disconnected location.
        /// </summary>
        InterestAnchorFailure,

        /// <summary>
        /// The generator exhausted all retries without producing a valid map.
        /// </summary>
        RetryBudgetExceeded
    }
}
