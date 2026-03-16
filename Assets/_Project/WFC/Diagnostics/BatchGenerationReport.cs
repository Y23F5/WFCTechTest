using System;
using System.Collections.Generic;
using System.Linq;

namespace WFCTechTest.WFC.Diagnostics
{
    /// <summary>
    /// @file BatchGenerationReport.cs
    /// @brief Aggregates outcomes from repeated multi-seed generation test runs.
    /// </summary>
    [Serializable]
    public sealed class BatchGenerationReport
    {
        /// <summary>
        /// Gets the reports collected during the batch.
        /// </summary>
        public List<GenerationReport> Runs { get; } = new List<GenerationReport>();

        /// <summary>
        /// Gets the success ratio across all recorded runs.
        /// </summary>
        public float SuccessRatio => Runs.Count == 0 ? 0f : Runs.Count(report => report.Success) / (float)Runs.Count;

        /// <summary>
        /// Gets the average number of attempts consumed by successful runs.
        /// </summary>
        public float AverageAttempts => Runs.Count == 0 ? 0f : (float)Runs.Average(report => report.Attempt);

        /// <summary>
        /// Gets the failure histogram keyed by failure reason.
        /// </summary>
        public Dictionary<GenerationFailureReason, int> BuildFailureHistogram()
        {
            return Runs
                .GroupBy(report => report.FailureReason)
                .ToDictionary(group => group.Key, group => group.Count());
        }
    }
}
