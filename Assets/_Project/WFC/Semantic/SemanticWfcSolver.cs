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
    public sealed partial class SemanticWfcSolver : IWfcSolver<SemanticGrid2D>
    {
        private readonly struct ResolvedInteriorStats
        {
            public ResolvedInteriorStats(Dictionary<SemanticArchetype, int> resolvedCounts, int resolvedInteriorCells, int resolvedOpenCells)
            {
                ResolvedCounts = resolvedCounts;
                ResolvedInteriorCells = resolvedInteriorCells;
                ResolvedOpenCells = resolvedOpenCells;
            }

            public Dictionary<SemanticArchetype, int> ResolvedCounts { get; }
            public int ResolvedInteriorCells { get; }
            public int ResolvedOpenCells { get; }
            public int ResolvedObstacleCells => Mathf.Max(0, ResolvedInteriorCells - ResolvedOpenCells);
            public int RemainingInteriorCells(int totalInteriorCells) => Mathf.Max(0, totalInteriorCells - ResolvedInteriorCells);
        }

        private readonly GenerationConfigAsset _config;
        private readonly SemanticTileSetAsset _tileSet;
        private readonly SemanticAdjacencyRules _rules;
        private readonly SemanticArchetype[] _domainTypes;
        private readonly Dictionary<SemanticArchetype, float> _weights;
        private readonly Dictionary<SemanticArchetype, float> _targetRatios;
        private readonly Dictionary<SemanticArchetype, int> _indices;
        private readonly ulong _allInteriorMask;
        private readonly int _interiorCellCount;

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
            _targetRatios = tileSet.GetDefinitions().ToDictionary(definition => definition.Archetype, definition => definition.TargetRatio);
            _indices = _domainTypes.Select((archetype, index) => (archetype, index)).ToDictionary(pair => pair.archetype, pair => pair.index);
            _allInteriorMask = BuildInteriorMask();
            _interiorCellCount = Mathf.Max(1, (_config.Width - 2) * (_config.Depth - 2));
        }

        /// <inheritdoc />
        public bool TrySolve(int seed, GenerationReport report, out SemanticGrid2D state)
        {
            state = null;
            report.RequestedInterestAnchorCount = _config.InterestAnchorCount;
            var lastContradiction = default(GridCoord2D);

            for (var anchorCount = _config.InterestAnchorCount; anchorCount >= 0; anchorCount--)
            {
                if (TrySolveWithAnchorCount(seed, report, anchorCount, out state, out lastContradiction))
                {
                    report.PlacedInterestAnchorCount = anchorCount;
                    report.InterestAnchorCount = anchorCount;
                    report.SoftConstraintDegradedCount = _config.InterestAnchorCount - anchorCount;
                    return true;
                }
            }

            report.PlacedInterestAnchorCount = 0;
            report.InterestAnchorCount = 0;
            report.SoftConstraintDegradedCount = _config.InterestAnchorCount;
            report.FailureReason = GenerationFailureReason.SemanticContradiction;
            report.Message = $"Contradiction at semantic cell {lastContradiction}.";
            return false;
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

        private bool TrySolveWithAnchorCount(int seed, GenerationReport report, int anchorCount, out SemanticGrid2D state, out GridCoord2D contradiction)
        {
            state = null;
            contradiction = default;
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

            EnqueueForcedBoundaryCells(queue, width, depth);
            ForceInterestAnchors(masks, queue, width, depth, anchorCount);

            while (true)
            {
                if (!Propagate(masks, queue, width, depth, report, out contradiction))
                {
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

        private void ForceInterestAnchors(ulong[] masks, Queue<GridCoord2D> queue, int width, int depth, int anchorCount)
        {
            if (anchorCount <= 0)
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

            var count = Math.Min(anchorCount, anchors.Length);
            for (var i = 0; i < count; i++)
            {
                var anchor = anchors[i];
                masks[GetIndex(anchor.X, anchor.Z, width)] = MaskOf(SemanticArchetype.InterestAnchor);
                queue.Enqueue(anchor);
            }
        }

        private static void EnqueueForcedBoundaryCells(Queue<GridCoord2D> queue, int width, int depth)
        {
            for (var x = 0; x < width; x++)
            {
                queue.Enqueue(new GridCoord2D(x, 0));
                queue.Enqueue(new GridCoord2D(x, depth - 1));
            }

            for (var z = 1; z < depth - 1; z++)
            {
                queue.Enqueue(new GridCoord2D(0, z));
                queue.Enqueue(new GridCoord2D(width - 1, z));
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
            var resolvedStats = BuildResolvedStats(masks, width, depth);
            var total = 0f;
            foreach (var archetype in Expand(mask))
            {
                total += GetSelectionWeight(archetype, selected, masks, width, depth, resolvedStats);
            }

            var pick = (float)random.NextDouble() * total;
            foreach (var archetype in Expand(mask))
            {
                pick -= GetSelectionWeight(archetype, selected, masks, width, depth, resolvedStats);
                if (pick <= 0f)
                {
                    return archetype;
                }
            }

            return ResolveSingle(mask);
        }

    }
}
