using NUnit.Framework;
using UnityEngine;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Runtime;

namespace WFCTechTest.WFC.Tests.Editor
{
    /// <summary>
    /// @file WfcGenerationPipelineTests.cs
    /// @brief Exercises the end-to-end pipeline to ensure it can produce valid blockout maps.
    /// </summary>
    public sealed class WfcGenerationPipelineTests
    {
        /// <summary>
        /// Verifies that the pipeline can produce a valid map within the configured retry budget.
        /// </summary>
        [Test]
        public void TryGenerate_ProducesValidResult()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var pipeline = new WfcGenerationPipeline(config, tileSet);

            var success = pipeline.TryGenerate(20260312, out var result, out var report);

            Assert.That(success, Is.True, report.Message);
            Assert.That(result, Is.Not.Null);
            Assert.That(report.Success, Is.True);
            Assert.That(report.GroundCoverageRatio, Is.InRange(config.MinGroundCoverage, config.MaxGroundCoverage));
            Assert.That(report.LargestComponentRatio, Is.GreaterThanOrEqualTo(config.MinLargestComponentRatio));
        }

        /// <summary>
        /// Verifies that generation also succeeds when coverage is measured by semantic floor-only openness.
        /// </summary>
        [Test]
        public void TryGenerate_ProducesValidResultWithFloorOnlyCoverageMetric()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var configType = typeof(GenerationConfigAsset);
            var coverageField = configType.GetField("coverageMetric", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var minField = configType.GetField("minGroundCoverage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxField = configType.GetField("maxGroundCoverage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            coverageField.SetValue(config, CoverageMetricMode.InteriorFloorOnly);
            minField.SetValue(config, 0.45f);
            maxField.SetValue(config, 0.7f);

            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var pipeline = new WfcGenerationPipeline(config, tileSet);

            var success = pipeline.TryGenerate(20260313, out var result, out var report);

            Assert.That(success, Is.True, report.Message);
            Assert.That(result, Is.Not.Null);
            Assert.That(report.Success, Is.True);
            Assert.That(report.CoverageMetricName, Is.EqualTo(CoverageMetricMode.InteriorFloorOnly.ToString()));
            Assert.That(report.GroundCoverageRatio, Is.InRange(config.MinGroundCoverage, config.MaxGroundCoverage));
        }
    }
}
