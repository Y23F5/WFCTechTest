using System.Linq;
using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;
using WFCTechTest.WFC.Runtime;

namespace WFCTechTest.WFC.Editor {
    /**
     * @file WfcFuzzTestWindow.Variants.cs
     * @brief Variant construction and reporting helpers for the fuzz test window.
     */
    public sealed partial class WfcFuzzTestWindow {
        private GenerationConfigAsset CreateConfigVariant(int setIndex) {
            var random = new System.Random(_startSeed * 31 + setIndex * 97 + 17);
            var clone = CreateInstance<GenerationConfigAsset>();
            EditorUtility.CopySerialized(_generationConfig, clone);
            clone.SetOpenCoverageTarget(
                Mathf.Lerp(_targetOpenCoverageMin, _targetOpenCoverageMax, (float)random.NextDouble()),
                Mathf.Lerp(_openToleranceMin, _openToleranceMax, (float)random.NextDouble()));
            clone.SetDenseRatios(
                Mathf.Lerp(_denseRatioMin, _denseRatioMax, (float)random.NextDouble()),
                Mathf.Lerp(_denseRatioMin, _denseRatioMax, (float)random.NextDouble()),
                Mathf.Lerp(_denseRatioMin, _denseRatioMax, (float)random.NextDouble()),
                Mathf.Lerp(_denseRatioMin, _denseRatioMax, (float)random.NextDouble()));
            return clone;
        }

        private SemanticTileSetAsset CreateTileSetVariant(int setIndex) {
            var random = new System.Random(_startSeed * 59 + setIndex * 131 + 29);
            var clone = CreateInstance<SemanticTileSetAsset>();
            EditorUtility.CopySerialized(_tileSet, clone);
            var definitions = clone.GetDefinitions();
            foreach (var definition in definitions) {
                if (definition.BoundaryOnly) continue;

                if (definition.Archetype == SemanticArchetype.Open || definition.Archetype == SemanticArchetype.InterestAnchor) {
                    definition.Weight *= Mathf.Lerp(_openWeightMinScale, _openWeightMaxScale, (float)random.NextDouble());
                } else {
                    definition.Weight *= Mathf.Lerp(_obstacleWeightMinScale, _obstacleWeightMaxScale, (float)random.NextDouble());
                }
            }

            return clone;
        }

        private PrefabRegistryAsset CreatePrefabRegistryVariant(int setIndex) {
            var random = new System.Random(_startSeed * 83 + setIndex * 149 + 43);
            var clone = CreateInstance<PrefabRegistryAsset>();
            EditorUtility.CopySerialized(_prefabRegistry, clone);
            foreach (var entry in clone.Entries) {
                if (entry == null || !entry.EnabledForAutoGeneration) continue;
                entry.Weight *= Mathf.Lerp(_obstacleWeightMinScale, _obstacleWeightMaxScale, (float)random.NextDouble());
            }

            return clone;
        }

        private static string DescribeVariant(GenerationConfigAsset config, SemanticTileSetAsset tileSet, PrefabRegistryAsset prefabRegistry, float ratio, System.Collections.Generic.List<GenerationReport> reports) {
            var open = tileSet.GetDefinition(SemanticArchetype.Open).Weight;
            var lowCover = tileSet.GetDefinition(SemanticArchetype.LowCoverSparse).Weight + tileSet.GetDefinition(SemanticArchetype.LowCoverDense).Weight;
            var blocker = tileSet.GetDefinition(SemanticArchetype.BlockerSparse).Weight + tileSet.GetDefinition(SemanticArchetype.BlockerDense).Weight;
            var avgPrefabRegistryWeight = prefabRegistry.Entries.Where(entry => entry.EnabledForAutoGeneration).DefaultIfEmpty().Average(entry => entry?.Weight ?? 0f);
            var avgCoverage = reports.Average(report => report.OpenCoverageActual);
            var avgLowDense = reports.Average(report => report.ObstacleDenseRatios.TryGetValue(ObstacleSemanticClass.LowCover, out var dense) ? dense : 0f);
            var avgBlockerDense = reports.Average(report => report.ObstacleDenseRatios.TryGetValue(ObstacleSemanticClass.Blocker, out var dense) ? dense : 0f);
            var avgDegraded = reports.Average(report => report.DegradedFootprintCount);
            return $"success={ratio:P1}, targetOpen={config.TargetOpenCoverage:P1}±{config.OpenCoverageTolerance:P1}, denseTargets={config.LowCoverDenseRatio:F2}/{config.HighCoverDenseRatio:F2}/{config.TowerDenseRatio:F2}/{config.BlockerDenseRatio:F2}, open={open:F2}, lowCover={lowCover:F2}, blocker={blocker:F2}, avgPrefabRegistryWeight={avgPrefabRegistryWeight:F2}, avgOpen={avgCoverage:P1}, avgObstacleFill={(1f - avgCoverage):P1}, avgLowDense={avgLowDense:P1}, avgBlockerDense={avgBlockerDense:P1}, avgDegraded={avgDegraded:F1}";
        }

        private static void DestroyVariant(UnityEngine.Object config, UnityEngine.Object tileSet, UnityEngine.Object prefabRegistry) {
            if (config != null) DestroyImmediate(config);
            if (tileSet != null) DestroyImmediate(tileSet);
            if (prefabRegistry != null) DestroyImmediate(prefabRegistry);
        }
    }
}
