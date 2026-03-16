using NUnit.Framework;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Validation;

namespace WFCTechTest.WFC.Tests.Editor
{
    /// <summary>
    /// @file ConnectivityValidatorTests.cs
    /// @brief Verifies largest-component connectivity scoring on simple voxel fixtures.
    /// </summary>
    public sealed class ConnectivityValidatorTests
    {
        /// <summary>
        /// Verifies that a clear flat arena reports full connectivity.
        /// </summary>
        [Test]
        public void ComputeLargestComponentRatio_ReturnsFullCoverageForFlatArena()
        {
            var map = new VoxelOccupancyMap(8, 6, 8);
            for (var x = 0; x < 8; x++)
            {
                for (var z = 0; z < 8; z++)
                {
                    map.SetCell(x, 0, z, VoxelCellKind.Floor);
                }
            }

            var validator = new ConnectivityValidator();
            var ratio = validator.ComputeLargestComponentRatio(map, 1.1f, 2.1f);

            Assert.That(ratio, Is.EqualTo(1f).Within(0.001f));
        }
    }
}
