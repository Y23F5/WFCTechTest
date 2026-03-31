using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Unity.Runtime;

namespace WFCTechTest.WFC.Editor {
    /**
     * @file WfcMapEditorWindow.ImportExport.cs
     * @brief Import/export helpers for the WFC map editor window.
     */
    public sealed partial class WfcMapEditorWindow {
        private static string BuildFallbackJson(AllObstacleInfo data) {
            var lines = new List<string> { "{\"Obstacles\":[" };
            for (var i = 0; i < data.Obstacles.Count; i++) {
                var obstacle = data.Obstacles[i];
                var suffix = i < data.Obstacles.Count - 1 ? "," : string.Empty;
                lines.Add($"  {{\"Type\":{obstacle.Type},\"Pos_X\":{obstacle.Pos_X.ToString("0.###", CultureInfo.InvariantCulture)},\"Pos_Y\":{obstacle.Pos_Y.ToString("0.###", CultureInfo.InvariantCulture)},\"Pos_Z\":{obstacle.Pos_Z.ToString("0.###", CultureInfo.InvariantCulture)},\"Rot_Y\":{obstacle.Rot_Y.ToString("0.###", CultureInfo.InvariantCulture)}}}{suffix}");
            }

            lines.Add("]}");
            return string.Join("\n", lines);
        }

        private static bool TrySerializeViaJsonHelper(string path, AllObstacleInfo data) {
            return WfcEditorJsonBridge.TrySerialize(path, data);
        }

        private bool TryDeserializeObstacleInfo(string path, out AllObstacleInfo data, out ImportParseDiagnostics diagnostics) {
            diagnostics = new ImportParseDiagnostics();
            data = null;
            if (TryDeserializeViaJsonHelper(path, out data) && data != null) return true;

            diagnostics.UsedFallback = true;
            diagnostics.Messages.Add("JsonHelper parse failed. Used fallback parser.");
            return TryDeserializeFallback(path, out data, diagnostics);
        }

        private static bool TryDeserializeViaJsonHelper(string path, out AllObstacleInfo data) {
            return WfcEditorJsonBridge.TryDeserialize(path, out data);
        }

        private static bool TryDeserializeFallback(string path, out AllObstacleInfo data, ImportParseDiagnostics diagnostics) {
            data = new AllObstacleInfo();
            if (!File.Exists(path)) {
                diagnostics.Messages.Add("File does not exist.");
                return false;
            }

            var content = File.ReadAllText(path);
            var matches = Regex.Matches(content, "\\{[^{}]*\\}");
            var parsedFragments = 0;
            foreach (Match match in matches) {
                if (!LooksLikeObstacleFragment(match.Value)) continue;

                parsedFragments++;
                if (!TryParseObstacle(match.Value, out var obstacle, out var error)) {
                    diagnostics.Messages.Add($"Fallback entry {parsedFragments}: {error}");
                    continue;
                }

                data.Obstacles.Add(obstacle);
            }

            if (parsedFragments == 0) diagnostics.Messages.Add("Fallback parser did not find any obstacle objects.");
            return data.Obstacles.Count > 0;
        }

        private static bool TryParseObstacle(string jsonFragment, out ObstacleInfo obstacle, out string error) {
            obstacle = null;
            error = string.Empty;
            if (!TryExtractInt(jsonFragment, "Type", out var type, out error)) return false;

            if (!TryExtractDouble(jsonFragment, "Pos_X", out var posX, out error)
                || !TryExtractDouble(jsonFragment, "Pos_Y", out var posY, out error)
                || !TryExtractDouble(jsonFragment, "Pos_Z", out var posZ, out error)
                || !TryExtractDouble(jsonFragment, "Rot_Y", out var rotY, out error)) return false;

            obstacle = new ObstacleInfo {
                Type = type,
                Pos_X = posX,
                Pos_Y = posY,
                Pos_Z = posZ,
                Rot_Y = rotY
            };
            return true;
        }

        private GameObject CreateObstacleInstanceFromInfo(ObstacleInfo info, Transform obstacleRoot) {
            var entry = _prefabRegistry?.GetEntry(info.Type);
            var fallback = _prefabRegistry?.GetEntry(0);
            var prefab = entry?.Prefab ?? fallback?.Prefab ?? _placeholderCubePrefab;
            var instance = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, obstacleRoot)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (instance == null) return null;

            if (instance.transform.parent != obstacleRoot) Undo.SetTransformParent(instance.transform, obstacleRoot, "Import Obstacles");

            var position = new Vector3((float)info.Pos_X, (float)info.Pos_Y, (float)info.Pos_Z);
            var groundedY = TryGetRegistryAlignedY(entry, out var resolvedY) ? resolvedY : GetGroundTopY();
            instance.transform.position = new Vector3(position.x, groundedY, position.z);
            instance.transform.rotation = Quaternion.Euler(0f, (float)info.Rot_Y, 0f);
            if (!TryGetRegistryAlignedY(entry, out _)) AlignTransformBottomToGround(instance.transform);

            instance.name = $"Imported_{info.Type}_{Round3(position.x)}_{Round3(position.z)}";
            var metadata = instance.GetComponent<ObstacleInstanceMetadata>();
            if (metadata == null) metadata = Undo.AddComponent<ObstacleInstanceMetadata>(instance);

            metadata.Type = info.Type;
            metadata.DisplayName = entry?.DisplayName ?? $"Imported Index {info.Type}";
            metadata.AllowRandomYaw = entry?.AllowRandomYaw ?? false;
            metadata.SemanticClass = entry?.SemanticClass ?? ObstacleSemanticClass.None;
            metadata.SourceArchetype = ResolveRepresentativeArchetype(metadata.SemanticClass);
            metadata.IsUnknownType = entry == null;
            metadata.IsGenerated = false;
            metadata.Registered = true;
            EditorUtility.SetDirty(instance);
            EditorUtility.SetDirty(metadata);
            return instance;
        }

        private float GetGroundTopY() {
            var config = GetGenerationConfig();
            var mapCenterY = config != null ? config.MapCenter.y : 0f;
            var edge = _prefabRegistry != null ? _prefabRegistry.GetPlacementCellEdge() : 1f;
            return mapCenterY + (edge * 0.5f);
        }

        private bool TryGetRegistryAlignedY(PrefabRegistryEntry entry, out float alignedY) {
            alignedY = GetGroundTopY();
            if (entry == null || !PrefabRegistryAsset.TryGetCombinedPrefabLocalBounds(entry.Prefab, out _)) return false;

            alignedY += entry.DefaultPosY;
            return true;
        }

        private void AlignTransformUsingRegistry(Transform transform, PrefabRegistryEntry entry) {
            if (transform == null) return;

            if (TryGetRegistryAlignedY(entry, out var alignedY)) {
                var position = transform.position;
                transform.position = new Vector3(position.x, alignedY, position.z);
                return;
            }

            AlignTransformBottomToGround(transform);
        }

        private void AlignTransformBottomToGround(Transform transform) {
            if (transform == null) return;

            var groundTopY = GetGroundTopY();
            if (!TryGetCombinedWorldBounds(transform.gameObject, out var bounds)) {
                var position = transform.position;
                transform.position = new Vector3(position.x, groundTopY, position.z);
                return;
            }

            transform.position += new Vector3(0f, groundTopY - bounds.min.y, 0f);
        }

        private static bool TryGetCombinedWorldBounds(GameObject gameObject, out Bounds bounds) {
            bounds = default;
            if (gameObject == null) return false;

            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static bool TryExtractInt(string jsonFragment, string key, out int value, out string error) {
            value = 0;
            error = string.Empty;
            if (!TryExtractToken(jsonFragment, key, out var token)) {
                error = $"missing field '{key}'";
                return false;
            }

            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
                error = $"invalid integer for '{key}': {token}";
                return false;
            }

            return true;
        }

        private static bool TryExtractDouble(string jsonFragment, string key, out double value, out string error) {
            value = 0d;
            error = string.Empty;
            if (!TryExtractToken(jsonFragment, key, out var token)) {
                error = $"missing field '{key}'";
                return false;
            }

            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) {
                error = $"invalid number for '{key}': {token}";
                return false;
            }

            return true;
        }

        private static bool TryExtractToken(string jsonFragment, string key, out string value) {
            value = string.Empty;
            var match = Regex.Match(jsonFragment, $"\"{key}\"\\s*:\\s*([^,}}\\s]+)");
            if (!match.Success) return false;

            value = match.Groups[1].Value.Trim().Trim('"');
            return true;
        }

        private static bool LooksLikeObstacleFragment(string jsonFragment) {
            return jsonFragment.Contains("\"Type\"")
                || jsonFragment.Contains("\"Pos_X\"")
                || jsonFragment.Contains("\"Pos_Y\"")
                || jsonFragment.Contains("\"Pos_Z\"")
                || jsonFragment.Contains("\"Rot_Y\"");
        }
    }
}
