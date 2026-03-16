using System;

namespace WFCTechTest.WFC.Core
{
    /// <summary>
    /// @file GridCoord2D.cs
    /// @brief Defines an immutable integer coordinate for semantic grid operations.
    /// </summary>
    [Serializable]
    public readonly struct GridCoord2D : IEquatable<GridCoord2D>
    {
        /// <summary>
        /// Initializes a new 2D coordinate.
        /// </summary>
        public GridCoord2D(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>
        /// Gets the horizontal x coordinate.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the horizontal z coordinate.
        /// </summary>
        public int Z { get; }

        /// <summary>
        /// Returns a translated coordinate.
        /// </summary>
        public GridCoord2D Offset(int dx, int dz)
        {
            return new GridCoord2D(X + dx, Z + dz);
        }

        /// <inheritdoc />
        public bool Equals(GridCoord2D other)
        {
            return X == other.X && Z == other.Z;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is GridCoord2D other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Z);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"({X}, {Z})";
        }
    }
}
