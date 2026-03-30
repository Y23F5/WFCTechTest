using System;
using System.Collections.Generic;

namespace WFCTechTest.WFC.Data
{
    /// <summary>
    /// @file ObstacleInfo.cs
    /// @brief Defines the JSON export schema for obstacle placements.
    /// </summary>
    [Serializable]
    public sealed class ObstacleInfo
    {
        /// <summary>
        /// Gets or sets the exported obstacle type id.
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// Gets or sets the world x position.
        /// </summary>
        public double Pos_X { get; set; }

        /// <summary>
        /// Gets or sets the world y position.
        /// </summary>
        public double Pos_Y { get; set; }

        /// <summary>
        /// Gets or sets the world z position.
        /// </summary>
        public double Pos_Z { get; set; }

        /// <summary>
        /// Gets or sets the world-space y rotation in degrees.
        /// </summary>
        public double Rot_Y { get; set; }
    }

    /// <summary>
    /// Wraps all exported obstacle placements in the requested JSON root schema.
    /// </summary>
    [Serializable]
    public sealed class AllObstacleInfo
    {
        /// <summary>
        /// Gets or sets the exported obstacle list.
        /// </summary>
        public List<ObstacleInfo> Obstacles { get; set; } = new List<ObstacleInfo>();
    }
}
