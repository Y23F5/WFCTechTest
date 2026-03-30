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
        private enum SemanticRuleFamily
        {
            Open,
            InterestAnchor,
            BoundaryWall,
            BoundaryCorner,
            LowCover,
            HighCover,
            Tower,
            Blocker
        }

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
            var left = ResolveFamily(a);
            var right = ResolveFamily(b);

            if (left == SemanticRuleFamily.Open || right == SemanticRuleFamily.Open)
            {
                return true;
            }

            if (left == SemanticRuleFamily.BoundaryCorner && right == SemanticRuleFamily.BoundaryCorner)
            {
                return false;
            }

            if (Contains(left, right, SemanticRuleFamily.InterestAnchor)
                && ContainsAny(left, right, SemanticRuleFamily.BoundaryWall, SemanticRuleFamily.BoundaryCorner, SemanticRuleFamily.Tower, SemanticRuleFamily.Blocker))
            {
                return false;
            }

            if (Contains(left, right, SemanticRuleFamily.Tower)
                && ContainsAny(left, right, SemanticRuleFamily.BoundaryWall, SemanticRuleFamily.BoundaryCorner, SemanticRuleFamily.Blocker))
            {
                return false;
            }

            if (Contains(left, right, SemanticRuleFamily.HighCover)
                && Contains(left, right, SemanticRuleFamily.BoundaryCorner))
            {
                return false;
            }

            if (Contains(left, right, SemanticRuleFamily.BoundaryWall)
                && ContainsAny(left, right, SemanticRuleFamily.HighCover, SemanticRuleFamily.Tower, SemanticRuleFamily.InterestAnchor))
            {
                return false;
            }

            return true;
        }

        private static SemanticRuleFamily ResolveFamily(SemanticArchetype archetype)
        {
            return archetype switch
            {
                SemanticArchetype.Open => SemanticRuleFamily.Open,
                SemanticArchetype.InterestAnchor => SemanticRuleFamily.InterestAnchor,
                SemanticArchetype.BoundaryWall => SemanticRuleFamily.BoundaryWall,
                SemanticArchetype.BoundaryCorner => SemanticRuleFamily.BoundaryCorner,
                _ => archetype.GetObstacleSemanticClass() switch
                {
                    ObstacleSemanticClass.LowCover => SemanticRuleFamily.LowCover,
                    ObstacleSemanticClass.HighCover => SemanticRuleFamily.HighCover,
                    ObstacleSemanticClass.Tower => SemanticRuleFamily.Tower,
                    ObstacleSemanticClass.Blocker => SemanticRuleFamily.Blocker,
                    _ => SemanticRuleFamily.Open
                }
            };
        }

        private static bool Contains(SemanticRuleFamily left, SemanticRuleFamily right, SemanticRuleFamily family)
        {
            return left == family || right == family;
        }

        private static bool ContainsAny(SemanticRuleFamily left, SemanticRuleFamily right, params SemanticRuleFamily[] families)
        {
            foreach (var family in families)
            {
                if (Contains(left, right, family))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
