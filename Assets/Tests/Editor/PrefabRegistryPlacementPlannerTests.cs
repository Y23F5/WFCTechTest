using NUnit.Framework;
using UnityEngine;
using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Semantic;

namespace WFCTechTest.WFC.Tests.Editor
{
    /// <summary>
    /// @file PrefabRegistryPlacementPlannerTests.cs
    /// @brief Verifies that the prefab-registry-driven planner emits single-cell obstacle placements that respect the new 1x1 obstacle rule.
    /// </summary>
    public sealed class PrefabRegistryPlacementPlannerTests
    {
        /// <summary>
        /// Verifies that obstacle planning now emits one-cell placements only.
        /// </summary>
        [Test]
        public void Plan_ProducesOnlySingleCellPlacements()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
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

            grid.Set(5, 5, SemanticArchetype.LowCoverSparse);
            grid.Set(6, 5, SemanticArchetype.LowCoverDense);
            grid.Set(7, 5, SemanticArchetype.LowCoverSparse);

            var planner = new PrefabRegistryPlacementPlanner(config, palette, 12345);
            var placements = planner.Plan(grid, out var degraded);

            Assert.That(degraded, Is.EqualTo(0));
            Assert.That(placements.Count, Is.EqualTo(3));
            foreach (var placement in placements)
            {
                Assert.That(placement.FootprintWidth, Is.EqualTo(1));
                Assert.That(placement.FootprintDepth, Is.EqualTo(1));
                Assert.That(placement.OccupiedCells.Count, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Verifies that the planner respects disabled auto-generation entries.
        /// </summary>
        [Test]
        public void Plan_UsesEnabledPaletteEntriesOnly()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);
            palette.GetEntry(0).EnabledForAutoGeneration = false;
            palette.GetEntry(1).EnabledForAutoGeneration = true;
            palette.GetEntry(2).EnabledForAutoGeneration = false;
            palette.GetEntry(1).SemanticClass = ObstacleSemanticClass.HighCover;

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

            grid.Set(5, 5, SemanticArchetype.HighCoverDense);

            var planner = new PrefabRegistryPlacementPlanner(config, palette, 2222);
            var placements = planner.Plan(grid, out _);

            Assert.That(placements.Count, Is.EqualTo(1));
            Assert.That(placements[0].Type, Is.EqualTo(1));
            Assert.That(placements[0].SemanticClass, Is.EqualTo(ObstacleSemanticClass.HighCover));
        }

        [Test]
        public void Plan_PrefersEntriesMatchingDensityWeights()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);

            var sparseEntry = palette.GetEntry(0);
            sparseEntry.SemanticClass = ObstacleSemanticClass.LowCover;
            sparseEntry.SparseWeight = 5f;
            sparseEntry.DenseWeight = 0.05f;

            var denseEntry = palette.AddEntry();
            denseEntry.Type = 10;
            denseEntry.DisplayName = "DenseFavored";
            denseEntry.SemanticClass = ObstacleSemanticClass.LowCover;
            denseEntry.Weight = 1f;
            denseEntry.SparseWeight = 0.05f;
            denseEntry.DenseWeight = 5f;

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

            grid.Set(5, 5, SemanticArchetype.LowCoverSparse);
            grid.Set(6, 5, SemanticArchetype.LowCoverDense);

            var planner = new PrefabRegistryPlacementPlanner(config, palette, 0);
            var placements = planner.Plan(grid, out _);

            Assert.That(placements.Count, Is.EqualTo(2));
            Assert.That(placements[0].DensityBand, Is.EqualTo(SemanticDensityBand.Sparse));
            Assert.That(placements[0].Type, Is.EqualTo(0));
            Assert.That(placements[1].DensityBand, Is.EqualTo(SemanticDensityBand.Dense));
            Assert.That(placements[1].Type, Is.EqualTo(10));
        }
    }
}
