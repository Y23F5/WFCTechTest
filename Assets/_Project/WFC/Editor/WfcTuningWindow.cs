using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;

namespace WFCTechTest.WFC.Editor
{
    /// <summary>
    /// @file WfcTuningWindow.cs
    /// @brief Provides a dedicated editor window for quickly tuning map openness, obstacle density, and coverage thresholds.
    /// </summary>
    public sealed class WfcTuningWindow : EditorWindow
    {
        private static readonly Dictionary<SemanticArchetype, (float sparse, float dense)> PresetWeights = new Dictionary<SemanticArchetype, (float sparse, float dense)>
        {
            { SemanticArchetype.Open, (8.6f, 7.4f) },
            { SemanticArchetype.InterestAnchor, (0.55f, 0.45f) },
            { SemanticArchetype.LowCoverSparse, (1.30f, 0.92f) },
            { SemanticArchetype.LowCoverDense, (0.78f, 1.18f) },
            { SemanticArchetype.HighCoverSparse, (0.92f, 0.66f) },
            { SemanticArchetype.HighCoverDense, (0.54f, 0.92f) },
            { SemanticArchetype.TowerSparse, (0.50f, 0.30f) },
            { SemanticArchetype.TowerDense, (0.20f, 0.38f) },
            { SemanticArchetype.BlockerSparse, (0.58f, 0.38f) },
            { SemanticArchetype.BlockerDense, (0.22f, 0.42f) }
        };

        private GenerationConfigAsset _generationConfig;
        private SemanticTileSetAsset _tileSet;
        private PrefabRegistryAsset _prefabRegistry;
        private SerializedObject _configObject;
        private SerializedObject _tileSetObject;
        private SerializedObject _prefabRegistryObject;

        /// <summary>
        /// Opens the WFC tuning window.
        /// </summary>
        [MenuItem("Window/WFC/Tuning")]
        public static void OpenWindow()
        {
            var window = GetWindow<WfcTuningWindow>("WFC Tuning");
            window.minSize = new Vector2(420f, 520f);
        }

        private void OnEnable()
        {
            TryAutoAssignAssets();
        }

        private void OnGUI()
        {
            DrawAssetSelection();
            if (_generationConfig == null || _tileSet == null || _prefabRegistry == null)
            {
                EditorGUILayout.HelpBox("Assign GenerationConfig, SemanticTileSet, and a Prefab Registry to enable heavy semantic tuning controls.", MessageType.Info);
                return;
            }

            EnsureSerializedObjects();
            _configObject.Update();
            _tileSetObject.Update();
            _prefabRegistryObject.Update();

            DrawCoverageControls();
            EditorGUILayout.Space(8f);
            DrawDensityControls();
            EditorGUILayout.Space(8f);
            DrawSemanticControls();
            EditorGUILayout.Space(8f);
            DrawPrefabRegistryControls();
            EditorGUILayout.Space(8f);
            DrawPresetButtons();

            _configObject.ApplyModifiedProperties();
            _tileSetObject.ApplyModifiedProperties();
            _prefabRegistryObject.ApplyModifiedProperties();
            if (GUI.changed)
            {
                EditorUtility.SetDirty(_generationConfig);
                EditorUtility.SetDirty(_tileSet);
                EditorUtility.SetDirty(_prefabRegistry);
            }
        }

        private void DrawAssetSelection()
        {
            EditorGUI.BeginChangeCheck();
            _generationConfig = (GenerationConfigAsset)EditorGUILayout.ObjectField("Generation Config", _generationConfig, typeof(GenerationConfigAsset), false);
            _tileSet = (SemanticTileSetAsset)EditorGUILayout.ObjectField("Semantic Tile Set", _tileSet, typeof(SemanticTileSetAsset), false);
            _prefabRegistry = (PrefabRegistryAsset)EditorGUILayout.ObjectField("Prefab Registry", _prefabRegistry, typeof(PrefabRegistryAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                _configObject = null;
                _tileSetObject = null;
                _prefabRegistryObject = null;
            }
        }

        private void DrawCoverageControls()
        {
            EditorGUILayout.LabelField("Overall Openness", EditorStyles.boldLabel);
            var targetProperty = _configObject.FindProperty("targetOpenCoverage");
            var toleranceProperty = _configObject.FindProperty("openCoverageTolerance");
            EditorGUILayout.PropertyField(_configObject.FindProperty("coverageMetric"));
            EditorGUILayout.Slider(targetProperty, 0.10f, 0.95f, new GUIContent("Overall Openness"));
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField(new GUIContent("Obstacle Fill"), 1f - targetProperty.floatValue);
            }
            EditorGUILayout.Slider(toleranceProperty, 0.01f, 0.08f, new GUIContent("Tolerance"));
            EditorGUILayout.HelpBox($"Accepted open coverage window: {Mathf.Clamp01(targetProperty.floatValue - toleranceProperty.floatValue):P1} - {Mathf.Clamp01(targetProperty.floatValue + toleranceProperty.floatValue):P1}", MessageType.None);
            if (targetProperty.floatValue <= 0.20f || targetProperty.floatValue >= 0.90f)
            {
                EditorGUILayout.HelpBox("This openness target is in the officially supported extreme zone. Generation will use a larger retry budget and stronger global openness bias.", MessageType.Warning);
            }
            EditorGUILayout.Slider(_configObject.FindProperty("minLargestComponentRatio"), 0.7f, 1f, new GUIContent("Min Component"));
        }

        private void DrawSemanticControls()
        {
            EditorGUILayout.LabelField("Heavy Semantic Weights", EditorStyles.boldLabel);
            DrawWeightSlider(SemanticArchetype.Open, 4f, 14f, "Open");
            DrawWeightSlider(SemanticArchetype.InterestAnchor, 0f, 1.5f, "Spawn/Respawn");
            EditorGUILayout.Space(4f);
            DrawWeightSlider(SemanticArchetype.LowCoverSparse, 0f, 2.5f, "LowCover Sparse");
            DrawWeightSlider(SemanticArchetype.LowCoverDense, 0f, 2.5f, "LowCover Dense");
            DrawWeightSlider(SemanticArchetype.HighCoverSparse, 0f, 2f, "HighCover Sparse");
            DrawWeightSlider(SemanticArchetype.HighCoverDense, 0f, 2f, "HighCover Dense");
            DrawWeightSlider(SemanticArchetype.TowerSparse, 0f, 1.5f, "Tower Sparse");
            DrawWeightSlider(SemanticArchetype.TowerDense, 0f, 1.5f, "Tower Dense");
            DrawWeightSlider(SemanticArchetype.BlockerSparse, 0f, 1.5f, "Blocker Sparse");
            DrawWeightSlider(SemanticArchetype.BlockerDense, 0f, 1.5f, "Blocker Dense");
        }

        private void DrawDensityControls()
        {
            EditorGUILayout.LabelField("Dense Ratio Targets", EditorStyles.boldLabel);
            EditorGUILayout.Slider(_configObject.FindProperty("lowCoverDenseRatio"), 0f, 1f, new GUIContent("LowCover Dense"));
            EditorGUILayout.Slider(_configObject.FindProperty("highCoverDenseRatio"), 0f, 1f, new GUIContent("HighCover Dense"));
            EditorGUILayout.Slider(_configObject.FindProperty("towerDenseRatio"), 0f, 1f, new GUIContent("Tower Dense"));
            EditorGUILayout.Slider(_configObject.FindProperty("blockerDenseRatio"), 0f, 1f, new GUIContent("Blocker Dense"));
        }

        private void DrawPrefabRegistryControls()
        {
            EditorGUILayout.LabelField("Prefab Registry By Semantic Class", EditorStyles.boldLabel);
            DrawPrefabRegistryGroup(ObstacleSemanticClass.LowCover);
            DrawPrefabRegistryGroup(ObstacleSemanticClass.HighCover);
            DrawPrefabRegistryGroup(ObstacleSemanticClass.Tower);
            DrawPrefabRegistryGroup(ObstacleSemanticClass.Blocker);
        }

        private void DrawPresetButtons()
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sparser"))
            {
                ApplyDensityPreset(useSparse: true);
            }

            if (GUILayout.Button("Balanced"))
            {
                _tileSet.ResetToDefaults();
                SetCoverageTarget(0.60f, 0.02f);
                SetDenseRatios(0.42f, 0.48f, 0.35f, 0.55f);
                EditorUtility.SetDirty(_tileSet);
                EditorUtility.SetDirty(_generationConfig);
            }

            if (GUILayout.Button("Denser"))
            {
                ApplyDensityPreset(useSparse: false);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWeightSlider(SemanticArchetype archetype, float min, float max, string label)
        {
            var property = FindDefinitionProperty(archetype);
            if (property == null)
            {
                return;
            }

            var weightProperty = property.FindPropertyRelative("Weight");
            weightProperty.floatValue = EditorGUILayout.Slider(label, weightProperty.floatValue, min, max);
        }

        private void ApplyDensityPreset(bool useSparse)
        {
            foreach (var pair in PresetWeights)
            {
                var property = FindDefinitionProperty(pair.Key);
                if (property == null)
                {
                    continue;
                }

                property.FindPropertyRelative("Weight").floatValue = useSparse ? pair.Value.sparse : pair.Value.dense;
            }

            SetCoverageTarget(useSparse ? 0.82f : 0.28f, 0.02f);
            SetDenseRatios(useSparse ? 0.20f : 0.72f, useSparse ? 0.26f : 0.78f, useSparse ? 0.16f : 0.66f, useSparse ? 0.28f : 0.82f);
            EditorUtility.SetDirty(_generationConfig);
            EditorUtility.SetDirty(_tileSet);
        }

        private void SetCoverageTarget(float openness, float tolerance)
        {
            _configObject.FindProperty("targetOpenCoverage").floatValue = openness;
            _configObject.FindProperty("openCoverageTolerance").floatValue = tolerance;
        }

        private void SetDenseRatios(float lowCover, float highCover, float tower, float blocker)
        {
            _configObject.FindProperty("lowCoverDenseRatio").floatValue = lowCover;
            _configObject.FindProperty("highCoverDenseRatio").floatValue = highCover;
            _configObject.FindProperty("towerDenseRatio").floatValue = tower;
            _configObject.FindProperty("blockerDenseRatio").floatValue = blocker;
        }

        private void DrawPrefabRegistryGroup(ObstacleSemanticClass semanticClass)
        {
            EditorGUILayout.LabelField(semanticClass.ToString(), EditorStyles.miniBoldLabel);
            var entries = _prefabRegistryObject.FindProperty("entries");
            var found = false;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var semanticProperty = entry.FindPropertyRelative("SemanticClass");
                if (semanticProperty.enumValueIndex != (int)semanticClass)
                {
                    continue;
                }

                found = true;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("DisplayName"), new GUIContent("Name"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Type"), new GUIContent("Type"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Weight"), new GUIContent("Weight"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("SparseWeight"), new GUIContent("Sparse Weight"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("DenseWeight"), new GUIContent("Dense Weight"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("EnabledForAutoGeneration"), new GUIContent("Auto"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("CanAppearNearBoundary"), new GUIContent("Near Boundary"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("CanAppearInCenter"), new GUIContent("In Center"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("RequiresClearance"), new GUIContent("Requires Clearance"));
                if (entry.FindPropertyRelative("RequiresClearance").boolValue)
                {
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("ClearanceRadius"), new GUIContent("Clearance Radius"));
                }
                EditorGUILayout.EndVertical();
            }

            if (!found)
            {
                EditorGUILayout.HelpBox($"No Prefab Registry entries are assigned to {semanticClass}.", MessageType.Info);
            }
        }

        private SerializedProperty FindDefinitionProperty(SemanticArchetype archetype)
        {
            var definitions = _tileSetObject.FindProperty("definitions");
            for (var i = 0; i < definitions.arraySize; i++)
            {
                var entry = definitions.GetArrayElementAtIndex(i);
                var archetypeProperty = entry.FindPropertyRelative("Archetype");
                if (archetypeProperty.enumValueIndex == (int)archetype)
                {
                    return entry;
                }
            }

            return null;
        }

        private void TryAutoAssignAssets()
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

        private void EnsureSerializedObjects()
        {
            if (_configObject == null)
            {
                _configObject = new SerializedObject(_generationConfig);
            }

            if (_tileSetObject == null)
            {
                _tileSetObject = new SerializedObject(_tileSet);
            }

            if (_prefabRegistryObject == null)
            {
                _prefabRegistryObject = new SerializedObject(_prefabRegistry);
            }
        }
    }
}
