using System.Linq;
using System.Text;
using UnityEngine;
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

            var count = GetSeedCount();
            var batchReport = new BatchGenerationReport();
            var pipeline = new WfcGenerationPipeline(generationConfig, semanticTileSet);

            for (var i = 0; i < count; i++)
            {
                pipeline.TryGenerate(startSeed + i, out _, out var report);
                batchReport.Runs.Add(report);
            }

            var builder = new StringBuilder();
            builder.AppendLine($"WFC batch {batchSize} seeds={count} metric={generationConfig.CoverageMetric} success={batchReport.SuccessRatio:P1} avgAttempts={batchReport.AverageAttempts:F2}");
            if (batchReport.Runs.Count > 0)
            {
                builder.AppendLine($"avg obstacleFill={batchReport.Runs.Average(report => report.ObstacleFillRatio):P1} avg singleCell={batchReport.Runs.Average(report => report.SingleCellObstacleRatio):P1} avg tall={batchReport.Runs.Average(report => report.TallObstacleRatio):P1} avg openRegion={batchReport.Runs.Average(report => report.LargestOpenAreaRatio):P1}");
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
    }
}
