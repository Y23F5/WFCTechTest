using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Unity.Runtime;

namespace WFCTechTest.WFC.Editor {
    /**
     * @file WfcMapEditorWindow.Validation.cs
     * @brief Validation and summary helpers for the WFC map editor window.
     */
    public sealed partial class WfcMapEditorWindow {
        private static SemanticArchetype ResolveRepresentativeArchetype(ObstacleSemanticClass semanticClass) {
            return semanticClass switch {
                ObstacleSemanticClass.LowCover => SemanticArchetype.LowCoverSparse,
                ObstacleSemanticClass.HighCover => SemanticArchetype.HighCoverSparse,
                ObstacleSemanticClass.Tower => SemanticArchetype.TowerSparse,
                ObstacleSemanticClass.Blocker => SemanticArchetype.BlockerSparse,
                _ => SemanticArchetype.Open
            };
        }

        private static void AddGroupedWarning(Dictionary<string, List<string>> groupedWarnings, string group, string warning) {
            if (!groupedWarnings.TryGetValue(group, out var warnings)) {
                warnings = new List<string>();
                groupedWarnings[group] = warnings;
            }

            warnings.Add(warning);
        }

        private static string BuildGroupedWarningSummary(Dictionary<string, List<string>> groupedWarnings, string successMessage) {
            if (groupedWarnings.Count == 0) return successMessage;

            var builder = new StringBuilder();
            var total = groupedWarnings.Sum(pair => pair.Value.Count);
            builder.AppendLine($"Found {total} issue(s).");
            foreach (var pair in groupedWarnings.Where(pair => pair.Value.Count > 0)) {
                builder.AppendLine();
                builder.AppendLine($"[{pair.Key}]");
                foreach (var warning in pair.Value) builder.AppendLine($"- {warning}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatImportStatus(string summary, ImportParseDiagnostics diagnostics) {
            if (diagnostics == null || diagnostics.Messages.Count == 0) return summary;

            var builder = new StringBuilder(summary);
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("[Import Diagnostics]");
            foreach (var message in diagnostics.Messages) builder.AppendLine($"- {message}");
            return builder.ToString().TrimEnd();
        }

        private bool HasUsablePrefabRegistryEntries(ObstacleSemanticClass semanticClass) {
            return _prefabRegistry != null
                && _prefabRegistry.GetEntriesForSemanticClass(semanticClass)
                    .Any(entry => entry != null && (entry.UsePlaceholder || entry.Prefab != null));
        }

        private static bool TryResolveGridCoord(Vector3 position, Vector3 mapCenter, Vector3 cell, GenerationConfigAsset config, out GridCoord2D coord) {
            coord = default;
            if (config == null) return false;

            var x = Mathf.RoundToInt((position.x - mapCenter.x) / Mathf.Max(0.0001f, cell.x) + ((config.Width - 1) * 0.5f));
            var z = Mathf.RoundToInt((position.z - mapCenter.z) / Mathf.Max(0.0001f, cell.z) + ((config.Depth - 1) * 0.5f));
            if (x < 0 || x >= config.Width || z < 0 || z >= config.Depth) return false;

            coord = new GridCoord2D(x, z);
            return true;
        }

        private static bool IsNearBoundary(GridCoord2D coord, GenerationConfigAsset config) {
            return coord.X <= 2 || coord.Z <= 2 || coord.X >= config.Width - 3 || coord.Z >= config.Depth - 3;
        }

        private static bool IsInCenter(GridCoord2D coord, GenerationConfigAsset config) {
            var minX = Mathf.FloorToInt(config.Width * 0.3f);
            var maxX = Mathf.CeilToInt(config.Width * 0.7f);
            var minZ = Mathf.FloorToInt(config.Depth * 0.3f);
            var maxZ = Mathf.CeilToInt(config.Depth * 0.7f);
            return coord.X >= minX && coord.X <= maxX && coord.Z >= minZ && coord.Z <= maxZ;
        }

        private static bool ViolatesClearance(Transform source, Transform obstacleRoot, Vector3 mapCenter, Vector3 cell, GenerationConfigAsset config, int clearanceRadius, out string conflictingName) {
            conflictingName = string.Empty;
            if (clearanceRadius <= 0 || !TryResolveGridCoord(source.position, mapCenter, cell, config, out var sourceCoord)) return false;

            foreach (Transform child in obstacleRoot) {
                if (child == null || child == source) continue;

                var metadata = child.GetComponent<ObstacleInstanceMetadata>();
                if (metadata == null || !metadata.Registered) continue;
                if (!TryResolveGridCoord(child.position, mapCenter, cell, config, out var otherCoord)) continue;

                if (Mathf.Abs(otherCoord.X - sourceCoord.X) <= clearanceRadius && Mathf.Abs(otherCoord.Z - sourceCoord.Z) <= clearanceRadius) {
                    conflictingName = child.name;
                    return true;
                }
            }

            return false;
        }
    }
}
