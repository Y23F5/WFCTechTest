using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;
using WFCTechTest.WFC.Runtime;

namespace WFCTechTest.WFC.Unity.Runtime
{
    /// <summary>
    /// @file WfcBatchRunner.cs
    /// @brief Runs repeated multi-seed generation batches and prints aggregate diagnostics for regression testing.
    /// </summary>
    public sealed class WfcBatchRunner : MonoBehaviour
    {
        /// <summary>
        /// Enumerates supported preset batch sizes.
        /// </summary>
        public enum BatchSize
        {
            /// <summary>
            /// Quick smoke run.
            /// </summary>
            Quick,

            /// <summary>
            /// Standard regression run.
            /// </summary>
            Standard,

            /// <summary>
            /// Stress regression run.
            /// </summary>
            Stress
        }

        [SerializeField] private GenerationConfigAsset generationConfig;
        [SerializeField] private SemanticTileSetAsset semanticTileSet;
        [FormerlySerializedAs("obstaclePalette")]
        [SerializeField] private PrefabRegistryAsset prefabRegistry;
        [SerializeField] private int startSeed = 1000;
        [SerializeField] private BatchSize batchSize = BatchSize.Standard;

        /// <summary>
        /// Executes a batch generation regression run.
        /// </summary>
        [ContextMenu("Run Batch")]
        public void RunBatch()
        {
            if (generationConfig == null)
            {
                Debug.LogError("WfcBatchRunner requires a GenerationConfigAsset.");
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

            var count = GetSeedCount();
            var batchReport = new BatchGenerationReport();
            var pipeline = new WfcGenerationPipeline(generationConfig, semanticTileSet, prefabRegistry);

            for (var i = 0; i < count; i++)
            {
                pipeline.TryGenerate(startSeed + i, out _, out var report);
                batchReport.Runs.Add(report);
            }

            var builder = new StringBuilder();
            builder.AppendLine($"WFC batch {batchSize} seeds={count} metric={generationConfig.CoverageMetric} success={batchReport.SuccessRatio:P1} avgAttempts={batchReport.AverageAttempts:F2} targetOpen={generationConfig.TargetOpenCoverage:P1}±{generationConfig.OpenCoverageTolerance:P1}");
            if (batchReport.Runs.Count > 0)
            {
                builder.AppendLine($"avg lowCover={AverageClassCount(batchReport, ObstacleSemanticClass.LowCover):F1} dense={AverageDenseRatio(batchReport, ObstacleSemanticClass.LowCover):P1} avg highCover={AverageClassCount(batchReport, ObstacleSemanticClass.HighCover):F1} dense={AverageDenseRatio(batchReport, ObstacleSemanticClass.HighCover):P1}");
                builder.AppendLine($"avg tower={AverageClassCount(batchReport, ObstacleSemanticClass.Tower):F1} dense={AverageDenseRatio(batchReport, ObstacleSemanticClass.Tower):P1} avg blocker={AverageClassCount(batchReport, ObstacleSemanticClass.Blocker):F1} dense={AverageDenseRatio(batchReport, ObstacleSemanticClass.Blocker):P1}");
                builder.AppendLine($"avg interestAnchors={batchReport.Runs.Average(report => report.PlacedInterestAnchorCount):F1}/{generationConfig.InterestAnchorCount:F1} avg openCoverage={batchReport.Runs.Average(report => report.OpenCoverageActual):P1} avg obstacleFill={batchReport.Runs.Average(report => report.ActualObstacleFill):P1} avg delta={batchReport.Runs.Average(report => report.OpenCoverageDelta):+0.0%;-0.0%;0.0%} avg openRegion={batchReport.Runs.Average(report => report.LargestOpenAreaRatio):P1}");
            }
            foreach (var pair in batchReport.BuildFailureHistogram())
            {
                builder.AppendLine($"- {pair.Key}: {pair.Value}");
            }

            builder.AppendLine("Last-attempt failure histogram:");
            foreach (var pair in batchReport.Runs
                         .GroupBy(report => report.LastAttemptFailureReason)
                         .OrderByDescending(group => group.Count()))
            {
                builder.AppendLine($"- {pair.Key}: {pair.Count()}");
            }

            Debug.Log(builder.ToString());
        }

        private int GetSeedCount()
        {
            return batchSize switch
            {
                BatchSize.Quick => generationConfig.QuickBatchSeeds,
                BatchSize.Standard => generationConfig.StandardBatchSeeds,
                _ => generationConfig.StressBatchSeeds
            };
        }

        private static float AverageClassCount(BatchGenerationReport batchReport, ObstacleSemanticClass semanticClass)
        {
            if (batchReport.Runs.Count == 0)
            {
                return 0f;
            }

            return (float)batchReport.Runs.Average(report => report.ObstacleClassCounts.TryGetValue(semanticClass, out var count) ? count : 0);
        }

        private static float AverageDenseRatio(BatchGenerationReport batchReport, ObstacleSemanticClass semanticClass)
        {
            if (batchReport.Runs.Count == 0)
            {
                return 0f;
            }

            return (float)batchReport.Runs.Average(report => report.ObstacleDenseRatios.TryGetValue(semanticClass, out var ratio) ? ratio : 0f);
        }
    }
}
