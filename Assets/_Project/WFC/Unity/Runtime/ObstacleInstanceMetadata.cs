using UnityEngine;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Unity.Runtime
{
    /// <summary>
    /// @file ObstacleInstanceMetadata.cs
    /// @brief Stores obstacle export and editor metadata on spawned or manually registered scene objects.
    /// </summary>
    public sealed class ObstacleInstanceMetadata : MonoBehaviour
    {
        /// <summary>
        /// Gets or sets the exported obstacle type id.
        /// </summary>
        public int Type = 65535;

        /// <summary>
        /// Gets or sets the prefab registry display name.
        /// </summary>
        public string DisplayName = string.Empty;

        /// <summary>
        /// Gets or sets the primary semantic class associated with this obstacle.
        /// </summary>
        public ObstacleSemanticClass SemanticClass;

        /// <summary>
        /// Gets or sets the source semantic archetype used during generation.
        /// </summary>
        public SemanticArchetype SourceArchetype = SemanticArchetype.Open;

        /// <summary>
        /// Gets or sets whether this obstacle currently points at an unknown imported index.
        /// </summary>
        public bool IsUnknownType;

        /// <summary>
        /// Gets or sets whether the obstacle came from auto-generation.
        /// </summary>
        public bool IsGenerated;

        /// <summary>
        /// Gets or sets whether the object has been formally registered for export.
        /// </summary>
        public bool Registered;

        /// <summary>
        /// Gets or sets whether random yaw is allowed for this obstacle.
        /// </summary>
        public bool AllowRandomYaw = true;
    }
}
