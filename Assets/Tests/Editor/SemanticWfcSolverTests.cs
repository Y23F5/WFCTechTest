using NUnit.Framework;
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
    }
}
