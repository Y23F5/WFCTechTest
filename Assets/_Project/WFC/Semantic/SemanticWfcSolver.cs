using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;

namespace WFCTechTest.WFC.Semantic
{
    /// <summary>
    /// @file SemanticWfcSolver.cs
    /// @brief Solves the semantic 2D WFC layer with deterministic seeded randomness, hard boundary constraints, and restart-friendly diagnostics.
    /// </summary>
    public sealed class SemanticWfcSolver : IWfcSolver<SemanticGrid2D>
    {
        private readonly GenerationConfigAsset _config;
        private readonly SemanticTileSetAsset _tileSet;
        private readonly SemanticAdjacencyRules _rules;
        private readonly SemanticArchetype[] _domainTypes;
        private readonly Dictionary<SemanticArchetype, float> _weights;
        private readonly Dictionary<SemanticArchetype, int> _indices;
        private readonly ulong _allInteriorMask;

        /// <summary>
        /// Initializes a semantic solver instance.
        /// </summary>
        public SemanticWfcSolver(GenerationConfigAsset config, SemanticTileSetAsset tileSet)
        {
            _config = config;
            _tileSet = tileSet;
            _rules = new SemanticAdjacencyRules(tileSet);
            _domainTypes = tileSet.GetDefinitions().Select(definition => definition.Archetype).ToArray();
            _weights = tileSet.GetDefinitions().ToDictionary(definition => definition.Archetype, definition => definition.Weight);
            _indices = _domainTypes.Select((archetype, index) => (archetype, index)).ToDictionary(pair => pair.archetype, pair => pair.index);
            _allInteriorMask = BuildInteriorMask();
        }

        /// <inheritdoc />
        public bool TrySolve(int seed, GenerationReport report, out SemanticGrid2D state)
        {
            state = null;
            var width = _config.Width;
            var depth = _config.Depth;
            var random = new System.Random(seed);
            var masks = new ulong[width * depth];
            var queue = new Queue<GridCoord2D>();

            for (var x = 0; x < width; x++)
            {
                for (var z = 0; z < depth; z++)
                {
                    masks[GetIndex(x, z, width)] = GetForcedMask(x, z, width, depth);
                }
            }

            ForceInterestAnchors(masks, queue, width, depth);

            while (true)
            {
                if (!Propagate(masks, queue, width, depth, report, out var contradiction))
                {
                    report.FailureReason = GenerationFailureReason.SemanticContradiction;
                    report.Message = $"Contradiction at semantic cell {contradiction}.";
                    return false;
                }

                if (!TrySelectCell(masks, width, depth, random, out var selected))
                {
                    state = BuildGrid(masks, width, depth, report);
                    return true;
                }

                report.ObservationCount++;
                var selectedIndex = GetIndex(selected.X, selected.Z, width);
                var chosen = PickWeightedArchetype(masks[selectedIndex], selected, masks, width, depth, random);
                masks[selectedIndex] = MaskOf(chosen);
                queue.Enqueue(selected);
            }
        }

        private SemanticGrid2D BuildGrid(ulong[] masks, int width, int depth, GenerationReport report)
        {
            var grid = new SemanticGrid2D(width, depth);
            foreach (var archetype in _domainTypes)
            {
                report.SemanticCounts[archetype] = 0;
            }

            for (var x = 0; x < width; x++)
            {
                for (var z = 0; z < depth; z++)
                {
                    var archetype = ResolveSingle(masks[GetIndex(x, z, width)]);
                    grid.Set(x, z, archetype);
                    report.SemanticCounts[archetype]++;
                }
            }

            return grid;
        }

        private void ForceInterestAnchors(ulong[] masks, Queue<GridCoord2D> queue, int width, int depth)
        {
            if (_config.InterestAnchorCount <= 0)
            {
                return;
            }

            var anchors = new[]
            {
                new GridCoord2D(width / 4, depth / 4),
                new GridCoord2D((3 * width) / 4, depth / 4),
                new GridCoord2D(width / 4, (3 * depth) / 4),
                new GridCoord2D((3 * width) / 4, (3 * depth) / 4)
            };

            var count = Math.Min(_config.InterestAnchorCount, anchors.Length);
            for (var i = 0; i < count; i++)
            {
                var anchor = anchors[i];
                masks[GetIndex(anchor.X, anchor.Z, width)] = MaskOf(SemanticArchetype.InterestAnchor);
                queue.Enqueue(anchor);
            }
        }

        private bool Propagate(ulong[] masks, Queue<GridCoord2D> queue, int width, int depth, GenerationReport report, out GridCoord2D contradiction)
        {
            contradiction = default;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentMask = masks[GetIndex(current.X, current.Z, width)];

                foreach (var (direction, dx, dz) in EnumerateNeighbors())
                {
                    var nx = current.X + dx;
                    var nz = current.Z + dz;
                    if (nx < 0 || nx >= width || nz < 0 || nz >= depth)
                    {
                        continue;
                    }

                    var neighborIndex = GetIndex(nx, nz, width);
                    var before = masks[neighborIndex];
                    var after = FilterMask(before, currentMask);
                    if (after == 0UL)
                    {
                        contradiction = new GridCoord2D(nx, nz);
                        return false;
                    }

                    if (after != before)
                    {
                        masks[neighborIndex] = after;
                        queue.Enqueue(new GridCoord2D(nx, nz));
                        report.PropagationCount++;
                    }
                }
            }

            return true;
        }

        private bool TrySelectCell(ulong[] masks, int width, int depth, System.Random random, out GridCoord2D selected)
        {
            selected = default;
            var bestEntropy = double.MaxValue;
            var found = false;

            for (var x = 0; x < width; x++)
            {
                for (var z = 0; z < depth; z++)
                {
                    var mask = masks[GetIndex(x, z, width)];
                    var count = CountBits(mask);
                    if (count <= 1)
                    {
                        continue;
                    }

                    var entropy = ComputeEntropy(mask) + random.NextDouble() * 0.00001d;
                    if (entropy < bestEntropy)
                    {
                        bestEntropy = entropy;
                        selected = new GridCoord2D(x, z);
                        found = true;
                    }
                }
            }

            return found;
        }

        private SemanticArchetype PickWeightedArchetype(ulong mask, GridCoord2D selected, ulong[] masks, int width, int depth, System.Random random)
        {
            var total = 0f;
            foreach (var archetype in Expand(mask))
            {
                total += GetSelectionWeight(archetype, selected, masks, width, depth);
            }

            var pick = (float)random.NextDouble() * total;
            foreach (var archetype in Expand(mask))
            {
                pick -= GetSelectionWeight(archetype, selected, masks, width, depth);
                if (pick <= 0f)
                {
                    return archetype;
                }
            }

            return ResolveSingle(mask);
        }

        private ulong FilterMask(ulong candidateMask, ulong sourceMask)
        {
            var filtered = 0UL;
            foreach (var candidate in Expand(candidateMask))
            {
                foreach (var source in Expand(sourceMask))
                {
                    if (_rules.IsAllowed(candidate, source))
                    {
                        filtered |= MaskOf(candidate);
                        break;
                    }
                }
            }

            return filtered;
        }

        private double ComputeEntropy(ulong mask)
        {
            var total = 0f;
            var weightedLog = 0d;
            foreach (var archetype in Expand(mask))
            {
                var weight = _weights[archetype];
                total += weight;
                weightedLog += weight * Math.Log(Math.Max(weight, 0.0001f));
            }

            return Math.Log(total) - (weightedLog / total);
        }

        private float GetSelectionWeight(SemanticArchetype archetype, GridCoord2D selected, ulong[] masks, int width, int depth)
        {
            var weight = _weights[archetype];
            var center = new Vector2((width - 1) * 0.5f, (depth - 1) * 0.5f);
            var position = new Vector2(selected.X, selected.Z);
            var normalizedCenterDistance = Vector2.Distance(position, center) / Mathf.Max(1f, center.magnitude);
            var resolvedObstacleNeighbors = CountResolvedNeighborFamily(selected, masks, width, height: depth, matchOpen: false);
            var resolvedOpenNeighbors = CountResolvedNeighborFamily(selected, masks, width, height: depth, matchOpen: true);

            if (archetype == SemanticArchetype.Open || archetype == SemanticArchetype.InterestAnchor)
            {
                weight *= Mathf.Lerp(1.35f, 0.92f, normalizedCenterDistance);
                weight *= resolvedOpenNeighbors >= 2 ? 1.12f : 1f;
                weight *= resolvedObstacleNeighbors >= 3 ? 0.78f : 1f;
                return weight;
            }

            if (archetype is SemanticArchetype.LowCover1x1 or SemanticArchetype.HighCover1x1 or SemanticArchetype.Tower1x1)
            {
                weight *= Mathf.Lerp(0.72f, 1.18f, normalizedCenterDistance);
                weight *= resolvedObstacleNeighbors == 0 ? 0.52f : 1f;
                weight *= resolvedObstacleNeighbors == 1 ? 0.84f : 1f;
                weight *= resolvedOpenNeighbors >= 3 ? 0.78f : 1f;
                return weight;
            }

            if (archetype is SemanticArchetype.LowCover1x2 or SemanticArchetype.HighCover1x2 or SemanticArchetype.Block2x2)
            {
                weight *= Mathf.Lerp(0.86f, 1.16f, normalizedCenterDistance);
                weight *= resolvedObstacleNeighbors >= 1 ? 1.26f : 0.94f;
                weight *= resolvedObstacleNeighbors >= 2 ? 1.08f : 1f;
                return weight;
            }

            return weight;
        }

        private int CountResolvedNeighborFamily(GridCoord2D selected, ulong[] masks, int width, int height, bool matchOpen)
        {
            var count = 0;
            foreach (var (_, dx, dz) in EnumerateNeighbors())
            {
                var nx = selected.X + dx;
                var nz = selected.Z + dz;
                if (nx < 0 || nx >= width || nz < 0 || nz >= height)
                {
                    continue;
                }

                var mask = masks[GetIndex(nx, nz, width)];
                if (CountBits(mask) != 1)
                {
                    continue;
                }

                var archetype = ResolveSingle(mask);
                var isOpen = archetype == SemanticArchetype.Open || archetype == SemanticArchetype.InterestAnchor;
                if (isOpen == matchOpen)
                {
                    count++;
                }
            }

            return count;
        }

        private IEnumerable<SemanticArchetype> Expand(ulong mask)
        {
            for (var i = 0; i < _domainTypes.Length; i++)
            {
                var bit = 1UL << i;
                if ((mask & bit) != 0UL)
                {
                    yield return _domainTypes[i];
                }
            }
        }

        private SemanticArchetype ResolveSingle(ulong mask)
        {
            foreach (var archetype in Expand(mask))
            {
                return archetype;
            }

            throw new InvalidOperationException("Cannot resolve an empty semantic domain.");
        }

        private ulong GetForcedMask(int x, int z, int width, int depth)
        {
            var onBoundary = x == 0 || x == width - 1 || z == 0 || z == depth - 1;
            var onCorner = (x == 0 || x == width - 1) && (z == 0 || z == depth - 1);
            if (onCorner)
            {
                return MaskOf(SemanticArchetype.BoundaryCorner);
            }

            if (onBoundary)
            {
                return MaskOf(SemanticArchetype.BoundaryWall);
            }

            return _allInteriorMask;
        }

        private ulong BuildInteriorMask()
        {
            var mask = 0UL;
            foreach (var definition in _tileSet.GetDefinitions())
            {
                if (!definition.BoundaryOnly)
                {
                    mask |= MaskOf(definition.Archetype);
                }
            }

            return mask;
        }

        private ulong MaskOf(SemanticArchetype archetype)
        {
            return 1UL << _indices[archetype];
        }

        private static int CountBits(ulong value)
        {
            var count = 0;
            while (value != 0UL)
            {
                value &= value - 1UL;
                count++;
            }

            return count;
        }

        private static IEnumerable<(Direction2D direction, int dx, int dz)> EnumerateNeighbors()
        {
            yield return (Direction2D.East, 1, 0);
            yield return (Direction2D.West, -1, 0);
            yield return (Direction2D.North, 0, 1);
            yield return (Direction2D.South, 0, -1);
        }

        private static int GetIndex(int x, int z, int width)
        {
            return (z * width) + x;
        }
    }
}
