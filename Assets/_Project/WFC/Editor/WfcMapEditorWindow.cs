using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Unity.Runtime;

namespace WFCTechTest.WFC.Editor
{
    /// <summary>
    /// @file WfcMapEditorWindow.cs
    /// @brief Provides editor workflows for obstacle registration, snapping, validation, and JSON export.
    /// </summary>
    public sealed partial class WfcMapEditorWindow : EditorWindow
    {
        private const int UnregisteredFallbackType = 65535;

        private sealed class ImportParseDiagnostics
        {
            public bool UsedFallback;
            public readonly List<string> Messages = new List<string>();
        }

        private WfcGenerationRunner _generationRunner;
        private PrefabRegistryAsset _prefabRegistry;
        private GameObject _placeholderCubePrefab;
        private SerializedObject _prefabRegistryObject;
        private SerializedObject _configObject;
        private Vector2 _scroll;
        private string _status = string.Empty;
        private int _registryIndexToRemove = -1;

        /// <summary>
        /// Opens the map editor window.
        /// </summary>
        [MenuItem("Window/WFC/Map Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<WfcMapEditorWindow>("WFC Map Editor");
            window.minSize = new Vector2(520f, 620f);
        }

        private void OnEnable()
        {
            if (_prefabRegistry == null)
            {
                _prefabRegistry = WfcEditorAssetLocator.LoadDefaultPrefabRegistry();
            }

            if (_placeholderCubePrefab == null)
            {
                _placeholderCubePrefab = WfcEditorAssetLocator.LoadDefaultCubePrefab();
            }

            if (_generationRunner == null)
            {
                _generationRunner = FindFirstObjectByType<WfcGenerationRunner>();
            }
        }

        private void OnGUI()
        {
            EnsureSerializedState();
            _prefabRegistryObject?.Update();
            _configObject?.Update();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawReferences();
            EditorGUILayout.Space(8f);
            DrawConfigSection();
            EditorGUILayout.Space(8f);
            DrawSetupSection();
            EditorGUILayout.Space(8f);
            DrawPrefabRegistrySection();
            EditorGUILayout.Space(8f);
            DrawGenerationSection();
            EditorGUILayout.Space(8f);
            DrawSelectionSection();
            EditorGUILayout.Space(8f);
            DrawExportSection();
            EditorGUILayout.Space(8f);
            DrawStatus();

            EditorGUILayout.EndScrollView();

            _prefabRegistryObject?.ApplyModifiedProperties();
            _configObject?.ApplyModifiedProperties();
        }

        private void DrawReferences()
        {
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            _generationRunner = (WfcGenerationRunner)EditorGUILayout.ObjectField("Generation Runner", _generationRunner, typeof(WfcGenerationRunner), true);
            _prefabRegistry = (PrefabRegistryAsset)EditorGUILayout.ObjectField("Prefab Registry", _prefabRegistry, typeof(PrefabRegistryAsset), false);
            _placeholderCubePrefab = (GameObject)EditorGUILayout.ObjectField("Placeholder Cube", _placeholderCubePrefab, typeof(GameObject), false);
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ensure Roots"))
                {
                    EnsureRoots();
                }

                if (GUILayout.Button("Seed Default Registry Entries"))
                {
                    EnsurePrefabRegistryPlaceholders();
                }

                if (GUILayout.Button("Add New Prefab Entry"))
                {
                    AddPrefabRegistryEntry();
                }
            }

            if (_prefabRegistry != null)
            {
                EditorGUILayout.HelpBox($"Placement cell edge: {_prefabRegistry.GetPlacementCellEdge():0.###} (cubic cell {FormatVector(_prefabRegistry.GetPlacementCellSize())})", MessageType.Info);
            }

            var config = GetGenerationConfig();
            if (config != null)
            {
                EditorGUILayout.HelpBox($"Map center: {FormatVector(config.MapCenter)}", MessageType.None);
            }
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("Map Config", EditorStyles.boldLabel);
            if (_configObject == null)
            {
                EditorGUILayout.HelpBox("Assign a generation runner with a GenerationConfig asset to edit centered map settings here.", MessageType.Info);
                return;
            }

            EditorGUILayout.PropertyField(_configObject.FindProperty("mapCenter"));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_configObject.FindProperty("width"));
                EditorGUILayout.PropertyField(_configObject.FindProperty("depth"));
                EditorGUILayout.PropertyField(_configObject.FindProperty("height"));
            }

            EditorGUILayout.PropertyField(_configObject.FindProperty("boundaryWallHeight"));
        }

        private void DrawPrefabRegistrySection()
        {
            EditorGUILayout.LabelField("Prefab Registry", EditorStyles.boldLabel);
            if (_prefabRegistryObject == null)
            {
                EditorGUILayout.HelpBox("Assign a Prefab Registry asset to add prefabs and configure placement rules here.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("在这里维护你的 Prefab Registry。你可以增删任意数量的 prefab 条目，并为每个条目配置语义类、权重、边界/中心限制和 clearance。当前版本所有障碍逻辑占地固定为 1x1，placement cell 使用 registry 内全部 prefab 的最大合并包围盒。", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Registry"))
                {
                    ValidatePrefabRegistry();
                }

                if (GUILayout.Button("Save Registry"))
                {
                    _prefabRegistryObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_prefabRegistry);
                    AssetDatabase.SaveAssets();
                    SetStatus("Saved Prefab Registry.");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Remove Index", GUILayout.Width(85f));
                _registryIndexToRemove = EditorGUILayout.IntField(_registryIndexToRemove);
                using (new EditorGUI.DisabledScope(_registryIndexToRemove < 0))
                {
                    if (GUILayout.Button("Remove Prefab Entry", GUILayout.Width(160f)))
                    {
                        RemovePrefabRegistryEntry(_registryIndexToRemove);
                    }
                }
            }

            DrawRegistryEntries();
        }

        private void DrawGenerationSection()
        {
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_generationRunner == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate Obstacles"))
                    {
                        EnsureRoots();
                        _generationRunner.Generate();
                        SetStatus("Generated obstacle layout.");
                    }

                    if (GUILayout.Button("Clear Obstacles"))
                    {
                        _generationRunner.ClearObstacles();
                        SetStatus("Cleared generated obstacles.");
                    }

                    if (GUILayout.Button("Rebuild Boundaries"))
                    {
                        _generationRunner.RebuildBoundaries();
                        SetStatus("Rebuilt boundaries.");
                    }
                }
            }
        }

        private void DrawSelectionSection()
        {
            EditorGUILayout.LabelField("Selection Tools", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_generationRunner == null || _prefabRegistry == null))
            {
                if (GUILayout.Button("Register Selection As Obstacles"))
                {
                    ShowTypePicker(RegisterSelection);
                }

                if (GUILayout.Button("Assign Index To Selection"))
                {
                    ShowTypePicker(AssignTypeToSelection);
                }

                if (GUILayout.Button("Snap Selection To Grid"))
                {
                    SnapSelectionToGrid();
                }

                if (GUILayout.Button("Validate Scene Obstacles"))
                {
                    ValidateSceneObstacles();
                }
            }
        }

        private void DrawExportSection()
        {
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_generationRunner == null))
            {
                if (GUILayout.Button("Import JSON"))
                {
                    ImportJson();
                }

                if (GUILayout.Button("Export JSON"))
                {
                    ExportJson();
                }
            }
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(_status) ? "Ready." : _status, MessageType.None);
        }

        private void EnsureRoots()
        {
            var spawner = GetSpawner();
            if (spawner == null)
            {
                SetStatus("Cannot ensure roots because the generation runner has no scene spawner.");
                return;
            }

            spawner.SendMessage("EnsureRoots", SendMessageOptions.DontRequireReceiver);
            if (_prefabRegistry != null)
            {
                spawner.SetPrefabRegistry(_prefabRegistry);
                AssignPrefabRegistryToRunner();
            }

            EditorUtility.SetDirty(spawner);
            SetStatus("Ensured map roots on the scene spawner.");
        }

        private void EnsurePrefabRegistryPlaceholders()
        {
            if (_prefabRegistry == null)
            {
                SetStatus("Assign a Prefab Registry first.");
                return;
            }

            _prefabRegistry.EnsureDefaultPlaceholders(_placeholderCubePrefab != null ? _placeholderCubePrefab : WfcEditorAssetLocator.LoadDefaultCubePrefab());
            EditorUtility.SetDirty(_prefabRegistry);
            AssetDatabase.SaveAssets();
            SetStatus("Seeded default Prefab Registry entries.");
        }

        private void AddPrefabRegistryEntry()
        {
            if (_prefabRegistry == null)
            {
                SetStatus("Assign a Prefab Registry first.");
                return;
            }

            var entry = _prefabRegistry.AddEntry(_placeholderCubePrefab != null ? _placeholderCubePrefab : WfcEditorAssetLocator.LoadDefaultCubePrefab());
            EditorUtility.SetDirty(_prefabRegistry);
            AssetDatabase.SaveAssets();
            SetStatus($"Added Prefab Registry entry {entry.Type}.");
        }

        private void RemovePrefabRegistryEntry(int type)
        {
            if (_prefabRegistry == null)
            {
                SetStatus("Assign a Prefab Registry first.");
                return;
            }

            if (!_prefabRegistry.RemoveEntry(type))
            {
                SetStatus($"Prefab Registry entry {type} was not found.");
                return;
            }

            EditorUtility.SetDirty(_prefabRegistry);
            AssetDatabase.SaveAssets();
            _registryIndexToRemove = -1;
            SetStatus($"Removed Prefab Registry entry {type}.");
        }

        private void ValidatePrefabRegistry()
        {
            if (_prefabRegistry == null)
            {
                SetStatus("Assign a Prefab Registry first.");
                return;
            }

            var warnings = new List<string>();
            if (_prefabRegistry.Entries.Count == 0)
            {
                warnings.Add("Prefab Registry 为空。请至少添加一个 prefab 条目。");
            }

            var duplicateTypes = _prefabRegistry.Entries.GroupBy(entry => entry.Type).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            foreach (var type in duplicateTypes)
            {
                warnings.Add($"检测到重复 Index：{type}");
            }

            foreach (var entry in _prefabRegistry.Entries)
            {
                if (entry.Prefab == null)
                {
                    warnings.Add($"Index {entry.Type}：没有绑定 Prefab。");
                }

                if (entry.SemanticClass == ObstacleSemanticClass.None)
                {
                    warnings.Add($"Index {entry.Type}：缺少语义类，必须指定为 LowCover / HighCover / Tower / Blocker。");
                }

                if (entry.RequiresClearance && entry.ClearanceRadius <= 0)
                {
                    warnings.Add($"Index {entry.Type}：开启了 Clearance，但 ClearanceRadius 必须大于 0。");
                }

                if (entry.FootprintWidth != 1 || entry.FootprintDepth != 1)
                {
                    warnings.Add($"Index {entry.Type}：当前系统要求所有障碍逻辑占地固定为 1x1。");
                }
            }

            foreach (var semanticClass in new[]
                     {
                         ObstacleSemanticClass.LowCover,
                         ObstacleSemanticClass.HighCover,
                         ObstacleSemanticClass.Tower,
                         ObstacleSemanticClass.Blocker
                     })
            {
                if (!_prefabRegistry.Entries.Any(entry => entry != null && entry.SemanticClass == semanticClass))
                {
                    warnings.Add($"缺少语义类 {semanticClass} 的 Prefab Registry 条目。");
                }
            }

            SetStatus(warnings.Count == 0 ? "Prefab Registry 校验通过。" : string.Join("\n", warnings));
        }

        private void DrawRegistryEntries()
        {
            var entries = _prefabRegistryObject.FindProperty("entries");
            if (entries == null)
            {
                return;
            }

            var requiresNormalize = false;

            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var indexProperty = entry.FindPropertyRelative("Type");
                var nameProperty = entry.FindPropertyRelative("DisplayName");
                var prefabProperty = entry.FindPropertyRelative("Prefab");
                var semanticProperty = entry.FindPropertyRelative("SemanticClass");
                var weightProperty = entry.FindPropertyRelative("Weight");
                var sparseWeightProperty = entry.FindPropertyRelative("SparseWeight");
                var denseWeightProperty = entry.FindPropertyRelative("DenseWeight");
                var autoProperty = entry.FindPropertyRelative("EnabledForAutoGeneration");
                var logicalHeightProperty = entry.FindPropertyRelative("LogicalHeightCells");
                var logicalHeightLockedProperty = entry.FindPropertyRelative("LogicalHeightLocked");
                var defaultPosYProperty = entry.FindPropertyRelative("DefaultPosY");
                var defaultPosYLockedProperty = entry.FindPropertyRelative("DefaultPosYLocked");
                var boundaryProperty = entry.FindPropertyRelative("CanAppearNearBoundary");
                var centerProperty = entry.FindPropertyRelative("CanAppearInCenter");
                var clearanceProperty = entry.FindPropertyRelative("RequiresClearance");
                var clearanceRadiusProperty = entry.FindPropertyRelative("ClearanceRadius");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Entry {i + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Reset Defaults", GUILayout.Width(110f)))
                {
                    _prefabRegistryObject.ApplyModifiedProperties();
                    _prefabRegistry.ApplyDefaultsAt(i);
                    EditorUtility.SetDirty(_prefabRegistry);
                    _prefabRegistryObject = new SerializedObject(_prefabRegistry);
                    _prefabRegistryObject.Update();
                    break;
                }
                if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                {
                    entries.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                var newIndex = EditorGUILayout.IntField(new GUIContent("Index"), indexProperty.intValue);
                if (EditorGUI.EndChangeCheck())
                {
                    indexProperty.intValue = Mathf.Max(0, newIndex);
                    requiresNormalize = true;
                }

                EditorGUILayout.PropertyField(nameProperty, new GUIContent("Name"));
                EditorGUILayout.PropertyField(prefabProperty, new GUIContent("Prefab"));
                EditorGUILayout.PropertyField(semanticProperty, new GUIContent("Semantic Class"));
                EditorGUILayout.PropertyField(weightProperty, new GUIContent("Weight"));
                EditorGUILayout.PropertyField(autoProperty, new GUIContent("Auto Generate"));
                EditorGUILayout.LabelField("Density Distribution", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(sparseWeightProperty, new GUIContent("Sparse Weight"));
                EditorGUILayout.PropertyField(denseWeightProperty, new GUIContent("Dense Weight"));
                EditorGUILayout.LabelField("Logical Shape", EditorStyles.miniBoldLabel);
                EditorGUI.BeginChangeCheck();
                var logicalHeight = EditorGUILayout.IntField(new GUIContent("Logical Height"), logicalHeightProperty.intValue);
                if (EditorGUI.EndChangeCheck())
                {
                    logicalHeightProperty.intValue = Mathf.Max(1, logicalHeight);
                    logicalHeightLockedProperty.boolValue = true;
                }

                EditorGUI.BeginChangeCheck();
                var defaultPosY = EditorGUILayout.FloatField(new GUIContent("Default Pos Y"), defaultPosYProperty.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    defaultPosYProperty.floatValue = defaultPosY;
                    defaultPosYLockedProperty.boolValue = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(logicalHeightLockedProperty.boolValue ? "Height: Manual" : "Height: Auto", EditorStyles.miniLabel);
                    if (GUILayout.Button("Recalc Height", GUILayout.Width(100f)))
                    {
                        _prefabRegistryObject.ApplyModifiedProperties();
                        _prefabRegistry.RecalculateLogicalHeightAt(i);
                        EditorUtility.SetDirty(_prefabRegistry);
                        _prefabRegistryObject = new SerializedObject(_prefabRegistry);
                        _prefabRegistryObject.Update();
                        break;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(defaultPosYLockedProperty.boolValue ? "Default Pos Y: Manual" : "Default Pos Y: Auto", EditorStyles.miniLabel);
                    if (GUILayout.Button("Recalc Y", GUILayout.Width(100f)))
                    {
                        _prefabRegistryObject.ApplyModifiedProperties();
                        _prefabRegistry.RecalculateDefaultPosYAt(i);
                        EditorUtility.SetDirty(_prefabRegistry);
                        _prefabRegistryObject = new SerializedObject(_prefabRegistry);
                        _prefabRegistryObject.Update();
                        break;
                    }
                }

                EditorGUILayout.LabelField("Placement Rules", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(boundaryProperty, new GUIContent("Can Appear Near Boundary"));
                EditorGUILayout.PropertyField(centerProperty, new GUIContent("Can Appear In Center"));
                EditorGUILayout.PropertyField(clearanceProperty, new GUIContent("Requires Clearance"));
                if (clearanceProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(clearanceRadiusProperty, new GUIContent("Clearance Radius"));
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
            }

            if (requiresNormalize)
            {
                _prefabRegistryObject.ApplyModifiedProperties();
                _prefabRegistry.NormalizeEntries();
                EditorUtility.SetDirty(_prefabRegistry);
                _prefabRegistryObject = new SerializedObject(_prefabRegistry);
                _prefabRegistryObject.Update();
            }
        }

        private void RegisterSelection(int type)
        {
            var obstacleRoot = GetObstacleRoot();
            if (obstacleRoot == null)
            {
                SetStatus("Cannot register selection because obstacleRoot could not be found.");
                return;
            }

            var entry = _prefabRegistry?.GetEntry(type);
            foreach (var gameObject in Selection.gameObjects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                Undo.SetTransformParent(gameObject.transform, obstacleRoot, "Register Obstacles");
                var metadata = gameObject.GetComponent<ObstacleInstanceMetadata>();
                if (metadata == null)
                {
                    metadata = Undo.AddComponent<ObstacleInstanceMetadata>(gameObject);
                }

                Undo.RecordObject(metadata, "Register Obstacles");
                metadata.Type = type;
                metadata.DisplayName = entry?.DisplayName ?? $"Index {type}";
                metadata.AllowRandomYaw = entry?.AllowRandomYaw ?? true;
                metadata.SemanticClass = entry?.SemanticClass ?? ObstacleSemanticClass.None;
                metadata.SourceArchetype = ResolveRepresentativeArchetype(metadata.SemanticClass);
                metadata.IsUnknownType = false;
                metadata.Registered = true;
                metadata.IsGenerated = false;
                EditorUtility.SetDirty(metadata);
                AlignTransformUsingRegistry(gameObject.transform, entry);
            }

            SetStatus($"Registered {Selection.gameObjects.Length} selected object(s) as obstacle index {type}.");
        }

        private void AssignTypeToSelection(int type)
        {
            var entry = _prefabRegistry?.GetEntry(type);
            var changed = 0;
            foreach (var gameObject in Selection.gameObjects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                var metadata = gameObject.GetComponent<ObstacleInstanceMetadata>();
                if (metadata == null)
                {
                    continue;
                }

                Undo.RecordObject(metadata, "Assign Obstacle Index");
                metadata.Type = type;
                metadata.DisplayName = entry?.DisplayName ?? $"Index {type}";
                metadata.AllowRandomYaw = entry?.AllowRandomYaw ?? true;
                metadata.SemanticClass = entry?.SemanticClass ?? ObstacleSemanticClass.None;
                metadata.SourceArchetype = ResolveRepresentativeArchetype(metadata.SemanticClass);
                metadata.IsUnknownType = false;
                metadata.Registered = true;
                EditorUtility.SetDirty(metadata);
                AlignTransformUsingRegistry(gameObject.transform, entry);
                changed++;
            }

            SetStatus($"Assigned index {type} to {changed} registered obstacle(s).");
        }

        private void SnapSelectionToGrid()
        {
            if (_prefabRegistry == null)
            {
                SetStatus("Assign a Prefab Registry first.");
                return;
            }

            var cell = _prefabRegistry.GetPlacementCellSize();
            var mapCenter = GetGenerationConfig() != null ? GetGenerationConfig().MapCenter : Vector3.zero;
            foreach (var transform in Selection.transforms)
            {
                var metadata = transform.GetComponent<ObstacleInstanceMetadata>();
                var entry = metadata != null ? _prefabRegistry.GetEntry(metadata.Type) : null;
                Undo.RecordObject(transform, "Snap Obstacles To Grid");
                var position = transform.position;
                transform.position = new Vector3(
                    SnapAxis(position.x, cell.x, mapCenter.x),
                    position.y,
                    SnapAxis(position.z, cell.z, mapCenter.z));
                var yaw = transform.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f, 0f);
                AlignTransformUsingRegistry(transform, entry);
            }

            SetStatus($"Snapped {Selection.transforms.Length} selected transform(s) to the placement grid.");
        }

        private void ValidateSceneObstacles()
        {
            var obstacleRoot = GetObstacleRoot();
            if (obstacleRoot == null)
            {
                SetStatus("Cannot validate scene obstacles because obstacleRoot could not be found.");
                return;
            }

            var groupedWarnings = new Dictionary<string, List<string>>();
            var config = GetGenerationConfig();
            var mapCenter = config != null ? config.MapCenter : Vector3.zero;
            var cell = _prefabRegistry != null ? _prefabRegistry.GetPlacementCellSize() : Vector3.one;
            var halfWidth = config != null ? ((config.Width - 1) * 0.5f) * cell.x : 0f;
            var halfDepth = config != null ? ((config.Depth - 1) * 0.5f) * cell.z : 0f;
            foreach (Transform child in obstacleRoot)
            {
                var metadata = child.GetComponent<ObstacleInstanceMetadata>();
                if (metadata == null)
                {
                    AddGroupedWarning(groupedWarnings, "Registration", $"{child.name}: missing ObstacleInstanceMetadata");
                    continue;
                }

                if (!metadata.Registered)
                {
                    AddGroupedWarning(groupedWarnings, "Registration", $"{child.name}: metadata exists but object is not registered");
                }

                var entry = _prefabRegistry != null && metadata.Type != UnregisteredFallbackType ? _prefabRegistry.GetEntry(metadata.Type) : null;
                if (metadata.IsUnknownType)
                {
                    AddGroupedWarning(groupedWarnings, "Unknown Imported Index", $"{child.name}: imported unknown index {metadata.Type}; placeholder is being used");
                }

                if (_prefabRegistry != null && metadata.Type != UnregisteredFallbackType && entry == null && !metadata.IsUnknownType)
                {
                    AddGroupedWarning(groupedWarnings, "Registry Missing", $"{child.name}: index {metadata.Type} is not present in the Prefab Registry");
                }

                if (entry != null && !entry.UsePlaceholder && entry.Prefab == null)
                {
                    AddGroupedWarning(groupedWarnings, "Registry Missing", $"{child.name}: index {metadata.Type} has no prefab assigned in the Prefab Registry");
                }

                if (entry != null && metadata.SemanticClass != ObstacleSemanticClass.None && entry.SemanticClass != metadata.SemanticClass)
                {
                    AddGroupedWarning(groupedWarnings, "Semantic Mismatch", $"{child.name}: metadata semantic class {metadata.SemanticClass} does not match Prefab Registry semantic class {entry.SemanticClass}");
                }

                if (metadata.SemanticClass != ObstacleSemanticClass.None && !HasUsablePrefabRegistryEntries(metadata.SemanticClass))
                {
                    AddGroupedWarning(groupedWarnings, "Registry Missing", $"{child.name}: semantic class {metadata.SemanticClass} has no usable Prefab Registry entries");
                }

                if (config != null && TryResolveGridCoord(child.position, mapCenter, cell, config, out var gridCoord) && entry != null)
                {
                    if (!entry.CanAppearNearBoundary && IsNearBoundary(gridCoord, config))
                    {
                        AddGroupedWarning(groupedWarnings, "Placement Rule Violations", $"{child.name}: violates near-boundary rule for index {metadata.Type}");
                    }

                    if (!entry.CanAppearInCenter && IsInCenter(gridCoord, config))
                    {
                        AddGroupedWarning(groupedWarnings, "Placement Rule Violations", $"{child.name}: violates center-region rule for index {metadata.Type}");
                    }

                    if (entry.RequiresClearance && ViolatesClearance(child, obstacleRoot, mapCenter, cell, config, entry.ClearanceRadius, out var conflictingName))
                    {
                        AddGroupedWarning(groupedWarnings, "Placement Rule Violations", $"{child.name}: violates clearance radius {entry.ClearanceRadius} because of {conflictingName}");
                    }
                }

                if (!IsAxisSnapped(child.position.x, mapCenter.x, cell.x) || !IsAxisSnapped(child.position.z, mapCenter.z, cell.z))
                {
                    AddGroupedWarning(groupedWarnings, "Alignment / Bounds", $"{child.name}: position is not aligned to the centered placement grid");
                }

                if (!IsCardinalYaw(child.eulerAngles.y))
                {
                    AddGroupedWarning(groupedWarnings, "Alignment / Bounds", $"{child.name}: y rotation is not snapped to a 90-degree step");
                }

                if (config != null)
                {
                    if (child.position.x < mapCenter.x - halfWidth - 0.001f || child.position.x > mapCenter.x + halfWidth + 0.001f)
                    {
                        AddGroupedWarning(groupedWarnings, "Alignment / Bounds", $"{child.name}: x position is outside the configured centered map bounds");
                    }

                    if (child.position.z < mapCenter.z - halfDepth - 0.001f || child.position.z > mapCenter.z + halfDepth + 0.001f)
                    {
                        AddGroupedWarning(groupedWarnings, "Alignment / Bounds", $"{child.name}: z position is outside the configured centered map bounds");
                    }
                }
            }

            SetStatus(BuildGroupedWarningSummary(groupedWarnings, "Scene obstacle validation passed."));
        }

        private void ExportJson()
        {
            var obstacleRoot = GetObstacleRoot();
            if (obstacleRoot == null)
            {
                SetStatus("Cannot export because obstacleRoot could not be found.");
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export Obstacles JSON", Application.dataPath, "Obstacles", "json");
            if (string.IsNullOrEmpty(path))
            {
                SetStatus("Export cancelled.");
                return;
            }

            var data = new AllObstacleInfo();
            foreach (Transform child in obstacleRoot)
            {
                var metadata = child.GetComponent<ObstacleInstanceMetadata>();
                var type = metadata != null && metadata.Registered ? metadata.Type : UnregisteredFallbackType;
                data.Obstacles.Add(new ObstacleInfo
                {
                    Type = type,
                    Pos_X = Round3(child.position.x),
                    Pos_Y = Round3(child.position.y),
                    Pos_Z = Round3(child.position.z),
                    Rot_Y = Round3(NormalizeYaw(child.eulerAngles.y))
                });
            }

            if (!TrySerializeViaJsonHelper(path, data))
            {
                File.WriteAllText(path, BuildFallbackJson(data));
            }

            SetStatus($"Exported {data.Obstacles.Count} obstacle(s) to {path}.");
        }

        private void ImportJson()
        {
            var obstacleRoot = GetObstacleRoot();
            if (obstacleRoot == null)
            {
                SetStatus("Cannot import because obstacleRoot could not be found.");
                return;
            }

            var path = EditorUtility.OpenFilePanel("Import Obstacles JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path))
            {
                SetStatus("Import cancelled.");
                return;
            }

            var action = EditorUtility.DisplayDialogComplex(
                "Import Obstacles JSON",
                "导入前是否清空当前 obstacleRoot 下的现有障碍？",
                "Replace",
                "Cancel",
                "Append");

            if (action == 1)
            {
                SetStatus("Import cancelled.");
                return;
            }

            if (action == 0)
            {
                ClearAllObstacleChildren(obstacleRoot);
            }

            if (!TryDeserializeObstacleInfo(path, out var data, out var diagnostics) || data == null)
            {
                SetStatus(FormatImportStatus("Import failed: could not parse the JSON file.", diagnostics));
                return;
            }

            var imported = 0;
            foreach (var obstacle in data.Obstacles)
            {
                var instance = CreateObstacleInstanceFromInfo(obstacle, obstacleRoot);
                if (instance != null)
                {
                    imported++;
                }
            }

            SetStatus(FormatImportStatus($"Imported {imported} obstacle(s) from {path}.", diagnostics));
        }

        private void ShowTypePicker(Action<int> onPicked)
        {
            if (_prefabRegistry == null)
            {
                SetStatus("Assign a Prefab Registry first.");
                return;
            }

            var menu = new GenericMenu();
            foreach (var entry in _prefabRegistry.Entries)
            {
                var capturedType = entry.Type;
                var semanticLabel = entry.SemanticClass == ObstacleSemanticClass.None ? "Unassigned" : entry.SemanticClass.ToString();
                var label = $"{entry.Type}: {(string.IsNullOrWhiteSpace(entry.DisplayName) ? $"Index {entry.Type}" : entry.DisplayName)} [{semanticLabel}]";
                menu.AddItem(new GUIContent(label), false, () => onPicked(capturedType));
            }

            menu.ShowAsContext();
        }

        private ObstacleSceneSpawner GetSpawner()
        {
            if (_generationRunner == null)
            {
                return null;
            }

            return _generationRunner.GetComponent<ObstacleSceneSpawner>()
                   ?? _generationRunner.GetComponentInChildren<ObstacleSceneSpawner>(true);
        }

        private Transform GetObstacleRoot()
        {
            var spawner = GetSpawner();
            if (spawner == null)
            {
                return null;
            }

            var serialized = new SerializedObject(spawner);
            serialized.Update();
            var obstacleRootProperty = serialized.FindProperty("obstacleRoot");
            if (obstacleRootProperty.objectReferenceValue == null)
            {
                spawner.SendMessage("EnsureRoots", SendMessageOptions.DontRequireReceiver);
                serialized.Update();
            }

            return (Transform)obstacleRootProperty.objectReferenceValue;
        }

        private GenerationConfigAsset GetGenerationConfig()
        {
            if (_generationRunner == null)
            {
                return null;
            }

            var serialized = new SerializedObject(_generationRunner);
            serialized.Update();
            return serialized.FindProperty("generationConfig")?.objectReferenceValue as GenerationConfigAsset;
        }

        private void AssignPrefabRegistryToRunner()
        {
            if (_generationRunner == null || _prefabRegistry == null)
            {
                return;
            }

            var serialized = new SerializedObject(_generationRunner);
            serialized.Update();
            var prefabRegistryProperty = serialized.FindProperty("prefabRegistry");
            if (prefabRegistryProperty != null)
            {
                prefabRegistryProperty.objectReferenceValue = _prefabRegistry;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_generationRunner);
            }
        }

        private void EnsureSerializedState()
        {
            if (_prefabRegistry != null && (_prefabRegistryObject == null || _prefabRegistryObject.targetObject != _prefabRegistry))
            {
                _prefabRegistryObject = new SerializedObject(_prefabRegistry);
            }

            var config = GetGenerationConfig();
            if (config != null && (_configObject == null || _configObject.targetObject != config))
            {
                _configObject = new SerializedObject(config);
            }
        }

        private static float SnapAxis(float value, float cellSize, float center)
        {
            return Mathf.Round((value - center) / Mathf.Max(0.0001f, cellSize)) * cellSize + center;
        }

        private static bool IsAxisSnapped(float value, float center, float cellSize)
        {
            var snapped = SnapAxis(value, cellSize, center);
            return Mathf.Abs(snapped - value) <= 0.001f;
        }

        private static bool IsCardinalYaw(float yaw)
        {
            var normalized = NormalizeYaw(yaw);
            var snapped = Mathf.Round(normalized / 90f) * 90f;
            return Mathf.Abs(snapped - normalized) <= 0.001f;
        }

        private static double Round3(float value)
        {
            return Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f)
            {
                yaw += 360f;
            }

            return yaw;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
        }

        private static void ClearAllObstacleChildren(Transform obstacleRoot)
        {
            for (var i = obstacleRoot.childCount - 1; i >= 0; i--)
            {
                var child = obstacleRoot.GetChild(i);
                if (child != null)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private void SetStatus(string message)
        {
            _status = message;
            Repaint();
        }
    }
}
