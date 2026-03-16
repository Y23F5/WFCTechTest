using System.Collections.Generic;
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
            { SemanticArchetype.Open, (10.4f, 8.4f) },
            { SemanticArchetype.InterestAnchor, (0.55f, 0.45f) },
            { SemanticArchetype.LowCover1x1, (0.75f, 1.35f) },
            { SemanticArchetype.LowCover1x2, (0.72f, 1.10f) },
            { SemanticArchetype.HighCover1x1, (0.28f, 0.58f) },
            { SemanticArchetype.HighCover1x2, (0.32f, 0.62f) },
            { SemanticArchetype.Tower1x1, (0.09f, 0.18f) },
            { SemanticArchetype.Block2x2, (0.20f, 0.40f) }
        };

        private GenerationConfigAsset _generationConfig;
        private SemanticTileSetAsset _tileSet;
        private SerializedObject _configObject;
        private SerializedObject _tileSetObject;

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
            if (_generationConfig == null || _tileSet == null)
            {
                EditorGUILayout.HelpBox("Assign both GenerationConfig and SemanticTileSet to enable tuning controls.", MessageType.Info);
                return;
            }

            EnsureSerializedObjects();
            _configObject.Update();
            _tileSetObject.Update();

            DrawCoverageControls();
            EditorGUILayout.Space(8f);
            DrawDensityControls();
            EditorGUILayout.Space(8f);
            DrawPresetButtons();

            _configObject.ApplyModifiedProperties();
            _tileSetObject.ApplyModifiedProperties();
            if (GUI.changed)
            {
                EditorUtility.SetDirty(_generationConfig);
                EditorUtility.SetDirty(_tileSet);
            }
        }

        private void DrawAssetSelection()
        {
            EditorGUI.BeginChangeCheck();
            _generationConfig = (GenerationConfigAsset)EditorGUILayout.ObjectField("Generation Config", _generationConfig, typeof(GenerationConfigAsset), false);
            _tileSet = (SemanticTileSetAsset)EditorGUILayout.ObjectField("Semantic Tile Set", _tileSet, typeof(SemanticTileSetAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                _configObject = null;
                _tileSetObject = null;
            }
        }

        private void DrawCoverageControls()
        {
            EditorGUILayout.LabelField("Coverage", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_configObject.FindProperty("coverageMetric"));
            EditorGUILayout.Slider(_configObject.FindProperty("minGroundCoverage"), 0.35f, 0.9f, new GUIContent("Min Coverage"));
            EditorGUILayout.Slider(_configObject.FindProperty("maxGroundCoverage"), 0.35f, 0.95f, new GUIContent("Max Coverage"));
            EditorGUILayout.Slider(_configObject.FindProperty("minLargestComponentRatio"), 0.7f, 1f, new GUIContent("Min Component"));
        }

        private void DrawDensityControls()
        {
            EditorGUILayout.LabelField("Obstacle Weights", EditorStyles.boldLabel);
            DrawWeightSlider(SemanticArchetype.Open, 4f, 14f, "Open");
            DrawWeightSlider(SemanticArchetype.InterestAnchor, 0f, 1.5f, "Spawn/Respawn");
            EditorGUILayout.Space(4f);
            DrawWeightSlider(SemanticArchetype.LowCover1x1, 0f, 2f, "LowCover 1x1");
            DrawWeightSlider(SemanticArchetype.LowCover1x2, 0f, 2f, "LowCover 1x2");
            DrawWeightSlider(SemanticArchetype.HighCover1x1, 0f, 1.2f, "HighCover 1x1");
            DrawWeightSlider(SemanticArchetype.HighCover1x2, 0f, 1.2f, "HighCover 1x2");
            DrawWeightSlider(SemanticArchetype.Block2x2, 0f, 1f, "Block 2x2");
            DrawWeightSlider(SemanticArchetype.Tower1x1, 0f, 0.5f, "Tower 1x1");
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
                EditorUtility.SetDirty(_tileSet);
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

            var minCoverage = _configObject.FindProperty("minGroundCoverage");
            var maxCoverage = _configObject.FindProperty("maxGroundCoverage");
            minCoverage.floatValue = useSparse ? 0.72f : 0.62f;
            maxCoverage.floatValue = useSparse ? 0.9f : 0.82f;
            EditorUtility.SetDirty(_generationConfig);
            EditorUtility.SetDirty(_tileSet);
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
                _generationConfig = AssetDatabase.LoadAssetAtPath<GenerationConfigAsset>("Assets/GenerationConfig.asset");
            }

            if (_tileSet == null)
            {
                _tileSet = AssetDatabase.LoadAssetAtPath<SemanticTileSetAsset>("Assets/SemanticTileSet.asset");
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
        }
    }
}
