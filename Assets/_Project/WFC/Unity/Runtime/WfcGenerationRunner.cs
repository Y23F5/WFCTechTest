using UnityEngine;
using UnityEngine.Serialization;
using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;
using WFCTechTest.WFC.Runtime;
using System.Text;

namespace WFCTechTest.WFC.Unity.Runtime
{
    /// <summary>
    /// @file WfcGenerationRunner.cs
    /// @brief Provides a scene-facing entry point for single-seed generation, diagnostics, and scene spawning.
    /// </summary>
    public sealed class WfcGenerationRunner : MonoBehaviour
    {
        [SerializeField] private GenerationConfigAsset generationConfig;
        [SerializeField] private SemanticTileSetAsset semanticTileSet;
        [FormerlySerializedAs("obstaclePalette")]
        [SerializeField] private PrefabRegistryAsset prefabRegistry;
        [SerializeField] private ObstacleSceneSpawner prefabSpawner;
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

            if (prefabRegistry == null)
            {
                prefabRegistry = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
                prefabRegistry.EnsureDefaultPlaceholders(null);
            }

            var pipeline = new WfcGenerationPipeline(generationConfig, semanticTileSet, prefabRegistry);
            if (pipeline.TryGenerate(seed, out var compileResult, out var report))
            {
                LastCompileResult = compileResult;
                LastReport = report;
                ApplySpawnerConfiguration();
                prefabSpawner?.Spawn(compileResult);
                Debug.Log(BuildSuccessMessage(report));
                return;
            }

            LastCompileResult = null;
            LastReport = report;
            Debug.LogWarning($"WFC failed seed={seed} reason={report.FailureReason} lastReason={report.LastAttemptFailureReason} coverageMetric={report.CoverageMetricName} targetOpen={generationConfig.TargetOpenCoverage:P1}±{generationConfig.OpenCoverageTolerance:P1} actualOpen={report.LastAttemptGroundCoverageRatio:P1} obstacleFill={1f - report.LastAttemptGroundCoverageRatio:P1} component={report.LastAttemptLargestComponentRatio:P1} degraded={report.LastAttemptDegradedFootprintCount} soft={report.SoftConstraintDegradedCount} message={report.Message}");
        }

        /// <summary>
        /// Clears the most recently spawned map.
        /// </summary>
        [ContextMenu("Clear Spawned")]
        public void ClearSpawned()
        {
            prefabSpawner?.ClearSpawned();
        }

        /// <summary>
        /// Clears generated obstacles without touching world boundaries.
        /// </summary>
        [ContextMenu("Clear Obstacles")]
        public void ClearObstacles()
        {
            prefabSpawner?.ClearObstacles();
        }

        /// <summary>
        /// Clears generated world boundaries without touching obstacles.
        /// </summary>
        [ContextMenu("Clear Boundaries")]
        public void ClearBoundaries()
        {
            prefabSpawner?.ClearBoundaries();
        }

        /// <summary>
        /// Rebuilds only the world boundary geometry from the configured dimensions.
        /// </summary>
        [ContextMenu("Rebuild Boundaries")]
        public void RebuildBoundaries()
        {
            if (generationConfig == null || prefabSpawner == null)
            {
                Debug.LogError("WfcGenerationRunner requires both a GenerationConfigAsset and an obstacle scene spawner to rebuild boundaries.");
                return;
            }

            var boundaryMap = BuildBoundaryMap();
            ApplySpawnerConfiguration();
            prefabSpawner.ClearBoundaries();
            prefabSpawner.SpawnBoundaries(boundaryMap);
        }

        private void ApplySpawnerConfiguration()
        {
            if (prefabSpawner == null || generationConfig == null)
            {
                return;
            }

            prefabSpawner.SetPrefabRegistry(prefabRegistry);
            prefabSpawner.SetMapCenter(generationConfig.MapCenter);
        }

        private VoxelOccupancyMap BuildBoundaryMap()
        {
            var map = new VoxelOccupancyMap(generationConfig.Width, generationConfig.Height, generationConfig.Depth);
            for (var x = 0; x < map.Width; x++)
            {
                for (var z = 0; z < map.Depth; z++)
                {
                    map.SetCell(x, 0, z, VoxelCellKind.Floor);
                    if (x != 0 && z != 0 && x != map.Width - 1 && z != map.Depth - 1)
                    {
                        continue;
                    }

                    for (var y = 1; y <= generationConfig.BoundaryWallHeight; y++)
                    {
                        map.SetCell(x, y, z, VoxelCellKind.Wall);
                    }
                }
            }

            return map;
        }

        private static string BuildSuccessMessage(GenerationReport report)
        {
            var builder = new StringBuilder();
            builder.Append($"WFC success seed={report.Seed} attempts={report.Attempt} coverageMetric={report.CoverageMetricName} openTarget={report.OpenCoverageTarget:P1}±{report.OpenCoverageTolerance:P1} openActual={report.OpenCoverageActual:P1} delta={report.OpenCoverageDelta:+0.0%;-0.0%;0.0%} component={report.LargestComponentRatio:P1} ");
            builder.Append($"lowCover={ResolveClassCount(report, ObstacleSemanticClass.LowCover)} dense={ResolveDenseRatio(report, ObstacleSemanticClass.LowCover):P1} ");
            builder.Append($"highCover={ResolveClassCount(report, ObstacleSemanticClass.HighCover)} dense={ResolveDenseRatio(report, ObstacleSemanticClass.HighCover):P1} ");
            builder.Append($"tower={ResolveClassCount(report, ObstacleSemanticClass.Tower)} dense={ResolveDenseRatio(report, ObstacleSemanticClass.Tower):P1} ");
            builder.Append($"blocker={ResolveClassCount(report, ObstacleSemanticClass.Blocker)} dense={ResolveDenseRatio(report, ObstacleSemanticClass.Blocker):P1} ");
            builder.Append($"interestAnchors={report.PlacedInterestAnchorCount}/{report.RequestedInterestAnchorCount} obstacleFill={report.ActualObstacleFill:P1} openRegion={report.LargestOpenAreaRatio:P1} degraded={report.DegradedFootprintCount} soft={report.SoftConstraintDegradedCount}");
            return builder.ToString();
        }

        private static int ResolveClassCount(GenerationReport report, ObstacleSemanticClass semanticClass)
        {
            return report.ObstacleClassCounts.TryGetValue(semanticClass, out var count) ? count : 0;
        }

        private static float ResolveDenseRatio(GenerationReport report, ObstacleSemanticClass semanticClass)
        {
            return report.ObstacleDenseRatios.TryGetValue(semanticClass, out var ratio) ? ratio : 0f;
        }
    }
}
