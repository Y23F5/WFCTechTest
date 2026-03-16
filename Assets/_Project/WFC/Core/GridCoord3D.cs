using System;
using UnityEngine;

namespace WFCTechTest.WFC.Core
{
    /// <summary>
    /// @file GridCoord3D.cs
    /// @brief Defines an immutable integer coordinate for voxel-space operations.
    /// </summary>
    [Serializable]
    public readonly struct GridCoord3D : IEquatable<GridCoord3D>
    {
        /// <summary>
        /// Initializes a new 3D coordinate.
        /// </summary>
        public GridCoord3D(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Gets the x coordinate.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the y coordinate.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the z coordinate.
        /// </summary>
        public int Z { get; }

        /// <summary>
        /// Converts the coordinate into a Unity vector.
        /// </summary>
        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }

        /// <inheritdoc />
        public bool Equals(GridCoord3D other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is GridCoord3D other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }
    }
}
