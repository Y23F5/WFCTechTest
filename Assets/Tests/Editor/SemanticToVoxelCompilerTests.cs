using NUnit.Framework;
using UnityEngine;
using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Semantic;

namespace WFCTechTest.WFC.Tests.Editor
{
    /// <summary>
    /// @file SemanticToVoxelCompilerTests.cs
    /// @brief Verifies hard floor, wall, and height mapping behavior in the semantic-to-voxel compiler.
    /// </summary>
    public sealed class SemanticToVoxelCompilerTests
    {
        /// <summary>
        /// Verifies that the compiler always writes the floor plane and boundary walls.
        /// </summary>
        [Test]
        public void Compile_WritesMandatoryFloorAndBoundaryWalls()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var grid = new SemanticGrid2D(config.Width, config.Depth);

            for (var x = 0; x < config.Width; x++)
            {
                for (var z = 0; z < config.Depth; z++)
                {
                    grid.Set(x, z, x == 0 || z == 0 || x == config.Width - 1 || z == config.Depth - 1 ? SemanticArchetype.BoundaryWall : SemanticArchetype.Open);
                }
            }

            var compiler = new SemanticToVoxelCompiler(config, tileSet);
            var result = compiler.Compile(grid, 99);

            Assert.That(result.Volume.GetCell(10, 0, 10), Is.EqualTo(VoxelCellKind.Floor));
            Assert.That(result.Volume.GetCell(0, 1, 10), Is.EqualTo(VoxelCellKind.Wall));
            Assert.That(result.Volume.GetCell(0, config.BoundaryWallHeight, 10), Is.EqualTo(VoxelCellKind.Wall));
        }

        /// <summary>
        /// Verifies that interior obstacle semantics compile into single-cell object placements.
        /// </summary>
        [Test]
        public void Compile_CreatesSingleCellObstaclePlacements()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            tileSet.ResetToDefaults();
            palette.EnsureDefaultPlaceholders(null);
            palette.GetEntry(1).EnabledForAutoGeneration = false;
            palette.GetEntry(2).EnabledForAutoGeneration = false;
            palette.GetEntry(0).SemanticClass = ObstacleSemanticClass.LowCover;

            var grid = new SemanticGrid2D(config.Width, config.Depth);
            for (var x = 0; x < config.Width; x++)
            {
                for (var z = 0; z < config.Depth; z++)
                {
                    grid.Set(x, z, x == 0 || z == 0 || x == config.Width - 1 || z == config.Depth - 1
                        ? SemanticArchetype.BoundaryWall
                        : SemanticArchetype.Open);
                }
            }

            grid.Set(10, 10, SemanticArchetype.LowCoverDense);
            grid.Set(11, 11, SemanticArchetype.LowCoverSparse);

            var compiler = new SemanticToVoxelCompiler(config, tileSet, palette);
            var result = compiler.Compile(grid, 7);

            Assert.That(result.ObstaclePlacements.Count, Is.EqualTo(2));
            Assert.That(result.ObstaclePlacements.TrueForAll(placement => placement.FootprintWidth == 1 && placement.FootprintDepth == 1 && placement.OccupiedCells.Count == 1));
            Assert.That(result.ObstaclePlacements.TrueForAll(placement => placement.SemanticClass == ObstacleSemanticClass.LowCover));
        }
    }
}
