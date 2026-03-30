using System;
using System.Collections.Generic;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Diagnostics
{
    /// <summary>
    /// @file GenerationReport.cs
    /// @brief Captures structured metrics, counters, and outcomes for a single generation run.
    /// </summary>
    [Serializable]
    public sealed class GenerationReport
    {
        /// <summary>
        /// Gets or sets the generation seed.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// Gets or sets the attempt number within the retry loop.
        /// </summary>
        public int Attempt { get; set; }

        /// <summary>
        /// Gets or sets whether the run produced a valid result.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the terminal failure reason when the run is invalid.
        /// </summary>
        public GenerationFailureReason FailureReason { get; set; }

        /// <summary>
        /// Gets or sets a human-readable diagnostic message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of observation steps.
        /// </summary>
        public int ObservationCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of propagation updates.
        /// </summary>
        public int PropagationCount { get; set; }

        /// <summary>
        /// Gets or sets the number of compile-time footprint degradations.
        /// </summary>
        public int DegradedFootprintCount { get; set; }

        /// <summary>
        /// Gets or sets the ground coverage ratio at y=1.
        /// </summary>
        public float GroundCoverageRatio { get; set; }

        /// <summary>
        /// Gets or sets the target openness ratio for the current run.
        /// </summary>
        public float OpenCoverageTarget { get; set; }

        /// <summary>
        /// Gets or sets the allowed openness tolerance for the current run.
        /// </summary>
        public float OpenCoverageTolerance { get; set; }

        /// <summary>
        /// Gets or sets the measured openness ratio for the current run.
        /// </summary>
        public float OpenCoverageActual { get; set; }

        /// <summary>
        /// Gets or sets the measured openness delta from the target.
        /// </summary>
        public float OpenCoverageDelta { get; set; }

        /// <summary>
        /// Gets or sets the target obstacle fill derived from the openness target.
        /// </summary>
        public float TargetObstacleFill { get; set; }

        /// <summary>
        /// Gets or sets the actual obstacle fill derived from the openness measurement.
        /// </summary>
        public float ActualObstacleFill { get; set; }

        /// <summary>
        /// Gets or sets the human-readable coverage metric name used for the current report.
        /// </summary>
        public string CoverageMetricName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the count of interior standable ground cells used for the primary coverage metric.
        /// </summary>
        public int InteriorGroundStandableCells { get; set; }

        /// <summary>
        /// Gets or sets the total number of interior ground candidate cells used by the primary coverage metric.
        /// </summary>
        public int InteriorGroundCandidateCells { get; set; }

        /// <summary>
        /// Gets or sets the largest connected component ratio.
        /// </summary>
        public float LargestComponentRatio { get; set; }

        /// <summary>
        /// Gets or sets the ratio of interior semantic cells occupied by obstacle archetypes.
        /// </summary>
        public float ObstacleFillRatio { get; set; }

        /// <summary>
        /// Gets or sets the ratio of obstacle semantic cells that come from single-cell obstacle archetypes.
        /// </summary>
        public float SingleCellObstacleRatio { get; set; }

        /// <summary>
        /// Gets or sets the ratio of obstacle semantic cells contributed by tall obstacle archetypes with height two or higher.
        /// </summary>
        public float TallObstacleRatio { get; set; }

        /// <summary>
        /// Gets or sets the ratio of open interior semantic cells contained within the largest open connected region.
        /// </summary>
        public float LargestOpenAreaRatio { get; set; }

        /// <summary>
        /// Gets or sets the number of standable positions present at all heights.
        /// </summary>
        public int TotalStandableCells { get; set; }

        /// <summary>
        /// Gets or sets the number of standable ground positions.
        /// </summary>
        public int GroundStandableCells { get; set; }

        /// <summary>
        /// Gets or sets the last concrete failure reason observed before retry exhaustion.
        /// </summary>
        public GenerationFailureReason LastAttemptFailureReason { get; set; }

        /// <summary>
        /// Gets or sets the last concrete failure message observed before retry exhaustion.
        /// </summary>
        public string LastAttemptFailureMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ground coverage ratio from the last failed attempt.
        /// </summary>
        public float LastAttemptGroundCoverageRatio { get; set; }

        /// <summary>
        /// Gets or sets the largest connected component ratio from the last failed attempt.
        /// </summary>
        public float LastAttemptLargestComponentRatio { get; set; }

        /// <summary>
        /// Gets or sets the degraded footprint count from the last failed attempt.
        /// </summary>
        public int LastAttemptDegradedFootprintCount { get; set; }

        /// <summary>
        /// Gets semantic archetype counts gathered from the solved grid.
        /// </summary>
        public Dictionary<SemanticArchetype, int> SemanticCounts { get; } = new Dictionary<SemanticArchetype, int>();

        /// <summary>
        /// Gets interest anchor positions compiled into the map.
        /// </summary>
        public List<GridCoord3D> InterestAnchorPositions { get; } = new List<GridCoord3D>();

        /// <summary>
        /// Gets obstacle placement counts by semantic class.
        /// </summary>
        public Dictionary<ObstacleSemanticClass, int> ObstacleClassCounts { get; } = new Dictionary<ObstacleSemanticClass, int>();

        /// <summary>
        /// Gets dense-placement ratios by semantic class.
        /// </summary>
        public Dictionary<ObstacleSemanticClass, float> ObstacleDenseRatios { get; } = new Dictionary<ObstacleSemanticClass, float>();

        /// <summary>
        /// Gets or sets the number of explicit interest anchors in the final compile result.
        /// </summary>
        public int InterestAnchorCount { get; set; }

        /// <summary>
        /// Gets or sets the number of interest anchors requested by configuration.
        /// </summary>
        public int RequestedInterestAnchorCount { get; set; }

        /// <summary>
        /// Gets or sets the number of interest anchors that survived solver degradation.
        /// </summary>
        public int PlacedInterestAnchorCount { get; set; }

        /// <summary>
        /// Gets or sets how many soft-constraint degradations were applied.
        /// </summary>
        public int SoftConstraintDegradedCount { get; set; }

        /// <summary>
        /// Clears per-attempt counters before a new retry begins.
        /// </summary>
        public void ResetAttempt(int attempt)
        {
            Attempt = attempt;
            Success = false;
            FailureReason = GenerationFailureReason.None;
            Message = string.Empty;
            ObservationCount = 0;
            PropagationCount = 0;
            DegradedFootprintCount = 0;
            GroundCoverageRatio = 0f;
            OpenCoverageTarget = 0f;
            OpenCoverageTolerance = 0f;
            OpenCoverageActual = 0f;
            OpenCoverageDelta = 0f;
            TargetObstacleFill = 0f;
            ActualObstacleFill = 0f;
            CoverageMetricName = string.Empty;
            InteriorGroundStandableCells = 0;
            InteriorGroundCandidateCells = 0;
            LargestComponentRatio = 0f;
            ObstacleFillRatio = 0f;
            SingleCellObstacleRatio = 0f;
            TallObstacleRatio = 0f;
            LargestOpenAreaRatio = 0f;
            TotalStandableCells = 0;
            GroundStandableCells = 0;
            InterestAnchorCount = 0;
            RequestedInterestAnchorCount = 0;
            PlacedInterestAnchorCount = 0;
            SoftConstraintDegradedCount = 0;
            SemanticCounts.Clear();
            ObstacleClassCounts.Clear();
            ObstacleDenseRatios.Clear();
            InterestAnchorPositions.Clear();
        }

        /// <summary>
        /// Captures the most recent non-successful validation state so retry exhaustion can report actionable diagnostics.
        /// </summary>
        public void CaptureLastFailureSnapshot()
        {
            LastAttemptFailureReason = FailureReason;
            LastAttemptFailureMessage = Message;
            LastAttemptGroundCoverageRatio = GroundCoverageRatio;
            LastAttemptLargestComponentRatio = LargestComponentRatio;
            LastAttemptDegradedFootprintCount = DegradedFootprintCount;
        }
    }
}
