using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WFCTechTest.WFC.Core;

namespace WFCTechTest.WFC.Data
{
    /// <summary>
    /// @file SemanticTileSetAsset.cs
    /// @brief Provides tunable semantic archetype definitions for the first-stage WFC solver.
    /// </summary>
    [CreateAssetMenu(menuName = "WFC/Semantic Tile Set", fileName = "SemanticTileSet")]
    public sealed class SemanticTileSetAsset : ScriptableObject
    {
        [SerializeField]
        private List<SemanticArchetypeDefinition> definitions = new List<SemanticArchetypeDefinition>();

        /// <summary>
        /// Returns the configured archetype definitions, or a built-in default set when the asset is empty.
        /// </summary>
        public IReadOnlyList<SemanticArchetypeDefinition> GetDefinitions()
        {
            return definitions.Count > 0 ? definitions : CreateDefaultDefinitions();
        }

        /// <summary>
        /// Returns a definition for a concrete archetype.
        /// </summary>
        public SemanticArchetypeDefinition GetDefinition(SemanticArchetype archetype)
        {
            return GetDefinitions().First(definition => definition.Archetype == archetype);
        }

        /// <summary>
        /// Replaces the asset contents with a built-in default tuning set.
        /// </summary>
        public void ResetToDefaults()
        {
            definitions = CreateDefaultDefinitions();
        }

        /// <summary>
        /// Creates the built-in semantic archetype definitions.
        /// </summary>
        public static List<SemanticArchetypeDefinition> CreateDefaultDefinitions()
        {
            return new List<SemanticArchetypeDefinition>
            {
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.Open, Weight = 9.8f, TargetRatio = 0.61f },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.InterestAnchor, Weight = 0.5f, TargetRatio = 0.03f, IsInterestAnchor = true },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.BoundaryWall, Weight = 0f, TargetRatio = 0f, Height = 4, BoundaryOnly = true },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.BoundaryCorner, Weight = 0f, TargetRatio = 0f, Height = 4, BoundaryOnly = true },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.LowCover1x1, Weight = 0.96f, TargetRatio = 0.095f, Height = 1 },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.LowCover1x2, Weight = 0.86f, TargetRatio = 0.065f, Height = 1, FootprintWidth = 2 },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.HighCover1x1, Weight = 0.38f, TargetRatio = 0.035f, Height = 2 },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.HighCover1x2, Weight = 0.42f, TargetRatio = 0.035f, Height = 2, FootprintWidth = 2 },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.Tower1x1, Weight = 0.12f, TargetRatio = 0.01f, Height = 3 },
                new SemanticArchetypeDefinition { Archetype = SemanticArchetype.Block2x2, Weight = 0.28f, TargetRatio = 0.022f, Height = 2, FootprintWidth = 2, FootprintDepth = 2 }
            };
        }

        private void OnValidate()
        {
            var unique = new HashSet<SemanticArchetype>();
            definitions.RemoveAll(definition => !unique.Add(definition.Archetype));
        }
    }
}
