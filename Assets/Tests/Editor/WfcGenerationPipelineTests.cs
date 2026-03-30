using NUnit.Framework;
using System.Linq;
using UnityEngine;
using WFCTechTest.WFC.Core;
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
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);
            palette.GetEntry(0).SemanticClass = ObstacleSemanticClass.LowCover;
            palette.GetEntry(1).SemanticClass = ObstacleSemanticClass.HighCover;
            palette.GetEntry(2).SemanticClass = ObstacleSemanticClass.Tower;
            var pipeline = new WfcGenerationPipeline(config, tileSet, palette);

            var success = pipeline.TryGenerate(20260312, out var result, out var report);

            Assert.That(success, Is.True, report.Message);
            Assert.That(result, Is.Not.Null);
            Assert.That(report.Success, Is.True);
            Assert.That(report.OpenCoverageActual, Is.InRange(config.MinGroundCoverage, config.MaxGroundCoverage));
            Assert.That(report.OpenCoverageDelta, Is.InRange(-config.OpenCoverageTolerance, config.OpenCoverageTolerance));
            Assert.That(report.LargestComponentRatio, Is.GreaterThanOrEqualTo(config.MinLargestComponentRatio));
            Assert.That(result.ObstaclePlacements.TrueForAll(placement => placement.FootprintWidth == 1 && placement.FootprintDepth == 1));
            Assert.That(report.ObstacleClassCounts.ContainsKey(ObstacleSemanticClass.LowCover), Is.True);
            Assert.That(report.ObstacleDenseRatios.ContainsKey(ObstacleSemanticClass.LowCover), Is.True);
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
            var targetField = configType.GetField("targetOpenCoverage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var toleranceField = configType.GetField("openCoverageTolerance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var initializedField = configType.GetField("coverageTargetInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            coverageField.SetValue(config, CoverageMetricMode.InteriorFloorOnly);
            targetField.SetValue(config, 0.58f);
            toleranceField.SetValue(config, 0.12f);
            initializedField.SetValue(config, true);

            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);
            palette.GetEntry(0).SemanticClass = ObstacleSemanticClass.LowCover;
            palette.GetEntry(1).SemanticClass = ObstacleSemanticClass.HighCover;
            palette.GetEntry(2).SemanticClass = ObstacleSemanticClass.Tower;
            var pipeline = new WfcGenerationPipeline(config, tileSet, palette);

            var success = pipeline.TryGenerate(20260313, out var result, out var report);

            Assert.That(success, Is.True, report.Message);
            Assert.That(result, Is.Not.Null);
            Assert.That(report.Success, Is.True);
            Assert.That(report.CoverageMetricName, Is.EqualTo(CoverageMetricMode.InteriorFloorOnly.ToString()));
            Assert.That(report.OpenCoverageActual, Is.InRange(config.MinGroundCoverage, config.MaxGroundCoverage));
        }

        /// <summary>
        /// Verifies that the heavy semantic generation pipeline produces stable placements for the same seed.
        /// </summary>
        [Test]
        public void TryGenerate_IsDeterministicForSameSeed()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);
            palette.GetEntry(0).SemanticClass = ObstacleSemanticClass.LowCover;
            palette.GetEntry(1).SemanticClass = ObstacleSemanticClass.HighCover;
            palette.GetEntry(2).SemanticClass = ObstacleSemanticClass.Tower;
            var pipeline = new WfcGenerationPipeline(config, tileSet, palette);

            Assert.That(pipeline.TryGenerate(4242, out var first, out var firstReport), Is.True, firstReport.Message);
            Assert.That(pipeline.TryGenerate(4242, out var second, out var secondReport), Is.True, secondReport.Message);
            Assert.That(first.ObstaclePlacements.Count, Is.EqualTo(second.ObstaclePlacements.Count));

            for (var i = 0; i < first.ObstaclePlacements.Count; i++)
            {
                var a = first.ObstaclePlacements[i];
                var b = second.ObstaclePlacements[i];
                Assert.That(a.Type, Is.EqualTo(b.Type));
                Assert.That(a.Anchor, Is.EqualTo(b.Anchor));
                Assert.That(a.RotationY, Is.EqualTo(b.RotationY));
                Assert.That(a.Archetype, Is.EqualTo(b.Archetype));
            }
        }

        [Test]
        public void TryGenerate_DefaultConfigHandlesSeed12345()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);

            var pipeline = new WfcGenerationPipeline(config, tileSet, palette);
            var success = pipeline.TryGenerate(12345, out var result, out var report);

            Assert.That(success, Is.True, report.Message);
            Assert.That(result, Is.Not.Null);
            Assert.That(report.OpenCoverageActual, Is.InRange(config.MinGroundCoverage, config.MaxGroundCoverage));
            Assert.That(report.OpenCoverageDelta, Is.InRange(-config.OpenCoverageTolerance, config.OpenCoverageTolerance));
        }

        [TestCase(0.20f)]
        [TestCase(0.90f)]
        public void TryGenerate_HitsExtremeOpenCoverageTargets(float targetOpenCoverage)
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var configType = typeof(GenerationConfigAsset);
            configType.GetField("targetOpenCoverage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(config, targetOpenCoverage);
            configType.GetField("openCoverageTolerance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(config, 0.02f);
            configType.GetField("coverageTargetInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(config, true);

            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var registry = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            registry.EnsureDefaultPlaceholders(null);
            var pipeline = new WfcGenerationPipeline(config, tileSet, registry);

            var success = pipeline.TryGenerate(targetOpenCoverage >= 0.5f ? 9090 : 2020, out var result, out var report);

            Assert.That(success, Is.True, report.Message);
            Assert.That(result, Is.Not.Null);
            Assert.That(report.OpenCoverageActual, Is.InRange(targetOpenCoverage - 0.02f, targetOpenCoverage + 0.02f));
            Assert.That(report.LargestComponentRatio, Is.GreaterThanOrEqualTo(config.MinLargestComponentRatio));
        }
    }
}
