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
    public sealed partial class WfcFuzzTestWindow : EditorWindow
    {
        private sealed class FuzzRunState
        {
            public int SetIndex;
            public int SeedOffset;
            public int GlobalRuns;
            public int GlobalSuccesses;
            public string BestSet = string.Empty;
            public float BestRatio = -1f;
            public string WorstSet = string.Empty;
            public float WorstRatio = 2f;
            public readonly Dictionary<GenerationFailureReason, int> GlobalFailures = new Dictionary<GenerationFailureReason, int>();
            public readonly List<GenerationReport> CurrentReports = new List<GenerationReport>();
            public GenerationConfigAsset CurrentConfig;
            public SemanticTileSetAsset CurrentTileSet;
            public PrefabRegistryAsset CurrentPrefabRegistry;
            public WfcGenerationPipeline CurrentPipeline;
        }

        private GenerationConfigAsset _generationConfig;
        private SemanticTileSetAsset _tileSet;
        private PrefabRegistryAsset _prefabRegistry;
        private int _parameterSets = 20;
        private int _seedsPerSet = 50;
        private int _startSeed = 1000;
        private float _openWeightMinScale = 0.9f;
        private float _openWeightMaxScale = 1.2f;
        private float _obstacleWeightMinScale = 0.6f;
        private float _obstacleWeightMaxScale = 1.2f;
        private float _denseRatioMin = 0.15f;
        private float _denseRatioMax = 0.85f;
        private float _targetOpenCoverageMin = 0.20f;
        private float _targetOpenCoverageMax = 0.90f;
        private float _openToleranceMin = 0.02f;
        private float _openToleranceMax = 0.05f;
        private Vector2 _scroll;
        private string _lastReport = string.Empty;
        private bool _isRunning;
        private FuzzRunState _runState;
        private const int RunsPerEditorTick = 10;

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
                _generationConfig = WfcEditorAssetLocator.LoadDefaultGenerationConfig();
            }

            if (_tileSet == null)
            {
                _tileSet = WfcEditorAssetLocator.LoadDefaultSemanticTileSet();
            }

            if (_prefabRegistry == null)
            {
                _prefabRegistry = WfcEditorAssetLocator.LoadDefaultPrefabRegistry();
            }
        }

        private void OnDisable()
        {
            StopFuzzTest("Fuzz test stopped because the window was closed.");
        }

        private void OnGUI()
        {
            _generationConfig = (GenerationConfigAsset)EditorGUILayout.ObjectField("Generation Config", _generationConfig, typeof(GenerationConfigAsset), false);
            _tileSet = (SemanticTileSetAsset)EditorGUILayout.ObjectField("Semantic Tile Set", _tileSet, typeof(SemanticTileSetAsset), false);
            _prefabRegistry = (PrefabRegistryAsset)EditorGUILayout.ObjectField("Prefab Registry", _prefabRegistry, typeof(PrefabRegistryAsset), false);

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

            EditorGUILayout.LabelField("Dense Ratio Randomization", EditorStyles.boldLabel);
            _denseRatioMin = EditorGUILayout.Slider("Dense Min", _denseRatioMin, 0f, 1f);
            _denseRatioMax = EditorGUILayout.Slider("Dense Max", _denseRatioMax, _denseRatioMin, 1f);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Overall Openness Randomization", EditorStyles.boldLabel);
            _targetOpenCoverageMin = EditorGUILayout.Slider("Open Min", _targetOpenCoverageMin, 0.10f, 0.90f);
            _targetOpenCoverageMax = EditorGUILayout.Slider("Open Max", _targetOpenCoverageMax, _targetOpenCoverageMin, 0.95f);
            _openToleranceMin = EditorGUILayout.Slider("Tolerance Min", _openToleranceMin, 0.01f, 0.05f);
            _openToleranceMax = EditorGUILayout.Slider("Tolerance Max", _openToleranceMax, _openToleranceMin, 0.08f);

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(_generationConfig == null || _tileSet == null || _prefabRegistry == null || _isRunning))
            {
                if (GUILayout.Button("Run Brutal Fuzz Test", GUILayout.Height(32f)))
                {
                    StartFuzzTest();
                }
            }

            using (new EditorGUI.DisabledScope(!_isRunning))
            {
                if (GUILayout.Button("Stop", GUILayout.Height(24f)))
                {
                    StopFuzzTest("Fuzz test stopped by user.");
                }
            }

            if (_isRunning && _runState != null)
            {
                var totalRuns = _parameterSets * _seedsPerSet;
                var progress = _runState.GlobalRuns / (float)Mathf.Max(1, totalRuns);
                EditorGUILayout.HelpBox($"Running... set {_runState.SetIndex + 1}/{_parameterSets}, seed {_runState.SeedOffset + 1}/{_seedsPerSet}, total {_runState.GlobalRuns}/{totalRuns} ({progress:P1})", MessageType.Info);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void StartFuzzTest()
        {
            StopFuzzTest(string.Empty, false);
            _runState = new FuzzRunState();
            _isRunning = true;
            _lastReport = "Fuzz test started...";
            EditorApplication.update += UpdateFuzzTest;
        }

        private void UpdateFuzzTest()
        {
            if (!_isRunning || _runState == null)
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            var totalRuns = _parameterSets * _seedsPerSet;
            var progress = _runState.GlobalRuns / (float)Mathf.Max(1, totalRuns);
            if (EditorUtility.DisplayCancelableProgressBar(
                    "WFC Fuzz Test",
                    $"Set {_runState.SetIndex + 1}/{_parameterSets}, seed {_runState.SeedOffset + 1}/{_seedsPerSet}, total {_runState.GlobalRuns}/{totalRuns}",
                    progress))
            {
                StopFuzzTest("Fuzz test cancelled.");
                return;
            }

            for (var i = 0; i < RunsPerEditorTick; i++)
            {
                if (_runState.SetIndex >= _parameterSets)
                {
                    FinishFuzzTest();
                    return;
                }

                EnsureCurrentVariant();
                _runState.CurrentPipeline.TryGenerate(_startSeed + (_runState.SetIndex * 10000) + _runState.SeedOffset, out _, out var report);
                _runState.CurrentReports.Add(report);
                _runState.GlobalRuns++;
                if (report.Success)
                {
                    _runState.GlobalSuccesses++;
                }
                else
                {
                    if (!_runState.GlobalFailures.ContainsKey(report.LastAttemptFailureReason))
                    {
                        _runState.GlobalFailures[report.LastAttemptFailureReason] = 0;
                    }

                    _runState.GlobalFailures[report.LastAttemptFailureReason]++;
                }

                _runState.SeedOffset++;
                if (_runState.SeedOffset < _seedsPerSet)
                {
                    continue;
                }

                FinalizeCurrentVariant();
                _runState.SetIndex++;
                _runState.SeedOffset = 0;
            }

            Repaint();
        }

        private void EnsureCurrentVariant()
        {
            if (_runState.CurrentPipeline != null)
            {
                return;
            }

            _runState.CurrentConfig = CreateConfigVariant(_runState.SetIndex);
            _runState.CurrentTileSet = CreateTileSetVariant(_runState.SetIndex);
            _runState.CurrentPrefabRegistry = CreatePrefabRegistryVariant(_runState.SetIndex);
            _runState.CurrentPipeline = new WfcGenerationPipeline(_runState.CurrentConfig, _runState.CurrentTileSet, _runState.CurrentPrefabRegistry);
            _runState.CurrentReports.Clear();
        }

        private void FinalizeCurrentVariant()
        {
            var ratio = _runState.CurrentReports.Count(report => report.Success) / (float)_runState.CurrentReports.Count;
            var descriptor = DescribeVariant(_runState.CurrentConfig, _runState.CurrentTileSet, _runState.CurrentPrefabRegistry, ratio, _runState.CurrentReports);
            if (ratio > _runState.BestRatio)
            {
                _runState.BestRatio = ratio;
                _runState.BestSet = descriptor;
            }

            if (ratio < _runState.WorstRatio)
            {
                _runState.WorstRatio = ratio;
                _runState.WorstSet = descriptor;
            }

            DestroyVariant(_runState.CurrentConfig, _runState.CurrentTileSet, _runState.CurrentPrefabRegistry);
            _runState.CurrentConfig = null;
            _runState.CurrentTileSet = null;
            _runState.CurrentPrefabRegistry = null;
            _runState.CurrentPipeline = null;
            _runState.CurrentReports.Clear();
        }

        private void FinishFuzzTest()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Brutal fuzz finished: parameterSets={_parameterSets}, seedsPerSet={_seedsPerSet}, totalRuns={_runState.GlobalRuns}, success={_runState.GlobalSuccesses / (float)Mathf.Max(1, _runState.GlobalRuns):P1}");
            builder.AppendLine($"Best: {_runState.BestSet}");
            builder.AppendLine($"Worst: {_runState.WorstSet}");
            builder.AppendLine("Failure histogram:");
            foreach (var pair in _runState.GlobalFailures.OrderByDescending(pair => pair.Value))
            {
                builder.AppendLine($"- {pair.Key}: {pair.Value}");
            }

            _lastReport = builder.ToString();
            Debug.Log(_lastReport);
            StopFuzzTest(string.Empty);
        }

        private void StopFuzzTest(string reason, bool updateReport = true)
        {
            if (_isRunning)
            {
                EditorApplication.update -= UpdateFuzzTest;
            }

            EditorUtility.ClearProgressBar();
            _isRunning = false;

            if (_runState != null)
            {
                DestroyVariant(_runState.CurrentConfig, _runState.CurrentTileSet, _runState.CurrentPrefabRegistry);
                _runState = null;
            }

            if (updateReport && !string.IsNullOrEmpty(reason))
            {
                _lastReport = string.IsNullOrEmpty(_lastReport) ? reason : $"{reason}\n\n{_lastReport}";
                Debug.Log(reason);
            }

            Repaint();
        }

    }
}
