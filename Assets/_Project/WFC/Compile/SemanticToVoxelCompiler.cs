using System;
using System.Collections.Generic;
using System.Linq;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Semantic;

namespace WFCTechTest.WFC.Compile
{
    /// <summary>
    /// @file SemanticToVoxelCompiler.cs
    /// @brief Expands solved semantic archetypes into a cube-based 48x8x48 voxel blockout.
    /// </summary>
    public sealed class SemanticToVoxelCompiler
    {
        private readonly GenerationConfigAsset _config;
        private readonly Dictionary<SemanticArchetype, SemanticArchetypeDefinition> _definitions;

        /// <summary>
        /// Initializes a compiler instance.
        /// </summary>
        public SemanticToVoxelCompiler(GenerationConfigAsset config, SemanticTileSetAsset tileSet)
        {
            _config = config;
            _definitions = tileSet.GetDefinitions().ToDictionary(definition => definition.Archetype, definition => definition);
        }

        /// <summary>
        /// Compiles a solved semantic grid into voxel occupancy.
        /// </summary>
        public CompileResult Compile(SemanticGrid2D grid, int seed)
        {
            var map = new VoxelOccupancyMap(_config.Width, _config.Height, _config.Depth);
            var result = new CompileResult(map, grid);
            FillGround(map);
            FillBoundaries(map);

            var occupiedAnchors = new HashSet<GridCoord2D>();

            foreach (var coord in grid.EnumerateCoords())
            {
                if (IsBoundary(coord.X, coord.Z))
                {
                    continue;
                }

                if (occupiedAnchors.Contains(coord))
                {
                    continue;
                }

                var archetype = grid.Get(coord.X, coord.Z);
                switch (archetype)
                {
                    case SemanticArchetype.Open:
                        break;
                    case SemanticArchetype.InterestAnchor:
                        result.InterestAnchors.Add(new GridCoord3D(coord.X, 1, coord.Z));
                        break;
                    case SemanticArchetype.LowCover1x1:
                        PlaceSingle(map, coord, archetype, occupiedAnchors);
                        break;
                    case SemanticArchetype.LowCover1x2:
                        PlaceStrip(map, grid, coord, archetype, occupiedAnchors, result, SemanticArchetype.LowCover1x1);
                        break;
                    case SemanticArchetype.HighCover1x1:
                        PlaceSingle(map, coord, archetype, occupiedAnchors);
                        break;
                    case SemanticArchetype.HighCover1x2:
                        PlaceStrip(map, grid, coord, archetype, occupiedAnchors, result, SemanticArchetype.HighCover1x1);
                        break;
                    case SemanticArchetype.Tower1x1:
                        PlaceSingle(map, coord, archetype, occupiedAnchors);
                        break;
                    case SemanticArchetype.Block2x2:
                        PlaceBlock2x2(map, grid, coord, occupiedAnchors, result);
                        break;
                }
            }

            return result;
        }

        private void FillGround(VoxelOccupancyMap map)
        {
            for (var x = 0; x < map.Width; x++)
            {
                for (var z = 0; z < map.Depth; z++)
                {
                    map.SetCell(x, 0, z, VoxelCellKind.Floor);
                }
            }
        }

        private void FillBoundaries(VoxelOccupancyMap map)
        {
            for (var x = 0; x < map.Width; x++)
            {
                for (var z = 0; z < map.Depth; z++)
                {
                    if (!IsBoundary(x, z))
                    {
                        continue;
                    }

                    for (var y = 1; y <= _config.BoundaryWallHeight; y++)
                    {
                        map.SetCell(x, y, z, VoxelCellKind.Wall);
                    }
                }
            }
        }

        private void PlaceSingle(VoxelOccupancyMap map, GridCoord2D coord, SemanticArchetype archetype, HashSet<GridCoord2D> occupiedAnchors)
        {
            var definition = _definitions[archetype];
            PlaceColumn(map, coord.X, coord.Z, definition.Height, ToVoxelKind(archetype));
            occupiedAnchors.Add(coord);
        }

        private void PlaceStrip(VoxelOccupancyMap map, SemanticGrid2D grid, GridCoord2D origin, SemanticArchetype archetype, HashSet<GridCoord2D> occupiedAnchors, CompileResult result, SemanticArchetype fallback)
        {
            var definition = _definitions[archetype];
            if (TryFindStripReservation(grid, origin, archetype, occupiedAnchors, out var second))
            {
                PlaceColumn(map, origin.X, origin.Z, definition.Height, ToVoxelKind(archetype));
                PlaceColumn(map, second.X, second.Z, definition.Height, ToVoxelKind(archetype));
                occupiedAnchors.Add(origin);
                occupiedAnchors.Add(second);
                return;
            }

            result.DegradedFootprints++;
            PlaceFallback(origin, fallback, map);
            occupiedAnchors.Add(origin);
        }

        private void PlaceBlock2x2(VoxelOccupancyMap map, SemanticGrid2D grid, GridCoord2D origin, HashSet<GridCoord2D> occupiedAnchors, CompileResult result)
        {
            var definition = _definitions[SemanticArchetype.Block2x2];
            if (!TryFindBlockReservation(grid, origin, occupiedAnchors, out var cells))
            {
                result.DegradedFootprints++;
                PlaceColumn(map, origin.X, origin.Z, _definitions[SemanticArchetype.HighCover1x1].Height, VoxelCellKind.HighCover);
                occupiedAnchors.Add(origin);
                return;
            }

            foreach (var cell in cells)
            {
                PlaceColumn(map, cell.X, cell.Z, definition.Height, VoxelCellKind.HighCover);
                occupiedAnchors.Add(cell);
            }
        }

        private void PlaceFallback(GridCoord2D coord, SemanticArchetype fallback, VoxelOccupancyMap map)
        {
            switch (fallback)
            {
                case SemanticArchetype.LowCover1x1:
                    PlaceColumn(map, coord.X, coord.Z, _definitions[fallback].Height, VoxelCellKind.LowCover);
                    break;
                default:
                    PlaceColumn(map, coord.X, coord.Z, _definitions[fallback].Height, VoxelCellKind.HighCover);
                    break;
            }
        }

        private void PlaceColumn(VoxelOccupancyMap map, int x, int z, int height, VoxelCellKind kind)
        {
            for (var y = 1; y <= height; y++)
            {
                if (map.IsInBounds(x, y, z))
                {
                    map.SetCell(x, y, z, kind);
                }
            }
        }

        private bool CanOccupy(int x, int z, HashSet<GridCoord2D> occupiedAnchors)
        {
            return !IsBoundary(x, z) && x >= 1 && z >= 1 && x < _config.Width - 1 && z < _config.Depth - 1 && !occupiedAnchors.Contains(new GridCoord2D(x, z));
        }

        private bool TryFindStripReservation(SemanticGrid2D grid, GridCoord2D origin, SemanticArchetype archetype, HashSet<GridCoord2D> occupiedAnchors, out GridCoord2D second)
        {
            var east = origin.Offset(1, 0);
            var north = origin.Offset(0, 1);
            var candidates = new List<GridCoord2D>();
            if (CanReserveStripCell(grid, archetype, east, occupiedAnchors))
            {
                candidates.Add(east);
            }

            if (CanReserveStripCell(grid, archetype, north, occupiedAnchors))
            {
                candidates.Add(north);
            }

            if (candidates.Count == 0)
            {
                second = default;
                return false;
            }

            second = candidates
                .OrderByDescending(candidate => ScoreStripReservation(grid, archetype, candidate))
                .ThenBy(candidate => candidate.Z)
                .ThenBy(candidate => candidate.X)
                .First();
            return true;
        }

        private bool TryFindBlockReservation(SemanticGrid2D grid, GridCoord2D origin, HashSet<GridCoord2D> occupiedAnchors, out GridCoord2D[] cells)
        {
            cells = new[]
            {
                origin,
                origin.Offset(1, 0),
                origin.Offset(0, 1),
                origin.Offset(1, 1)
            };

            foreach (var cell in cells)
            {
                if (!CanReserveBlockCell(grid, cell, occupiedAnchors))
                {
                    cells = Array.Empty<GridCoord2D>();
                    return false;
                }
            }

            return true;
        }

        private bool CanReserveStripCell(SemanticGrid2D grid, SemanticArchetype source, GridCoord2D candidate, HashSet<GridCoord2D> occupiedAnchors)
        {
            if (!CanOccupy(candidate.X, candidate.Z, occupiedAnchors))
            {
                return false;
            }

            var target = grid.Get(candidate.X, candidate.Z);
            return source switch
            {
                SemanticArchetype.LowCover1x2 => target is SemanticArchetype.LowCover1x1 or SemanticArchetype.LowCover1x2,
                SemanticArchetype.HighCover1x2 => target is SemanticArchetype.HighCover1x1 or SemanticArchetype.HighCover1x2 or SemanticArchetype.LowCover1x1,
                _ => false
            };
        }

        private bool CanReserveBlockCell(SemanticGrid2D grid, GridCoord2D candidate, HashSet<GridCoord2D> occupiedAnchors)
        {
            if (!CanOccupy(candidate.X, candidate.Z, occupiedAnchors))
            {
                return false;
            }

            var target = grid.Get(candidate.X, candidate.Z);
            return target is SemanticArchetype.Block2x2 or SemanticArchetype.HighCover1x1 or SemanticArchetype.HighCover1x2;
        }

        private int ScoreStripReservation(SemanticGrid2D grid, SemanticArchetype source, GridCoord2D candidate)
        {
            var target = grid.Get(candidate.X, candidate.Z);
            return source switch
            {
                SemanticArchetype.LowCover1x2 when target == SemanticArchetype.LowCover1x2 => 3,
                SemanticArchetype.LowCover1x2 when target == SemanticArchetype.LowCover1x1 => 2,
                SemanticArchetype.HighCover1x2 when target == SemanticArchetype.HighCover1x2 => 4,
                SemanticArchetype.HighCover1x2 when target == SemanticArchetype.HighCover1x1 => 3,
                SemanticArchetype.HighCover1x2 when target == SemanticArchetype.LowCover1x1 => 1,
                _ => 0
            };
        }

        private bool IsBoundary(int x, int z)
        {
            return x == 0 || z == 0 || x == _config.Width - 1 || z == _config.Depth - 1;
        }

        private static VoxelCellKind ToVoxelKind(SemanticArchetype archetype)
        {
            return archetype switch
            {
                SemanticArchetype.LowCover1x1 => VoxelCellKind.LowCover,
                SemanticArchetype.LowCover1x2 => VoxelCellKind.LowCover,
                SemanticArchetype.HighCover1x1 => VoxelCellKind.HighCover,
                SemanticArchetype.HighCover1x2 => VoxelCellKind.HighCover,
                SemanticArchetype.Tower1x1 => VoxelCellKind.Tower,
                _ => VoxelCellKind.HighCover
            };
        }
    }
}
