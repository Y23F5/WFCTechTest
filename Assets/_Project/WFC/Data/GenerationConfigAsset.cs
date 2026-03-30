using UnityEngine;
using UnityEngine.Serialization;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Data
{
    /// <summary>
    /// @file GenerationConfigAsset.cs
    /// @brief Stores generation, validation, diagnostics, and batch-testing settings for the WFC pipeline.
    /// </summary>
    public enum CoverageMetricMode
    {
        /// <summary>
        /// Counts standable positions in the interior playable area.
        /// </summary>
        InteriorStandable,

        /// <summary>
        /// Counts only interior cells whose semantic result remains open or reserved for spawn or respawn use.
        /// </summary>
        InteriorFloorOnly
    }

    /// <summary>
    /// Stores generation, validation, diagnostics, and batch-testing settings for the WFC pipeline.
    /// </summary>
    [CreateAssetMenu(menuName = "WFC/Generation Config", fileName = "GenerationConfig")]
    public sealed class GenerationConfigAsset : ScriptableObject
    {
        /// <summary>
        /// Gets the world-space map center.
        /// </summary>
        public Vector3 MapCenter => mapCenter;

        /// <summary>
        /// Gets the semantic grid width.
        /// </summary>
        public int Width => width;

        /// <summary>
        /// Gets the semantic grid depth.
        /// </summary>
        public int Depth => depth;

        /// <summary>
        /// Gets the voxel grid height.
        /// </summary>
        public int Height => height;

        /// <summary>
        /// Gets the configured maximum number of restart attempts per seed.
        /// </summary>
        public int MaxRetries => maxRetries;

        /// <summary>
        /// Gets the effective retry budget after applying openness-extreme scaling.
        /// </summary>
        public int EffectiveMaxRetries => Mathf.Max(maxRetries, Mathf.RoundToInt(maxRetries * GetRetryBudgetMultiplier()));

        /// <summary>
        /// Gets the enforced boundary wall height.
        /// </summary>
        public int BoundaryWallHeight => boundaryWallHeight;

        /// <summary>
        /// Gets the coverage metric mode used by validation.
        /// </summary>
        public CoverageMetricMode CoverageMetric => coverageMetric;

        /// <summary>
        /// Gets the desired interior openness ratio.
        /// </summary>
        public float TargetOpenCoverage => targetOpenCoverage;

        /// <summary>
        /// Gets the acceptable openness tolerance around the target.
        /// </summary>
        public float OpenCoverageTolerance => openCoverageTolerance;

        /// <summary>
        /// Gets the target minimum walkable ground ratio derived from the openness target.
        /// </summary>
        public float MinGroundCoverage => Mathf.Clamp01(targetOpenCoverage - openCoverageTolerance);

        /// <summary>
        /// Gets the target maximum walkable ground ratio derived from the openness target.
        /// </summary>
        public float MaxGroundCoverage => Mathf.Clamp01(targetOpenCoverage + openCoverageTolerance);

        /// <summary>
        /// Gets the derived target obstacle fill ratio.
        /// </summary>
        public float TargetObstacleFill => 1f - targetOpenCoverage;

        /// <summary>
        /// Gets the required minimum largest-component ratio.
        /// </summary>
        public float MinLargestComponentRatio => minLargestComponentRatio;

        /// <summary>
        /// Gets the maximum supported upward jump height.
        /// </summary>
        public float MaxJumpHeight => maxJumpHeight;

        /// <summary>
        /// Gets the maximum supported jump distance.
        /// </summary>
        public float MaxJumpDistance => maxJumpDistance;

        /// <summary>
        /// Gets the number of semantic interest anchors to force into the layout.
        /// </summary>
        public int InterestAnchorCount => interestAnchorCount;

        /// <summary>
        /// Gets the number of seeds used for quick batch tests.
        /// </summary>
        public int QuickBatchSeeds => quickBatchSeeds;

        /// <summary>
        /// Gets the number of seeds used for standard batch tests.
        /// </summary>
        public int StandardBatchSeeds => standardBatchSeeds;

        /// <summary>
        /// Gets the number of seeds used for stress batch tests.
        /// </summary>
        public int StressBatchSeeds => stressBatchSeeds;

        /// <summary>
        /// Gets the target dense ratio for low-cover semantics.
        /// </summary>
        public float LowCoverDenseRatio => lowCoverDenseRatio;

        /// <summary>
        /// Gets the target dense ratio for high-cover semantics.
        /// </summary>
        public float HighCoverDenseRatio => highCoverDenseRatio;

        /// <summary>
        /// Gets the target dense ratio for tower semantics.
        /// </summary>
        public float TowerDenseRatio => towerDenseRatio;

        /// <summary>
        /// Gets the target dense ratio for blocker semantics.
        /// </summary>
        public float BlockerDenseRatio => blockerDenseRatio;

        [Header("Dimensions")]
        [SerializeField] private Vector3 mapCenter = Vector3.zero;
        [SerializeField] private int width = 48;
        [SerializeField] private int height = 8;
        [SerializeField] private int depth = 48;

        [Header("Constraints")]
        [SerializeField] private int maxRetries = 20;
        [SerializeField] private int boundaryWallHeight = 4;
        [SerializeField] private int interestAnchorCount = 4;

        [Header("Movement")]
        [SerializeField] private float maxJumpHeight = 1.1f;
        [SerializeField] private float maxJumpDistance = 2.1f;

        [Header("Validation")]
        [SerializeField] private CoverageMetricMode coverageMetric = CoverageMetricMode.InteriorStandable;
        [SerializeField] private float targetOpenCoverage = 0.60f;
        [SerializeField] private float openCoverageTolerance = 0.02f;
        [SerializeField] private float minLargestComponentRatio = 0.85f;

        [FormerlySerializedAs("minGroundCoverage")]
        [SerializeField, HideInInspector] private float legacyMinGroundCoverage = 0.55f;
        [FormerlySerializedAs("maxGroundCoverage")]
        [SerializeField, HideInInspector] private float legacyMaxGroundCoverage = 0.65f;
        [SerializeField, HideInInspector] private bool coverageTargetInitialized;

        [Header("Heavy Semantic Density")]
        [SerializeField] private float lowCoverDenseRatio = 0.42f;
        [SerializeField] private float highCoverDenseRatio = 0.48f;
        [SerializeField] private float towerDenseRatio = 0.35f;
        [SerializeField] private float blockerDenseRatio = 0.55f;

        [Header("Batch Testing")]
        [SerializeField] private int quickBatchSeeds = 20;
        [SerializeField] private int standardBatchSeeds = 100;
        [SerializeField] private int stressBatchSeeds = 500;

        private void OnValidate()
        {
            width = Mathf.Max(8, width);
            height = Mathf.Clamp(height, 5, 32);
            depth = Mathf.Max(8, depth);
            maxRetries = Mathf.Clamp(maxRetries, 1, 512);
            boundaryWallHeight = Mathf.Clamp(boundaryWallHeight, 2, height - 1);
            interestAnchorCount = Mathf.Clamp(interestAnchorCount, 0, 16);
            maxJumpHeight = Mathf.Max(0.1f, maxJumpHeight);
            maxJumpDistance = Mathf.Max(1f, maxJumpDistance);
            if (!coverageTargetInitialized)
            {
                var migratedMin = Mathf.Clamp01(legacyMinGroundCoverage);
                var migratedMax = Mathf.Clamp(legacyMaxGroundCoverage, migratedMin, 1f);
                targetOpenCoverage = (migratedMin + migratedMax) * 0.5f;
                openCoverageTolerance = Mathf.Max(0.01f, (migratedMax - migratedMin) * 0.5f);
                coverageTargetInitialized = true;
            }

            targetOpenCoverage = Mathf.Clamp(targetOpenCoverage, 0.10f, 0.95f);
            openCoverageTolerance = Mathf.Clamp(openCoverageTolerance, 0.01f, 0.20f);
            minLargestComponentRatio = Mathf.Clamp01(minLargestComponentRatio);
            lowCoverDenseRatio = Mathf.Clamp01(lowCoverDenseRatio);
            highCoverDenseRatio = Mathf.Clamp01(highCoverDenseRatio);
            towerDenseRatio = Mathf.Clamp01(towerDenseRatio);
            blockerDenseRatio = Mathf.Clamp01(blockerDenseRatio);
            quickBatchSeeds = Mathf.Max(1, quickBatchSeeds);
            standardBatchSeeds = Mathf.Max(quickBatchSeeds, standardBatchSeeds);
            stressBatchSeeds = Mathf.Max(standardBatchSeeds, stressBatchSeeds);
            legacyMinGroundCoverage = MinGroundCoverage;
            legacyMaxGroundCoverage = MaxGroundCoverage;
        }

        /// <summary>
        /// Returns the configured dense target ratio for the supplied obstacle semantic class.
        /// </summary>
        public float GetDenseRatio(ObstacleSemanticClass semanticClass)
        {
            return semanticClass switch
            {
                ObstacleSemanticClass.LowCover => lowCoverDenseRatio,
                ObstacleSemanticClass.HighCover => highCoverDenseRatio,
                ObstacleSemanticClass.Tower => towerDenseRatio,
                ObstacleSemanticClass.Blocker => blockerDenseRatio,
                _ => 0f
            };
        }

        /**
         * @brief Applies a new openness target and tolerance, then revalidates the asset state.
         * @param targetOpen The desired open coverage ratio.
         * @param tolerance The allowed deviation around the target.
         */
        public void SetOpenCoverageTarget(float targetOpen, float tolerance)
        {
            targetOpenCoverage = targetOpen;
            openCoverageTolerance = tolerance;
            coverageTargetInitialized = true;
            OnValidate();
        }

        /**
         * @brief Applies all dense ratio controls at once and revalidates the asset state.
         */
        public void SetDenseRatios(float lowCover, float highCover, float tower, float blocker)
        {
            lowCoverDenseRatio = lowCover;
            highCoverDenseRatio = highCover;
            towerDenseRatio = tower;
            blockerDenseRatio = blocker;
            OnValidate();
        }

        private float GetRetryBudgetMultiplier()
        {
            if (targetOpenCoverage <= 0.15f || targetOpenCoverage >= 0.95f)
            {
                return 4f;
            }

            if (targetOpenCoverage <= 0.20f || targetOpenCoverage >= 0.90f)
            {
                return 3f;
            }

            if (targetOpenCoverage <= 0.25f || targetOpenCoverage >= 0.85f)
            {
                return 2f;
            }

            return 1f;
        }
    }
}
