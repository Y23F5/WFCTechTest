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
        private readonly PrefabRegistryAsset _prefabRegistry;

        /// <summary>
        /// Initializes a compiler instance.
        /// </summary>
        public SemanticToVoxelCompiler(GenerationConfigAsset config, SemanticTileSetAsset tileSet, PrefabRegistryAsset prefabRegistry = null)
        {
            _config = config;
            _prefabRegistry = prefabRegistry;
        }

        /// <summary>
        /// Compiles a solved semantic grid into voxel occupancy.
        /// </summary>
        public CompileResult Compile(SemanticGrid2D grid, int seed)
        {
            var map = new VoxelOccupancyMap(_config.Width, _config.Height, _config.Depth);
            var result = new CompileResult(map, grid) { Seed = seed };
            FillGround(map);
            FillBoundaries(map);

            foreach (var coord in grid.EnumerateCoords())
            {
                if (IsBoundary(coord.X, coord.Z))
                {
                    continue;
                }

                var archetype = grid.Get(coord.X, coord.Z);
                if (archetype == SemanticArchetype.InterestAnchor)
                {
                    result.InterestAnchors.Add(new GridCoord3D(coord.X, 1, coord.Z));
                }
            }

            PlanAndBakeObstaclePlacements(grid, result);

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

        private void PlanAndBakeObstaclePlacements(SemanticGrid2D grid, CompileResult result)
        {
            if (_prefabRegistry == null)
            {
                return;
            }

            var planner = new PrefabRegistryPlacementPlanner(_config, _prefabRegistry, seed: result.Seed);
            var placements = planner.Plan(grid, out var degradedPlacements);
            result.DegradedFootprints = degradedPlacements;
            foreach (var placement in placements)
            {
                result.ObstaclePlacements.Add(placement);
                BakePlacement(result.Volume, placement);
            }
        }

        private static void BakePlacement(VoxelOccupancyMap map, ObstaclePlacement placement)
        {
            var kind = placement.SemanticClass switch
            {
                ObstacleSemanticClass.LowCover => VoxelCellKind.LowCover,
                ObstacleSemanticClass.HighCover => VoxelCellKind.HighCover,
                ObstacleSemanticClass.Blocker => VoxelCellKind.Blocker,
                _ => VoxelCellKind.Tower
            };

            foreach (var cell in placement.OccupiedCells)
            {
                for (var y = 1; y <= placement.Height; y++)
                {
                    if (map.IsInBounds(cell.X, y, cell.Z))
                    {
                        map.SetCell(cell.X, y, cell.Z, kind);
                    }
                }
            }
        }

        private bool IsBoundary(int x, int z)
        {
            return x == 0 || z == 0 || x == _config.Width - 1 || z == _config.Depth - 1;
        }
    }
}
