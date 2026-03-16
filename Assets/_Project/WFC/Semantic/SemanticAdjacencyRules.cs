using System.Collections.Generic;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;

namespace WFCTechTest.WFC.Semantic
{
    /// <summary>
    /// @file SemanticAdjacencyRules.cs
    /// @brief Builds runtime compatibility rules for the semantic WFC domain.
    /// </summary>
    public sealed class SemanticAdjacencyRules
    {
        private readonly HashSet<(SemanticArchetype, SemanticArchetype)> _compatibility;

        /// <summary>
        /// Initializes runtime adjacency rules from the configured tile set.
        /// </summary>
        public SemanticAdjacencyRules(SemanticTileSetAsset tileSet)
        {
            _compatibility = BuildCompatibility(tileSet);
        }

        /// <summary>
        /// Returns whether two archetypes may be adjacent in the semantic layer.
        /// </summary>
        public bool IsAllowed(SemanticArchetype a, SemanticArchetype b)
        {
            return _compatibility.Contains((a, b));
        }

        private static HashSet<(SemanticArchetype, SemanticArchetype)> BuildCompatibility(SemanticTileSetAsset tileSet)
        {
            var compatibility = new HashSet<(SemanticArchetype, SemanticArchetype)>();
            var definitions = tileSet.GetDefinitions();
            foreach (var left in definitions)
            {
                foreach (var right in definitions)
                {
                    if (AllowPair(left.Archetype, right.Archetype))
                    {
                        compatibility.Add((left.Archetype, right.Archetype));
                    }
                }
            }

            return compatibility;
        }

        private static bool AllowPair(SemanticArchetype a, SemanticArchetype b)
        {
            if (IsBoundary(a) != IsBoundary(b))
            {
                return true;
            }

            if (a == SemanticArchetype.BoundaryCorner && b == SemanticArchetype.BoundaryCorner)
            {
                return false;
            }

            if (a == SemanticArchetype.Tower1x1 && b == SemanticArchetype.Tower1x1)
            {
                return false;
            }

            if ((a == SemanticArchetype.Tower1x1 && b == SemanticArchetype.Block2x2)
                || (a == SemanticArchetype.Block2x2 && b == SemanticArchetype.Tower1x1))
            {
                return false;
            }

            if ((a == SemanticArchetype.InterestAnchor && b == SemanticArchetype.Block2x2)
                || (a == SemanticArchetype.Block2x2 && b == SemanticArchetype.InterestAnchor))
            {
                return false;
            }

            return true;
        }

        private static bool IsBoundary(SemanticArchetype archetype)
        {
            return archetype == SemanticArchetype.BoundaryWall || archetype == SemanticArchetype.BoundaryCorner;
        }
    }
}
