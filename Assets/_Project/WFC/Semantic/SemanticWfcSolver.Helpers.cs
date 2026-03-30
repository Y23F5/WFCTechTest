using System;
using System.Collections.Generic;
using UnityEngine;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Semantic {
    /**
     * @file SemanticWfcSolver.Helpers.cs
     * @brief Helper methods for semantic solver mask filtering, biasing, and bookkeeping.
     */
    public sealed partial class SemanticWfcSolver {
        private ulong FilterMask(ulong candidateMask, ulong sourceMask) {
            var filtered = 0UL;
            foreach (var candidate in Expand(candidateMask)) {
                foreach (var source in Expand(sourceMask)) {
                    if (_rules.IsAllowed(candidate, source)) {
                        filtered |= MaskOf(candidate);
                        break;
                    }
                }
            }

            return filtered;
        }

        private double ComputeEntropy(ulong mask) {
            var total = 0f;
            var weightedLog = 0d;
            foreach (var archetype in Expand(mask)) {
                var weight = _weights[archetype];
                total += weight;
                weightedLog += weight * Math.Log(Math.Max(weight, 0.0001f));
            }

            return Math.Log(total) - (weightedLog / total);
        }

        private float GetSelectionWeight(SemanticArchetype archetype, GridCoord2D selected, ulong[] masks, int width, int depth, ResolvedInteriorStats resolvedStats) {
            var weight = _weights[archetype];
            weight *= GetConfiguredDensityWeight(archetype);
            weight *= GetTargetRatioBias(archetype, resolvedStats.ResolvedCounts);
            weight *= GetOpenCoverageBias(archetype, resolvedStats);
            var center = new Vector2((width - 1) * 0.5f, (depth - 1) * 0.5f);
            var position = new Vector2(selected.X, selected.Z);
            var normalizedCenterDistance = Vector2.Distance(position, center) / Mathf.Max(1f, center.magnitude);
            var resolvedObstacleNeighbors = CountResolvedNeighborFamily(selected, masks, width, depth, false);
            var resolvedOpenNeighbors = CountResolvedNeighborFamily(selected, masks, width, depth, true);

            if (archetype.IsOpenLike()) {
                weight *= Mathf.Lerp(1.35f, 0.92f, normalizedCenterDistance);
                weight *= resolvedOpenNeighbors >= 2 ? 1.12f : 1f;
                weight *= resolvedObstacleNeighbors >= 3 ? 0.78f : 1f;
                return weight;
            }

            var densityBand = archetype.GetDensityBand();
            weight *= densityBand == SemanticDensityBand.Dense
                ? resolvedObstacleNeighbors >= 2 ? 1.28f : 0.72f
                : resolvedObstacleNeighbors <= 1 ? 1.18f : 0.84f;

            switch (archetype.GetObstacleSemanticClass()) {
                case ObstacleSemanticClass.LowCover:
                    weight *= Mathf.Lerp(0.94f, 1.08f, normalizedCenterDistance);
                    weight *= resolvedOpenNeighbors >= 2 ? 1.08f : 1f;
                    return weight;
                case ObstacleSemanticClass.HighCover:
                    weight *= Mathf.Lerp(0.86f, 1.16f, normalizedCenterDistance);
                    weight *= resolvedObstacleNeighbors >= 1 ? 1.08f : 0.84f;
                    return weight;
                case ObstacleSemanticClass.Tower:
                    weight *= Mathf.Lerp(0.78f, 1.26f, normalizedCenterDistance);
                    weight *= resolvedObstacleNeighbors >= 2 ? 1.14f : 0.76f;
                    weight *= resolvedOpenNeighbors >= 3 ? 0.82f : 1f;
                    return weight;
                case ObstacleSemanticClass.Blocker:
                    weight *= Mathf.Lerp(1.18f, 0.82f, normalizedCenterDistance);
                    weight *= resolvedObstacleNeighbors >= 1 ? 1.12f : 0.8f;
                    return weight;
                default:
                    return weight;
            }
        }

        private float GetTargetRatioBias(SemanticArchetype archetype, Dictionary<SemanticArchetype, int> resolvedCounts) {
            if (!_targetRatios.TryGetValue(archetype, out var targetRatio) || targetRatio <= 0f || archetype.IsBoundary()) return 1f;

            var targetCount = targetRatio * _interiorCellCount;
            if (targetCount <= 0f) return 1f;

            var resolvedCount = resolvedCounts.TryGetValue(archetype, out var count) ? count : 0;
            var projectedNormalized = (resolvedCount + 1f) / targetCount;
            if (projectedNormalized <= 1f) return Mathf.Lerp(1.18f, 1f, projectedNormalized);
            return Mathf.Lerp(1f, 0.82f, Mathf.Clamp01((projectedNormalized - 1f) / 1.5f));
        }

        private float GetOpenCoverageBias(SemanticArchetype archetype, ResolvedInteriorStats resolvedStats) {
            if (archetype.IsBoundary()) return 1f;

            var remainingCells = Mathf.Max(1, resolvedStats.RemainingInteriorCells(_interiorCellCount));
            var targetOpenCount = _config.TargetOpenCoverage * _interiorCellCount;
            var openNeeded = Mathf.Clamp(targetOpenCount - resolvedStats.ResolvedOpenCells, 0f, remainingCells);
            var requiredOpenShare = Mathf.Clamp01(openNeeded / remainingCells);
            var extremity = Mathf.Clamp01(Mathf.Abs(_config.TargetOpenCoverage - 0.5f) / 0.4f);
            var minBias = Mathf.Lerp(0.38f, 0.14f, extremity);
            var maxBias = Mathf.Lerp(2.35f, 4.5f, extremity);

            return archetype.IsOpenLike()
                ? Mathf.Lerp(minBias, maxBias, requiredOpenShare)
                : Mathf.Lerp(maxBias, minBias, requiredOpenShare);
        }

        private float GetConfiguredDensityWeight(SemanticArchetype archetype) {
            if (!archetype.IsObstacle()) return 1f;

            var semanticClass = archetype.GetObstacleSemanticClass();
            var densityRatio = _config.GetDenseRatio(semanticClass);
            return archetype.GetDensityBand() == SemanticDensityBand.Dense
                ? Mathf.Lerp(0.35f, 1.75f, densityRatio)
                : Mathf.Lerp(1.75f, 0.35f, densityRatio);
        }

        private int CountResolvedNeighborFamily(GridCoord2D selected, ulong[] masks, int width, int height, bool matchOpen) {
            var count = 0;
            foreach (var (_, dx, dz) in EnumerateNeighbors()) {
                var nx = selected.X + dx;
                var nz = selected.Z + dz;
                if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;

                var mask = masks[GetIndex(nx, nz, width)];
                if (CountBits(mask) != 1) continue;

                var archetype = ResolveSingle(mask);
                if (archetype.IsOpenLike() == matchOpen) count++;
            }

            return count;
        }

        private IEnumerable<SemanticArchetype> Expand(ulong mask) {
            for (var i = 0; i < _domainTypes.Length; i++) {
                var bit = 1UL << i;
                if ((mask & bit) != 0UL) yield return _domainTypes[i];
            }
        }

        private SemanticArchetype ResolveSingle(ulong mask) {
            foreach (var archetype in Expand(mask)) return archetype;
            throw new InvalidOperationException("Cannot resolve an empty semantic domain.");
        }

        private ResolvedInteriorStats BuildResolvedStats(ulong[] masks, int width, int depth) {
            var counts = new Dictionary<SemanticArchetype, int>();
            var resolvedInteriorCells = 0;
            var resolvedOpenCells = 0;
            for (var x = 1; x < width - 1; x++) {
                for (var z = 1; z < depth - 1; z++) {
                    var mask = masks[GetIndex(x, z, width)];
                    if (CountBits(mask) != 1) continue;

                    var archetype = ResolveSingle(mask);
                    counts[archetype] = counts.TryGetValue(archetype, out var count) ? count + 1 : 1;
                    resolvedInteriorCells++;
                    if (archetype.IsOpenLike()) resolvedOpenCells++;
                }
            }

            return new ResolvedInteriorStats(counts, resolvedInteriorCells, resolvedOpenCells);
        }

        private ulong GetForcedMask(int x, int z, int width, int depth) {
            var onBoundary = x == 0 || x == width - 1 || z == 0 || z == depth - 1;
            var onCorner = (x == 0 || x == width - 1) && (z == 0 || z == depth - 1);
            if (onCorner) return MaskOf(SemanticArchetype.BoundaryCorner);
            if (onBoundary) return MaskOf(SemanticArchetype.BoundaryWall);
            return _allInteriorMask;
        }

        private ulong BuildInteriorMask() {
            var mask = 0UL;
            foreach (var definition in _tileSet.GetDefinitions()) {
                if (!definition.BoundaryOnly) mask |= MaskOf(definition.Archetype);
            }

            return mask;
        }

        private ulong MaskOf(SemanticArchetype archetype) {
            return 1UL << _indices[archetype];
        }

        private static int CountBits(ulong value) {
            var count = 0;
            while (value != 0UL) {
                value &= value - 1UL;
                count++;
            }

            return count;
        }

        private static IEnumerable<(Direction2D direction, int dx, int dz)> EnumerateNeighbors() {
            yield return (Direction2D.East, 1, 0);
            yield return (Direction2D.West, -1, 0);
            yield return (Direction2D.North, 0, 1);
            yield return (Direction2D.South, 0, -1);
        }

        private static int GetIndex(int x, int z, int width) {
            return (z * width) + x;
        }
    }
}
