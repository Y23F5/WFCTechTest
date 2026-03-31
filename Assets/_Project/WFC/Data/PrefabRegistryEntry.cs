using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Data
{
    /// <summary>
    /// @file PrefabRegistryEntry.cs
    /// @brief Defines one obstacle type entry used by editor tooling, export, and future rule-driven generation.
    /// </summary>
    [MovedFrom(true, sourceNamespace: "WFCTechTest.WFC.Data", sourceClassName: "ObstaclePaletteEntry")]
    [Serializable]
    public sealed class PrefabRegistryEntry
    {
        /// <summary>
        /// Gets or sets the exported obstacle type id.
        /// </summary>
        public int Type;

        /// <summary>
        /// Gets or sets the display name shown in editor tooling.
        /// </summary>
        public string DisplayName = string.Empty;

        /// <summary>
        /// Gets or sets the prefab used for this obstacle type.
        /// </summary>
        public GameObject Prefab;

        /// <summary>
        /// Gets or sets whether random yaw is allowed for this type.
        /// </summary>
        public bool AllowRandomYaw = true;

        /// <summary>
        /// Gets or sets whether this entry is one of the default placeholder slots.
        /// </summary>
        public bool UsePlaceholder;

        /// <summary>
        /// Gets or sets whether auto-generation may place this type.
        /// </summary>
        public bool EnabledForAutoGeneration = true;

        /// <summary>
        /// Gets or sets the primary semantic family this type belongs to.
        /// </summary>
        public ObstacleSemanticClass SemanticClass = ObstacleSemanticClass.LowCover;

        /// <summary>
        /// Gets or sets the logical footprint width in placement cells.
        /// </summary>
        public int FootprintWidth = 1;

        /// <summary>
        /// Gets or sets the logical footprint depth in placement cells.
        /// </summary>
        public int FootprintDepth = 1;

        /// <summary>
        /// Gets or sets the logical occupied height in placement cells.
        /// </summary>
        public int LogicalHeightCells = 1;

        /// <summary>
        /// Gets or sets whether the logical height has been manually overridden and should no longer auto-refresh.
        /// </summary>
        public bool LogicalHeightLocked;

        /// <summary>
        /// Gets or sets the default pivot Y offset relative to the ground top used when placing this prefab.
        /// </summary>
        public float DefaultPosY;

        /// <summary>
        /// Gets or sets whether the default pivot Y offset has been manually overridden and should no longer auto-refresh.
        /// </summary>
        public bool DefaultPosYLocked;

        /// <summary>
        /// Gets or sets the base placement weight.
        /// </summary>
        public float Weight = 1f;

        /// <summary>
        /// Gets or sets the placement weight multiplier used when the semantic result is in a sparse density band.
        /// </summary>
        public float SparseWeight = 1f;

        /// <summary>
        /// Gets or sets the placement weight multiplier used when the semantic result is in a dense density band.
        /// </summary>
        public float DenseWeight = 1f;

        /// <summary>
        /// Gets or sets the minimum desired count.
        /// </summary>
        public int MinCount;

        /// <summary>
        /// Gets or sets the maximum desired count.
        /// </summary>
        public int MaxCount;

        /// <summary>
        /// Gets or sets whether this type may appear near map boundaries.
        /// </summary>
        public bool CanAppearNearBoundary = true;

        /// <summary>
        /// Gets or sets whether this type may appear in the central play region.
        /// </summary>
        public bool CanAppearInCenter = true;

        /// <summary>
        /// Gets or sets whether this type requires local obstacle clearance.
        /// </summary>
        public bool RequiresClearance;

        /// <summary>
        /// Gets or sets the planar clearance radius in placement cells.
        /// </summary>
        public int ClearanceRadius;

        /// <summary>
        /// Gets or sets a bitmask for future height-band restrictions.
        /// </summary>
        public int AllowedHeightMask = -1;
    }
}
