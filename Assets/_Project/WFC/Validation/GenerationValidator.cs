using System.Collections.Generic;
using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;
using WFCTechTest.WFC.Semantic;

namespace WFCTechTest.WFC.Validation
{
    /// <summary>
    /// @file GenerationValidator.cs
    /// @brief Applies structural, coverage, connectivity, and interest-anchor validation to compiled outputs.
    /// </summary>
    public sealed class GenerationValidator
    {
        private readonly GenerationConfigAsset _config;
        private readonly ConnectivityValidator _connectivityValidator = new ConnectivityValidator();

        /// <summary>
        /// Initializes a validator instance.
        /// </summary>
        public GenerationValidator(GenerationConfigAsset config)
        {
            _config = config;
        }

        /// <summary>
        /// Validates a compile result and updates the supplied report.
        /// </summary>
        public bool Validate(CompileResult compileResult, GenerationReport report)
        {
            if (!ValidateBoundary(compileResult.Volume))
            {
                report.FailureReason = GenerationFailureReason.BoundaryViolation;
                report.Message = "Boundary wall validation failed.";
                return false;
            }

            report.DegradedFootprintCount = compileResult.DegradedFootprints;
            if (compileResult.DegradedFootprints > GetAllowedDegradedFootprintCount(compileResult))
            {
                report.FailureReason = GenerationFailureReason.FootprintConflict;
                report.Message = "Too many obstacle placements had no legal Prefab Registry entry and fell back during compilation.";
                return false;
            }

            report.GroundStandableCells = MovementRules.CountGroundStandableCells(compileResult.Volume);
            report.InteriorGroundStandableCells = CountInteriorGroundStandableCells(compileResult.Volume);
            report.InteriorGroundCandidateCells = GetInteriorGroundCandidateCellCount(compileResult.Volume);
            report.TotalStandableCells = MovementRules.CountAllStandableCells(compileResult.Volume);
            report.CoverageMetricName = _config.CoverageMetric.ToString();
            report.GroundCoverageRatio = ComputeCoverageRatio(compileResult, report);
            report.OpenCoverageTarget = _config.TargetOpenCoverage;
            report.OpenCoverageTolerance = _config.OpenCoverageTolerance;
            report.OpenCoverageActual = report.GroundCoverageRatio;
            report.OpenCoverageDelta = report.OpenCoverageActual - report.OpenCoverageTarget;
            report.TargetObstacleFill = _config.TargetObstacleFill;
            report.ActualObstacleFill = 1f - report.OpenCoverageActual;
            PopulateSemanticDensityMetrics(compileResult, report);

            if (report.GroundCoverageRatio < _config.MinGroundCoverage || report.GroundCoverageRatio > _config.MaxGroundCoverage)
            {
                report.FailureReason = GenerationFailureReason.CoverageOutOfRange;
                report.Message = $"Open coverage target {_config.TargetOpenCoverage:P1} ± {_config.OpenCoverageTolerance:P1} produced {report.OpenCoverageActual:P1} ({report.OpenCoverageDelta:+0.0%;-0.0%;0.0%}).";
                return false;
            }

            report.LargestComponentRatio = _connectivityValidator.ComputeLargestComponentRatio(compileResult.Volume, _config.MaxJumpHeight, _config.MaxJumpDistance);
            if (report.LargestComponentRatio < _config.MinLargestComponentRatio)
            {
                report.FailureReason = GenerationFailureReason.ConnectivityFailure;
                report.Message = $"Largest reachable component ratio {report.LargestComponentRatio:P1} is too low.";
                return false;
            }

            report.InterestAnchorPositions.AddRange(compileResult.InterestAnchors);
            report.PlacedInterestAnchorCount = compileResult.InterestAnchors.Count;
            report.InterestAnchorCount = compileResult.InterestAnchors.Count;
            foreach (var anchor in compileResult.InterestAnchors)
            {
                if (!MovementRules.IsStandable(compileResult.Volume, anchor.X, anchor.Y, anchor.Z))
                {
                    report.FailureReason = GenerationFailureReason.InterestAnchorFailure;
                    report.Message = $"Interest anchor at {anchor} is not standable.";
                    return false;
                }
            }

            report.Success = true;
            report.FailureReason = GenerationFailureReason.None;
            report.Message = "Generation succeeded.";
            return true;
        }

        private bool ValidateBoundary(VoxelOccupancyMap map)
        {
            for (var x = 0; x < map.Width; x++)
            {
                for (var z = 0; z < map.Depth; z++)
                {
                    if (x != 0 && z != 0 && x != map.Width - 1 && z != map.Depth - 1)
                    {
                        continue;
                    }

                    for (var y = 1; y <= _config.BoundaryWallHeight; y++)
                    {
                        if (map.GetCell(x, y, z) != VoxelCellKind.Wall)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static int CountInteriorGroundStandableCells(VoxelOccupancyMap map)
        {
            var count = 0;
            for (var x = 1; x < map.Width - 1; x++)
            {
                for (var z = 1; z < map.Depth - 1; z++)
                {
                    if (MovementRules.IsStandable(map, x, 1, z))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int GetInteriorGroundCandidateCellCount(VoxelOccupancyMap map)
        {
            return (map.Width - 2) * (map.Depth - 2);
        }

        private static int GetAllowedDegradedFootprintCount(CompileResult compileResult)
        {
            var interiorCellCount = (compileResult.SemanticGrid.Width - 2) * (compileResult.SemanticGrid.Depth - 2);
            return (int)(interiorCellCount * 0.12f);
        }

        private float ComputeCoverageRatio(CompileResult compileResult, GenerationReport report)
        {
            var numerator = _config.CoverageMetric switch
            {
                CoverageMetricMode.InteriorFloorOnly => CountInteriorFloorSemanticCells(compileResult),
                _ => report.InteriorGroundStandableCells
            };

            return report.InteriorGroundCandidateCells > 0
                ? numerator / (float)report.InteriorGroundCandidateCells
                : 0f;
        }

        private static int CountInteriorFloorSemanticCells(CompileResult compileResult)
        {
            var count = 0;
            for (var x = 1; x < compileResult.SemanticGrid.Width - 1; x++)
            {
                for (var z = 1; z < compileResult.SemanticGrid.Depth - 1; z++)
                {
                    var archetype = compileResult.SemanticGrid.Get(x, z);
                    if (archetype.IsOpenLike())
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void PopulateSemanticDensityMetrics(CompileResult compileResult, GenerationReport report)
        {
            var obstacleCells = 0;
            var singleCellObstacleCells = 0;
            var tallObstacleCells = 0;
            var occupied = new HashSet<GridCoord2D>();
            var classCounts = new Dictionary<ObstacleSemanticClass, int>();
            var denseCounts = new Dictionary<ObstacleSemanticClass, int>();

            foreach (var placement in compileResult.ObstaclePlacements)
            {
                var placementCells = placement.OccupiedCells.Count;
                obstacleCells += placementCells;
                foreach (var cell in placement.OccupiedCells)
                {
                    occupied.Add(cell);
                }

                if (placement.FootprintWidth * placement.FootprintDepth == 1)
                {
                    singleCellObstacleCells += placementCells;
                }

                if (placement.Height >= 2)
                {
                    tallObstacleCells += placementCells;
                }

                if (placement.SemanticClass != ObstacleSemanticClass.None)
                {
                    classCounts[placement.SemanticClass] = classCounts.TryGetValue(placement.SemanticClass, out var classCount) ? classCount + 1 : 1;
                    if (placement.DensityBand == SemanticDensityBand.Dense)
                    {
                        denseCounts[placement.SemanticClass] = denseCounts.TryGetValue(placement.SemanticClass, out var denseCount) ? denseCount + 1 : 1;
                    }
                }
            }

            var openCells = 0;
            var visited = new HashSet<GridCoord2D>();
            var largestOpenRegion = 0;
            for (var x = 1; x < compileResult.SemanticGrid.Width - 1; x++)
            {
                for (var z = 1; z < compileResult.SemanticGrid.Depth - 1; z++)
                {
                    var coord = new GridCoord2D(x, z);
                    if (occupied.Contains(coord))
                    {
                        continue;
                    }

                    openCells++;
                    if (visited.Add(coord))
                    {
                        var region = FloodOpenRegion(compileResult.SemanticGrid, occupied, coord, visited);
                        if (region > largestOpenRegion)
                        {
                            largestOpenRegion = region;
                        }
                    }
                }
            }

            report.ObstacleFillRatio = report.InteriorGroundCandidateCells > 0 ? obstacleCells / (float)report.InteriorGroundCandidateCells : 0f;
            report.SingleCellObstacleRatio = obstacleCells > 0 ? singleCellObstacleCells / (float)obstacleCells : 0f;
            report.TallObstacleRatio = obstacleCells > 0 ? tallObstacleCells / (float)obstacleCells : 0f;
            report.LargestOpenAreaRatio = openCells > 0 ? largestOpenRegion / (float)openCells : 0f;
            report.InterestAnchorCount = compileResult.InterestAnchors.Count;

            foreach (var semanticClass in new[]
                     {
                         ObstacleSemanticClass.LowCover,
                         ObstacleSemanticClass.HighCover,
                         ObstacleSemanticClass.Tower,
                         ObstacleSemanticClass.Blocker
                     })
            {
                var count = classCounts.TryGetValue(semanticClass, out var classCount) ? classCount : 0;
                var dense = denseCounts.TryGetValue(semanticClass, out var denseCount) ? denseCount : 0;
                report.ObstacleClassCounts[semanticClass] = count;
                report.ObstacleDenseRatios[semanticClass] = count > 0 ? dense / (float)count : 0f;
            }
        }

        private static int FloodOpenRegion(SemanticGrid2D grid, HashSet<GridCoord2D> occupied, GridCoord2D start, HashSet<GridCoord2D> visited)
        {
            var count = 0;
            var queue = new Queue<GridCoord2D>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                count++;

                foreach (var next in EnumerateOpenNeighbors(grid, occupied, current))
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return count;
        }

        private static IEnumerable<GridCoord2D> EnumerateOpenNeighbors(SemanticGrid2D grid, HashSet<GridCoord2D> occupied, GridCoord2D coord)
        {
            var offsets = new[]
            {
                new GridCoord2D(1, 0),
                new GridCoord2D(-1, 0),
                new GridCoord2D(0, 1),
                new GridCoord2D(0, -1)
            };

            foreach (var offset in offsets)
            {
                var next = coord.Offset(offset.X, offset.Z);
                if (next.X <= 0 || next.X >= grid.Width - 1 || next.Z <= 0 || next.Z >= grid.Depth - 1)
                {
                    continue;
                }

                if (!occupied.Contains(next))
                {
                    yield return next;
                }
            }
        }
    }
}
