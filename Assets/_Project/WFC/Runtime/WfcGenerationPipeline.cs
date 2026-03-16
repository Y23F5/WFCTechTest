using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Diagnostics;
using WFCTechTest.WFC.Semantic;
using WFCTechTest.WFC.Validation;

namespace WFCTechTest.WFC.Runtime
{
    /// <summary>
    /// @file WfcGenerationPipeline.cs
    /// @brief Orchestrates semantic solve, 3D compile, validation, and retry handling for a single seed.
    /// </summary>
    public sealed class WfcGenerationPipeline
    {
        private readonly GenerationConfigAsset _config;
        private readonly SemanticTileSetAsset _tileSet;
        private readonly SemanticWfcSolver _solver;
        private readonly SemanticToVoxelCompiler _compiler;
        private readonly GenerationValidator _validator;

        /// <summary>
        /// Initializes a generation pipeline.
        /// </summary>
        public WfcGenerationPipeline(GenerationConfigAsset config, SemanticTileSetAsset tileSet)
        {
            _config = config;
            _tileSet = tileSet;
            _solver = new SemanticWfcSolver(config, tileSet);
            _compiler = new SemanticToVoxelCompiler(config, tileSet);
            _validator = new GenerationValidator(config);
        }

        /// <summary>
        /// Attempts to build a valid map for the supplied seed.
        /// </summary>
        public bool TryGenerate(int seed, out CompileResult result, out GenerationReport report)
        {
            report = new GenerationReport { Seed = seed };
            result = null;
            var hadConcreteFailure = false;

            for (var attempt = 1; attempt <= _config.MaxRetries; attempt++)
            {
                report.ResetAttempt(attempt);
                report.Seed = seed;

                if (!_solver.TrySolve(seed + attempt - 1, report, out var semanticGrid))
                {
                    report.CaptureLastFailureSnapshot();
                    hadConcreteFailure = true;
                    continue;
                }

                var compiled = _compiler.Compile(semanticGrid, seed + attempt - 1);
                if (_validator.Validate(compiled, report))
                {
                    result = compiled;
                    return true;
                }

                report.CaptureLastFailureSnapshot();
                hadConcreteFailure = true;
            }

            report.FailureReason = GenerationFailureReason.RetryBudgetExceeded;
            report.Message = hadConcreteFailure
                ? $"The generator exhausted all retries without producing a valid map. Last failure={report.LastAttemptFailureReason}, coverage={report.LastAttemptGroundCoverageRatio:P1}, component={report.LastAttemptLargestComponentRatio:P1}, degraded={report.LastAttemptDegradedFootprintCount}. {report.LastAttemptFailureMessage}"
                : "The generator exhausted all retries without producing a valid map.";
            return false;
        }
    }
}
