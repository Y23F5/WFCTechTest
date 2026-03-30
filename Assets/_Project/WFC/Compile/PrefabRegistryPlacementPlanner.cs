using System;
using System.Collections.Generic;
using System.Linq;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Semantic;

namespace WFCTechTest.WFC.Compile
{
    /// <summary>
    /// @file PrefabRegistryPlacementPlanner.cs
    /// @brief Builds object-level obstacle placements directly from prefab registry rules over the semantic obstacle candidate field.
    /// </summary>
    public sealed class PrefabRegistryPlacementPlanner
    {
        private const int FallbackType = 0;

        private readonly GenerationConfigAsset _config;
        private readonly PrefabRegistryAsset _registry;
        private readonly List<PrefabRegistryEntry> _autoEntries;
        private readonly Dictionary<int, int> _generatedCounts = new Dictionary<int, int>();
        private readonly HashSet<GridCoord2D> _occupied = new HashSet<GridCoord2D>();
        private readonly HashSet<GridCoord2D> _blockedByClearance = new HashSet<GridCoord2D>();
        private readonly Random _random;

        /// <summary>
        /// Initializes a planner instance.
        /// </summary>
        public PrefabRegistryPlacementPlanner(GenerationConfigAsset config, PrefabRegistryAsset registry, int seed)
        {
            _config = config;
            _registry = registry;
            _random = new Random(seed);
            _autoEntries = registry != null
                ? registry.Entries.Where(entry => entry != null && entry.EnabledForAutoGeneration).OrderBy(entry => entry.Type).ToList()
                : new List<PrefabRegistryEntry>();
        }

        /// <summary>
        /// Plans object-level obstacle placements over the semantic obstacle candidate field.
        /// </summary>
        public List<ObstaclePlacement> Plan(SemanticGrid2D grid, out int degradedPlacements)
        {
            degradedPlacements = 0;
            var placements = new List<ObstaclePlacement>();
            if (_autoEntries.Count == 0)
            {
                return placements;
            }

            foreach (var coord in EnumerateCandidateCoords(grid))
            {
                if (_occupied.Contains(coord))
                {
                    continue;
                }

                var candidates = BuildCandidatePlacements(grid, coord);
                if (candidates.Count == 0)
                {
                    var fallback = BuildFallbackPlacement(coord, grid.Get(coord.X, coord.Z));
                    placements.Add(fallback);
                    RegisterPlacement(fallback, clearanceRadius: 0);
                    degradedPlacements++;
                    continue;
                }

                var chosen = ChoosePlacement(candidates);
                placements.Add(chosen);
                RegisterPlacement(chosen, ResolveEntry(chosen.Type)?.ClearanceRadius ?? 0);
            }

            return placements;
        }

        private IEnumerable<GridCoord2D> EnumerateCandidateCoords(SemanticGrid2D grid)
        {
            for (var x = 1; x < grid.Width - 1; x++)
            {
                for (var z = 1; z < grid.Depth - 1; z++)
                {
                    var archetype = grid.Get(x, z);
                    if (!archetype.IsObstacle())
                    {
                        continue;
                    }

                    yield return new GridCoord2D(x, z);
                }
            }
        }

        private List<ObstaclePlacement> BuildCandidatePlacements(SemanticGrid2D grid, GridCoord2D anchor)
        {
            var candidates = new List<ObstaclePlacement>();
            var archetype = grid.Get(anchor.X, anchor.Z);
            var semanticClass = archetype.GetObstacleSemanticClass();
            foreach (var entry in _autoEntries)
            {
                if (!CanUseEntry(entry, anchor, semanticClass))
                {
                    continue;
                }

                if (!TryCollectFootprint(grid, anchor, out var occupiedCells))
                {
                    continue;
                }

                var placement = new ObstaclePlacement
                {
                    Type = entry.Type,
                    DisplayName = entry.DisplayName,
                    AllowRandomYaw = entry.AllowRandomYaw,
                    Anchor = anchor,
                    Height = entry.LogicalHeightCells,
                    FootprintWidth = 1,
                    FootprintDepth = 1,
                    RotationY = NormalizeRotation(entry.AllowRandomYaw ? 90f * _random.Next(0, 4) : 0f),
                    Archetype = archetype,
                    SemanticClass = semanticClass,
                    DensityBand = archetype.GetDensityBand(),
                    OccupiedCells = occupiedCells
                };
                candidates.Add(placement);
            }

            return candidates;
        }

        private bool CanUseEntry(PrefabRegistryEntry entry, GridCoord2D anchor, ObstacleSemanticClass semanticClass)
        {
            if (entry == null)
            {
                return false;
            }

            if (!entry.UsePlaceholder && entry.Prefab == null)
            {
                return false;
            }

            if (entry.SemanticClass != semanticClass)
            {
                return false;
            }

            if (entry.MaxCount > 0 && GetGeneratedCount(entry.Type) >= entry.MaxCount)
            {
                return false;
            }

            if (!entry.CanAppearNearBoundary && IsNearBoundary(anchor.X, anchor.Z))
            {
                return false;
            }

            if (!entry.CanAppearInCenter && IsInCenter(anchor.X, anchor.Z))
            {
                return false;
            }

            if (!MatchesHeightMask(entry))
            {
                return false;
            }

            return true;
        }

        private bool TryCollectFootprint(SemanticGrid2D grid, GridCoord2D anchor, out List<GridCoord2D> occupiedCells)
        {
            occupiedCells = new List<GridCoord2D>();
            if (anchor.X <= 0 || anchor.X >= grid.Width - 1 || anchor.Z <= 0 || anchor.Z >= grid.Depth - 1)
            {
                return false;
            }

            if (_occupied.Contains(anchor) || _blockedByClearance.Contains(anchor))
            {
                return false;
            }

            var archetype = grid.Get(anchor.X, anchor.Z);
            if (!archetype.IsObstacle())
            {
                return false;
            }

            occupiedCells.Add(anchor);
            return true;
        }

        private ObstaclePlacement ChoosePlacement(List<ObstaclePlacement> candidates)
        {
            var total = candidates.Sum(GetPlacementWeight);
            var pick = (float)_random.NextDouble() * total;
            foreach (var candidate in candidates)
            {
                pick -= GetPlacementWeight(candidate);
                if (pick <= 0f)
                {
                    return candidate;
                }
            }

            return candidates[candidates.Count - 1];
        }

        private float GetPlacementWeight(ObstaclePlacement placement)
        {
            var entry = ResolveEntry(placement.Type);
            if (entry == null)
            {
                return 0.001f;
            }

            var weight = Math.Max(0.001f, entry.Weight);
            weight *= GetDensityMultiplier(placement, entry);
            if (entry.MinCount > 0 && GetGeneratedCount(entry.Type) < entry.MinCount)
            {
                weight *= 1.5f;
            }

            weight *= Math.Max(1, placement.FootprintWidth * placement.FootprintDepth);
            return weight;
        }

        private void RegisterPlacement(ObstaclePlacement placement, int clearanceRadius)
        {
            foreach (var cell in placement.OccupiedCells)
            {
                _occupied.Add(cell);
            }

            if (clearanceRadius > 0)
            {
                foreach (var cell in placement.OccupiedCells)
                {
                    for (var dx = -clearanceRadius; dx <= clearanceRadius; dx++)
                    {
                        for (var dz = -clearanceRadius; dz <= clearanceRadius; dz++)
                        {
                            _blockedByClearance.Add(cell.Offset(dx, dz));
                        }
                    }
                }
            }

            _generatedCounts[placement.Type] = GetGeneratedCount(placement.Type) + 1;
        }

        private ObstaclePlacement BuildFallbackPlacement(GridCoord2D anchor, SemanticArchetype archetype)
        {
            var semanticClass = archetype.GetObstacleSemanticClass();
            var fallback = ResolveFallbackEntry(semanticClass);
            return new ObstaclePlacement
            {
                Type = fallback?.Type ?? FallbackType,
                DisplayName = fallback?.DisplayName ?? "Fallback",
                AllowRandomYaw = fallback?.AllowRandomYaw ?? false,
                Anchor = anchor,
                Height = Math.Max(1, fallback?.LogicalHeightCells ?? 1),
                FootprintWidth = 1,
                FootprintDepth = 1,
                RotationY = 0f,
                Archetype = archetype,
                SemanticClass = semanticClass,
                DensityBand = archetype.GetDensityBand(),
                OccupiedCells = new List<GridCoord2D> { anchor }
            };
        }

        private static float NormalizeRotation(float rotationY)
        {
            var normalized = rotationY % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private bool MatchesHeightMask(PrefabRegistryEntry entry)
        {
            if (entry.AllowedHeightMask < 0)
            {
                return true;
            }

            var clampedHeight = Math.Clamp(entry.LogicalHeightCells, 1, 31);
            var bit = 1 << (clampedHeight - 1);
            return (entry.AllowedHeightMask & bit) != 0;
        }

        private bool IsNearBoundary(int x, int z)
        {
            return x <= 2 || z <= 2 || x >= _config.Width - 3 || z >= _config.Depth - 3;
        }

        private bool IsInCenter(int x, int z)
        {
            var minX = (int)Math.Floor(_config.Width * 0.3f);
            var maxX = (int)Math.Ceiling(_config.Width * 0.7f);
            var minZ = (int)Math.Floor(_config.Depth * 0.3f);
            var maxZ = (int)Math.Ceiling(_config.Depth * 0.7f);
            return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
        }

        private int GetGeneratedCount(int type)
        {
            return _generatedCounts.TryGetValue(type, out var count) ? count : 0;
        }

        private PrefabRegistryEntry ResolveEntry(int type)
        {
            return _registry?.GetEntry(type);
        }

        private PrefabRegistryEntry ResolveFallbackEntry(ObstacleSemanticClass semanticClass)
        {
            var semanticFallback = _registry?.Entries.FirstOrDefault(entry =>
                entry != null
                && entry.SemanticClass == semanticClass
                && entry.UsePlaceholder);
            return semanticFallback ?? ResolveEntry(FallbackType);
        }

        private static float GetDensityMultiplier(ObstaclePlacement placement, PrefabRegistryEntry entry)
        {
            return placement.DensityBand switch
            {
                SemanticDensityBand.Dense => Math.Max(0.001f, entry.DenseWeight),
                SemanticDensityBand.Sparse => Math.Max(0.001f, entry.SparseWeight),
                _ => 1f
            };
        }
    }
}
