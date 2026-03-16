using WFCTechTest.WFC.Diagnostics;

namespace WFCTechTest.WFC.Core
{
    /// <summary>
    /// @file IWfcSolver.cs
    /// @brief Defines the common solver contract shared by the current semantic solver and future true 3D solvers.
    /// </summary>
    public interface IWfcSolver<TState>
    {
        /// <summary>
        /// Attempts to solve a generation request for the supplied seed and reports solver metrics into the run report.
        /// </summary>
        bool TrySolve(int seed, GenerationReport report, out TState state);
    }
}
