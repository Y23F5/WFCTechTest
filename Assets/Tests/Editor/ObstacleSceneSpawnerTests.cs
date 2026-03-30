using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;
using WFCTechTest.WFC.Semantic;
using WFCTechTest.WFC.Unity.Runtime;

namespace WFCTechTest.WFC.Tests.Editor
{
    /// <summary>
    /// @file ObstacleSceneSpawnerTests.cs
    /// @brief Verifies scene spawning keeps prefab visuals intact while grounding bottoms to the floor top.
    /// </summary>
    public sealed class ObstacleSceneSpawnerTests
    {
        [Test]
        public void SpawnObstaclePlacements_PreservesPrefabScaleAndGroundsBottom()
        {
            var palette = ScriptableObject.CreateInstance<PrefabRegistryAsset>();
            palette.EnsureDefaultPlaceholders(null);

            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = "ScaledObstacle";
            prefab.transform.localScale = new Vector3(2f, 3f, 4f);

            var entry = palette.AddEntry(prefab);
            entry.Type = 10;
            entry.Prefab = prefab;
            entry.UsePlaceholder = false;
            entry.SemanticClass = ObstacleSemanticClass.LowCover;
            palette.RefreshDerivedValues();

            var spawnerGo = new GameObject("Spawner");
            var spawner = spawnerGo.AddComponent<ObstacleSceneSpawner>();
            spawner.SetPrefabRegistry(palette);
            spawner.SetMapCenter(Vector3.zero);

            var volume = new VoxelOccupancyMap(3, 2, 3);
            var grid = new SemanticGrid2D(3, 3);
            grid.Set(1, 1, SemanticArchetype.LowCoverSparse);
            var compileResult = new CompileResult(volume, grid);
            compileResult.ObstaclePlacements.Add(new ObstaclePlacement
            {
                Type = 10,
                DisplayName = "ScaledObstacle",
                Anchor = new GridCoord2D(1, 1),
                OccupiedCells = new List<GridCoord2D> { new GridCoord2D(1, 1) },
                Height = entry.LogicalHeightCells,
                FootprintWidth = 1,
                FootprintDepth = 1,
                RotationY = 0f,
                Archetype = SemanticArchetype.LowCoverSparse,
                SemanticClass = ObstacleSemanticClass.LowCover,
                DensityBand = SemanticDensityBand.Sparse
            });

            spawner.SpawnObstaclePlacements(compileResult);

            var obstacleRoot = spawner.transform.Find("MapRoot/obstacleRoot");
            Assert.That(obstacleRoot, Is.Not.Null);
            Assert.That(obstacleRoot.childCount, Is.EqualTo(1));

            var spawned = obstacleRoot.GetChild(0).gameObject;
            Assert.That(spawned.transform.localScale, Is.EqualTo(prefab.transform.localScale));
            Assert.That(TryGetCombinedWorldBounds(spawned, out var spawnedBounds), Is.True);
            Assert.That(spawnedBounds.min.y, Is.EqualTo(palette.GetPlacementCellEdge() * 0.5f).Within(0.001f));

            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(spawnerGo);
            Object.DestroyImmediate(palette);
        }

        private static bool TryGetCombinedWorldBounds(GameObject gameObject, out Bounds bounds)
        {
            bounds = default;
            if (gameObject == null)
            {
                return false;
            }

            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }
    }
}
