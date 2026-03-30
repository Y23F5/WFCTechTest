using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;
using WFCTechTest.WFC.Semantic;

namespace WFCTechTest.WFC.Tests.Editor
{
    /// <summary>
    /// @file SemanticWfcSolverTests.cs
    /// @brief Covers semantic solver determinism and hard boundary constraint behavior.
    /// </summary>
    public sealed class SemanticWfcSolverTests
    {
        /// <summary>
        /// Verifies that the semantic solver produces identical layouts for the same seed.
        /// </summary>
        [Test]
        public void TrySolve_IsDeterministicForSameSeed()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var solver = new SemanticWfcSolver(config, tileSet);

            Assert.That(solver.TrySolve(1234, new GenerationReport(), out var a), Is.True);
            Assert.That(solver.TrySolve(1234, new GenerationReport(), out var b), Is.True);

            for (var x = 0; x < config.Width; x++)
            {
                for (var z = 0; z < config.Depth; z++)
                {
                    Assert.That(a.Get(x, z), Is.EqualTo(b.Get(x, z)));
                }
            }
        }

        /// <summary>
        /// Verifies that boundary cells resolve to boundary archetypes.
        /// </summary>
        [Test]
        public void TrySolve_AlwaysForcesBoundaryArchetypes()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var solver = new SemanticWfcSolver(config, tileSet);

            Assert.That(solver.TrySolve(42, new GenerationReport(), out var grid), Is.True);
            Assert.That(grid.Get(0, 0), Is.EqualTo(SemanticArchetype.BoundaryCorner));
            Assert.That(grid.Get(0, 10), Is.EqualTo(SemanticArchetype.BoundaryWall));
            Assert.That(grid.Get(config.Width - 1, config.Depth - 1), Is.EqualTo(SemanticArchetype.BoundaryCorner));
        }

        /// <summary>
        /// Verifies that the heavy semantic solver emits only approved obstacle families.
        /// </summary>
        [Test]
        public void TrySolve_EmitsHeavySemanticObstacleFamilies()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var solver = new SemanticWfcSolver(config, tileSet);

            Assert.That(solver.TrySolve(99, new GenerationReport(), out var grid), Is.True);

            var foundObstacleSemantic = false;
            for (var x = 1; x < config.Width - 1; x++)
            {
                for (var z = 1; z < config.Depth - 1; z++)
                {
                    var archetype = grid.Get(x, z);
                    if (!archetype.IsObstacle())
                    {
                        continue;
                    }

                    foundObstacleSemantic = true;
                    Assert.That(archetype.GetObstacleSemanticClass(), Is.Not.EqualTo(ObstacleSemanticClass.None));
                }
            }

            Assert.That(foundObstacleSemantic, Is.True);
        }

        [Test]
        public void AdjacencyRules_RejectInterestAnchorsBesideTowerAndBlocker()
        {
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var rules = new SemanticAdjacencyRules(tileSet);

            Assert.That(rules.IsAllowed(SemanticArchetype.InterestAnchor, SemanticArchetype.TowerSparse), Is.False);
            Assert.That(rules.IsAllowed(SemanticArchetype.InterestAnchor, SemanticArchetype.BlockerDense), Is.False);
            Assert.That(rules.IsAllowed(SemanticArchetype.InterestAnchor, SemanticArchetype.LowCoverSparse), Is.True);
        }

        [Test]
        public void TrySolve_UsesConfiguredDenseRatios()
        {
            var sparseConfig = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var denseConfig = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            SetPrivateField(sparseConfig, "interestAnchorCount", 0);
            SetPrivateField(denseConfig, "interestAnchorCount", 0);
            SetPrivateField(sparseConfig, "lowCoverDenseRatio", 0.05f);
            SetPrivateField(denseConfig, "lowCoverDenseRatio", 0.95f);

            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            foreach (var definition in tileSet.GetDefinitions())
            {
                switch (definition.Archetype)
                {
                    case SemanticArchetype.Open:
                        definition.Weight = 0.2f;
                        break;
                    case SemanticArchetype.LowCoverSparse:
                    case SemanticArchetype.LowCoverDense:
                        definition.Weight = 1f;
                        break;
                    case SemanticArchetype.BoundaryWall:
                    case SemanticArchetype.BoundaryCorner:
                        break;
                    default:
                        definition.Weight = 0f;
                        break;
                }
            }

            var sparseSolver = new SemanticWfcSolver(sparseConfig, tileSet);
            var denseSolver = new SemanticWfcSolver(denseConfig, tileSet);

            Assert.That(sparseSolver.TrySolve(777, new GenerationReport(), out var sparseGrid), Is.True);
            Assert.That(denseSolver.TrySolve(777, new GenerationReport(), out var denseGrid), Is.True);

            var sparseDenseCells = CountArchetype(sparseGrid, SemanticArchetype.LowCoverDense);
            var denseDenseCells = CountArchetype(denseGrid, SemanticArchetype.LowCoverDense);

            Assert.That(denseDenseCells, Is.GreaterThan(sparseDenseCells));
        }

        [TestCase(0.20f, 7771)]
        [TestCase(0.90f, 7772)]
        public void TrySolve_BiasesTowardConfiguredOpenCoverage(float targetOpenCoverage, int seed)
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            SetPrivateField(config, "targetOpenCoverage", targetOpenCoverage);
            SetPrivateField(config, "openCoverageTolerance", 0.02f);
            SetPrivateField(config, "coverageTargetInitialized", true);

            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var solver = new SemanticWfcSolver(config, tileSet);

            Assert.That(solver.TrySolve(seed, new GenerationReport(), out var grid), Is.True);

            var openLikeCount = 0;
            var interiorCellCount = 0;
            for (var x = 1; x < config.Width - 1; x++)
            {
                for (var z = 1; z < config.Depth - 1; z++)
                {
                    interiorCellCount++;
                    if (grid.Get(x, z).IsOpenLike())
                    {
                        openLikeCount++;
                    }
                }
            }

            var openRatio = openLikeCount / (float)interiorCellCount;
            Assert.That(openRatio, Is.InRange(targetOpenCoverage - 0.12f, targetOpenCoverage + 0.12f));
        }

        [Test]
        public void TrySolve_PropagatesBoundaryConstraintsBeforeSelectingInterior()
        {
            var config = ScriptableObject.CreateInstance<GenerationConfigAsset>();
            var tileSet = ScriptableObject.CreateInstance<SemanticTileSetAsset>();
            tileSet.ResetToDefaults();
            var solver = new SemanticWfcSolver(config, tileSet);

            var report = new GenerationReport();
            var success = solver.TrySolve(12345, report, out var grid);

            Assert.That(success, Is.True, report.Message);
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.Get(config.Width - 1, 6), Is.EqualTo(SemanticArchetype.BoundaryWall));
            Assert.That(grid.Get(config.Width - 2, 6), Is.Not.EqualTo(SemanticArchetype.HighCoverDense));
            Assert.That(grid.Get(config.Width - 2, 6), Is.Not.EqualTo(SemanticArchetype.TowerDense));
        }

        private static int CountArchetype(SemanticGrid2D grid, SemanticArchetype archetype)
        {
            var count = 0;
            for (var x = 0; x < grid.Width; x++)
            {
                for (var z = 0; z < grid.Depth; z++)
                {
                    if (grid.Get(x, z) == archetype)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }
    }
}
