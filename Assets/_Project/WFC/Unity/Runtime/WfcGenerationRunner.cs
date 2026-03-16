using UnityEngine;
using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;
using WFCTechTest.WFC.Runtime;

namespace WFCTechTest.WFC.Unity.Runtime
{
    /// <summary>
    /// @file WfcGenerationRunner.cs
    /// @brief Provides a scene-facing entry point for single-seed generation, diagnostics, and cube spawning.
    /// </summary>
    public sealed class WfcGenerationRunner : MonoBehaviour
    {
        [SerializeField] private GenerationConfigAsset generationConfig;
        [SerializeField] private SemanticTileSetAsset semanticTileSet;
        [SerializeField] private VoxelPrefabSpawner prefabSpawner;
        [SerializeField] private int seed = 12345;

        /// <summary>
        /// Gets the most recent successful compile result.
        /// </summary>
        public CompileResult LastCompileResult { get; private set; }

        /// <summary>
        /// Gets the most recent generation report.
        /// </summary>
        public GenerationReport LastReport { get; private set; }

        /// <summary>
        /// Generates and optionally spawns a map for the configured seed.
        /// </summary>
        [ContextMenu("Generate")]
        public void Generate()
        {
            if (generationConfig == null)
            {
                Debug.LogError("WfcGenerationRunner requires a GenerationConfigAsset.");
                return;
            }

            if (semanticTileSet == null)
            {
                semanticTileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
                semanticTileSet.ResetToDefaults();
            }

            var pipeline = new WfcGenerationPipeline(generationConfig, semanticTileSet);
            if (pipeline.TryGenerate(seed, out var compileResult, out var report))
            {
                LastCompileResult = compileResult;
                LastReport = report;
                prefabSpawner?.Spawn(compileResult.Volume);
                Debug.Log($"WFC success seed={seed} attempts={report.Attempt} coverageMetric={report.CoverageMetricName} coverage={report.GroundCoverageRatio:P1} component={report.LargestComponentRatio:P1} obstacleFill={report.ObstacleFillRatio:P1} singleCell={report.SingleCellObstacleRatio:P1} tall={report.TallObstacleRatio:P1} openRegion={report.LargestOpenAreaRatio:P1} degraded={report.DegradedFootprintCount}");
                return;
            }

            LastCompileResult = null;
            LastReport = report;
            Debug.LogWarning($"WFC failed seed={seed} reason={report.FailureReason} lastReason={report.LastAttemptFailureReason} coverageMetric={report.CoverageMetricName} coverage={report.LastAttemptGroundCoverageRatio:P1} component={report.LastAttemptLargestComponentRatio:P1} degraded={report.LastAttemptDegradedFootprintCount} message={report.Message}");
        }

        /// <summary>
        /// Clears the most recently spawned map.
        /// </summary>
        [ContextMenu("Clear Spawned")]
        public void ClearSpawned()
        {
            prefabSpawner?.ClearSpawned();
        }
    }
}
