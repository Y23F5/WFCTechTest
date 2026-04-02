using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Data
{
    /// <summary>
    /// @file PrefabRegistryAsset.cs
    /// @brief Stores prefab registry entries, placeholder slots, and shared placement bounds for the map editor workflow.
    /// </summary>
    [MovedFrom(true, sourceNamespace: "WFCTechTest.WFC.Data", sourceClassName: "ObstaclePaletteAsset")]
    [CreateAssetMenu(menuName = "WFC/Prefab Registry", fileName = "PrefabRegistry")]
    public sealed class PrefabRegistryAsset : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/PrefabRegistry.asset";

        [SerializeField] private List<PrefabRegistryEntry> entries = new List<PrefabRegistryEntry>();

        /// <summary>
        /// Gets the configured prefab registry entries.
        /// </summary>
        public IReadOnlyList<PrefabRegistryEntry> Entries => entries;

        /// <summary>
        /// Returns the prefab registry entry for a concrete index, or null when no matching entry exists.
        /// </summary>
        public PrefabRegistryEntry GetEntry(int type)
        {
            return entries.FirstOrDefault(entry => entry.Type == type);
        }

        /// <summary>
        /// Returns all prefab registry entries that belong to the supplied semantic class.
        /// </summary>
        public IReadOnlyList<PrefabRegistryEntry> GetEntriesForSemanticClass(ObstacleSemanticClass semanticClass)
        {
            return entries.Where(entry => entry != null && entry.SemanticClass == semanticClass).ToList();
        }

        /// <summary>
        /// Ensures the default placeholder entries for indices 0, 1, 2, and 3 exist.
        /// </summary>
        public void EnsureDefaultPlaceholders(GameObject placeholderPrefab)
        {
            EnsureEntry(0, "Placeholder Index 0", placeholderPrefab);
            EnsureEntry(1, "Placeholder Index 1", placeholderPrefab);
            EnsureEntry(2, "Placeholder Index 2", placeholderPrefab);
            EnsureEntry(3, "Placeholder Index 3", placeholderPrefab);
            RefreshDerivedValues();
        }

        /// <summary>
        /// Returns the next available registry index.
        /// </summary>
        public int GetNextType()
        {
            return entries.Count == 0 ? 0 : entries.Max(entry => entry.Type) + 1;
        }

        /// <summary>
        /// Adds a new empty prefab registry entry and returns it.
        /// </summary>
        public PrefabRegistryEntry AddEntry(GameObject defaultPrefab = null)
        {
            var nextType = GetNextType();
            var entry = new PrefabRegistryEntry
            {
                Type = nextType,
                DisplayName = $"Index {nextType}",
                SemanticClass = ObstacleSemanticClass.LowCover,
                Prefab = defaultPrefab
            };
            ApplySemanticDefaults(entry, entry.SemanticClass);
            entries.Add(entry);
            RefreshDerivedValues();
            return entry;
        }

        /// <summary>
        /// Removes the first prefab registry entry with the supplied index.
        /// </summary>
        public bool RemoveEntry(int type)
        {
            var entry = GetEntry(type);
            if (entry == null)
            {
                return false;
            }

            entries.Remove(entry);
            RefreshDerivedValues();
            return true;
        }

        /// <summary>
        /// Re-sorts entries by index and removes null placeholders introduced by editor array edits.
        /// </summary>
        public void NormalizeEntries()
        {
            entries = entries
                .Where(entry => entry != null)
                .OrderBy(entry => entry.Type)
                .ToList();
        }

        /// <summary>
        /// Recalculates all unlocked derived values and normalizes the registry.
        /// </summary>
        public void RefreshDerivedValues()
        {
            NormalizeEntries();
            var cellEdge = GetPlacementCellEdge();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.DisplayName))
                {
                    entry.DisplayName = $"Index {entry.Type}";
                }

                entry.FootprintWidth = 1;
                entry.FootprintDepth = 1;
                entry.Weight = Mathf.Max(0.001f, entry.Weight);
                entry.SparseWeight = Mathf.Max(0.001f, entry.SparseWeight);
                entry.DenseWeight = Mathf.Max(0.001f, entry.DenseWeight);
                if (!entry.LogicalHeightLocked)
                {
                    entry.LogicalHeightCells = ComputeLogicalHeightCells(entry, cellEdge);
                }
                else
                {
                    entry.LogicalHeightCells = Mathf.Max(1, entry.LogicalHeightCells);
                }

            }
        }

        /// <summary>
        /// Reapplies the default semantic configuration for the entry at the supplied list index.
        /// </summary>
        public void ApplyDefaultsAt(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= entries.Count || entries[entryIndex] == null)
            {
                return;
            }

            ApplySemanticDefaults(entries[entryIndex], entries[entryIndex].SemanticClass);
            entries[entryIndex].LogicalHeightLocked = false;
            entries[entryIndex].DefaultPosYLocked = false;
            RefreshDerivedValues();
        }

        /// <summary>
        /// Unlocks and recalculates the logical height for the entry at the supplied list index.
        /// </summary>
        public void RecalculateLogicalHeightAt(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= entries.Count || entries[entryIndex] == null)
            {
                return;
            }

            entries[entryIndex].LogicalHeightLocked = false;
            RefreshDerivedValues();
        }

        /// <summary>
        /// Unlocks and recalculates the default placement world Y for the entry at the supplied list index.
        /// </summary>
        public void RecalculateDefaultPosYAt(int entryIndex, float groundTopY)
        {
            if (entryIndex < 0 || entryIndex >= entries.Count || entries[entryIndex] == null)
            {
                return;
            }

            entries[entryIndex].DefaultPosYLocked = false;
            if (TryComputeDefaultPosY(entries[entryIndex], groundTopY, out var defaultPosY))
            {
                entries[entryIndex].DefaultPosY = defaultPosY;
            }
            else
            {
                entries[entryIndex].DefaultPosY = 0f;
            }
        }

        /// <summary>
        /// Recalculates the default placement world Y for every unlocked entry.
        /// </summary>
        public void RecalculateUnlockedDefaultPosY(float groundTopY)
        {
            foreach (var entry in entries)
            {
                if (entry == null || entry.DefaultPosYLocked)
                {
                    continue;
                }

                if (TryComputeDefaultPosY(entry, groundTopY, out var defaultPosY))
                {
                    entry.DefaultPosY = defaultPosY;
                }
                else
                {
                    entry.DefaultPosY = 0f;
                }
            }
        }

        /// <summary>
        /// Computes the maximum prefab bounds across all assigned registry prefabs.
        /// </summary>
        public Vector3 GetMaximumPrefabBounds()
        {
            var maximum = Vector3.one;
            foreach (var entry in GetCellSourceEntries(preferRealPrefabs: false))
            {
                if (!TryGetCombinedPrefabBounds(entry.Prefab, out var bounds))
                {
                    continue;
                }

                maximum = Vector3.Max(maximum, bounds.size);
            }

            return maximum;
        }

        /// <summary>
        /// Returns the cubic placement cell edge derived from the largest prefab side length.
        /// </summary>
        public float GetPlacementCellEdge()
        {
            var edge = 1f;
            foreach (var entry in GetCellSourceEntries(preferRealPrefabs: true))
            {
                if (!TryGetCombinedPrefabBounds(entry.Prefab, out var bounds))
                {
                    continue;
                }

                edge = Mathf.Max(edge, bounds.size.x, bounds.size.y, bounds.size.z);
            }

            return edge;
        }

        /// <summary>
        /// Returns the placement cell size derived from the maximum registry bounds.
        /// </summary>
        public Vector3 GetPlacementCellSize()
        {
            var edge = GetPlacementCellEdge();
            return new Vector3(edge, edge, edge);
        }

        /// <summary>
        /// Returns the combined renderer bounds for the supplied prefab when available.
        /// The size intentionally includes the prefab root transform scale so registry cells reflect
        /// the actual visual footprint users see when the prefab is instantiated at scale 1.
        /// </summary>
        public static bool TryGetCombinedPrefabBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            if (prefab == null)
            {
                return false;
            }

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        /// <summary>
        /// Returns the combined renderer bounds for the supplied prefab in prefab-root local space.
        /// </summary>
        public static bool TryGetCombinedPrefabLocalBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            if (prefab == null || !TryGetCombinedPrefabBounds(prefab, out var worldBounds))
            {
                return false;
            }

            bounds = new Bounds(worldBounds.center - prefab.transform.position, worldBounds.size);
            return true;
        }

        /// <summary>
        /// Computes the default placement world Y for the supplied entry and ground-top height.
        /// </summary>
        public static bool TryComputeDefaultPosY(PrefabRegistryEntry entry, float groundTopY, out float defaultPosY)
        {
            defaultPosY = 0f;
            if (entry?.Prefab == null || !TryGetCombinedPrefabLocalBounds(entry.Prefab, out var bounds))
            {
                return false;
            }

            defaultPosY = groundTopY - bounds.min.y;
            return true;
        }

        private void OnValidate()
        {
            RefreshDerivedValues();
        }

        private void EnsureEntry(int type, string name, GameObject prefab)
        {
            if (entries.Any(entry => entry.Type == type))
            {
                return;
            }

            entries.Add(new PrefabRegistryEntry
            {
                Type = type,
                DisplayName = name,
                Prefab = prefab,
                AllowRandomYaw = false,
                UsePlaceholder = true,
                EnabledForAutoGeneration = true,
                SemanticClass = ResolvePlaceholderSemanticClass(type),
                MaxCount = 0
            });

            ApplySemanticDefaults(entries[entries.Count - 1], ResolvePlaceholderSemanticClass(type));
            entries[entries.Count - 1].DisplayName = name;
            entries[entries.Count - 1].Prefab = prefab;
            entries[entries.Count - 1].UsePlaceholder = true;
            entries[entries.Count - 1].AllowRandomYaw = false;
            entries[entries.Count - 1].EnabledForAutoGeneration = true;
            entries[entries.Count - 1].LogicalHeightLocked = false;
            entries[entries.Count - 1].DefaultPosYLocked = false;
        }

        private static void ApplySemanticDefaults(PrefabRegistryEntry entry, ObstacleSemanticClass semanticClass)
        {
            if (entry == null)
            {
                return;
            }

            entry.SemanticClass = semanticClass;
            entry.FootprintWidth = 1;
            entry.FootprintDepth = 1;
            entry.AllowedHeightMask = -1;
            entry.Weight = 1f;
            entry.LogicalHeightLocked = false;
            entry.DefaultPosYLocked = false;

            switch (semanticClass)
            {
                case ObstacleSemanticClass.HighCover:
                    entry.CanAppearNearBoundary = true;
                    entry.CanAppearInCenter = true;
                    entry.RequiresClearance = false;
                    entry.ClearanceRadius = 0;
                    entry.SparseWeight = 0.9f;
                    entry.DenseWeight = 1.2f;
                    break;
                case ObstacleSemanticClass.Tower:
                    entry.CanAppearNearBoundary = true;
                    entry.CanAppearInCenter = true;
                    entry.RequiresClearance = false;
                    entry.ClearanceRadius = 0;
                    entry.SparseWeight = 1.15f;
                    entry.DenseWeight = 0.82f;
                    break;
                case ObstacleSemanticClass.Blocker:
                    entry.CanAppearNearBoundary = true;
                    entry.CanAppearInCenter = true;
                    entry.RequiresClearance = false;
                    entry.ClearanceRadius = 0;
                    entry.SparseWeight = 0.82f;
                    entry.DenseWeight = 1.25f;
                    break;
                case ObstacleSemanticClass.LowCover:
                default:
                    entry.CanAppearNearBoundary = true;
                    entry.CanAppearInCenter = true;
                    entry.RequiresClearance = false;
                    entry.ClearanceRadius = 0;
                    entry.SparseWeight = 1.18f;
                    entry.DenseWeight = 0.92f;
                    break;
            }
        }

        private static ObstacleSemanticClass ResolvePlaceholderSemanticClass(int type)
        {
            return type switch
            {
                1 => ObstacleSemanticClass.HighCover,
                2 => ObstacleSemanticClass.Tower,
                3 => ObstacleSemanticClass.Blocker,
                _ => ObstacleSemanticClass.LowCover
            };
        }

        private IEnumerable<PrefabRegistryEntry> GetCellSourceEntries(bool preferRealPrefabs)
        {
            var nonPlaceholderEntries = entries.Where(entry => entry != null && !entry.UsePlaceholder && entry.Prefab != null).ToList();
            if (preferRealPrefabs && nonPlaceholderEntries.Count > 0)
            {
                return nonPlaceholderEntries;
            }

            return entries.Where(entry => entry != null && entry.Prefab != null).ToList();
        }

        private static int ComputeLogicalHeightCells(PrefabRegistryEntry entry, float cellEdge)
        {
            if (entry?.Prefab == null || cellEdge <= 0f || !TryGetCombinedPrefabBounds(entry.Prefab, out var bounds))
            {
                return 1;
            }

            return Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / cellEdge));
        }

    }
}
