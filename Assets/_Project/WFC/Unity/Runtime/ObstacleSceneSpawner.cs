using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;

namespace WFCTechTest.WFC.Unity.Runtime
{
    /// <summary>
    /// @file ObstacleSceneSpawner.cs
    /// @brief Internal scene spawner that instantiates generated boundaries and obstacle prefabs from compile outputs.
    /// </summary>
    public partial class ObstacleSceneSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject cubePrefab;
        [FormerlySerializedAs("obstaclePalette")]
        [SerializeField] private PrefabRegistryAsset prefabRegistry;
        [SerializeField] private Vector3 mapCenter;
        [SerializeField] private Transform mapRoot;
        [SerializeField] private Transform worldBoundaryRoot;
        [SerializeField] private Transform obstacleRoot;
        [SerializeField] private Color floorColor = new Color(0.58f, 0.58f, 0.62f);
        [SerializeField] private Color wallColor = new Color(0.18f, 0.20f, 0.24f);
        [SerializeField] private Color lowCoverColor = new Color(0.31f, 0.55f, 0.66f);
        [SerializeField] private Color highCoverColor = new Color(0.84f, 0.57f, 0.25f);
        [SerializeField] private Color towerColor = new Color(0.73f, 0.32f, 0.26f);
        [SerializeField] private Color blockerColor = new Color(0.45f, 0.34f, 0.18f);

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private MaterialPropertyBlock _propertyBlock;

        public void ClearSpawned()
        {
            foreach (var instance in _spawned)
            {
                if (instance != null)
                {
                    DestroySpawnedObject(instance);
                }
            }

            ClearUntrackedSpawnedObjects();
            _spawned.Clear();
        }

        public void ClearObstacles()
        {
            if (TryGetObstacleRoot(out var resolvedObstacleRoot))
            {
                ClearGeneratedObstacleChildren(resolvedObstacleRoot);
            }

            _spawned.RemoveAll(instance => instance == null);
        }

        public void ClearBoundaries()
        {
            if (TryGetBoundaryRoot(out var boundaryRoot))
            {
                ClearBoundaryChildren(boundaryRoot);
            }

            _spawned.RemoveAll(instance => instance == null);
        }

        public void Spawn(CompileResult compileResult)
        {
            ClearSpawned();
            EnsureRoots();
            if (cubePrefab != null)
            {
                SpawnBoundaries(compileResult.Volume);
            }
            else
            {
                Debug.LogWarning("ObstacleSceneSpawner has no cube prefab. Boundary cubes were skipped, but obstacle prefabs will still spawn.");
            }

            SpawnObstaclePlacements(compileResult);
        }

        public void Spawn(VoxelOccupancyMap map)
        {
            ClearSpawned();
            EnsureRoots();
            if (cubePrefab == null)
            {
                Debug.LogWarning("ObstacleSceneSpawner requires a cube prefab before spawning.");
                return;
            }

            SpawnBoundaries(map);
        }

        public void SpawnBoundaries(VoxelOccupancyMap map)
        {
            EnsureRoots();
            if (cubePrefab == null)
            {
                Debug.LogWarning("ObstacleSceneSpawner requires a cube prefab before spawning boundaries.");
                return;
            }

            SpawnBoundaryVoxels(map);
        }

        public void SpawnObstaclePlacements(CompileResult compileResult)
        {
            EnsureRoots();
            var cellSize = GetPlacementCellSize();
            foreach (var placement in compileResult.ObstaclePlacements)
            {
                var entry = ResolvePlacementEntry(placement);
                var prefab = entry?.Prefab != null ? entry.Prefab : cubePrefab;
                var worldPosition = ResolveCenteredPlacementPosition(compileResult.Volume, placement, cellSize);
                var rotation = ResolvePlacementRotation(placement);
                var usingCubeFallback = entry == null || entry.UsePlaceholder || prefab == cubePrefab || prefab == null;
                var instance = prefab != null
                    ? Instantiate(prefab, worldPosition, rotation, obstacleRoot)
                    : CreatePrimitiveObstacle(worldPosition, rotation);
                instance.name = $"Obstacle_{placement.Type}_{placement.Anchor.X}_{placement.Anchor.Z}";
                if (TryResolveRegistryPlacementY(entry, out var placementY))
                {
                    var position = instance.transform.position;
                    instance.transform.position = new Vector3(position.x, placementY, position.z);
                }
                else
                {
                    AlignInstanceBottomToGround(instance, GetGroundTopY(cellSize));
                }

                ApplyColor(instance, ResolvePlacementColorKind(placement), shouldApplyTint: ShouldApplyFallbackTint(instance, usingCubeFallback));
                AttachPlacementMetadata(instance, placement, entry);
                _spawned.Add(instance);
            }
        }

        public void SetPrefabRegistry(PrefabRegistryAsset registry)
        {
            prefabRegistry = registry;
        }

        public void SetMapCenter(Vector3 center)
        {
            mapCenter = center;
        }

        private void SpawnBoundaryVoxels(VoxelOccupancyMap map)
        {
            var cellSize = GetPlacementCellSize();
            for (var x = 0; x < map.Width; x++)
            {
                for (var y = 0; y < map.Height; y++)
                {
                    for (var z = 0; z < map.Depth; z++)
                    {
                        var kind = map.GetCell(x, y, z);
                        if (kind == VoxelCellKind.Air || !IsBoundaryKind(kind))
                        {
                            continue;
                        }

                        var worldPosition = ResolveCenteredCellPosition(map, x, y, z, cellSize);
                        var instance = Instantiate(cubePrefab, worldPosition, Quaternion.identity, worldBoundaryRoot);
                        instance.name = $"{kind}_{x}_{y}_{z}";
                        ApplyScale(instance.transform, cellSize, cubePrefab != null);
                        ApplyColor(instance, kind, shouldApplyTint: true);
                        _spawned.Add(instance);
                    }
                }
            }
        }

        private void ClearUntrackedSpawnedObjects()
        {
            if (TryGetBoundaryRoot(out var boundaryRoot))
            {
                ClearBoundaryChildren(boundaryRoot);
            }

            if (TryGetObstacleRoot(out var resolvedObstacleRoot))
            {
                ClearGeneratedObstacleChildren(resolvedObstacleRoot);
            }
        }

        private static bool IsSpawnedVoxelName(string name)
        {
            return name.StartsWith("Floor_")
                   || name.StartsWith("Wall_")
                   || name.StartsWith("LowCover_")
                   || name.StartsWith("HighCover_")
                   || name.StartsWith("Tower_")
                   || name.StartsWith("InterestAnchor_");
        }

        private void AttachPlacementMetadata(GameObject instance, ObstaclePlacement placement, PrefabRegistryEntry entry)
        {
            var metadata = instance.GetComponent<ObstacleInstanceMetadata>();
            if (metadata == null)
            {
                metadata = instance.AddComponent<ObstacleInstanceMetadata>();
            }

            metadata.IsGenerated = true;
            metadata.Registered = true;
            metadata.AllowRandomYaw = placement.AllowRandomYaw;
            metadata.Type = placement.Type;
            metadata.SemanticClass = placement.SemanticClass;
            metadata.SourceArchetype = placement.Archetype;
            metadata.IsUnknownType = false;
            metadata.DisplayName = !string.IsNullOrWhiteSpace(placement.DisplayName) ? placement.DisplayName : entry?.DisplayName ?? placement.Archetype.ToString();
        }

        private PrefabRegistryEntry ResolvePlacementEntry(ObstaclePlacement placement)
        {
            return prefabRegistry?.GetEntry(placement.Type);
        }

        private Vector3 GetPlacementCellSize()
        {
            return prefabRegistry != null ? prefabRegistry.GetPlacementCellSize() : Vector3.one;
        }

        private void Reset()
        {
            EnsureRoots();
        }

        private void OnValidate()
        {
            EnsureRoots();
        }
    }
}
