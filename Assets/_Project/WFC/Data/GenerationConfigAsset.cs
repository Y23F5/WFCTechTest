using UnityEngine;

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
        /// Gets the maximum number of restart attempts per seed.
        /// </summary>
        public int MaxRetries => maxRetries;

        /// <summary>
        /// Gets the enforced boundary wall height.
        /// </summary>
        public int BoundaryWallHeight => boundaryWallHeight;

        /// <summary>
        /// Gets the coverage metric mode used by validation.
        /// </summary>
        public CoverageMetricMode CoverageMetric => coverageMetric;

        /// <summary>
        /// Gets the target minimum walkable ground ratio.
        /// </summary>
        public float MinGroundCoverage => minGroundCoverage;

        /// <summary>
        /// Gets the target maximum walkable ground ratio.
        /// </summary>
        public float MaxGroundCoverage => maxGroundCoverage;

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

        [Header("Dimensions")]
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
        [SerializeField] private float minGroundCoverage = 0.55f;
        [SerializeField] private float maxGroundCoverage = 0.65f;
        [SerializeField] private float minLargestComponentRatio = 0.85f;

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
            minGroundCoverage = Mathf.Clamp01(minGroundCoverage);
            maxGroundCoverage = Mathf.Clamp(maxGroundCoverage, minGroundCoverage, 1f);
            minLargestComponentRatio = Mathf.Clamp01(minLargestComponentRatio);
            quickBatchSeeds = Mathf.Max(1, quickBatchSeeds);
            standardBatchSeeds = Mathf.Max(quickBatchSeeds, standardBatchSeeds);
            stressBatchSeeds = Mathf.Max(standardBatchSeeds, stressBatchSeeds);
        }
    }
}
