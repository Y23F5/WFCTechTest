using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;
using WFCTechTest.WFC.Runtime;

namespace WFCTechTest.WFC.Editor
{
    /// <summary>
    /// @file WfcFuzzTestWindow.cs
    /// @brief Runs brute-force randomized tuning sweeps across many weight sets and seeds to stress-test generator stability.
    /// </summary>
    public sealed class WfcFuzzTestWindow : EditorWindow
    {
        private GenerationConfigAsset _generationConfig;
        private SemanticTileSetAsset _tileSet;
        private int _parameterSets = 20;
        private int _seedsPerSet = 50;
        private int _startSeed = 1000;
        private float _openWeightMinScale = 0.9f;
        private float _openWeightMaxScale = 1.2f;
        private float _obstacleWeightMinScale = 0.6f;
        private float _obstacleWeightMaxScale = 1.2f;
        private float _minCoverageLowerBound = 0.55f;
        private float _minCoverageUpperBound = 0.78f;
        private float _maxCoverageLowerBound = 0.75f;
        private float _maxCoverageUpperBound = 0.92f;
        private Vector2 _scroll;
        private string _lastReport = string.Empty;

        /// <summary>
        /// Opens the brute-force fuzz test window.
        /// </summary>
        [MenuItem("Window/WFC/Fuzz Test")]
        public static void OpenWindow()
        {
            var window = GetWindow<WfcFuzzTestWindow>("WFC Fuzz Test");
            window.minSize = new Vector2(520f, 620f);
        }

        private void OnEnable()
        {
            if (_generationConfig == null)
            {
                _generationConfig = AssetDatabase.LoadAssetAtPath<GenerationConfigAsset>("Assets/GenerationConfig.asset");
            }

            if (_tileSet == null)
            {
                _tileSet = AssetDatabase.LoadAssetAtPath<SemanticTileSetAsset>("Assets/SemanticTileSet.asset");
            }
        }

        private void OnGUI()
        {
            _generationConfig = (GenerationConfigAsset)EditorGUILayout.ObjectField("Generation Config", _generationConfig, typeof(GenerationConfigAsset), false);
            _tileSet = (SemanticTileSetAsset)EditorGUILayout.ObjectField("Semantic Tile Set", _tileSet, typeof(SemanticTileSetAsset), false);

            EditorGUILayout.Space(6f);
            _parameterSets = EditorGUILayout.IntSlider("Parameter Sets", _parameterSets, 1, 200);
            _seedsPerSet = EditorGUILayout.IntSlider("Seeds Per Set", _seedsPerSet, 1, 500);
            _startSeed = EditorGUILayout.IntField("Start Seed", _startSeed);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Open Weight Scale", EditorStyles.boldLabel);
            _openWeightMinScale = EditorGUILayout.Slider("Open Min", _openWeightMinScale, 0.2f, 2f);
            _openWeightMaxScale = EditorGUILayout.Slider("Open Max", _openWeightMaxScale, _openWeightMinScale, 2.5f);

            EditorGUILayout.LabelField("Obstacle Weight Scale", EditorStyles.boldLabel);
            _obstacleWeightMinScale = EditorGUILayout.Slider("Obstacle Min", _obstacleWeightMinScale, 0.1f, 2f);
            _obstacleWeightMaxScale = EditorGUILayout.Slider("Obstacle Max", _obstacleWeightMaxScale, _obstacleWeightMinScale, 2.5f);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Coverage Range Randomization", EditorStyles.boldLabel);
            _minCoverageLowerBound = EditorGUILayout.Slider("MinCov Low", _minCoverageLowerBound, 0.35f, 0.9f);
            _minCoverageUpperBound = EditorGUILayout.Slider("MinCov High", _minCoverageUpperBound, _minCoverageLowerBound, 0.95f);
            _maxCoverageLowerBound = EditorGUILayout.Slider("MaxCov Low", _maxCoverageLowerBound, _minCoverageUpperBound, 0.95f);
            _maxCoverageUpperBound = EditorGUILayout.Slider("MaxCov High", _maxCoverageUpperBound, _maxCoverageLowerBound, 0.99f);

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(_generationConfig == null || _tileSet == null))
            {
                if (GUILayout.Button("Run Brutal Fuzz Test", GUILayout.Height(32f)))
                {
                    RunFuzzTest();
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunFuzzTest()
        {
            var builder = new StringBuilder();
            var globalRuns = 0;
            var globalSuccesses = 0;
            var bestSet = string.Empty;
            var bestRatio = -1f;
            var worstSet = string.Empty;
            var worstRatio = 2f;
            var globalFailures = new Dictionary<GenerationFailureReason, int>();

            for (var setIndex = 0; setIndex < _parameterSets; setIndex++)
            {
                var config = CreateConfigVariant(setIndex);
                var tileSet = CreateTileSetVariant(setIndex);
                var pipeline = new WfcGenerationPipeline(config, tileSet);
                var reports = new List<GenerationReport>();

                for (var seedOffset = 0; seedOffset < _seedsPerSet; seedOffset++)
                {
                    pipeline.TryGenerate(_startSeed + (setIndex * 10000) + seedOffset, out _, out var report);
                    reports.Add(report);
                    globalRuns++;
                    if (report.Success)
                    {
                        globalSuccesses++;
                    }
                    else
                    {
                        if (!globalFailures.ContainsKey(report.LastAttemptFailureReason))
                        {
                            globalFailures[report.LastAttemptFailureReason] = 0;
                        }

                        globalFailures[report.LastAttemptFailureReason]++;
                    }
                }

                var ratio = reports.Count(report => report.Success) / (float)reports.Count;
                var descriptor = DescribeVariant(config, tileSet, ratio, reports);
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    bestSet = descriptor;
                }

                if (ratio < worstRatio)
                {
                    worstRatio = ratio;
                    worstSet = descriptor;
                }

                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(tileSet);
            }

            builder.AppendLine($"Brutal fuzz finished: parameterSets={_parameterSets}, seedsPerSet={_seedsPerSet}, totalRuns={globalRuns}, success={globalSuccesses / (float)Mathf.Max(1, globalRuns):P1}");
            builder.AppendLine($"Best: {bestSet}");
            builder.AppendLine($"Worst: {worstSet}");
            builder.AppendLine("Failure histogram:");
            foreach (var pair in globalFailures.OrderByDescending(pair => pair.Value))
            {
                builder.AppendLine($"- {pair.Key}: {pair.Value}");
            }

            _lastReport = builder.ToString();
            Debug.Log(_lastReport);
        }

        private GenerationConfigAsset CreateConfigVariant(int setIndex)
        {
            var random = new System.Random(_startSeed * 31 + setIndex * 97 + 17);
            var clone = CreateInstance<GenerationConfigAsset>();
            EditorUtility.CopySerialized(_generationConfig, clone);
            SetPrivateField(clone, "minGroundCoverage", Mathf.Lerp(_minCoverageLowerBound, _minCoverageUpperBound, (float)random.NextDouble()));
            SetPrivateField(clone, "maxGroundCoverage", Mathf.Lerp(_maxCoverageLowerBound, _maxCoverageUpperBound, (float)random.NextDouble()));
            return clone;
        }

        private SemanticTileSetAsset CreateTileSetVariant(int setIndex)
        {
            var random = new System.Random(_startSeed * 59 + setIndex * 131 + 29);
            var clone = CreateInstance<SemanticTileSetAsset>();
            EditorUtility.CopySerialized(_tileSet, clone);
            var definitions = clone.GetDefinitions();
            foreach (var definition in definitions)
            {
                if (definition.BoundaryOnly)
                {
                    continue;
                }

                if (definition.Archetype == SemanticArchetype.Open || definition.Archetype == SemanticArchetype.InterestAnchor)
                {
                    definition.Weight *= Mathf.Lerp(_openWeightMinScale, _openWeightMaxScale, (float)random.NextDouble());
                }
                else
                {
                    definition.Weight *= Mathf.Lerp(_obstacleWeightMinScale, _obstacleWeightMaxScale, (float)random.NextDouble());
                }
            }

            return clone;
        }

        private static string DescribeVariant(GenerationConfigAsset config, SemanticTileSetAsset tileSet, float ratio, List<GenerationReport> reports)
        {
            var open = tileSet.GetDefinition(SemanticArchetype.Open).Weight;
            var low = tileSet.GetDefinition(SemanticArchetype.LowCover1x1).Weight + tileSet.GetDefinition(SemanticArchetype.LowCover1x2).Weight;
            var high = tileSet.GetDefinition(SemanticArchetype.HighCover1x1).Weight + tileSet.GetDefinition(SemanticArchetype.HighCover1x2).Weight + tileSet.GetDefinition(SemanticArchetype.Block2x2).Weight;
            var avgCoverage = reports.Average(report => report.GroundCoverageRatio);
            var avgSingle = reports.Average(report => report.SingleCellObstacleRatio);
            var avgDegraded = reports.Average(report => report.DegradedFootprintCount);
            return $"success={ratio:P1}, covRange={config.MinGroundCoverage:F2}-{config.MaxGroundCoverage:F2}, open={open:F2}, low={low:F2}, high={high:F2}, avgCoverage={avgCoverage:P1}, avgSingle={avgSingle:P1}, avgDegraded={avgDegraded:F1}";
        }

        private static void SetPrivateField<T>(UnityEngine.Object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
