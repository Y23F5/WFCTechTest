using System.Linq;
using UnityEngine;
using WFCTechTest.WFC.Compile;
using WFCTechTest.WFC.Core;
using WFCTechTest.WFC.Data;

namespace WFCTechTest.WFC.Unity.Runtime {
    /**
     * @file ObstacleSceneSpawner.Helpers.cs
     * @brief Rendering, placement, and hierarchy helpers for the obstacle scene spawner.
     */
    public partial class ObstacleSceneSpawner {
        private void ApplyColor(GameObject instance, VoxelCellKind kind, bool shouldApplyTint) {
            if (!shouldApplyTint) return;

            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer == null) return;

            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.Clear();
            _propertyBlock.SetColor("_BaseColor", GetColor(kind));
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private static bool ShouldApplyFallbackTint(GameObject instance, bool usingCubeFallback) {
            if (instance == null) return false;
            if (usingCubeFallback) return true;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;

            foreach (var renderer in renderers) {
                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0) continue;

                foreach (var material in materials) {
                    if (material != null) return false;
                }
            }

            return true;
        }

        private Color GetColor(VoxelCellKind kind) {
            return kind switch {
                VoxelCellKind.Floor => floorColor,
                VoxelCellKind.Wall => wallColor,
                VoxelCellKind.LowCover => lowCoverColor,
                VoxelCellKind.HighCover => highCoverColor,
                VoxelCellKind.Tower => towerColor,
                VoxelCellKind.Blocker => blockerColor,
                _ => Color.white
            };
        }

        private static Quaternion ResolvePlacementRotation(ObstaclePlacement placement) {
            return Quaternion.Euler(0f, placement.RotationY, 0f);
        }

        private Vector3 ResolveCenteredPlacementPosition(VoxelOccupancyMap map, ObstaclePlacement placement, Vector3 cellSize) {
            var averageX = placement.OccupiedCells.Average(cell => cell.X);
            var averageZ = placement.OccupiedCells.Average(cell => cell.Z);
            var centeredX = (float)((averageX - ((map.Width - 1) * 0.5f)) * cellSize.x);
            var centeredZ = (float)((averageZ - ((map.Depth - 1) * 0.5f)) * cellSize.z);
            return new Vector3(mapCenter.x + centeredX, GetGroundTopY(cellSize), mapCenter.z + centeredZ);
        }

        private Vector3 ResolveCenteredCellPosition(VoxelOccupancyMap map, int x, int y, int z, Vector3 cellSize) {
            var centeredX = (x - ((map.Width - 1) * 0.5f)) * cellSize.x;
            var centeredZ = (z - ((map.Depth - 1) * 0.5f)) * cellSize.z;
            return mapCenter + new Vector3(centeredX, y * cellSize.y, centeredZ);
        }

        private static VoxelCellKind ResolvePlacementColorKind(ObstaclePlacement placement) {
            return placement.SemanticClass switch {
                ObstacleSemanticClass.LowCover => VoxelCellKind.LowCover,
                ObstacleSemanticClass.HighCover => VoxelCellKind.HighCover,
                ObstacleSemanticClass.Blocker => VoxelCellKind.Blocker,
                _ => VoxelCellKind.Tower
            };
        }

        private static float GetGroundTopY(Vector3 cellSize) {
            return cellSize.y * 0.5f;
        }

        private bool TryResolveRegistryPlacementY(PrefabRegistryEntry entry, out float placementY) {
            placementY = 0f;
            if (entry == null || !PrefabRegistryAsset.TryGetCombinedPrefabLocalBounds(entry.Prefab, out _)) return false;

            placementY = entry.DefaultPosY;
            return true;
        }

        private void AlignInstanceBottomToGround(GameObject instance, float groundTopOffset) {
            if (instance == null) return;

            var groundTopY = mapCenter.y + groundTopOffset;
            if (!TryGetCombinedWorldBounds(instance, out var bounds)) {
                var position = instance.transform.position;
                instance.transform.position = new Vector3(position.x, groundTopY, position.z);
                return;
            }

            instance.transform.position += new Vector3(0f, groundTopY - bounds.min.y, 0f);
        }

        private static bool TryGetCombinedWorldBounds(GameObject gameObject, out Bounds bounds) {
            bounds = default;
            if (gameObject == null) return false;

            var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static void ApplyScale(Transform target, Vector3 cellSize, bool usingCubePrefab) {
            if (target == null) return;
            target.localScale = usingCubePrefab ? new Vector3(cellSize.x, cellSize.y, cellSize.z) : Vector3.one;
        }

        private bool TryGetBoundaryRoot(out Transform boundaryRoot) {
            EnsureRoots();
            boundaryRoot = worldBoundaryRoot;
            return boundaryRoot != null;
        }

        private bool TryGetObstacleRoot(out Transform resolvedObstacleRoot) {
            EnsureRoots();
            resolvedObstacleRoot = obstacleRoot;
            return resolvedObstacleRoot != null;
        }

        private static void ClearBoundaryChildren(Transform root) {
            for (var i = root.childCount - 1; i >= 0; i--) {
                var child = root.GetChild(i);
                if (child != null && IsSpawnedVoxelName(child.name)) DestroySpawnedObject(child.gameObject);
            }
        }

        private static void ClearGeneratedObstacleChildren(Transform root) {
            for (var i = root.childCount - 1; i >= 0; i--) {
                var child = root.GetChild(i);
                if (child == null) continue;

                var metadata = child.GetComponent<ObstacleInstanceMetadata>();
                if ((metadata != null && metadata.IsGenerated) || child.name.StartsWith("Obstacle_")) DestroySpawnedObject(child.gameObject);
            }
        }

        private static bool IsBoundaryKind(VoxelCellKind kind) {
            return kind == VoxelCellKind.Floor || kind == VoxelCellKind.Wall;
        }

        private void EnsureRoots() {
            mapRoot = EnsureChild(transform, mapRoot, "MapRoot");
            worldBoundaryRoot = EnsureChild(mapRoot, worldBoundaryRoot, "worldBoundaryRoot");
            obstacleRoot = EnsureChild(mapRoot, obstacleRoot, "obstacleRoot");
        }

        private static Transform EnsureChild(Transform parent, Transform current, string childName) {
            if (current != null) return current;

            var existing = parent.Find(childName);
            if (existing != null) return existing;

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private static void DestroySpawnedObject(GameObject instance) {
            if (Application.isPlaying) {
                Destroy(instance);
                return;
            }

            DestroyImmediate(instance);
        }

        private GameObject CreatePrimitiveObstacle(Vector3 worldPosition, Quaternion rotation) {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.transform.SetParent(obstacleRoot, false);
            instance.transform.position = worldPosition;
            instance.transform.rotation = rotation;
            return instance;
        }
    }
}
